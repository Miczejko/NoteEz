import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  withCredentials: true,
})

// Import dynamiczny, zeby uniknac cyklu client.js <-> stores/auth.js przy starcie
// (auth.js importuje ten plik jako `api`).
api.interceptors.request.use(async (config) => {
  const isAuthRoute =
    config.url?.includes('/auth/login') ||
    config.url?.includes('/auth/register') ||
    config.url?.includes('/auth/refresh')

  if (!isAuthRoute) {
    const { useAuthStore } = await import('../stores/auth')
    const token = useAuthStore().accessToken
    if (token) {
      config.headers.Authorization = `Bearer ${token}`
    }
  }
  return config
})

// Access token zyje tylko 30 minut. Gdy wygasnie, pierwsze 401 probuje raz
// wymienic refresh-token cookie na nowy access token i powtarza oryginalne
// zadanie - dzieki temu uzytkownik nie jest wylogowywany co 30 minut.
// refreshPromise "spina" rownolegle 401-ki w jedno wywolanie /auth/refresh.
let refreshPromise = null

// Azure App Service Free tier has no "Always On" - after ~20min idle the backend
// (and the SQL Serverless DB behind it) can go to sleep, so the first request back
// hits a slow cold start. Without this, a single transient failure of /auth/refresh
// (timeout, 502/504, network drop) looked identical to a genuinely invalid refresh
// token and logged the user out - even though the refresh token was still valid in
// the DB. Only retry on transient failures; a real 401 from the refresh endpoint
// itself means the token is actually invalid/revoked, so that still logs out immediately.
const REFRESH_RETRY_DELAYS_MS = [1000, 3000]

function isTransientError(err) {
  const status = err.response?.status
  return !status || status >= 500
}

function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms))
}

async function refreshWithRetry() {
  const { useAuthStore } = await import('../stores/auth')
  const auth = useAuthStore()

  for (let attempt = 0; ; attempt++) {
    try {
      return await auth.refresh()
    } catch (err) {
      if (attempt >= REFRESH_RETRY_DELAYS_MS.length || !isTransientError(err)) throw err
      await sleep(REFRESH_RETRY_DELAYS_MS[attempt])
    }
  }
}

api.interceptors.response.use(
  (response) => response,
  async (error) => {
    const { config, response } = error
    const isAuthRoute =
      config?.url?.includes('/auth/login') ||
      config?.url?.includes('/auth/refresh') ||
      config?.url?.includes('/auth/logout')

    if (response?.status !== 401 || config._retried || isAuthRoute) {
      return Promise.reject(error)
    }
    config._retried = true

    try {
      if (!refreshPromise) {
        // import dynamiczny, zeby uniknac cyklu client.js <-> stores/auth.js przy starcie
        refreshPromise = refreshWithRetry()
      }
      const accessToken = await refreshPromise
      config.headers.Authorization = `Bearer ${accessToken}`
      return api(config)
    } catch (refreshError) {
      const { useAuthStore } = await import('../stores/auth')
      useAuthStore().logout()
      return Promise.reject(refreshError)
    } finally {
      refreshPromise = null
    }
  }
)

export default api
