import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '../api/client'

// payloadJson jest dowolnym stringiem JSON zwracanym przez backend - parsujemy
// defensywnie, zeby jeden zle sformatowany rekord nie wywalil calej listy powiadomien.
function parsePayload(json) {
  try {
    return JSON.parse(json)
  } catch {
    return {}
  }
}

export const useNotificationsStore = defineStore('notifications', () => {
  const notifications = ref([])
  const loading = ref(false)
  const error = ref(null)

  const unreadCount = computed(() => notifications.value.filter((n) => !n.isRead).length)

  function withPayload(n) {
    return { ...n, payload: parsePayload(n.payloadJson) }
  }

  async function fetchAll() {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get('/notifications')
      notifications.value = data.map(withPayload)
      return notifications.value
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać powiadomień'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function markRead(id) {
    await api.post(`/notifications/${id}/read`)
    const n = notifications.value.find((x) => x.id === id)
    if (n) n.isRead = true
  }

  async function markAllRead() {
    await api.post('/notifications/read-all')
    notifications.value.forEach((n) => (n.isRead = true))
  }

  // Wolane przez listener SignalR ReceiveNotification, zeby nowe powiadomienie
  // pojawilo sie na dzwonku natychmiast, bez czekania na kolejny fetchAll().
  function handleIncoming(notificationDto) {
    notifications.value.unshift(withPayload(notificationDto))
  }

  function $reset() {
    notifications.value = []
    loading.value = false
    error.value = null
  }

  return {
    notifications,
    unreadCount,
    loading,
    error,
    fetchAll,
    markRead,
    markAllRead,
    handleIncoming,
    $reset,
  }
})
