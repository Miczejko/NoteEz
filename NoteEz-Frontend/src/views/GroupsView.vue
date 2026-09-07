<script setup>
import { ref, onMounted } from 'vue'
import { useRouter } from 'vue-router'
import { useGroupsStore } from '../stores/groups'
import AppLayout from '../components/AppLayout.vue'
import ToolbarIcon from '../components/ToolbarIcon.vue'

const groupsStore = useGroupsStore()
const router = useRouter()

const showCreate = ref(false)
const newName = ref('')
const creating = ref(false)

onMounted(() => {
  groupsStore.fetchGroups()
})

async function handleCreate() {
  if (!newName.value.trim()) return
  creating.value = true
  try {
    const group = await groupsStore.createGroup(newName.value.trim())
    newName.value = ''
    showCreate.value = false
    router.push(`/groups/${group.id}`)
  } finally {
    creating.value = false
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
    <div class="groups-page">
      <div class="groups-glow" aria-hidden="true" />
      <div class="page-header">
        <div class="page-header-title">
          <router-link :to="{ name: 'notes' }" class="btn btn-ghost btn-sm back-btn">← Powrót</router-link>
          <h1>Grupy</h1>
        </div>
        <button class="btn btn-primary" @click="showCreate = !showCreate">
          {{ showCreate ? 'Anuluj' : '+ Nowa grupa' }}
        </button>
      </div>

      <div v-if="showCreate" class="create-form card">
        <input
          v-model="newName"
          type="text"
          class="input-field"
          placeholder="Nazwa grupy…"
          maxlength="100"
          @keyup.enter="handleCreate"
        />
        <button class="btn btn-accent" :disabled="creating || !newName.trim()" @click="handleCreate">
          {{ creating ? 'Tworzenie…' : 'Utwórz' }}
        </button>
      </div>

      <div v-if="groupsStore.loading" class="state-msg">Ładowanie grup…</div>
      <div v-else-if="groupsStore.error" class="state-msg error-msg">{{ groupsStore.error }}</div>
      <div v-else-if="!groupsStore.groups.length" class="empty-state card">
        <span class="empty-icon"><ToolbarIcon name="users" /></span>
        <p>Nie należysz jeszcze do żadnej grupy. Utwórz pierwszą!</p>
      </div>
      <div v-else class="groups-grid">
        <router-link
          v-for="group in groupsStore.groups"
          :key="group.id"
          :to="`/groups/${group.id}`"
          class="group-card card"
        >
          <div class="group-card-header">
            <h3 class="group-name">{{ group.name }}</h3>
            <span class="badge" :class="group.role === 'Owner' ? 'badge-owner' : 'badge-member'">
              {{ group.role === 'Owner' ? 'Właściciel' : 'Członek' }}
            </span>
          </div>
          <div class="group-meta">
            <span>{{ group.memberCount }} {{ group.memberCount === 1 ? 'osoba' : 'osób' }}</span>
            <span>Utworzono: {{ formatDate(group.createdAt) }}</span>
          </div>
        </router-link>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.groups-page {
  position: relative;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.groups-glow {
  position: fixed;
  inset: 0;
  z-index: -1;
  pointer-events: none;
  background:
    radial-gradient(circle at 18% 0%, rgba(121, 199, 197, 0.14), transparent 45%),
    radial-gradient(circle at 90% 10%, rgba(153, 209, 156, 0.12), transparent 42%),
    radial-gradient(circle at 50% 60%, rgba(115, 171, 132, 0.08), transparent 55%);
}

.groups-glow::before {
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

.groups-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(260px, 1fr));
  gap: 1rem;
}

.group-card {
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
  padding: 1.125rem 1.25rem;
  text-decoration: none;
  color: inherit;
  transition: transform 0.15s, box-shadow 0.15s;
}

.group-card:hover {
  transform: translateY(-2px);
  box-shadow: var(--shadow-lg);
  text-decoration: none;
}

.group-card-header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: 0.75rem;
}

.group-name {
  font-size: 1.0625rem;
  font-weight: 600;
  color: var(--color-primary);
  min-width: 0;
  overflow: hidden;
  text-overflow: ellipsis;
}

.group-meta {
  display: flex;
  flex-direction: column;
  gap: 0.25rem;
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.badge {
  font-size: 0.75rem;
  padding: 0.2rem 0.625rem;
  border-radius: 999px;
  font-weight: 600;
  white-space: nowrap;
}

.badge-owner {
  background: rgba(153, 209, 156, 0.2);
  color: var(--celadon);
}

.badge-member {
  background: rgba(115, 171, 132, 0.2);
  color: var(--muted-teal);
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

  .groups-grid {
    grid-template-columns: 1fr;
  }
}
</style>
