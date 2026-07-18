import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../api/client'

export const useDevicesStore = defineStore('devices', () => {
  const devices = ref([])
  const pairing = ref(null) // { code, expiresAt }
  const loading = ref(false)
  const error = ref(null)

  async function fetchAll() {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get('/devices')
      devices.value = data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać urządzeń'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function startPairing() {
    error.value = null
    try {
      const { data } = await api.post('/devices/pair-init')
      pairing.value = data
      return data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się wygenerować kodu parowania'
      throw e
    }
  }

  function clearPairing() {
    pairing.value = null
  }

  async function revoke(deviceId) {
    await api.delete(`/devices/${deviceId}`)
    const idx = devices.value.findIndex((d) => d.id === deviceId)
    if (idx !== -1) {
      devices.value[idx] = { ...devices.value[idx], revoked: true }
    }
  }

  return {
    devices,
    pairing,
    loading,
    error,
    fetchAll,
    startPairing,
    clearPairing,
    revoke,
  }
})
