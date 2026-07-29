<script setup>
import { ref, computed, onMounted, watch } from 'vue'
import { useRouter } from 'vue-router'
import { useCalendarNotesStore } from '../stores/calendarNotes'
import { useNotesStore } from '../stores/notes'
import { tiptapToPlainText } from '../utils/tiptapText'
import AppLayout from '../components/AppLayout.vue'
import ToolbarIcon from '../components/ToolbarIcon.vue'

const calendarStore = useCalendarNotesStore()
const notesStore = useNotesStore()
const router = useRouter()

const today = new Date()
const viewYear = ref(today.getFullYear())
const viewMonth = ref(today.getMonth() + 1) // 1-12
const zoomed = ref(false)

const selectedDate = ref(null) // 'YYYY-MM-DD' or null
const createForDate = ref(null) // 'YYYY-MM-DD' or null
const newTitle = ref('')
const creating = ref(false)

const monthKey = computed(() => `${viewYear.value}-${String(viewMonth.value).padStart(2, '0')}`)
const monthNotes = computed(() => calendarStore.notesByMonth[monthKey.value] || [])

const monthLabel = computed(() =>
  new Date(viewYear.value, viewMonth.value - 1, 1).toLocaleDateString('pl-PL', {
    month: 'long',
    year: 'numeric',
  })
)

const WEEKDAY_LABELS = ['Pon', 'Wt', 'Śr', 'Czw', 'Pt', 'Sob', 'Nd']

function pad(n) {
  return String(n).padStart(2, '0')
}

function toDateStr(year, month, day) {
  return `${year}-${pad(month)}-${pad(day)}`
}

// Standard month-grid: 6 rows x 7 cols, Monday-first, filled with leading/trailing days.
const gridCells = computed(() => {
  const first = new Date(viewYear.value, viewMonth.value - 1, 1)
  const firstWeekday = (first.getDay() + 6) % 7 // 0 = Monday
  const start = new Date(viewYear.value, viewMonth.value - 1, 1 - firstWeekday)

  const cells = []
  for (let i = 0; i < 42; i++) {
    const d = new Date(start)
    d.setDate(start.getDate() + i)
    const dateStr = toDateStr(d.getFullYear(), d.getMonth() + 1, d.getDate())
    cells.push({
      date: d,
      dateStr,
      day: d.getDate(),
      inMonth: d.getMonth() === viewMonth.value - 1,
      isToday: dateStr === toDateStr(today.getFullYear(), today.getMonth() + 1, today.getDate()),
    })
  }
  return cells
})

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

function previewOf(note) {
  return tiptapToPlainText(note.textContent)
}

function load() {
  calendarStore.fetchMonth(viewYear.value, viewMonth.value)
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
  newTitle.value = ''
}

async function handleCreate() {
  if (!newTitle.value.trim() || !createForDate.value) return
  creating.value = true
  try {
    const note = await notesStore.create(newTitle.value.trim(), null, createForDate.value)
    newTitle.value = ''
    createForDate.value = null
    calendarStore.invalidateMonth(viewYear.value, viewMonth.value)
    await load()
    router.push(`/notes/${note.id}`)
  } finally {
    creating.value = false
  }
}

function goToNote(id) {
  router.push(`/notes/${id}`)
}

watch([viewYear, viewMonth], () => {
  selectedDate.value = null
  createForDate.value = null
  load()
})

onMounted(load)
</script>

