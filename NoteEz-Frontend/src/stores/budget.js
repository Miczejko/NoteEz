import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../api/client'

export const useBudgetStore = defineStore('budget', () => {
  const categories = ref([])
  const entries = ref([])
  const stats = ref(null)
  const recurring = ref([])
  const dueSoon = ref([])
  const loading = ref(false)
  const error = ref(null)

  // groupId === null|undefined -> budzet osobisty zalogowanego usera
  function groupParam(groupId) {
    return groupId ? { groupId } : {}
  }

  async function fetchCategories(groupId) {
    const { data } = await api.get('/budget/categories', { params: groupParam(groupId) })
    categories.value = data
    return data
  }

  async function createCategory(groupId, { name, icon, color }) {
    const { data } = await api.post('/budget/categories', { groupId, name, icon, color })
    categories.value.push(data)
    return data
  }

  async function fetchEntries(groupId, { from, to, createdByUserId } = {}) {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get('/budget/entries', {
        params: { ...groupParam(groupId), from, to, createdByUserId },
      })
      entries.value = data
      return data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać wpisów budżetu'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function createEntry(groupId, { categoryId, type, amount, currency, description, date }) {
    const { data } = await api.post('/budget/entries', {
      groupId,
      categoryId,
      type,
      amount,
      currency,
      description,
      date,
    })
    entries.value.unshift(data)
    return data
  }

  async function updateEntry(entryId, { categoryId, type, amount, currency, description, date }) {
    const { data } = await api.put(`/budget/entries/${entryId}`, {
      categoryId,
      type,
      amount,
      currency,
      description,
      date,
    })
    const idx = entries.value.findIndex((e) => e.id === entryId)
    if (idx !== -1) entries.value[idx] = data
    return data
  }

  async function deleteEntry(entryId) {
    await api.delete(`/budget/entries/${entryId}`)
    entries.value = entries.value.filter((e) => e.id !== entryId)
  }

  async function fetchStats(groupId, { from, to } = {}) {
    const { data } = await api.get('/budget/stats', { params: { ...groupParam(groupId), from, to } })
    stats.value = data
    return data
  }

  async function fetchRecurring(groupId) {
    const { data } = await api.get('/budget/recurring', { params: groupParam(groupId) })
    recurring.value = data
    return data
  }

  async function fetchDueSoon(groupId, daysAhead = 3) {
    const { data } = await api.get('/budget/recurring/due-soon', {
      params: { ...groupParam(groupId), daysAhead },
    })
    dueSoon.value = data
    return data
  }

  async function createRecurring(groupId, { categoryId, type, amount, currency, description, frequency, nextDueDate }) {
    const { data } = await api.post('/budget/recurring', {
      groupId,
      categoryId,
      type,
      amount,
      currency,
      description,
      frequency,
      nextDueDate,
    })
    recurring.value.push(data)
    return data
  }

  async function deleteRecurring(recurringId) {
    await api.delete(`/budget/recurring/${recurringId}`)
    recurring.value = recurring.value.filter((r) => r.id !== recurringId)
    dueSoon.value = dueSoon.value.filter((r) => r.id !== recurringId)
  }

  async function scanReceipt(file) {
    const formData = new FormData()
    formData.append('file', file)
    const { data } = await api.post('/budget/receipts/scan', formData, {
      headers: { 'Content-Type': 'multipart/form-data' },
    })
    return data
  }

  async function confirmRecurring(recurringId, { amount, description, date }) {
    const { data } = await api.post(`/budget/recurring/${recurringId}/confirm`, { amount, description, date })
    entries.value.unshift(data)
    dueSoon.value = dueSoon.value.filter((r) => r.id !== recurringId)
    return data
  }

  function $reset() {
    categories.value = []
    entries.value = []
    stats.value = null
    recurring.value = []
    dueSoon.value = []
    loading.value = false
    error.value = null
  }

  return {
    categories,
    entries,
    stats,
    recurring,
    dueSoon,
    loading,
    error,
    fetchCategories,
    createCategory,
    fetchEntries,
    createEntry,
    updateEntry,
    deleteEntry,
    fetchStats,
    fetchRecurring,
    fetchDueSoon,
    createRecurring,
    deleteRecurring,
    confirmRecurring,
    scanReceipt,
    $reset,
  }
})
