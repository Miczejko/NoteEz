<script setup>
import { ref } from 'vue'
import { useAuthStore } from '../stores/auth'
import AppLayout from '../components/AppLayout.vue'
import TurnstileWidget from '../components/TurnstileWidget.vue'

const auth = useAuthStore()

const username = ref('')
const email = ref('')
const password = ref('')
const confirmPassword = ref('')
const error = ref('')
const loading = ref(false)
const registered = ref(false)
const turnstileToken = ref('')
const turnstileWidget = ref(null)
const consentAccepted = ref(false)

const USERNAME_PATTERN = /^[a-zA-Z0-9_.-]{3,32}$/
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/

async function handleSubmit() {
  error.value = ''
  if (!USERNAME_PATTERN.test(username.value)) {
    error.value = 'Nazwa użytkownika: 3-32 znaki, tylko litery, cyfry, "_", "." i "-"'
    return
  }
  if (!EMAIL_PATTERN.test(email.value)) {
    error.value = 'Podaj poprawny adres e-mail'
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
  if (!turnstileToken.value) {
    error.value = 'Potwierdź, że nie jesteś botem'
    return
  }
  if (!consentAccepted.value) {
    error.value = 'Musisz zapoznać się z polityką prywatności i regulaminem'
    return
  }
  loading.value = true
  try {
    await auth.register(
      username.value,
      email.value,
      password.value,
      turnstileToken.value,
      consentAccepted.value,
    )
    registered.value = true
  } catch (e) {
    const data = e.response?.data
    if (typeof data === 'string') {
      error.value = data
    } else if (data?.errors) {
      error.value = Object.values(data.errors).flat().join(' ')
    } else if (e.response?.status === 409) {
      error.value = data || 'Użytkownik o tej nazwie lub e-mailu już istnieje'
    } else {
      error.value = 'Rejestracja nie powiodła się'
    }
    turnstileWidget.value?.reset()
    turnstileToken.value = ''
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
        <template v-if="registered">
          <div class="auth-header">
            <img src="/noteez-logo-tealtext.png" alt="NoteEz" class="auth-icon" />
            <h1>Sprawdź swoją skrzynkę</h1>
            <p>
              Wysłaliśmy link potwierdzający na adres <strong>{{ email }}</strong>. Kliknij go, aby
              dokończyć zakładanie konta.
            </p>
          </div>
          <p class="auth-footer">
            <router-link to="/login">Wróć do logowania</router-link>
          </p>
        </template>
        <template v-else>
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
                minlength="3"
                maxlength="32"
                pattern="[a-zA-Z0-9_.\-]+"
                required
              />
            </div>
            <div class="field">
              <label for="email">Adres e-mail</label>
              <input
                id="email"
                v-model="email"
                type="email"
                class="input-field"
                autocomplete="email"
                maxlength="256"
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
                minlength="8"
                maxlength="100"
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
                minlength="8"
                maxlength="100"
                required
              />
            </div>
            <label class="consent-field">
              <input type="checkbox" v-model="consentAccepted" required />
              <span>
                Zapoznałem się z
                <router-link to="/polityka-prywatnosci" target="_blank">polityką prywatności</router-link>
                i
                <router-link to="/regulamin" target="_blank">regulaminem</router-link>
                oraz je akceptuję.
              </span>
            </label>
            <TurnstileWidget
              ref="turnstileWidget"
              @verified="turnstileToken = $event"
              @expired="turnstileToken = ''"
            />
            <p v-if="error" class="error-msg">{{ error }}</p>
            <button type="submit" class="btn btn-primary auth-submit" :disabled="loading">
              {{ loading ? 'Rejestracja…' : 'Zarejestruj się' }}
            </button>
          </form>

          <p class="auth-footer">
            Masz już konto?
            <router-link to="/login">Zaloguj się</router-link>
          </p>
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

.consent-field {
  display: flex;
  align-items: flex-start;
  gap: 0.5rem;
  font-size: 0.8125rem;
  color: var(--color-text-muted);
  cursor: pointer;
}

.consent-field input {
  margin-top: 0.2rem;
  flex-shrink: 0;
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
