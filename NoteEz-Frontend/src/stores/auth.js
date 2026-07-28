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

  async function register(user, email, password) {
    await api.post('/auth/register', { username: user, email, password })
  }

  async function login(user, password) {
    const { data } = await api.post('/auth/login', { username: user, password })
    setSession(data.accessToken, user)
  }

  async function forgotPassword(userEmail) {
    await api.post('/auth/password/forgot', { email: userEmail })
  }

  async function requestPasswordChange() {
    await api.post('/auth/password/change-request')
  }

  async function resetPassword(token, newPassword) {
    await api.post('/auth/password/reset', { token, newPassword })
  }

  async function logout() {
    try {
      await api.post('/auth/logout')
    } catch {
      /* token may already be expired */
    }
    clearSession()
  }

  // Wymienia refresh-token cookie na nowy access token. Wolane przez interceptor
  // w api/client.js po kazdym 401, zeby uzytkownik nie musial sie logowac ponownie
  // co 30 minut (tyle zyje access token).
  async function refresh() {
    const { data } = await api.post('/auth/refresh')
    setSession(data.accessToken, data.username)
    return data.accessToken
  }

  return {
    accessToken,
    username,
    isAuthenticated,
    register,
    login,
    logout,
    refresh,
    forgotPassword,
    requestPasswordChange,
    resetPassword,
  }
})
