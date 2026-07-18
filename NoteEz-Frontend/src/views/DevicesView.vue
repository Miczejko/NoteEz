<script setup>
import { ref, onMounted, onUnmounted, computed } from 'vue'
import { useDevicesStore } from '../stores/devices'
import AppLayout from '../components/AppLayout.vue'

const devicesStore = useDevicesStore()

const pairingLoading = ref(false)
const revokingId = ref(null)
const now = ref(Date.now())
let clock = null

onMounted(() => {
  devicesStore.fetchAll()
  clock = setInterval(() => {
    now.value = Date.now()
  }, 1000)
})

onUnmounted(() => {
  clearInterval(clock)
})

const secondsLeft = computed(() => {
  if (!devicesStore.pairing) return 0
  const diff = Math.floor((new Date(devicesStore.pairing.expiresAt).getTime() - now.value) / 1000)
  return Math.max(0, diff)
})

const isExpired = computed(() => devicesStore.pairing && secondsLeft.value === 0)

function formatCountdown(sec) {
  const m = Math.floor(sec / 60)
  const s = sec % 60
  return `${m}:${s.toString().padStart(2, '0')}`
}

function formatDate(dateStr) {
  if (!dateStr) return '—'
  return new Date(dateStr).toLocaleDateString('pl-PL', {
    day: 'numeric',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  })
}

async function handlePairing() {
  pairingLoading.value = true
  try {
    await devicesStore.startPairing()
  } finally {
    pairingLoading.value = false
  }
}

async function handleRevoke(deviceId) {
  revokingId.value = deviceId
  try {
    await devicesStore.revoke(deviceId)
  } finally {
    revokingId.value = null
  }
}
</script>

<template>
  <AppLayout>
    <div class="devices-page">
      <div class="page-header">
        <div class="page-header-title">
          <router-link to="/" class="btn btn-ghost btn-sm back-btn">← Powrót</router-link>
          <h1>Urządzenia</h1>
        </div>
        <button class="btn btn-primary" :disabled="pairingLoading" @click="handlePairing">
          {{ pairingLoading ? 'Generowanie…' : '+ Sparuj urządzenie' }}
        </button>
      </div>

      <div v-if="devicesStore.pairing" class="pairing-card card">
        <span class="section-title">Kod parowania</span>
        <div class="pairing-code" :class="{ expired: isExpired }">{{ devicesStore.pairing.code }}</div>
        <p v-if="!isExpired" class="pairing-hint">
          Wpisz ten kod na ekranie konfiguracji ESP32. Kod wygaśnie za
          <strong>{{ formatCountdown(secondsLeft) }}</strong>.
        </p>
        <p v-else class="pairing-hint expired-hint">Kod wygasł. Wygeneruj nowy.</p>
        <button class="btn btn-ghost btn-sm" @click="devicesStore.clearPairing">Zamknij</button>
      </div>

      <div class="devices-section">
        <span class="section-title">Sparowane urządzenia</span>

        <div v-if="devicesStore.loading" class="state-msg">Ładowanie urządzeń…</div>
        <div v-else-if="devicesStore.error" class="state-msg error-msg">{{ devicesStore.error }}</div>
        <div v-else-if="!devicesStore.devices.length" class="empty-state card">
          <span class="empty-icon">📟</span>
          <p>Brak sparowanych urządzeń. Sparuj swoje ESP32!</p>
        </div>
        <div v-else class="devices-list">
          <div
            v-for="device in devicesStore.devices"
            :key="device.id"
            class="device-card card"
            :class="{ 'device-revoked': device.revoked }"
          >
            <div class="device-info">
              <div class="device-name-row">
                <span class="device-icon">📟</span>
                <span class="device-name">{{ device.name || 'Bez nazwy' }}</span>
                <span v-if="device.revoked" class="badge badge-revoked">Odwołane</span>
                <span v-else class="badge badge-active">Aktywne</span>
              </div>
              <div class="device-meta">
                <span>Sparowano: {{ formatDate(device.pairedAt) }}</span>
                <span>Ostatnio aktywne: {{ formatDate(device.lastSeenAt) }}</span>
              </div>
            </div>
            <button
              v-if="!device.revoked"
              class="btn btn-danger btn-sm"
              :disabled="revokingId === device.id"
              @click="handleRevoke(device.id)"
            >
              {{ revokingId === device.id ? 'Odwoływanie…' : 'Odwołaj' }}
            </button>
          </div>
        </div>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.devices-page {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.page-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  flex-wrap: wrap;
}

.page-header-title {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.page-header h1 {
  font-size: 1.5rem;
  color: var(--color-primary);
}

.back-btn {
  flex-shrink: 0;
}

.pairing-card {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 0.5rem;
  padding: 1.5rem;
  text-align: center;
}

.pairing-code {
  font-size: 2.25rem;
  font-weight: 700;
  letter-spacing: 0.2em;
  color: var(--color-primary);
  font-variant-numeric: tabular-nums;
}

.pairing-code.expired {
  color: var(--color-danger);
}

.pairing-hint {
  font-size: 0.875rem;
  color: var(--color-text-muted);
}

.expired-hint {
  color: var(--color-danger);
}

.devices-section {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.devices-list {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.device-card {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 1rem;
  padding: 1rem 1.25rem;
  flex-wrap: wrap;
}

.device-revoked {
  opacity: 0.6;
}

.device-info {
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
  min-width: 0;
}

.device-name-row {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
}

.device-icon {
  font-size: 1.125rem;
}

.device-name {
  font-weight: 600;
}

.device-meta {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.badge {
  font-size: 0.75rem;
  padding: 0.2rem 0.625rem;
  border-radius: 999px;
  font-weight: 600;
}

.badge-active {
  background: rgba(175, 227, 192, 0.5);
  color: #3a7a5a;
}

.badge-revoked {
  background: rgba(196, 92, 92, 0.15);
  color: var(--color-danger);
}

.empty-state {
  text-align: center;
  padding: 3rem 1.5rem;
  color: var(--color-text-muted);
}

.empty-icon {
  font-size: 3rem;
  display: block;
  margin-bottom: 0.75rem;
}

.state-msg {
  text-align: center;
  padding: 2rem;
  color: var(--color-text-muted);
}

@media (max-width: 640px) {
  .page-header h1 {
    font-size: 1.25rem;
  }

  .pairing-code {
    font-size: 1.75rem;
  }

  .device-card {
    flex-direction: column;
    align-items: stretch;
  }
}
</style>
