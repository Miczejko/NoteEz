<script setup>
import { useAuthStore } from '../stores/auth'
import { useRouter } from 'vue-router'

defineProps({
  showNav: { type: Boolean, default: true },
})

const auth = useAuthStore()
const router = useRouter()

async function handleLogout() {
  await auth.logout()
  router.push({ name: 'login' })
}
</script>

<template>
  <div class="layout">
    <header v-if="showNav" class="header">
      <router-link to="/" class="logo">
        <span class="logo-icon">📝</span>
        <span class="logo-text">NoteEz</span>
      </router-link>
      <div class="header-actions">
        <span v-if="auth.username" class="username">{{ auth.username }}</span>
        <button class="btn btn-ghost btn-sm" @click="handleLogout">Wyloguj</button>
      </div>
    </header>
    <main class="main">
      <slot />
    </main>
  </div>
</template>

<style scoped>
.layout {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
}

.header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.875rem 1.25rem;
  background: linear-gradient(135deg, var(--color-primary), var(--color-secondary));
  color: white;
  position: sticky;
  top: 0;
  z-index: 100;
  box-shadow: var(--shadow);
}

.logo {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: white;
  text-decoration: none;
  font-weight: 700;
  font-size: 1.25rem;
}

.logo:hover {
  text-decoration: none;
  opacity: 0.9;
}

.logo-icon {
  font-size: 1.5rem;
}

.header-actions {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.username {
  font-size: 0.875rem;
  opacity: 0.85;
}

.header-actions .btn-ghost {
  color: rgba(255, 255, 255, 0.9);
}

.header-actions .btn-ghost:hover {
  background: rgba(255, 255, 255, 0.15);
  color: white;
}

.main {
  flex: 1;
  padding: 1.25rem;
  max-width: 960px;
  width: 100%;
  margin: 0 auto;
}

@media (max-width: 640px) {
  .main {
    padding: 1rem;
  }

  .username {
    display: none;
  }
}
</style>