<template>
  <AppLayout>
    <div class="calendar-page">
      <div class="page-header">
        <h1>Kalendarz notatek</h1>
        <div class="page-header-actions">
          <router-link to="/notes" class="btn btn-outline"><ToolbarIcon name="list" /> Notatki</router-link>
          <button
            class="btn btn-outline btn-icon"
            :title="zoomed ? 'Widok miesiąca' : 'Widok kafelków'"
            :aria-label="zoomed ? 'Widok miesiąca' : 'Widok kafelków'"
            @click="zoomed = !zoomed"
          >
            <ToolbarIcon :name="zoomed ? 'list' : 'grid'" />
          </button>
        </div>
      </div>

      <div class="month-nav">
        <button class="btn btn-ghost btn-icon" aria-label="Poprzedni miesiąc" @click="prevMonth">
          <ToolbarIcon name="chevron-left" />
        </button>
        <span class="month-label">{{ monthLabel }}</span>
        <button class="btn btn-ghost btn-icon" aria-label="Następny miesiąc" @click="nextMonth">
          <ToolbarIcon name="chevron-right" />
        </button>
      </div>

      <div v-if="calendarStore.loading" class="state-msg">Ładowanie…</div>
      <div v-else-if="calendarStore.error" class="state-msg error-msg">{{ calendarStore.error }}</div>

      <div v-else class="weekday-row">
        <span v-for="wd in WEEKDAY_LABELS" :key="wd" class="weekday">{{ wd }}</span>
      </div>

      <!-- Month view: compact grid with dot indicators -->
      <div v-if="!zoomed" class="month-grid">
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

      <!-- Tile view: bigger cells with inline previews -->
      <div v-else class="tile-grid">
        <div
          v-for="cell in gridCells"
          :key="cell.dateStr"
          class="day-tile"
          :class="{ 'not-in-month': !cell.inMonth, today: cell.isToday }"
        >
          <template v-if="cell.inMonth">
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
          </template>
        </div>
      </div>

      <!-- Day detail panel -->
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
            v-model="newTitle"
            type="text"
            class="input-field"
            placeholder="Tytuł notatki…"
            maxlength="200"
            autofocus
            @keyup.enter="handleCreate"
          />
          <div class="create-form-actions">
            <button class="btn btn-ghost btn-sm" @click="createForDate = null">Anuluj</button>
            <button class="btn btn-accent btn-sm" :disabled="creating || !newTitle.trim()" @click="handleCreate">
              {{ creating ? 'Tworzenie…' : 'Utwórz' }}
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
            <span v-if="previewOf(note)" class="day-note-preview">{{ previewOf(note) }}</span>
          </button>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.calendar-page {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.page-header h1 {
  font-size: 1.5rem;
  color: var(--color-primary);
}

.page-header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.month-nav {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 1rem;
}

.month-label {
  font-size: 1.1rem;
  font-weight: 600;
  min-width: 11rem;
  text-align: center;
  text-transform: capitalize;
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

.state-msg {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-muted);
}

/* Month view */
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

/* Tile view */
.tile-grid {
  display: grid;
  grid-template-columns: repeat(7, 1fr);
  gap: 0.375rem;
}

.day-tile {
  min-height: 6.5rem;
  display: flex;
  flex-direction: column;
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-sm);
  padding: 0.375rem;
  gap: 0.25rem;
}

.day-tile.not-in-month {
  opacity: 0.2;
}

.day-tile.today {
  border-color: var(--color-primary);
}

.day-tile-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
}

.tile-add {
  width: 1.5rem;
  height: 1.5rem;
}

.day-tile-notes {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  overflow: hidden;
}

.tile-note {
  text-align: left;
  background: var(--color-surface-alt);
  border-left: 3px solid var(--color-secondary);
  border-radius: 4px;
  padding: 0.2rem 0.4rem;
  display: flex;
  flex-direction: column;
  gap: 0.05rem;
  min-width: 0;
}

.tile-note-title {
  font-size: 0.75rem;
  font-weight: 600;
  color: var(--color-text);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tile-note-preview {
  font-size: 0.6875rem;
  color: var(--color-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

.tile-more {
  font-size: 0.6875rem;
  color: var(--color-secondary);
  text-align: left;
  padding: 0.1rem 0.4rem;
}

/* Day panel */
.day-panel {
  padding: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 1rem;
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

.create-form {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

.create-form-actions {
  display: flex;
  justify-content: flex-end;
  gap: 0.5rem;
}

.new-note-tile {
  align-self: flex-start;
}

.empty-state {
  color: var(--color-text-muted);
  text-align: center;
  padding: 1rem 0;
}

.day-notes-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
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
}

.day-note-title {
  font-weight: 600;
  color: var(--color-text);
}

.day-note-preview {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}

@media (max-width: 640px) {
  .page-header h1 {
    font-size: 1.25rem;
  }

  .month-label {
    min-width: 8rem;
    font-size: 1rem;
  }

  .day-number {
    font-size: 0.75rem;
  }

  .day-tile {
    min-height: 5.5rem;
  }

  .tile-note-title,
  .tile-note-preview {
    font-size: 0.625rem;
  }

  /* 7 narrow tile columns don't fit on phones - collapse to one scrollable column of day-cards. */
  .tile-grid {
    grid-template-columns: 1fr;
  }

  .day-tile.not-in-month {
    display: none;
  }

  .day-tile {
    min-height: 0;
    flex-direction: row;
    align-items: flex-start;
    gap: 0.5rem;
  }

  .day-tile-header {
    flex-direction: column;
    align-items: center;
    gap: 0.2rem;
    min-width: 2rem;
  }

  .day-tile-notes {
    flex: 1;
  }
}
</style>
