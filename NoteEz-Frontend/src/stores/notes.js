import { defineStore } from 'pinia'
import { ref } from 'vue'
import api from '../api/client'

export const useNotesStore = defineStore('notes', () => {
  const notes = ref([])
  const currentNote = ref(null)
  const loading = ref(false)
  const error = ref(null)

  async function fetchAll() {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get('/notes')
      // Ensure drawings and audioClips are always arrays
      notes.value = data.map(note => ({
        ...note,
        drawings: note.drawings || [],
        audioClips: note.audioClips || []
      }))
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać notatek'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function fetchById(id) {
    loading.value = true
    error.value = null
    try {
      const { data } = await api.get(`/notes/${id}`)
      // Ensure drawings and audioClips are always arrays
      if (data) {
        data.drawings = data.drawings || []
        data.audioClips = data.audioClips || []
      }
      currentNote.value = data
      return data
    } catch (e) {
      error.value = e.response?.data || 'Nie udało się pobrać notatki'
      throw e
    } finally {
      loading.value = false
    }
  }

  async function create(title, textContent = null) {
    const { data } = await api.post('/notes', { title, textContent })
    notes.value.unshift(data)
    return data
  }

  async function update(id, payload) {
    await api.put(`/notes/${id}`, payload)
    if (currentNote.value?.id === id) {
      Object.assign(currentNote.value, payload)
    }
    const idx = notes.value.findIndex((n) => n.id === id)
    if (idx !== -1) {
      notes.value[idx] = { ...notes.value[idx], ...payload }
    }
  }

  async function remove(id) {
    await api.delete(`/notes/${id}`)
    notes.value = notes.value.filter((n) => n.id !== id)
    if (currentNote.value?.id === id) {
      currentNote.value = null
    }
  }

  async function addDrawing(noteId, strokesJson) {
    const { data } = await api.post(`/notes/${noteId}/drawings`, { strokesJson })
    if (currentNote.value?.id === noteId) {
      if (!currentNote.value.drawings) {
        currentNote.value.drawings = []
      }
      currentNote.value.drawings.push(data)
    }
    return data
  }

  async function updateDrawing(noteId, drawingId, strokesJson) {
    const { data } = await api.put(`/notes/${noteId}/drawings/${drawingId}`, { strokesJson })
    if (currentNote.value?.id === noteId) {
      if (!currentNote.value.drawings) {
        currentNote.value.drawings = []
      }
      const index = currentNote.value.drawings.findIndex((d) => d.id === drawingId)
      if (index !== -1) {
        currentNote.value.drawings[index] = data
      }
    }
    return data
  }

  async function deleteDrawing(noteId, drawingId) {
    await api.delete(`/notes/${noteId}/drawings/${drawingId}`)
    if (currentNote.value?.id === noteId && currentNote.value.drawings) {
      currentNote.value.drawings = currentNote.value.drawings.filter((d) => d.id !== drawingId)
    }
  }

  async function uploadAudio(noteId, file, durationSeconds) {
    const form = new FormData()
    form.append('file', file)
    form.append('durationSeconds', durationSeconds)
    const { data } = await api.post(`/notes/${noteId}/audio`, form)
    if (currentNote.value?.id === noteId) {
      // Ensure audioClips array exists
      if (!currentNote.value.audioClips) {
        currentNote.value.audioClips = []
      }
      currentNote.value.audioClips.push(data)
    }
    return data
  }

  async function deleteAudio(noteId, audioId) {
    await api.delete(`/notes/${noteId}/audio/${audioId}`)
    if (currentNote.value?.id === noteId && currentNote.value.audioClips) {
      currentNote.value.audioClips = currentNote.value.audioClips.filter((a) => a.id !== audioId)
    }
  }

  return {
    notes,
    currentNote,
    loading,
    error,
    fetchAll,
    fetchById,
    create,
    update,
    remove,
    addDrawing,
    updateDrawing,
    deleteDrawing,
    uploadAudio,
    deleteAudio,
  }
})
