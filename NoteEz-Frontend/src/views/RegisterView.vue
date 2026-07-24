<script setup>
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import AppLayout from '../components/AppLayout.vue'

const auth = useAuthStore()
const router = useRouter()

const username = ref('')
const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const loading = ref(false)

async function handleSubmit() {
  error.value = ''
  if (password.value !== confirmPassword.value) {
    error.value = 'Hasła nie są identyczne'
    return
  }
  if (password.value.length < 6) {
    error.value = 'Hasło musi mieć co najmniej 6 znaków'
    return
  }
  loading.value = true
  try {
    await auth.register(username.value, password.value)
    await auth.login(username.value, password.value)
    router.push({ name: 'notes' })
  } catch (e) {
    error.value = e.response?.data || 'Rejestracja nie powiodła się'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppLayout :show-nav="false">
    <div class="auth-page">
      <router-link :to="{ name: 'home' }" class="back-link">← Wróć</router-link>
      <div class="auth-card card">
        <div class="auth-header">
          <img src="/noteez-logo-tealtext.png" alt="NoteEz" class="auth-icon" />
          <h1>Utwórz konto</h1>
          <p>Dołącz do NoteEz</p>
        </div>

        <form @submit.prevent="handleSubmit" class="auth-form">
          <div class="field">
            <label for="username">Nazwa użytkownika</label>
            <input
              id="username"
              v-model="username"
              type="text"
              class="input-field"
              autocomplete="username"
              required
            />
          </div>
          <div class="field">
            <label for="password">Hasło</label>
            <input
              id="password"
              v-model="password"
              type="password"
              class="input-field"
              autocomplete="new-password"
              required
            />
          </div>
          <div class="field">
            <label for="confirm">Potwierdź hasło</label>
            <input
              id="confirm"
              v-model="confirmPassword"
              type="password"
              class="input-field"
              autocomplete="new-password"
              required
            />
          </div>
          <p v-if="error" class="error-msg">{{ error }}</p>
          <button type="submit" class="btn btn-primary auth-submit" :disabled="loading">
            {{ loading ? 'Rejestracja…' : 'Zarejestruj się' }}
          </button>
        </form>

        <p class="auth-footer">
          Masz już konto?
          <router-link to="/login">Zaloguj się</router-link>
        </p>
      </div>
    </div>
  </AppLayout>
</template>

<style scoped>
.auth-page {
  position: relative;
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: calc(100dvh - 2.5rem);
  padding: 1rem 0;
}

.back-link {
  position: absolute;
  top: 1rem;
  left: 1rem;
  font-size: 0.9375rem;
  font-weight: 600;
  color: var(--color-secondary);
}

.auth-card {
  width: 100%;
  max-width: 420px;
  padding: 2rem;
}

.auth-header {
  text-align: center;
  margin-bottom: 2rem;
}

.auth-icon {
  height: 5.5rem;
  width: auto;
  max-width: 100%;
  object-fit: contain;
  display: block;
  margin: 0 auto 0.75rem;
}

.auth-header h1 {
  font-size: 1.75rem;
  color: var(--color-primary);
  margin-bottom: 0.25rem;
}

.auth-header p {
  color: var(--color-text-muted);
  font-size: 0.9375rem;
}

.auth-form {
  display: flex;
  flex-direction: column;
  gap: 1rem;
}

.field label {
  display: block;
  font-size: 0.875rem;
  font-weight: 600;
  margin-bottom: 0.375rem;
}

.auth-submit {
  width: 100%;
  margin-top: 0.5rem;
  padding: 0.875rem;
}

.auth-footer {
  text-align: center;
  margin-top: 1.5rem;
  font-size: 0.9375rem;
  color: var(--color-text-muted);
}
</style>
