<script setup>
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useNotesStore } from '../stores/notes'
import { useCalendarNotesStore } from '../stores/calendarNotes'
import signalr from '../api/signalr'
import AppLayout from '../components/AppLayout.vue'
import AudioRecorder from '../components/AudioRecorder.vue'
import AudioPlayer from '../components/AudioPlayer.vue'
import NoteEditor from '../components/NoteEditor.vue'

const route = useRoute()
const router = useRouter()
const notesStore = useNotesStore()
const calendarStore = useCalendarNotesStore()

const title = ref('')
const textContent = ref('')
const color = ref(null)
const saving = ref(false)
const activeTab = ref('text')
const conflict = ref(false)
const editorPresence = ref(null) // username of another user currently editing, or null
let joinedNoteId = null

const noteColors = ['#8963ba', '#d64545', '#e08a2c', '#e0c93c', '#3aa0a0', '#3468c0', '#90c290']

const note = computed(() => notesStore.currentNote)
const backTarget = computed(() => {
  if (note.value?.groupId) return { name: 'group-detail', params: { id: note.value.groupId } }
  return note.value?.scheduledDate ? { name: 'calendar' } : { name: 'notes' }
})

function invalidateCalendarMonthFor(scheduledDate) {
  if (!scheduledDate) return
  const [year, month] = scheduledDate.split('-').map(Number)
  calendarStore.invalidateMonth(year, month)
}

function handleEditorPresence({ userId, username, editing }) {
  editorPresence.value = editing ? username : null
}

// Broadcast idzie do calego kanalu note-{id}, wiec dostajemy tez echo wlasnego
// zapisu - odrozniamy je krotkim oknem czasowym po naszym ostatnim udanym save,
// zeby nie pokazywac banera konfliktu po kazdym wlasnym autosave.
let lastLocalSaveAt = 0
const SELF_ECHO_WINDOW_MS = 3000

function handleNoteChanged({ noteId, changeType }) {
  if (!note.value || noteId !== note.value.id) return
  if (changeType === 'deleted') {
    conflict.value = true
    return
  }
  if (Date.now() - lastLocalSaveAt < SELF_ECHO_WINDOW_MS) return
  conflict.value = true
}

function joinEditing(id) {
  if (!id) return
  joinedNoteId = id
  signalr.invoke('JoinNoteEditing', id)
}

function leaveEditing() {
  if (!joinedNoteId) return
  signalr.invoke('LeaveNoteEditing', joinedNoteId)
  joinedNoteId = null
}

async function loadNote(id) {
  conflict.value = false
  editorPresence.value = null
  leaveEditing()
  await notesStore.fetchById(id)
  if (note.value) {
    title.value = note.value.title || ''
    textContent.value = note.value.textContent || ''
    color.value = note.value.color || null
    if (note.value.groupId) {
      joinEditing(note.value.id)
    }
  }
}

onMounted(() => {
  signalr.on('EditorPresence', handleEditorPresence)
  signalr.on('NoteChanged', handleNoteChanged)
  loadNote(route.params.id)
})

onBeforeUnmount(() => {
  signalr.off('EditorPresence', handleEditorPresence)
  signalr.off('NoteChanged', handleNoteChanged)
  leaveEditing()
})

watch(
  () => route.params.id,
  (id) => loadNote(id)
)

function pickColor(c) {
  color.value = color.value === c ? null : c
  saveNote()
}

let saveTimeout = null
function scheduleSave() {
  clearTimeout(saveTimeout)
  saveTimeout = setTimeout(saveNote, 800)
}

function handleContentUpdate(json) {
  textContent.value = json
  scheduleSave()
}

async function saveNote() {
  if (!note.value || conflict.value) return
  saving.value = true
  // Ustawiane PRZED wyslaniem zadania, nie po nim - broadcast NoteChanged od
  // serwera moze dotrzec przez SignalR szybciej niz wroci odpowiedz HTTP na
  // nasz wlasny zapis, wiec okno musi obejmowac caly czas trwania zadania.
  lastLocalSaveAt = Date.now()
  try {
    await notesStore.update(note.value.id, {
      title: title.value,
      textContent: textContent.value,
      color: color.value || '',
      rowVersionBase64: note.value.rowVersion || null,
    })
    invalidateCalendarMonthFor(note.value.scheduledDate)
  } catch (e) {
    if (e.isConflict) {
      conflict.value = true
    } else {
      throw e
    }
  } finally {
    saving.value = false
  }
}

async function handleReload() {
  conflict.value = false
  const groupId = note.value?.groupId
  try {
    await loadNote(note.value?.id || route.params.id)
  } catch (e) {
    if (e.response?.status === 404) {
      // notatka zostala usunieta przez kogos innego - nie ma juz gdzie odswiezac
      router.push(groupId ? { name: 'group-detail', params: { id: groupId } } : { name: 'notes' })
      return
    }
    throw e
  }
}

async function handleDelete() {
  if (!confirm('Czy na pewno chcesz usunąć tę notatkę?')) return
  const scheduledDate = note.value.scheduledDate
  const groupId = note.value.groupId
  await notesStore.remove(note.value.id)
  invalidateCalendarMonthFor(scheduledDate)
  if (groupId) {
    router.push({ name: 'group-detail', params: { id: groupId } })
  } else {
    router.push(scheduledDate ? { name: 'calendar' } : { name: 'notes' })
  }
}

async function handleRecorded({ blob, durationSeconds }) {
  const file = new File([blob], 'recording.webm', { type: blob.type })
  await notesStore.uploadAudio(note.value.id, file, durationSeconds)
}

