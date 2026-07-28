<script setup>
import ToolbarIcon from '../components/ToolbarIcon.vue'
import AppFooter from '../components/AppFooter.vue'
</script>

<template>
  <div class="home-page">
    <div class="home-glow" aria-hidden="true" />
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
        <div class="hero-copy">
          <span class="eyebrow">Twoje myśli, w spokoju</span>
          <h1>Spokojne miejsce<br />na każdą notatkę.</h1>
          <p class="hero-lead">
            Zapisz myśl na telefonie, dopracuj ją na tablecie i znajdź czekającą na komputerze.
            NoteEz trzyma wszystkie detale razem, w jednym miejscu.
          </p>
          <div class="hero-cta">
            <router-link to="/register" class="btn btn-primary btn-lg">Załóż darmowe konto</router-link>
          </div>
          <p class="hero-note">Bezpłatny start · Synchronizacja na każdym urządzeniu</p>
        </div>

        <div class="hero-mockup" aria-hidden="true">
          <div class="mockup-card">
            <div class="mockup-titlebar">
              <span>Moje notatki</span>
              <span class="mockup-sync">⟳ Zsynchronizowano</span>
            </div>
            <div class="mockup-body">
              <p class="mockup-section-title">Ostatnie notatki</p>
              <div class="mockup-note">
                <strong>Zakupy</strong>
                <span>Mleko, pieczywo, kawa</span>
              </div>
              <div class="mockup-note">
                <strong>Pomysły</strong>
                <span>Prosty rytuał na wieczór</span>
              </div>
              <div class="mockup-note">
                <strong>Pytania do researchu</strong>
                <span>Trzy rzeczy do sprawdzenia</span>
              </div>
            </div>
            <div class="mockup-footer">
              <ToolbarIcon name="device" />
              <span>Jedna notatka, wszędzie</span>
            </div>
          </div>
        </div>
      </section>

      <section class="follow-bar">
        <p>Twoja notatka podąża za Tobą a nie odwrotnie.</p>
        <span class="follow-hint">Działa wszędzie tam, gdzie Ty</span>
      </section>

      <section class="features-section">
        <span class="eyebrow">Zbudowane pod sposób, w jaki przychodzą myśli</span>
        <h2>Prosty notes, proste notatki.</h2>

        <div class="features">
          <div class="feature card">
            <span class="feature-icon"><ToolbarIcon name="bold" /></span>
            <h3>Bogaty edytor tekstu</h3>
            <p>Pogrubienie, kursywa, nagłówki, listy, checkboxy i własne kolory tekstu, wszystko zapisywane automatycznie.</p>
          </div>
          <div class="feature card">
            <span class="feature-icon"><ToolbarIcon name="drawing" /></span>
            <h3>Rysunki w notatce</h3>
            <p>Odręczne rysunki wektorowe wstawiane bezpośrednio w treść notatki, z dowolnym kolorem i grubością linii.</p>
          </div>
          <div class="feature card">
            <span class="feature-icon"><ToolbarIcon name="mic" /></span>
            <h3>Głosówki</h3>
            <p>Nagraj krótką notatkę głosową, gdy nie masz czasu pisać - zostanie dopięta do notatki.</p>
          </div>
          <div class="feature card">
            <span class="feature-icon"><ToolbarIcon name="device" /></span>
            <h3>Fizyczne urządzenie</h3>
            <p>Sparuj notatnik ESP32 i czytaj swoje notatki oraz rysunki na małym ekranie dotykowym.</p>
          </div>
        </div>
      </section>

      <section class="cta-section">
        <h2>Zrób miejsce na to, co ważne.</h2>
        <p>Załóż darmowe konto NoteEz i miej tę samą, znajomą notatkę na każdym urządzeniu.</p>
        <router-link to="/register" class="btn btn-primary btn-lg">Utwórz darmowe konto</router-link>
      </section>
    </div>

    <AppFooter />
  </div>
</template>

<style scoped>
.home-page {
  position: relative;
  min-height: 100dvh;
  background: var(--color-bg);
  color: var(--color-text);
}

/* Decorative radial glow + dot grid, echoing a dark hero-mockup style while
   staying within the app's own celadon/teal/black palette. */
.home-glow {
  position: absolute;
  inset: 0;
  height: 900px;
  pointer-events: none;
  z-index: 0;
  background:
    radial-gradient(circle at 18% 10%, rgba(121, 199, 197, 0.16), transparent 45%),
    radial-gradient(circle at 85% 5%, rgba(153, 209, 156, 0.14), transparent 42%),
    radial-gradient(circle at 50% 55%, rgba(115, 171, 132, 0.1), transparent 55%);
}

