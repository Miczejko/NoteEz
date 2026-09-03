<script setup>
import { ref, onMounted, onBeforeUnmount } from 'vue'
import { useRouter } from 'vue-router'
import { useNotificationsStore } from '../stores/notifications'
import { useGroupsStore } from '../stores/groups'
import signalr from '../api/signalr'
import ToolbarIcon from './ToolbarIcon.vue'

const notificationsStore = useNotificationsStore()
const groupsStore = useGroupsStore()
const router = useRouter()

const open = ref(false)
const respondingId = ref(null)

function handleReceiveNotification(dto) {
  notificationsStore.handleIncoming(dto)
}

onMounted(() => {
  notificationsStore.fetchAll().catch(() => {})
  signalr.on('ReceiveNotification', handleReceiveNotification)
})

onBeforeUnmount(() => {
  signalr.off('ReceiveNotification', handleReceiveNotification)
})

function toggle() {
  open.value = !open.value
}

function close() {
  open.value = false
}

async function handleMarkAllRead() {
  try {
    await notificationsStore.markAllRead()
  } catch {
    /* ignore */
  }
}

async function handleClickNotification(n) {
  if (!n.isRead) {
    try {
      await notificationsStore.markRead(n.id)
    } catch {
      /* ignore */
    }
  }
  if (n.type === 'Mention' && n.payload?.noteId) {
    close()
    router.push(`/notes/${n.payload.noteId}`)
  }
}

function inviteId(n) {
  return n.payload?.inviteId || n.payload?.id
}

async function respond(n, accept) {
  const id = inviteId(n)
  if (!id) return
  respondingId.value = n.id
  try {
    await groupsStore.respondToInvite(id, accept)
    await notificationsStore.markRead(n.id)
  } catch {
    /* ignore, user can retry */
  } finally {
    respondingId.value = null
  }
}

function formatDate(dateStr) {
  return new Date(dateStr).toLocaleDateString('pl-PL', {
    day: 'numeric',
    month: 'short',
    hour: '2-digit',
    minute: '2-digit',
  })
}
</script>

<template>
  <div class="notification-bell">
    <button
      class="btn bell-btn"
      aria-label="Powiadomienia"
      title="Powiadomienia"
      @click="toggle"
    >
      <ToolbarIcon name="bell" :size="22" />
      <span v-if="notificationsStore.unreadCount" class="unread-badge">{{ notificationsStore.unreadCount }}</span>
    </button>

    <div v-if="open" class="backdrop" @click="close" />

    <div v-if="open" class="dropdown card">
      <div class="dropdown-header">
        <span class="section-title">Powiadomienia</span>
        <button
          v-if="notificationsStore.unreadCount"
          class="btn btn-ghost btn-sm"
          @click="handleMarkAllRead"
        >
          Oznacz wszystkie
        </button>
      </div>

      <div v-if="notificationsStore.loading" class="state-msg">Ładowanie…</div>
      <div v-else-if="!notificationsStore.notifications.length" class="state-msg">Brak powiadomień.</div>
      <div v-else class="notification-list">
        <div
          v-for="n in notificationsStore.notifications"
          :key="n.id"
          class="notification-item"
          :class="{ unread: !n.isRead }"
          @click="handleClickNotification(n)"
        >
          <template v-if="n.type === 'GroupInvite'">
            <p class="notification-text">
              <strong>{{ n.payload?.invitedByUsername || 'Ktoś' }}</strong>
              zaprasza Cię do grupy
              <strong>{{ n.payload?.groupName || 'bez nazwy' }}</strong>
            </p>
            <div class="notification-actions">
              <button
                class="btn btn-accent btn-sm"
                :disabled="respondingId === n.id"
                @click.stop="respond(n, true)"
              >
                Akceptuj
              </button>
              <button
                class="btn btn-ghost btn-sm"
                :disabled="respondingId === n.id"
                @click.stop="respond(n, false)"
              >
                Odrzuć
              </button>
            </div>
          </template>
          <template v-else-if="n.type === 'Mention'">
            <p class="notification-text">
              <strong>{{ n.payload?.mentionedByUsername || 'Ktoś' }}</strong>
              oznaczył(a) Cię w notatce
              <strong>{{ n.payload?.noteTitle || 'bez tytułu' }}</strong>
              <span v-if="n.payload?.groupName"> w grupie {{ n.payload.groupName }}</span>
            </p>
          </template>
          <template v-else>
            <p class="notification-text">{{ n.type }}</p>
          </template>
          <time class="notification-date">{{ formatDate(n.createdAt) }}</time>
        </div>
      </div>
    </div>
  </div>
</template>

<style scoped>
.notification-bell {
  position: relative;
}

.bell-btn {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  width: 2.375rem;
  height: 2.375rem;
  padding: 0;
  background: rgba(255, 255, 255, 0.22);
  color: var(--color-on-accent);
  border-radius: 50%;
  border: 1px solid rgba(255, 255, 255, 0.35);
  transition: background 0.15s, transform 0.15s;
}

.bell-btn:hover {
  background: rgba(255, 255, 255, 0.34);
  transform: translateY(-1px);
}

.unread-badge {
  position: absolute;
  top: -0.25rem;
  right: -0.25rem;
  background: var(--color-danger);
  color: #fff;
  font-size: 0.6875rem;
  font-weight: 700;
  min-width: 1.1rem;
  height: 1.1rem;
  border-radius: 999px;
  display: flex;
  align-items: center;
  justify-content: center;
  padding: 0 0.25rem;
}

.backdrop {
  position: fixed;
  inset: 0;
  z-index: 150;
}

.dropdown {
  position: absolute;
  top: calc(100% + 0.5rem);
  right: 0;
  width: 320px;
  max-width: 90vw;
  max-height: 420px;
  overflow-y: auto;
  z-index: 151;
  padding: 0.875rem;
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
  color: var(--color-text);
}

.dropdown-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.notification-list {
  display: flex;
  flex-direction: column;
  gap: 0.5rem;
}

.notification-item {
  padding: 0.625rem 0.75rem;
  border-radius: var(--radius-sm);
  background: var(--color-surface-alt);
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  cursor: pointer;
}

.notification-item.unread {
  border-left: 3px solid var(--color-secondary);
}

.notification-text {
  font-size: 0.875rem;
}

.notification-actions {
  display: flex;
  gap: 0.5rem;
}

.notification-date {
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.state-msg {
  text-align: center;
  padding: 1.5rem;
  color: var(--color-text-muted);
  font-size: 0.875rem;
}
</style>
