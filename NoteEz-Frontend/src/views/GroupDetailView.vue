<script setup>
import { ref, computed, onMounted, onBeforeUnmount, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useGroupsStore } from '../stores/groups'
import { useNotesStore } from '../stores/notes'
import { useCalendarNotesStore } from '../stores/calendarNotes'
import { useAuthStore } from '../stores/auth'
import signalr from '../api/signalr'
import { tiptapToPlainText } from '../utils/tiptapText'
import AppLayout from '../components/AppLayout.vue'
import NoteCard from '../components/NoteCard.vue'
import ToolbarIcon from '../components/ToolbarIcon.vue'

const route = useRoute()
const router = useRouter()
const groupsStore = useGroupsStore()
const notesStore = useNotesStore()
const calendarStore = useCalendarNotesStore()
const auth = useAuthStore()

const groupId = computed(() => route.params.id)
const group = computed(() => groupsStore.groups.find((g) => g.id === groupId.value))
const groupMembers = computed(() => groupsStore.members[groupId.value] || [])
const isOwner = computed(() => group.value?.role === 'Owner')

const activeTab = ref('notes')
const showCreate = ref(false)
const newTitle = ref('')
const creating = ref(false)

const showInvite = ref(false)
const inviteUsername = ref('')
const inviting = ref(false)
const inviteError = ref(null)
const inviteSuccess = ref(false)

const removingMemberId = ref(null)
const leaving = ref(false)

const today = new Date()
const viewYear = ref(today.getFullYear())
const viewMonth = ref(today.getMonth() + 1)
const monthKey = computed(() => `${viewYear.value}-${String(viewMonth.value).padStart(2, '0')}`)
const monthNotes = computed(() => calendarStore.notesByMonth[`${groupId.value}:${monthKey.value}`] || [])
const monthLabel = computed(() =>
  new Date(viewYear.value, viewMonth.value - 1, 1).toLocaleDateString('pl-PL', { month: 'long', year: 'numeric' })
)
const selectedDate = ref(null)
const createForDate = ref(null)
const newCalTitle = ref('')
const creatingCal = ref(false)

const notesByDate = computed(() => {
  const map = {}
  for (const note of monthNotes.value) {
    const key = note.scheduledDate
    if (!map[key]) map[key] = []
    map[key].push(note)
  }
  return map
})
const selectedNotes = computed(() => (selectedDate.value ? notesByDate.value[selectedDate.value] || [] : []))

const zoomed = ref(false)
const WEEKDAY_LABELS = ['Pon', 'Wt', 'Śr', 'Czw', 'Pt', 'Sob', 'Nd']
const PREVIEW_MAX_CHARS = 60

function previewOf(note) {
  const text = tiptapToPlainText(note.textContent).replace(/\s+/g, ' ').trim()
  return text.length > PREVIEW_MAX_CHARS ? `${text.slice(0, PREVIEW_MAX_CHARS)}…` : text
}

function pad(n) {
  return String(n).padStart(2, '0')
}
function toDateStr(year, month, day) {
  return `${year}-${pad(month)}-${pad(day)}`
}

const gridCells = computed(() => {
  const first = new Date(viewYear.value, viewMonth.value - 1, 1)
  const firstWeekday = (first.getDay() + 6) % 7
  const start = new Date(viewYear.value, viewMonth.value - 1, 1 - firstWeekday)
  const cells = []
  for (let i = 0; i < 42; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    const dateStr = toDateStr(d.getFullYear(), d.getMonth() + 1, d.getDate())
    cells.push({
      dateStr,
      day: d.getDate(),
      inMonth: d.getMonth() === viewMonth.value - 1,
      isToday: dateStr === toDateStr(today.getFullYear(), today.getMonth() + 1, today.getDate()),
    })
  }
  return cells
})

const monthCells = computed(() => gridCells.value.filter((c) => c.inMonth))

function loadCalendar(force = false) {
  return calendarStore.fetchMonth(viewYear.value, viewMonth.value, force, groupId.value)
}

