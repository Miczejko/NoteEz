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

const showDeleteConfirm = ref(false)
const deletePassword = ref('')
const deleteError = ref('')
const deleteLoading = ref(false)

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}

async function handleDeleteAccount() {
  deleteError.value = ''
  deleteLoading.value = true
  try {
    await auth.deleteAccount(deletePassword.value)
    router.push({ name: 'home' })
  } catch (e) {
    deleteError.value =
      e.response?.status === 400 ? 'Nieprawidłowe hasło.' : 'Nie udało się usunąć konta.'
  } finally {
    deleteLoading.value = false
  }
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

        <div class="danger-zone">
          <h2>Usuń konto</h2>
          <p>
            Trwale usunie Twoje konto oraz wszystkie notatki, rysunki, nagrania i sparowane
            urządzenia. Tej operacji nie można cofnąć. Zobacz
            <router-link to="/polityka-prywatnosci">politykę prywatności</router-link>.
          </p>

          <button
            v-if="!showDeleteConfirm"
            class="btn btn-danger"
            @click="showDeleteConfirm = true"
          >
            Usuń konto
          </button>

          <form v-else @submit.prevent="handleDeleteAccount" class="delete-form">
            <label for="delete-password">Potwierdź hasłem</label>
            <input
              id="delete-password"
              v-model="deletePassword"
              type="password"
              class="input-field"
              autocomplete="current-password"
              required
            />
            <p v-if="deleteError" class="error-msg">{{ deleteError }}</p>
            <div class="delete-actions">
              <button type="submit" class="btn btn-danger" :disabled="deleteLoading">
                {{ deleteLoading ? 'Usuwanie…' : 'Potwierdź usunięcie konta' }}
              </button>
              <button
                type="button"
                class="btn btn-outline"
                @click="showDeleteConfirm = false; deletePassword = ''; deleteError = ''"
              >
                Anuluj
              </button>
            </div>
          </form>
        </div>
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

.danger-zone {
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid var(--color-border);
}

.danger-zone h2 {
  font-size: 1.0625rem;
  color: var(--color-danger, #c0392b);
  margin-bottom: 0.5rem;
}

.danger-zone p {
  font-size: 0.875rem;
  color: var(--color-text-muted);
  margin-bottom: 1rem;
}

.delete-form {
  display: flex;
  flex-direction: column;
  gap: 0.75rem;
}

.delete-form label {
  font-size: 0.875rem;
  font-weight: 600;
}

.delete-actions {
  display: flex;
  gap: 0.75rem;
}
</style>
