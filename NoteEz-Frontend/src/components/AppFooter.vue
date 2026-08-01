<script setup>
const year = new Date().getFullYear()
</script>

<template>
  <footer class="app-footer">
    <span class="footer-copy">© {{ year }} NoteEz</span>
    <nav class="footer-links">
      <router-link to="/polityka-prywatnosci">Polityka prywatności</router-link>
      <router-link to="/regulamin">Regulamin</router-link>
    </nav>
  </footer>
</template>

<style scoped>
.app-footer {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.75rem;
  padding: 1.25rem 1.5rem;
  border-top: 1px solid var(--color-border);
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.footer-links {
  display: flex;
  gap: 1rem;
}

.footer-links a {
  color: var(--color-text-muted);
}

.footer-links a:hover {
  color: var(--color-primary);
}
</style>
