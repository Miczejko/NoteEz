<script setup>
import { ref } from 'vue'
import { useRouter, useRoute } from 'vue-router'
import { useAuthStore } from '../stores/auth'
import AppLayout from '../components/AppLayout.vue'
import TurnstileWidget from '../components/TurnstileWidget.vue'

const auth = useAuthStore()
const router = useRouter()
const route = useRoute()

const username = ref('')
const password = ref('')
const error = ref('')
const loading = ref(false)
const loginTurnstileToken = ref('')
const loginTurnstileWidget = ref(null)

const info = ref(
  route.query.verified === '1'
    ? 'Konto zostało potwierdzone. Możesz się teraz zalogować.'
    : route.query.verified === '0'
      ? 'Link weryfikacyjny jest nieprawidłowy lub wygasł.'
      : '',
)

const showForgotPassword = ref(false)
const forgotEmail = ref('')
const forgotLoading = ref(false)
const forgotSent = ref(false)
const forgotTurnstileToken = ref('')
const forgotTurnstileWidget = ref(null)

async function handleForgotPassword() {
  if (!forgotTurnstileToken.value) {
    return
  }
  forgotLoading.value = true
  try {
    await auth.forgotPassword(forgotEmail.value, forgotTurnstileToken.value)
    forgotSent.value = true
  } finally {
    forgotLoading.value = false
    forgotTurnstileWidget.value?.reset()
    forgotTurnstileToken.value = ''
  }
}

async function handleSubmit() {
  error.value = ''
  if (!loginTurnstileToken.value) {
    error.value = 'Potwierdź, że nie jesteś botem'
    return
  }
  loading.value = true
  try {
    await auth.login(username.value, password.value, loginTurnstileToken.value)
    router.push({ name: 'notes' })
  } catch {
    error.value = 'Nieprawidłowa nazwa użytkownika lub hasło'
    loginTurnstileWidget.value?.reset()
    loginTurnstileToken.value = ''
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
          <p>Zaloguj się do swoich notatek</p>
        </div>

        <form v-if="!showForgotPassword" @submit.prevent="handleSubmit" class="auth-form">
          <p v-if="info" class="info-msg">{{ info }}</p>
          <div class="field">
            <label for="username">Nazwa użytkownika</label>
            <input
              id="username"
              v-model="username"
              type="text"
              class="input-field"
              autocomplete="username"
              maxlength="32"
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
              autocomplete="current-password"
              maxlength="100"
              required
            />
          </div>
          <TurnstileWidget
            ref="loginTurnstileWidget"
            @verified="loginTurnstileToken = $event"
            @expired="loginTurnstileToken = ''"
          />
          <p v-if="error" class="error-msg">{{ error }}</p>
          <button type="submit" class="btn btn-primary auth-submit" :disabled="loading">
            {{ loading ? 'Logowanie…' : 'Zaloguj się' }}
          </button>
          <button
            type="button"
            class="btn btn-ghost auth-submit"
            @click="showForgotPassword = true"
          >
            Nie pamiętam hasła
          </button>
        </form>

        <div v-else class="auth-form">
          <template v-if="!forgotSent">
            <div class="field">
              <label for="forgot-email">Adres e-mail</label>
              <input
                id="forgot-email"
                v-model="forgotEmail"
                type="email"
                class="input-field"
                autocomplete="email"
                required
              />
            </div>
            <TurnstileWidget
              ref="forgotTurnstileWidget"
              @verified="forgotTurnstileToken = $event"
              @expired="forgotTurnstileToken = ''"
            />
            <button
              type="button"
              class="btn btn-primary auth-submit"
              :disabled="forgotLoading || !forgotTurnstileToken"
              @click="handleForgotPassword"
            >
              {{ forgotLoading ? 'Wysyłanie…' : 'Wyślij link do zmiany hasła' }}
            </button>
            <button
              type="button"
              class="btn btn-ghost auth-submit"
              @click="showForgotPassword = false"
            >
              Wróć do logowania
            </button>
          </template>
          <template v-else>
            <p class="info-msg">
              Jeśli konto z tym adresem e-mail istnieje, wysłaliśmy na nie link do zmiany hasła.
            </p>
            <button
              type="button"
              class="btn btn-primary auth-submit"
              @click="showForgotPassword = false"
            >
              Wróć do logowania
            </button>
          </template>
        </div>

        <p class="auth-footer">
          Nie masz konta?
          <router-link to="/register">Zarejestruj się</router-link>
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
  color: var(--color-text);
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