function prevMonth() {
  if (viewMonth.value === 1) {
    viewMonth.value = 12
    viewYear.value -= 1
  } else {
    viewMonth.value -= 1
  }
}
function nextMonth() {
  if (viewMonth.value === 12) {
    viewMonth.value = 1
    viewYear.value += 1
  } else {
    viewMonth.value += 1
  }
}

function openDay(cell) {
  if (!cell.inMonth) return
  selectedDate.value = selectedDate.value === cell.dateStr ? null : cell.dateStr
  createForDate.value = null
}

function startCreate(dateStr) {
  createForDate.value = dateStr
  selectedDate.value = dateStr
  newCalTitle.value = ''
}

async function handleCreateCalNote() {
  if (!newCalTitle.value.trim() || !createForDate.value) return
  creatingCal.value = true
  try {
    const note = await notesStore.create(newCalTitle.value.trim(), null, createForDate.value, null, groupId.value)
    newCalTitle.value = ''
    createForDate.value = null
    await loadCalendar(true)
    router.push(`/notes/${note.id}`)
  } finally {
    creatingCal.value = false
  }
}

function goToNote(id) {
  router.push(`/notes/${id}`)
}

watch([viewYear, viewMonth], () => {
  selectedDate.value = null
  createForDate.value = null
  if (activeTab.value === 'calendar') loadCalendar()
})

watch(activeTab, (tab) => {
  if (tab === 'calendar') loadCalendar()
})

async function loadNotes() {
  await notesStore.fetchAll(groupId.value)
}

async function loadMembers() {
  await groupsStore.fetchMembers(groupId.value)
}

function handleNoteChanged() {
  if (activeTab.value === 'notes') {
    loadNotes()
  } else if (activeTab.value === 'calendar') {
    loadCalendar(true)
  }
}

onMounted(async () => {
  if (!groupsStore.groups.length) {
    await groupsStore.fetchGroups()
  }
  await Promise.all([loadNotes(), loadMembers()])
  signalr.invoke('JoinGroupChannel', groupId.value)
  signalr.on('NoteChanged', handleNoteChanged)
})

onBeforeUnmount(() => {
  signalr.invoke('LeaveGroupChannel', groupId.value)
  signalr.off('NoteChanged', handleNoteChanged)
})

watch(groupId, async () => {
  await Promise.all([loadNotes(), loadMembers()])
  if (activeTab.value === 'calendar') loadCalendar()
})

async function handleCreate() {
  if (!newTitle.value.trim()) return
  creating.value = true
  try {
    const note = await notesStore.create(newTitle.value.trim(), null, null, null, groupId.value)
    newTitle.value = ''
    showCreate.value = false
    router.push(`/notes/${note.id}`)
  } finally {
    creating.value = false
  }
}

async function handleInvite() {
  if (!inviteUsername.value.trim()) return
  inviting.value = true
  inviteError.value = null
  inviteSuccess.value = false
  try {
    await groupsStore.inviteToGroup(groupId.value, inviteUsername.value.trim())
    inviteSuccess.value = true
    inviteUsername.value = ''
  } catch (e) {
    if (e.response?.status === 429) {
      inviteError.value = 'Zbyt wiele zaproszeń w krótkim czasie. Spróbuj ponownie za chwilę.'
    } else {
      inviteError.value = e.response?.data?.error || e.response?.data || 'Nie udało się wysłać zaproszenia'
    }
  } finally {
    inviting.value = false
  }
}

async function handleRemoveMember(userId) {
  if (!confirm('Usunąć tego członka z grupy?')) return
  removingMemberId.value = userId
  try {
    await groupsStore.removeMember(groupId.value, userId)
  } finally {
    removingMemberId.value = null
  }
}

async function handleLeave() {
  if (!confirm('Czy na pewno chcesz opuścić tę grupę?')) return
  leaving.value = true
  try {
    await groupsStore.leaveGroup(groupId.value)
    router.push({ name: 'groups' })
  } catch (e) {
    alert(e.response?.data?.error || e.response?.data || 'Nie udało się opuścić grupy')
  } finally {
    leaving.value = false
  }
}

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString('pl-PL', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
  })
}
</script>

