<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import AppLayout from '../components/AppLayout.vue'
import ToolbarIcon from '../components/ToolbarIcon.vue'

const auth = useAuthStore()
const router = useRouter()

const loading = ref(false)
const error = ref('')
const info = ref('')

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}

async function handleChangePassword() {
  error.value = ''
  info.value = ''
  loading.value = true
  try {
    await auth.requestPasswordChange()
    info.value = 'Wysłaliśmy link do zmiany hasła na Twój adres e-mail. Sprawdź skrzynkę.'
  } catch {
    error.value = 'Nie udało się wysłać e-maila. Spróbuj ponownie później.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppLayout>
    <div class="account-page">
      <div class="account-card card">
        <button class="back-link" @click="router.push({ name: 'notes' })">
          <ToolbarIcon name="arrow-left" />
          Wróć
        </button>
        <h1>Ustawienia konta</h1>
        <p class="username-row">
          Zalogowano jako <strong>{{ auth.username }}</strong>
        </p>

        <div class="actions">
          <router-link to="/devices" class="btn btn-outline"><ToolbarIcon name="device" /> Urządzenia</router-link>
          <button
            class="btn btn-secondary"
            :disabled="loading"
            @click="handleChangePassword"
          >
            {{ loading ? 'Wysyłanie…' : 'Zmień hasło' }}
          </button>
          <button class="btn btn-danger" @click="handleLogout">Wyloguj</button>
        </div>

        <p v-if="info" class="info-msg">{{ info }}</p>
        <p v-if="error" class="error-msg">{{ error }}</p>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.account-page {
  display: flex;
  justify-content: center;
  padding: 1rem 0;
}

.account-card {
  width: 100%;
  max-width: 420px;
  padding: 2rem;
}

.back-link {
  display: inline-flex;
  align-items: center;
  gap: 0.375rem;
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--color-secondary);
  margin-bottom: 1rem;
}

.back-link:hover {
  text-decoration: underline;
}

.account-card h1 {
  font-size: 1.5rem;
  color: var(--color-primary);
  margin-bottom: 0.75rem;
}

.username-row {
  color: var(--color-text-muted);
  margin-bottom: 1.5rem;
}

.actions {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}
</style>
