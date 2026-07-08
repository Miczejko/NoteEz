<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useNotesStore } from '../stores/notes'
import AppLayout from '../components/AppLayout.vue'
import DrawingCanvas from '../components/DrawingCanvas.vue'
import AudioRecorder from '../components/AudioRecorder.vue'
import AudioPlayer from '../components/AudioPlayer.vue'

const route = useRoute()
const router = useRouter()
const notesStore = useNotesStore()

const title = ref('')
const textContent = ref('')
const saving = ref(false)
const showDrawing = ref(false)
const activeTab = ref('text')
const editingDrawingId = ref(null)

const note = computed(() => notesStore.currentNote)

onMounted(async () => {
  await notesStore.fetchById(route.params.id)
  if (note.value) {
    title.value = note.value.title || ''
    textContent.value = note.value.textContent || ''
  }
})

watch(
  () => route.params.id,
  async (id) => {
    await notesStore.fetchById(id)
    if (note.value) {
      title.value = note.value.title || ''
      textContent.value = note.value.textContent || ''
    }
  }
)

let saveTimeout = null
function scheduleSave() {
  clearTimeout(saveTimeout)
  saveTimeout = setTimeout(saveNote, 800)
}

async function saveNote() {
  if (!note.value) return
  saving.value = true
  try {
    await notesStore.update(note.value.id, {
      title: title.value,
      textContent: textContent.value,
    })
  } finally {
    saving.value = false
  }
}

async function handleDelete() {
  if (!confirm('Czy na pewno chcesz usunąć tę notatkę?')) return
  await notesStore.remove(note.value.id)
  router.push({ name: 'notes' })
}

async function handleSaveDrawing(strokesJson) {
  await notesStore.addDrawing(note.value.id, strokesJson)
  showDrawing.value = false
  activeTab.value = 'drawings'
}

async function handleEditDrawing(drawingId) {
  editingDrawingId.value = drawingId
  showDrawing.value = true
}

async function handleUpdateDrawing(strokesJson) {
  await notesStore.updateDrawing(note.value.id, editingDrawingId.value, strokesJson)
  showDrawing.value = false
  editingDrawingId.value = null
}

function handleCancelDrawing() {
  showDrawing.value = false
  editingDrawingId.value = null
}

async function handleDeleteDrawing(drawingId) {
  if (!confirm('Usunąć ten rysunek?')) return
  await notesStore.deleteDrawing(note.value.id, drawingId)
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
        <router-link to="/" class="back-link">← Wróć</router-link>
        <div class="header-actions">
          <span v-if="saving" class="save-indicator">Zapisywanie…</span>
          <button class="btn btn-danger btn-sm" @click="handleDelete">Usuń</button>
        </div>
      </div>

      <input
        v-model="title"
        type="text"
        class="title-input"
        placeholder="Tytuł notatki"
        @input="scheduleSave"
      />

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
          :class="{ active: activeTab === 'drawings' }"
          @click="activeTab = 'drawings'"
        >
          Rysunki ({{ note.drawings?.length || 0 }})
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
        <textarea
          v-model="textContent"
          class="text-area input-field"
          placeholder="Treść notatki…"
          rows="10"
          @input="scheduleSave"
        />
      </div>

      <div v-show="activeTab === 'drawings'" class="tab-content">
        <div class="section-header">
          <p class="section-title">Rysunki wektorowe</p>
          <button 
            v-if="!showDrawing"
            class="btn btn-secondary btn-sm" 
            @click="showDrawing = true; editingDrawingId = null"
          >
            + Nowy rysunek
          </button>
          <button 
            v-else
            class="btn btn-ghost btn-sm" 
            @click="handleCancelDrawing"
          >
            Anuluj
          </button>
        </div>

        <DrawingCanvas
          v-if="showDrawing && !editingDrawingId"
          @save="handleSaveDrawing"
        />

        <DrawingCanvas
          v-if="showDrawing && editingDrawingId"
          :key="editingDrawingId"
          :strokes-json="note.drawings?.find((d) => d.id === editingDrawingId)?.strokesJson"
          @save="handleUpdateDrawing"
        />

        <div v-if="note.drawings?.length" class="drawings-list">
          <div 
            v-for="drawing in note.drawings" 
            :key="drawing.id" 
            class="drawing-item card"
            :class="{ editing: editingDrawingId === drawing.id }"
          >
            <div v-if="editingDrawingId !== drawing.id" class="drawing-container">
              <DrawingCanvas :strokes-json="drawing.strokesJson" readonly />
              <div class="drawing-actions">
                <button
                  class="btn btn-secondary btn-sm"
                  @click="handleEditDrawing(drawing.id)"
                >
                  ✎ Edytuj
                </button>
                <button
                  class="btn btn-ghost btn-sm delete-drawing"
                  @click="handleDeleteDrawing(drawing.id)"
                >
                  Usuń
                </button>
              </div>
            </div>
          </div>
        </div>
        <p v-else-if="!showDrawing" class="empty-hint">Brak rysunków. Dodaj pierwszy!</p>
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

.text-area {
  min-height: 200px;
  resize: vertical;
  line-height: 1.6;
}

.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
}

.drawings-list {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.drawing-item {
  padding: 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  max-width: fit-content;
}

.drawing-item.editing {
  opacity: 0.5;
  pointer-events: none;
}

.drawing-container {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.drawing-actions {
  display: flex;
  gap: 0.5rem;
  justify-content: flex-end;
}

.delete-drawing {
  align-self: flex-end;
  color: var(--color-danger);
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