<template>
  <AppLayout>
    <div class="group-detail-page">
      <div class="group-glow" aria-hidden="true" />
      <div class="page-header">
        <div class="page-header-title">
          <router-link :to="{ name: 'groups' }" class="btn btn-ghost btn-sm back-btn">← Grupy</router-link>
          <h1>{{ group?.name || 'Grupa' }}</h1>
        </div>
      </div>

      <div class="tabs">
        <button class="tab" :class="{ active: activeTab === 'notes' }" @click="activeTab = 'notes'">
          Notatki
        </button>
        <button class="tab" :class="{ active: activeTab === 'calendar' }" @click="activeTab = 'calendar'">
          Kalendarz
        </button>
        <button class="tab" :class="{ active: activeTab === 'members' }" @click="activeTab = 'members'">
          Członkowie ({{ groupMembers.length }})
        </button>
      </div>

      <div v-show="activeTab === 'notes'" class="tab-content">
        <div class="page-header-actions">
          <button class="btn btn-primary" @click="showCreate = !showCreate">
            {{ showCreate ? 'Anuluj' : '+ Nowa notatka' }}
          </button>
        </div>

        <div v-if="showCreate" class="create-form card">
          <input
            v-model="newTitle"
            type="text"
            class="input-field"
            placeholder="Tytuł notatki…"
            maxlength="200"
            @keyup.enter="handleCreate"
          />
          <button class="btn btn-accent" :disabled="creating || !newTitle.trim()" @click="handleCreate">
            {{ creating ? 'Tworzenie…' : 'Utwórz' }}
          </button>
        </div>

        <div v-if="notesStore.loading" class="state-msg">Ładowanie notatek…</div>
        <div v-else-if="notesStore.error" class="state-msg error-msg">{{ notesStore.error }}</div>
        <div v-else-if="!notesStore.notes.length" class="empty-state card">
          <span class="empty-icon"><ToolbarIcon name="clipboard" /></span>
          <p>Brak notatek w tej grupie. Utwórz pierwszą!</p>
        </div>
        <div v-else class="notes-grid">
          <NoteCard v-for="note in notesStore.notes" :key="note.id" :note="note" />
        </div>
      </div>

      <div v-show="activeTab === 'calendar'" class="tab-content">
        <div class="calendar-toolbar">
          <div class="month-nav">
            <button class="btn btn-ghost btn-icon" aria-label="Poprzedni miesiąc" @click="prevMonth">
              <ToolbarIcon name="chevron-left" />
            </button>
            <span class="month-label">{{ monthLabel }}</span>
            <button class="btn btn-ghost btn-icon" aria-label="Następny miesiąc" @click="nextMonth">
              <ToolbarIcon name="chevron-right" />
            </button>
          </div>
          <button
            class="btn btn-outline btn-icon"
            :title="zoomed ? 'Widok miesiąca' : 'Widok kafelków'"
            :aria-label="zoomed ? 'Widok miesiąca' : 'Widok kafelków'"
            @click="zoomed = !zoomed"
          >
            <ToolbarIcon :name="zoomed ? 'list' : 'grid'" />
          </button>
        </div>

        <div v-if="calendarStore.loading" class="state-msg">Ładowanie…</div>
        <div v-else-if="calendarStore.error" class="state-msg error-msg">{{ calendarStore.error }}</div>

        <div v-else-if="!zoomed" class="weekday-row">
          <span v-for="wd in WEEKDAY_LABELS" :key="wd" class="weekday">{{ wd }}</span>
        </div>

        <div v-if="!calendarStore.loading && !calendarStore.error && !zoomed" class="month-grid">
          <button
            v-for="cell in gridCells"
            :key="cell.dateStr"
            type="button"
            class="day-cell"
            :class="{
              'not-in-month': !cell.inMonth,
              today: cell.isToday,
              selected: selectedDate === cell.dateStr,
            }"
            :disabled="!cell.inMonth"
            @click="openDay(cell)"
          >
            <span class="day-number">{{ cell.day }}</span>
            <span class="day-dots">
              <span
                v-for="note in (notesByDate[cell.dateStr] || []).slice(0, 4)"
                :key="note.id"
                class="dot"
                :style="{ background: note.color || 'var(--color-secondary)' }"
              />
            </span>
          </button>
        </div>

        <div v-else-if="!calendarStore.loading && !calendarStore.error" class="tile-grid">
          <div
            v-for="cell in monthCells"
            :key="cell.dateStr"
            class="day-tile"
            :class="{ today: cell.isToday }"
          >
            <div class="day-tile-header">
              <span class="day-number">{{ cell.day }}</span>
              <button
                class="btn btn-ghost btn-icon tile-add"
                aria-label="Nowa notatka"
                title="Nowa notatka"
                @click="startCreate(cell.dateStr)"
              >
                <ToolbarIcon name="plus" :size="14" />
              </button>
            </div>
            <div class="day-tile-notes">
              <button
                v-for="note in (notesByDate[cell.dateStr] || []).slice(0, 3)"
                :key="note.id"
                type="button"
                class="tile-note"
                :style="{ borderLeftColor: note.color || 'var(--color-secondary)' }"
                @click="goToNote(note.id)"
              >
                <span class="tile-note-title">{{ note.title || 'Bez tytułu' }}</span>
                <span v-if="previewOf(note)" class="tile-note-preview">{{ previewOf(note) }}</span>
              </button>
              <button
                v-if="(notesByDate[cell.dateStr] || []).length > 3"
                type="button"
                class="tile-more"
                @click="openDay(cell)"
              >
                +{{ (notesByDate[cell.dateStr] || []).length - 3 }} więcej
              </button>
            </div>
          </div>
        </div>

        <div v-if="selectedDate" class="day-panel card">
          <div class="day-panel-header">
            <h2>
              {{
                new Date(selectedDate).toLocaleDateString('pl-PL', {
                  day: 'numeric',
                  month: 'long',
                  year: 'numeric',
                })
              }}
            </h2>
            <button class="btn btn-ghost btn-icon" aria-label="Zamknij" @click="selectedDate = null">
              <ToolbarIcon name="close" />
            </button>
          </div>

          <div v-if="createForDate === selectedDate" class="create-form">
            <input
              v-model="newCalTitle"
              type="text"
              class="input-field"
              placeholder="Tytuł notatki…"
              maxlength="200"
              autofocus
              @keyup.enter="handleCreateCalNote"
            />
            <div class="create-form-actions">
              <button class="btn btn-ghost btn-sm" @click="createForDate = null">Anuluj</button>
              <button
                class="btn btn-accent btn-sm"
                :disabled="creatingCal || !newCalTitle.trim()"
                @click="handleCreateCalNote"
              >
                {{ creatingCal ? 'Tworzenie…' : 'Utwórz' }}
              </button>
            </div>
          </div>
          <button v-else class="btn btn-primary new-note-tile" @click="startCreate(selectedDate)">
            <ToolbarIcon name="plus" /> Nowa notatka
          </button>

          <div v-if="!selectedNotes.length" class="empty-state">
            <p>Brak notatek w tym dniu.</p>
          </div>
          <div v-else class="day-notes-list">
            <button
              v-for="note in selectedNotes"
              :key="note.id"
              type="button"
              class="day-note-item"
              :style="{ borderLeftColor: note.color || 'var(--color-secondary)' }"
              @click="goToNote(note.id)"
            >
              <span class="day-note-title">{{ note.title || 'Bez tytułu' }}</span>
              <span v-if="note.authorUsername" class="day-note-author">{{ note.authorUsername }}</span>
            </button>
          </div>
        </div>
      </div>

      <div v-show="activeTab === 'members'" class="tab-content">
        <div v-if="isOwner" class="page-header-actions">
          <button class="btn btn-primary" @click="showInvite = !showInvite">
            {{ showInvite ? 'Anuluj' : '+ Zaproś' }}
          </button>
        </div>

        <div v-if="showInvite" class="create-form card">
          <input
            v-model="inviteUsername"
            type="text"
            class="input-field"
            placeholder="Nazwa użytkownika…"
            @keyup.enter="handleInvite"
          />
          <button class="btn btn-accent" :disabled="inviting || !inviteUsername.trim()" @click="handleInvite">
            {{ inviting ? 'Wysyłanie…' : 'Wyślij zaproszenie' }}
          </button>
          <p v-if="inviteError" class="hint error-msg">{{ inviteError }}</p>
          <p v-if="inviteSuccess" class="hint success-msg">Zaproszenie wysłane.</p>
        </div>

        <div class="members-list">
          <div v-for="member in groupMembers" :key="member.userId" class="member-card card">
            <div class="member-info">
              <span class="member-name">{{ member.username }}</span>
              <span class="badge" :class="member.role === 'Owner' ? 'badge-owner' : 'badge-member'">
                {{ member.role === 'Owner' ? 'Właściciel' : 'Członek' }}
              </span>
              <span class="member-meta">Dołączył: {{ formatDate(member.joinedAt) }}</span>
            </div>
            <button
              v-if="isOwner && member.username !== auth.username"
              class="btn btn-danger btn-sm"
              :disabled="removingMemberId === member.userId"
              @click="handleRemoveMember(member.userId)"
            >
              Usuń
            </button>
          </div>
        </div>

        <button class="btn btn-ghost btn-sm leave-btn" :disabled="leaving" @click="handleLeave">
          {{ leaving ? 'Opuszczanie…' : 'Opuść grupę' }}
        </button>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.group-detail-page {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
}

