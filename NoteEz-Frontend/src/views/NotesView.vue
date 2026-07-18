<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useNotesStore } from '../stores/notes'
import AppLayout from '../components/AppLayout.vue'
import NoteCard from '../components/NoteCard.vue'

const notesStore = useNotesStore()
const router = useRouter()

const showCreate = ref(false)
const newTitle = ref('')
const creating = ref(false)

onMounted(() => {
  notesStore.fetchAll()
})

async function handleCreate() {
  if (!newTitle.value.trim()) return
  creating.value = true
  try {
    const note = await notesStore.create(newTitle.value.trim())
    newTitle.value = ''
    showCreate.value = false
    router.push(`/notes/${note.id}`)
  } finally {
    creating.value = false
  }
}
</script>

<template>
  <AppLayout>
    <div class="notes-page">
      <div class="page-header">
        <h1>Moje notatki</h1>
        <div class="page-header-actions">
          <router-link to="/devices" class="btn btn-outline">📟 Urządzenia</router-link>
          <button class="btn btn-primary" @click="showCreate = !showCreate">
            {{ showCreate ? 'Anuluj' : '+ Nowa notatka' }}
          </button>
        </div>
      </div>

      <div v-if="showCreate" class="create-form card">
        <input
          v-model="newTitle"
          type="text"
          class="input-field"
          placeholder="Tytuł notatki…"
          @keyup.enter="handleCreate"
        />
        <button class="btn btn-accent" :disabled="creating || !newTitle.trim()" @click="handleCreate">
          {{ creating ? 'Tworzenie…' : 'Utwórz' }}
        </button>
      </div>

      <div v-if="notesStore.loading" class="state-msg">Ładowanie notatek…</div>
      <div v-else-if="notesStore.error" class="state-msg error-msg">{{ notesStore.error }}</div>
      <div v-else-if="!notesStore.notes.length" class="empty-state card">
        <span class="empty-icon">📋</span>
        <p>Brak notatek. Utwórz pierwszą!</p>
      </div>
      <div v-else class="notes-grid">
        <NoteCard v-for="note in notesStore.notes" :key="note.id" :note="note" />
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.notes-page {
  display: flex;
  flex-direction: column;
  gap: 1.25rem;
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
  flex-wrap: wrap;
}

.create-form {
  display: flex;
  gap: 0.75rem;
  padding: 1rem;
  flex-wrap: wrap;
}

.create-form .input-field {
  flex: 1;
  min-width: 200px;
}

.notes-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: 1rem;
}

.empty-state {
  text-align: center;
  padding: 3rem 1.5rem;
  color: var(--color-text-muted);
}

.empty-icon {
  font-size: 3rem;
  display: block;
  margin-bottom: 0.75rem;
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

  .page-header-actions {
    width: 100%;
  }

  .page-header-actions .btn {
    flex: 1;
  }

  .notes-grid {
    grid-template-columns: 1fr;
  }

  .create-form {
    flex-direction: column;
  }

  .create-form .btn {
    width: 100%;
  }
}
</style>
