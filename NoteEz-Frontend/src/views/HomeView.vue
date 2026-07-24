<script setup>
</script>

<template>
  <div class="home-page">
    <div class="home">
      <header class="home-header">
        <div class="home-logo">
          <img src="/noteez-logo-tealtext.png" alt="NoteEz" class="home-logo-img" />
        </div>
        <div class="home-header-actions">
          <router-link to="/login" class="btn btn-ghost btn-sm">Zaloguj się</router-link>
          <router-link to="/register" class="btn btn-outline btn-sm">Zarejestruj się</router-link>
        </div>
      </header>

      <section class="hero">
        <h1>Notatki, które są zawsze pod ręką</h1>
        <p class="hero-lead">
          NoteEz to prosty notatnik online z edytorem tekstu (pogrubienia, kolory, listy, checkboxy),
          odręcznymi rysunkami wektorowymi i głosówkami — dostępny w przeglądarce, a dodatkowo
          synchronizowany z fizycznym notatnikiem na ekranie ESP32.
        </p>

        <div class="hero-cta">
          <router-link to="/register" class="btn btn-primary btn-lg">Zacznij za darmo</router-link>
          <router-link to="/login" class="btn btn-outline btn-lg">Mam już konto</router-link>
        </div>
      </section>

      <section class="features">
        <div class="feature card">
          <span class="feature-icon">✍️</span>
          <h3>Bogaty edytor tekstu</h3>
          <p>Pogrubienie, kursywa, nagłówki, listy, checkboxy i własne kolory tekstu — wszystko zapisywane automatycznie.</p>
        </div>
        <div class="feature card">
          <span class="feature-icon">🖊️</span>
          <h3>Rysunki w notatce</h3>
          <p>Odręczne rysunki wektorowe wstawiane bezpośrednio w treść notatki, z dowolnym kolorem i grubością linii.</p>
        </div>
        <div class="feature card">
          <span class="feature-icon">🎙️</span>
          <h3>Głosówki</h3>
          <p>Nagraj krótką notatkę głosową, gdy nie masz czasu pisać — zostanie dopięta do notatki.</p>
        </div>
        <div class="feature card">
          <span class="feature-icon">📟</span>
          <h3>Fizyczne urządzenie</h3>
          <p>Sparuj notatnik ESP32 i czytaj swoje notatki oraz rysunki na małym ekranie dotykowym.</p>
        </div>
      </section>

      <footer class="home-footer">
        <p>Gotowy, by zacząć?</p>
        <router-link to="/register" class="btn btn-primary btn-lg">Utwórz darmowe konto</router-link>
      </footer>
    </div>
  </div>
</template>

<style scoped>
.home-page {
  min-height: 100dvh;
  background: var(--color-bg);
  color: var(--color-text);
}

.home {
  min-height: 100dvh;
  display: flex;
  flex-direction: column;
  max-width: 1100px;
  margin: 0 auto;
}

.home-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1rem 1.5rem;
}

.home-logo-img {
  height: 2.25rem;
  width: auto;
  object-fit: contain;
}

.home-header-actions {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.hero {
  flex: 1;
  display: flex;
  flex-direction: column;
  align-items: center;
  justify-content: center;
  text-align: center;
  gap: 1.25rem;
  padding: 3rem 1.5rem;
  max-width: 680px;
  margin: 0 auto;
}

.hero h1 {
  font-size: 2.25rem;
  color: var(--color-primary);
  line-height: 1.2;
}

.hero-lead {
  font-size: 1.0625rem;
  color: var(--color-text-muted);
  max-width: 560px;
}

.hero-cta {
  display: flex;
  gap: 1rem;
  flex-wrap: wrap;
  justify-content: center;
  margin-top: 0.5rem;
}

.btn-lg {
  padding: 1rem 2rem;
  font-size: 1.0625rem;
}

.features {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.25rem;
  max-width: 960px;
  margin: 0 auto;
  padding: 1.5rem;
  width: 100%;
}

.feature {
  padding: 1.5rem;
  text-align: center;
}

.feature-icon {
  font-size: 2rem;
  display: block;
  margin-bottom: 0.75rem;
}

.feature h3 {
  font-size: 1.0625rem;
  color: var(--color-text);
  margin-bottom: 0.5rem;
}

.feature p {
  font-size: 0.875rem;
  color: var(--color-text-muted);
}

.home-footer {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
  padding: 3rem 1.5rem;
  text-align: center;
}

.home-footer p {
  color: var(--color-text-muted);
  font-size: 1.0625rem;
}

@media (max-width: 640px) {
  .hero h1 {
    font-size: 1.75rem;
  }
}
</style>
