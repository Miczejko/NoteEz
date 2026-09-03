import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../api/client'

function monthKey(year, month, groupId = null) {
  return groupId ? `${groupId}:${year}-${String(month).padStart(2, '0')}` : `${year}-${String(month).padStart(2, '0')}`
}

export const useCalendarNotesStore = defineStore('calendarNotes', () => {
  const notesByMonth = ref({})
  const loading = ref(false)
  const error = ref(null)

  async function fetchMonth(year, month, force = false, groupId = null) {
    const key = monthKey(year, month, groupId)
    if (!force && notesByMonth.value[key]) return notesByMonth.value[key]

    loading.value = true
    error.value = null
    try {
      const params = { year, month }
      if (groupId) params.groupId = groupId
      const { data } = await api.get('/notes/calendar', { params })
      notesByMonth.value = { ...notesByMonth.value, [key]: data }
      return data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać notatek kalendarza'
      throw e
    } finally {
      loading.value = false
    }
  }

  function invalidateMonth(year, month, groupId = null) {
    const key = monthKey(year, month, groupId)
    const { [key]: _removed, ...rest } = notesByMonth.value
    notesByMonth.value = rest
  }

  // Wolane przy wylogowaniu - bez tego cache miesiecy zostalby w pamieci SPA i kolejny
  // uzytkownik logujacy sie w tej samej karcie (bez przeladowania) zobaczylby przez chwile
  // notatki poprzedniej osoby, bo fetchMonth() pomija fetch gdy miesiac jest juz w cache.
  function $reset() {
    notesByMonth.value = {}
    loading.value = false
    error.value = null
  }

  return {
    notesByMonth,
    loading,
    error,
    fetchMonth,
    invalidateMonth,
    $reset,
  }
})
