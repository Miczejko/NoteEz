<script setup>
import { ref } from 'vue'
import { useRoute, useRouter } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import AppLayout from '../components/AppLayout.vue'

const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const token = route.query.token || ''
const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const loading = ref(false)
const done = ref(false)

async function handleSubmit() {
  error.value = ''
  if (!token) {
    error.value = 'Brak tokenu w linku. Sprawdź, czy link jest kompletny.'
    return
  }
  if (password.value.length < 8 || password.value.length > 100) {
    error.value = 'Hasło musi mieć od 8 do 100 znaków'
    return
  }
  if (password.value !== confirmPassword.value) {
    error.value = 'Hasła nie są identyczne'
    return
  }
  loading.value = true
  try {
    await auth.resetPassword(token, password.value)
    done.value = true
  } catch (e) {
    const data = e.response?.data
    error.value = typeof data === 'string' ? data : 'Nie udało się zmienić hasła. Link mógł wygasnąć.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <AppLayout :show-nav="false">
    <div class="auth-page">
      <div class="auth-card card">
        <template v-if="done">
          <div class="auth-header">
            <img src="/noteez-logo-tealtext.png" alt="NoteEz" class="auth-icon" />
            <h1>Hasło zmienione</h1>
            <p>Możesz się teraz zalogować nowym hasłem.</p>
          </div>
          <button class="btn btn-primary auth-submit" @click="router.push({ name: 'login' })">
            Przejdź do logowania
          </button>
        </template>
        <template v-else>
          <div class="auth-header">
            <img src="/noteez-logo-tealtext.png" alt="NoteEz" class="auth-icon" />
            <h1>Ustaw nowe hasło</h1>
          </div>

          <form @submit.prevent="handleSubmit" class="auth-form">
            <div class="field">
              <label for="password">Nowe hasło</label>
              <input
                id="password"
                v-model="password"
                type="password"
                class="input-field"
                autocomplete="new-password"
                minlength="8"
                maxlength="100"
                required
              />
            </div>
            <div class="field">
              <label for="confirm">Potwierdź nowe hasło</label>
              <input
                id="confirm"
                v-model="confirmPassword"
                type="password"
                class="input-field"
                autocomplete="new-password"
                minlength="8"
                maxlength="100"
                required
              />
            </div>
            <p v-if="error" class="error-msg">{{ error }}</p>
            <button type="submit" class="btn btn-primary auth-submit" :disabled="loading">
              {{ loading ? 'Zapisywanie…' : 'Ustaw nowe hasło' }}
            </button>
          </form>
        </template>
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
</style>