/* Ta sama dekoracyjna poswiata co na stronie "Moje notatki"/kalendarza - fixed
   (nie absolute), zeby rozciagala sie na cala szerokosc viewportu zamiast byc
   ograniczona przez wyśrodkowany .main z max-width w AppLayout. */
.group-glow {
  position: fixed;
  inset: 0;
  z-index: -1;
  pointer-events: none;
  background:
    radial-gradient(circle at 18% 0%, rgba(121, 199, 197, 0.14), transparent 45%),
    radial-gradient(circle at 90% 10%, rgba(153, 209, 156, 0.12), transparent 42%),
    radial-gradient(circle at 50% 60%, rgba(115, 171, 132, 0.08), transparent 55%);
}

.group-glow::before {
  content: '';
  position: absolute;
  inset: 0;
  background-image: radial-gradient(rgba(234, 245, 240, 0.08) 1px, transparent 1px);
  background-size: 26px 26px;
  -webkit-mask-image: radial-gradient(ellipse 70% 55% at 50% 10%, black, transparent 70%);
  mask-image: radial-gradient(ellipse 70% 55% at 50% 10%, black, transparent 70%);
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.page-header-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.page-header h1 {
  font-size: 1.5rem;
  color: var(--color-primary);
}

.back-btn {
  flex-shrink: 0;
}

.tabs {
  display: flex;
  gap: 0.25rem;
  border-bottom: 2px solid var(--color-border);
}

.tab {
  padding: 0.625rem 1rem;
  font-size: 0.875rem;
  font-weight: 600;
  color: var(--color-text-muted);
  border-bottom: 2px solid transparent;
  margin-bottom: -2px;
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

.page-header-actions {
  display: flex;
  justify-content: flex-end;
}

.create-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  padding: 1rem;
}

.hint {
  font-size: 0.8125rem;
}

.success-msg {
  color: var(--celadon);
}

.notes-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.calendar-toolbar {
  display: flex;
  align-items: center;
  justify-content: center;
  position: relative;
  gap: 1rem;
}

.calendar-toolbar .btn-outline {
  position: absolute;
  right: 0;
}

.month-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
}

