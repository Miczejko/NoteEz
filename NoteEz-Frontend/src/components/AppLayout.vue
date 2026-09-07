<script setup>
import { useAuthStore } from '../stores/auth'
import AppFooter from './AppFooter.vue'
import ToolbarIcon from './ToolbarIcon.vue'
import NotificationBell from './NotificationBell.vue'

defineProps({
  showNav: { type: Boolean, default: true },
})

const auth = useAuthStore()
</script>

<template>
  <div class="layout">
    <header v-if="showNav" class="header">
      <router-link :to="auth.isAuthenticated ? { name: 'notes' } : { name: 'home' }" class="logo">
        <img src="/noteez-logo.png" alt="NoteEz" class="logo-icon" />
      </router-link>
      <div class="header-actions">
        <span v-if="auth.username" class="username">{{ auth.username }}</span>
        <router-link
          v-if="auth.isAuthenticated"
          :to="{ name: 'groups' }"
          class="btn groups-link"
          aria-label="Grupy"
          title="Grupy"
        >
          <ToolbarIcon name="users" :size="20" />
          <span class="groups-label">Grupy</span>
        </router-link>
        <NotificationBell v-if="auth.isAuthenticated" />
        <router-link
          v-if="auth.isAuthenticated"
          :to="{ name: 'account' }"
          class="btn btn-ghost btn-icon settings-link"
          aria-label="Ustawienia konta"
          title="Ustawienia konta"
        >
          <ToolbarIcon name="gear" :size="22" />
        </router-link>
      </div>
    </header>
    <main class="main">
      <slot />
    </main>
    <AppFooter />
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
  color: var(--color-on-accent);
  position: sticky;
  top: 0;
  z-index: 100;
  box-shadow: var(--shadow);
}

.logo {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  color: var(--color-on-accent);
  text-decoration: none;
  font-weight: 700;
  font-size: 1.25rem;
}

.logo:hover {
  text-decoration: none;
  opacity: 0.9;
}

.logo-icon {
  height: 2.25rem;
  width: auto;
  object-fit: contain;
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
  color: rgba(0, 5, 1, 0.75);
}

.header-actions .btn-ghost:hover {
  background: rgba(0, 5, 1, 0.1);
  color: var(--color-on-accent);
}

.groups-link {
  display: flex;
  align-items: center;
  gap: 0.4rem;
  padding: 0.45rem 0.9rem;
  background: rgba(255, 255, 255, 0.22);
  color: var(--color-on-accent);
  font-weight: 700;
  font-size: 0.9375rem;
  border-radius: 999px;
  border: 1px solid rgba(255, 255, 255, 0.35);
  text-decoration: none;
  transition: background 0.15s, transform 0.15s;
}

.groups-link:hover {
  background: rgba(255, 255, 255, 0.34);
  text-decoration: none;
  transform: translateY(-1px);
}

@media (max-width: 640px) {
  .groups-label {
    display: none;
  }

  .groups-link {
    padding: 0.5rem;
    border-radius: 50%;
  }
}

.main {
  flex: 1;
  min-width: 0;
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
