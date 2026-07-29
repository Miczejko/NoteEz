import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../api/client'

function monthKey(year, month) {
  return `${year}-${String(month).padStart(2, '0')}`
}

export const useCalendarNotesStore = defineStore('calendarNotes', () => {
  const notesByMonth = ref({})
  const loading = ref(false)
  const error = ref(null)

  async function fetchMonth(year, month, force = false) {
    const key = monthKey(year, month)
    if (!force && notesByMonth.value[key]) return notesByMonth.value[key]

    loading.value = true
    error.value = null
    try {
      const { data } = await api.get('/notes/calendar', { params: { year, month } })
      notesByMonth.value = { ...notesByMonth.value, [key]: data }
      return data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać notatek kalendarza'
      throw e
    } finally {
      loading.value = false
    }
  }

  function invalidateMonth(year, month) {
    const key = monthKey(year, month)
    const { [key]: _removed, ...rest } = notesByMonth.value
    notesByMonth.value = rest
  }

  return {
    notesByMonth,
    loading,
    error,
    fetchMonth,
    invalidateMonth,
  }
})