.weekday-row {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 0.375rem;
  text-align: center;
}

.weekday {
  font-size: 0.75rem;
  font-weight: 700;
  color: var(--color-text-muted);
  text-transform: uppercase;
  letter-spacing: 0.04em;
}

.tile-grid {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.day-tile {
  display: flex;
  align-items: flex-start;
  gap: 0.75rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: 0.625rem 0.75rem;
}

.day-tile.today {
  border-color: var(--color-primary);
}

.day-tile-header {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.25rem;
  min-width: 2.25rem;
  padding-top: 0.15rem;
}

.tile-add {
  width: 1.5rem;
  height: 1.5rem;
}

.day-tile-notes {
  flex: 1;
  min-width: 0;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.tile-note {
  text-align: left;
  background: var(--color-surface-alt);
  border-left: 3px solid var(--color-secondary);
  border-radius: 4px;
  padding: 0.4rem 0.5rem;
  min-height: 2.25rem;
  display: flex;
  flex-direction: column;
  justify-content: center;
  gap: 0.1rem;
  width: 100%;
  min-width: 0;
  max-width: 100%;
}

.tile-note-title,
.tile-note-preview {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  width: 100%;
  min-width: 0;
  max-width: 100%;
}

.tile-note-title {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--color-text);
}

.tile-note-preview {
  font-size: 0.6875rem;
  color: var(--color-text-muted);
}

.tile-more {
  font-size: 0.6875rem;
  color: var(--color-secondary);
  text-align: left;
  padding: 0.1rem 0.4rem;
}

.month-label {
  font-size: 1.1rem;
  font-weight: 600;
  min-width: 11rem;
  text-align: center;
  text-transform: capitalize;
}

.month-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 0.375rem;
}