async function handleDeleteAudio(audioId) {
  if (!confirm('Usunąć tę głosówkę?')) return
  await notesStore.deleteAudio(note.value.id, audioId)
}
</script>

<template>
  <AppLayout>
    <div v-if="notesStore.loading && !note" class="state-msg">Ładowanie…</div>
    <div v-else-if="!note" class="state-msg error-msg">Notatka nie znaleziona</div>
    <div v-else class="note-detail">
      <div class="detail-header">
        <router-link :to="backTarget" class="back-link">← Wróć</router-link>
        <div class="header-actions">
          <span v-if="saving" class="save-indicator">Zapisywanie…</span>
          <button class="btn btn-danger btn-sm" @click="handleDelete">Usuń</button>
        </div>
      </div>

      <p v-if="note.groupId && note.authorUsername" class="note-author">dodane przez {{ note.authorUsername }}</p>

      <div v-if="editorPresence" class="presence-banner">
        <strong>{{ editorPresence }}</strong> aktualnie edytuje tę notatkę
      </div>

      <div v-if="conflict" class="conflict-banner">
        <span>Notatka została zmieniona przez kogoś innego. Odśwież, aby zobaczyć najnowszą wersję.</span>
        <button class="btn btn-accent btn-sm" @click="handleReload">Odśwież</button>
      </div>

      <input
        v-model="title"
        type="text"
        class="title-input"
        placeholder="Tytuł notatki"
        maxlength="200"
        @input="scheduleSave"
      />

      <div class="color-row">
        <span class="color-row-label">Kolor kafelka:</span>
        <button
          v-for="c in noteColors"
          :key="c"
          type="button"
          class="color-swatch"
          :class="{ active: color === c }"
          :style="{ background: c }"
          :title="c"
          @click="pickColor(c)"
        />
        <button
          v-if="color"
          type="button"
          class="color-swatch color-swatch-none"
          title="Usuń kolor"
          @click="pickColor(null)"
        >
          ×
        </button>
      </div>

      <div class="tabs">
        <button
          class="tab"
          :class="{ active: activeTab === 'text' }"
          @click="activeTab = 'text'"
        >
          Tekst
        </button>
        <button
          class="tab"
          :class="{ active: activeTab === 'audio' }"
          @click="activeTab = 'audio'"
        >
          Głosówki ({{ note.audioClips?.length || 0 }})
        </button>
      </div>

      <div v-show="activeTab === 'text'" class="tab-content">
        <NoteEditor
          :key="note.id"
          :model-value="textContent"
          :note-id="note.id"
          @update:model-value="handleContentUpdate"
        />
      </div>

      <div v-show="activeTab === 'audio'" class="tab-content">
        <p class="section-title">Głosówki</p>
        <AudioRecorder @recorded="handleRecorded" />

        <div v-if="note.audioClips?.length" class="audio-list">
          <AudioPlayer
            v-for="clip in note.audioClips"
            :key="clip.id"
            :note-id="note.id"
            :audio-id="clip.id"
            :duration-seconds="clip.durationSeconds"
            @delete="handleDeleteAudio(clip.id)"
          />
        </div>
        <p v-else class="empty-hint">Brak głosówek. Nagraj pierwszą!</p>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.note-detail {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.detail-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.back-link {
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--color-secondary);
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.save-indicator {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.note-author {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  font-style: italic;
}

.presence-banner {
  font-size: 0.875rem;
  padding: 0.625rem 0.875rem;
  border-radius: var(--radius-sm);
  background: rgba(115, 171, 132, 0.15);
  color: var(--muted-teal);
}

.conflict-banner {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.875rem;
  padding: 0.75rem 1rem;
  border-radius: var(--radius-sm);
  background: rgba(224, 118, 110, 0.15);
  color: var(--color-danger);
}

.title-input {
  width: 100%;
  font-size: 1.5rem;
  font-weight: 700;
  color: var(--color-primary);
  border: none;
  background: transparent;
  padding: 0.25rem 0;
  border-bottom: 2px solid var(--color-border);
  transition: border-color 0.2s;
}

.title-input:focus {
  outline: none;
  border-bottom-color: var(--color-secondary);
}

.color-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.color-row-label {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  margin-right: 0.25rem;
}

.color-swatch {
  width: 1.5rem;
  height: 1.5rem;
  border-radius: 50%;
  border: 2px solid transparent;
  cursor: pointer;
  padding: 0;
  transition: transform 0.1s;
}

.color-swatch.active {
  border-color: var(--color-text);
  transform: scale(1.15);
}

.color-swatch-none {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  border: 1px solid var(--color-border);
  background: transparent;
  color: var(--color-text-muted);
  font-size: 0.9375rem;
  line-height: 1;
}

.tabs {
  display: flex;
  gap: 0.25rem;
  border-bottom: 2px solid var(--color-border);
  overflow-x: auto;
  -webkit-overflow-scrolling: touch;
}

.tab {
  padding: 0.625rem 1rem;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-muted);
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
  white-space: nowrap;
  transition: color 0.2s, border-color 0.2s;
}

.tab.active {
  color: var(--color-primary);
  border-bottom-color: var(--color-primary);
}

.tab-content {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.audio-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  margin-top: 0.75rem;
}

.empty-hint {
  color: var(--color-text-muted);
  font-size: 0.9375rem;
  text-align: center;
  padding: 1.5rem;
}

.state-msg {
  text-align: center;
  padding: 3rem;
  color: var(--color-text-muted);
}

@media (max-width: 640px) {
  .title-input {
    font-size: 1.25rem;
  }

  .tabs {
    gap: 0;
  }

  .tab {
    flex: 1;
    text-align: center;
    padding: 0.625rem 0.5rem;
    font-size: 0.8125rem;
  }
}
</style>
