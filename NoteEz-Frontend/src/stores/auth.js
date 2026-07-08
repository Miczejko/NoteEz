import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '../api/client'

export const useAuthStore = defineStore('auth', () => {
  const accessToken = ref(localStorage.getItem('accessToken') || null)
  const username = ref(localStorage.getItem('username') || null)

  const isAuthenticated = computed(() => !!accessToken.value)

  function setSession(token, name) {
    accessToken.value = token
    username.value = name
    localStorage.setItem('accessToken', token)
    localStorage.setItem('username', name)
  }

  function clearSession() {
    accessToken.value = null
    username.value = null
    localStorage.removeItem('accessToken')
    localStorage.removeItem('username')
  }

  async function register(user, password) {
    await api.post('/auth/register', { username: user, password })
  }

  async function login(user, password) {
    const { data } = await api.post('/auth/login', { username: user, password })
    setSession(data.accessToken, user)
  }

  async function logout() {
    try {
      await api.post('/auth/logout')
    } catch {
      /* token may already be expired */
    }
    clearSession()
  }

  return { accessToken, username, isAuthenticated, register, login, logout }
})