.home-glow::before {
  content: '';
  position: absolute;
  inset: 0;
  background-image: radial-gradient(rgba(234, 245, 240, 0.09) 1px, transparent 1px);
  background-size: 26px 26px;
  -webkit-mask-image: radial-gradient(ellipse 65% 55% at 50% 15%, black, transparent 70%);
  mask-image: radial-gradient(ellipse 65% 55% at 50% 15%, black, transparent 70%);
}

.home {
  position: relative;
  z-index: 1;
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

.eyebrow {
  display: inline-block;
  font-size: 0.75rem;
  font-weight: 700;
  letter-spacing: 0.08em;
  text-transform: uppercase;
  color: var(--color-primary);
  margin-bottom: 0.75rem;
}

.btn-lg {
  padding: 1rem 2rem;
  font-size: 1.0625rem;
}

/* --- Hero --- */

.hero {
  display: flex;
  align-items: center;
  gap: 2.5rem;
  padding: 3rem 1.5rem 4rem;
}

.hero-copy {
  flex: 1;
  min-width: 0;
}

.hero h1 {
  font-size: 2.5rem;
  line-height: 1.15;
  color: var(--color-text);
}

.hero-lead {
  margin-top: 1rem;
  font-size: 1.0625rem;
  color: var(--color-text-muted);
  max-width: 480px;
}

.hero-cta {
  margin-top: 1.5rem;
}

.hero-note {
  margin-top: 0.75rem;
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.hero-mockup {
  flex: 1;
  display: flex;
  justify-content: center;
  min-width: 0;
}

.mockup-card {
  width: 100%;
  max-width: 360px;
  border-radius: var(--radius);
  border: 1px solid var(--color-border);
  background: var(--color-surface);
  box-shadow: var(--shadow-lg);
  overflow: hidden;
}

.mockup-titlebar {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 0.75rem 1rem;
  background: var(--color-surface-alt);
  border-bottom: 1px solid var(--color-border);
  font-size: 0.8125rem;
  font-weight: 600;
}

.mockup-sync {
  font-size: 0.6875rem;
  font-weight: 500;
  color: var(--color-text-muted);
}

.mockup-body {
  padding: 1rem;
  display: flex;
  flex-direction: column;
  gap: 0.625rem;
}

.mockup-section-title {
  font-size: 0.6875rem;
  font-weight: 700;
  letter-spacing: 0.05em;
  text-transform: uppercase;
  color: var(--color-primary);
}

.mockup-note {
  display: flex;
  flex-direction: column;
  gap: 0.125rem;
  padding: 0.625rem 0.75rem;
  border-radius: var(--radius-sm);
  background: rgba(153, 209, 156, 0.08);
}

.mockup-note strong {
  font-size: 0.875rem;
}

.mockup-note span {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

.mockup-footer {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  padding: 0.75rem 1rem;
  border-top: 1px solid var(--color-border);
  font-size: 0.75rem;
  color: var(--color-text-muted);
}

.mockup-footer :deep(svg) {
  color: var(--color-primary);
}

/* --- Follow bar --- */

.follow-bar {
  display: flex;
  flex-wrap: wrap;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
  margin: 0 1.5rem;
  padding: 1rem 1.5rem;
  border-top: 1px solid var(--color-border);
  border-bottom: 1px solid var(--color-border);
}

.follow-bar p {
  font-weight: 600;
}

.follow-hint {
  font-size: 0.8125rem;
  color: var(--color-text-muted);
}

/* --- Features --- */

.features-section {
  padding: 4rem 1.5rem 1rem;
  text-align: center;
}

.features-section h2 {
  font-size: 1.75rem;
  color: var(--color-text);
  max-width: 520px;
  margin: 0 auto;
}

.features {
  display: grid;
  grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
  gap: 1.25rem;
  max-width: 960px;
  margin: 2rem auto 0;
}

.feature {
  padding: 1.5rem;
  text-align: center;
}

.feature-icon {
  display: flex;
  justify-content: center;
  color: var(--color-primary);
  margin-bottom: 0.75rem;
}

.feature-icon :deep(svg) {
  width: 2rem;
  height: 2rem;
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

/* --- CTA --- */

.cta-section {
  margin: 4rem 1.5rem 3rem;
  padding: 3rem 1.5rem;
  border-radius: var(--radius);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  text-align: center;
}

.cta-section h2 {
  font-size: 1.75rem;
  color: var(--color-text);
}

.cta-section p {
  margin: 0.75rem auto 1.5rem;
  max-width: 480px;
  color: var(--color-text-muted);
}

@media (max-width: 780px) {
  .hero {
    flex-direction: column;
    text-align: center;
    padding-top: 2rem;
  }

  .hero-lead {
    margin-left: auto;
    margin-right: auto;
  }

  .hero h1 {
    font-size: 2rem;
  }

  .follow-bar {
    flex-direction: column;
    text-align: center;
  }
}
</style>
