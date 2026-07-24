import axios from 'axios'

const api = axios.create({
  baseURL: import.meta.env.VITE_API_URL || '/api',
  withCredentials: true,
})

api.interceptors.request.use((config) => {
  const token = localStorage.getItem('accessToken')
  if (token) {
    config.headers.Authorization = `Bearer ${token}`
  }
  return config
})

// Access token zyje tylko 30 minut. Gdy wygasnie, pierwsze 401 probuje raz
// wymienic refresh-token cookie na nowy access token i powtarza oryginalne
// zadanie - dzieki temu uzytkownik nie jest wylogowywany co 30 minut.
// refreshPromise "spina" rownolegle 401-ki w jedno wywolanie /auth/refresh.
let refreshPromise = null

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
        refreshPromise = import('../stores/auth').then(({ useAuthStore }) => useAuthStore().refresh())
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