.day-cell {
  aspect-ratio: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  gap: 0.3rem;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  color: var(--color-text);
  transition: border-color 0.15s, background 0.15s;
}

.day-cell:not(.not-in-month):hover {
  border-color: var(--color-secondary);
}

.day-cell.not-in-month {
  opacity: 0.25;
  cursor: default;
}

.day-cell.today {
  border-color: var(--color-primary);
}

.day-cell.selected {
  background: var(--color-surface-alt);
  border-color: var(--color-secondary);
}

.day-number {
  font-size: 0.875rem;
  font-weight: 600;
}

.day-dots {
  display: flex;
  gap: 0.2rem;
  min-height: 0.4rem;
}

.dot {
  width: 0.4rem;
  height: 0.4rem;
  border-radius: 50%;
}

.day-panel {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
  min-width: 0;
}

.day-panel-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
}

.day-panel-header h2 {
  font-size: 1.1rem;
  color: var(--color-primary);
  text-transform: capitalize;
}

.create-form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.new-note-tile {
  align-self: flex-start;
}

.day-notes-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
  min-width: 0;
}

.day-note-item {
  text-align: left;
  background: var(--color-surface-alt);
  border-left: 3px solid var(--color-secondary);
  border-radius: 6px;
  padding: 0.5rem 0.75rem;
  display: flex;
  flex-direction: column;
  gap: 0.15rem;
  width: 100%;
  min-width: 0;
  max-width: 100%;
}

.day-note-title,
.day-note-author {
  display: block;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
  width: 100%;
  min-width: 0;
  max-width: 100%;
}

.day-note-title {
  font-weight: 600;
  color: var(--color-text);
}

.day-note-author {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.members-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.member-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 0.875rem 1.125rem;
  flex-wrap: wrap;
}

.member-info {
  display: flex;
  align-items: center;
  gap: 0.75rem;
  flex-wrap: wrap;
}

.member-name {
  font-weight: 600;
}

.member-meta {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.badge {
  font-size: 0.75rem;
  padding: 0.2rem 0.625rem;
  border-radius: 999px;
  font-weight: 600;
}

.badge-owner {
  background: rgba(153, 209, 156, 0.2);
  color: var(--celadon);
}

.badge-member {
  background: rgba(115, 171, 132, 0.2);
  color: var(--muted-teal);
}

.leave-btn {
  align-self: flex-start;
  color: var(--color-danger);
}

.empty-state {
  text-align: center;
  padding: 3rem 1.5rem;
  color: var(--color-text-muted);
}

.empty-icon {
  display: flex;
  justify-content: center;
  margin-bottom: 0.75rem;
}

.empty-icon :deep(svg) {
  width: 3rem;
  height: 3rem;
}

.state-msg {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-muted);
}

@media (max-width: 640px) {
  .page-header h1 {
    font-size: 1.25rem;
  }

  .notes-grid {
    grid-template-columns: 1fr;
  }
}
</style>
