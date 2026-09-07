import { HubConnectionBuilder, HttpTransportType, LogLevel } from '@microsoft/signalr'
import { ref } from 'vue'

// Malutki singleton wrapper wokol polaczenia SignalR z hubem app. VITE_API_URL
// wskazuje na baze API (np. https://api.example.com/api albo /api w dev), a hub
// jest siostrzana sciezka wzgledem /api, nie pod nim - stad trzeba odciac ewentualny
// sufiks /api zanim dolozymy /hubs/app.
function hubUrl() {
  const apiUrl = import.meta.env.VITE_API_URL || '/api'
  const base = apiUrl.replace(/\/api\/?$/, '')
  return `${base}/hubs/app`
}

let connection = null
const connectionState = ref('Disconnected')
const pendingHandlers = [] // { event, handler } zarejestrowane zanim polaczenie istnialo

function buildConnection() {
  const conn = new HubConnectionBuilder()
    .withUrl(hubUrl(), {
      // accessTokenFactory samo dolacza token jako ?access_token= przy WebSockets/SSE,
      // wiec nie trzeba go recznie doklejac do URL.
      accessTokenFactory: async () => {
        const { useAuthStore } = await import('../stores/auth')
        return useAuthStore().accessToken
      },
      transport: HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents | HttpTransportType.LongPolling,
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()

  conn.onreconnecting(() => {
    connectionState.value = 'Reconnecting'
  })
  conn.onreconnected(() => {
    connectionState.value = 'Connected'
  })
  conn.onclose(() => {
    connectionState.value = 'Disconnected'
  })

  // Ponownie podpinamy handlery zarejestrowane przed startem polaczenia.
  for (const { event, handler } of pendingHandlers) {
    conn.on(event, handler)
  }

  return conn
}

async function connect() {
  if (connection && connectionState.value !== 'Disconnected') return connection
  connection = buildConnection()
  try {
    await connection.start()
    connectionState.value = 'Connected'
  } catch (e) {
    connectionState.value = 'Disconnected'
    throw e
  }
  return connection
}

async function disconnect() {
  if (!connection) return
  try {
    await connection.stop()
  } catch {
    /* ignore */
  } finally {
    connection = null
    connectionState.value = 'Disconnected'
  }
}

function on(event, handler) {
  pendingHandlers.push({ event, handler })
  connection?.on(event, handler)
}

function off(event, handler) {
  const idx = pendingHandlers.findIndex((h) => h.event === event && h.handler === handler)
  if (idx !== -1) pendingHandlers.splice(idx, 1)
  connection?.off(event, handler)
}

async function invoke(method, ...args) {
  if (!connection || connectionState.value !== 'Connected') return
  return connection.invoke(method, ...args)
}

export default {
  connect,
  disconnect,
  on,
  off,
  invoke,
  connectionState,
}
