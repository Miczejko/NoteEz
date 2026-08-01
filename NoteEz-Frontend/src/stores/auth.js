import { defineStore } from 'pinia'
import { ref, computed } from 'vue'
import api from '../api/client'
import { useNotesStore } from './notes'
import { useCalendarNotesStore } from './calendarNotes'
import { useDevicesStore } from './devices'

export const useAuthStore = defineStore('auth', () => {
  // Token trzymany WYLACZNIE w pamieci (nie w localStorage) - localStorage jest
  // czytelny dla kazdego skryptu na stronie, wiec token XSS-owalny w localStorage
  // moze zostac wykradziony i uzyty poza przegladarka ofiary. Token w pamieci znika
  // razem z zamknieciem karty/odswiezeniem strony; sesje po odswiezeniu strony
  // odzyskujemy przez initialize() (cichy /auth/refresh na podstawie httpOnly cookie).
  const accessToken = ref(null)
  const username = ref(null)
  let initPromise = null

  const isAuthenticated = computed(() => !!accessToken.value)

  function setSession(token, name) {
    accessToken.value = token
    username.value = name
  }

  function clearSession() {
    accessToken.value = null
    username.value = null

    // Bez tego dane poprzedniego uzytkownika zostalyby w pamieci SPA (Pinia store
    // przezywa nawigacje) i byłyby widoczne, gdyby ktos inny zalogowal sie w tej
    // samej karcie bez pelnego przeladowania strony.
    useNotesStore().$reset()
    useCalendarNotesStore().$reset()
    useDevicesStore().$reset()
  }

  // Wolane raz przy starcie aplikacji (router guard) - probuje cicho wymienic
  // refresh-token cookie na nowy access token, zeby uzytkownik nie musial sie
  // logowac ponownie po kazdym odswiezeniu strony (skoro accessToken nie jest
  // juz trzymany w localStorage). Brak/nieprawidlowe cookie to normalny stan
  // wylogowania, nie blad.
  function initialize() {
    if (!initPromise) {
      initPromise = refresh().catch(() => clearSession())
    }
    return initPromise
  }

  async function register(user, email, password, turnstileToken, consentAccepted) {
    await api.post('/auth/register', {
      username: user,
      email,
      password,
      turnstileToken,
      consentAccepted,
    })
  }

  async function login(user, password, turnstileToken) {
    const { data } = await api.post('/auth/login', { username: user, password, turnstileToken })
    setSession(data.accessToken, user)
  }

  async function forgotPassword(userEmail, turnstileToken) {
    await api.post('/auth/password/forgot', { email: userEmail, turnstileToken })
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

  async function deleteAccount(password) {
    await api.delete('/account', { data: { password } })
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
    deleteAccount,
    refresh,
    initialize,
    forgotPassword,
    requestPasswordChange,
    resetPassword,
  }
})
