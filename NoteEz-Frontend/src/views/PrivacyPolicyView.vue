<script setup>
import AppLayout from '../components/AppLayout.vue'
</script>

<template>
  <AppLayout>
    <div class="policy card">
      <h1>Polityka prywatności</h1>
      <p class="updated">Ostatnia aktualizacja: 28 lipca 2026 r.</p>

      <section>
        <h2>1. Administrator danych</h2>
        <p>
          Administratorem danych osobowych zbieranych za pośrednictwem aplikacji NoteEz jest
          <strong>[nazwa administratora / firmy]</strong>, kontakt:
          <strong>[adres e-mail kontaktowy]</strong>. W sprawach dotyczących ochrony danych osobowych
          można kontaktować się pod powyższym adresem.
        </p>
      </section>

      <section>
        <h2>2. Jakie dane zbieramy</h2>
        <ul>
          <li>Dane konta: adres e-mail, nazwa użytkownika, hasło (przechowywane w formie zahaszowanej).</li>
          <li>Treść notatek: tekst, rysunki odręczne oraz nagrania głosowe dodane przez użytkownika.</li>
          <li>Dane urządzeń: identyfikatory sparowanych urządzeń ESP32 oraz znaczniki czasu ich aktywności.</li>
          <li>Dane techniczne: adres IP, typ przeglądarki i podstawowe logi serwera, przetwarzane w celu zapewnienia bezpieczeństwa usługi.</li>
        </ul>
      </section>

      <section>
        <h2>3. Cele i podstawy prawne przetwarzania</h2>
        <ul>
          <li>
            <strong>Świadczenie usługi</strong> (zakładanie konta, zapis i synchronizacja notatek) —
            art. 6 ust. 1 lit. b RODO (niezbędność do wykonania umowy).
          </li>
          <li>
            <strong>Bezpieczeństwo i przeciwdziałanie nadużyciom</strong> — art. 6 ust. 1 lit. f RODO
            (prawnie uzasadniony interes administratora).
          </li>
          <li>
            <strong>Obowiązki prawne</strong> (np. rozpatrywanie reklamacji) — art. 6 ust. 1 lit. c RODO.
          </li>
        </ul>
      </section>

      <section>
        <h2>4. Okres przechowywania danych</h2>
        <p>
          Dane konta i treść notatek przechowywane są przez czas istnienia konta użytkownika.
          Po usunięciu konta dane są usuwane lub anonimizowane, chyba że ich dłuższe przechowywanie
          wynika z obowiązujących przepisów prawa.
        </p>
      </section>

      <section>
        <h2>5. Odbiorcy danych</h2>
        <p>
          Dane mogą być powierzane podmiotom świadczącym usługi hostingowe i infrastrukturalne
          niezbędne do działania aplikacji, na podstawie umów powierzenia przetwarzania danych
          zgodnych z RODO. Dane nie są sprzedawane ani udostępniane w celach marketingowych osobom trzecim.
        </p>
      </section>

      <section>
        <h2>6. Prawa użytkownika</h2>
        <p>Zgodnie z RODO przysługuje Ci prawo do:</p>
        <ul>
          <li>dostępu do swoich danych oraz otrzymania ich kopii,</li>
          <li>sprostowania (poprawiania) danych,</li>
          <li>usunięcia danych ("prawo do bycia zapomnianym"),</li>
          <li>ograniczenia przetwarzania,</li>
          <li>przenoszenia danych,</li>
          <li>wniesienia sprzeciwu wobec przetwarzania opartego na prawnie uzasadnionym interesie,</li>
          <li>wniesienia skargi do Prezesa Urzędu Ochrony Danych Osobowych (UODO).</li>
        </ul>
      </section>

      <section>
        <h2>7. Przechowywanie sesji i pliki cookies</h2>
        <p>
          Aplikacja wykorzystuje mechanizm <code>localStorage</code> przeglądarki do przechowywania
          tokenu sesji, dzięki czemu logowanie utrzymuje się między odwiedzinami. Nie wykorzystujemy
          plików cookies do celów marketingowych ani śledzenia użytkowników w innych serwisach.
        </p>
      </section>

      <section>
        <h2>8. Bezpieczeństwo</h2>
        <p>
          Stosujemy odpowiednie środki techniczne i organizacyjne w celu ochrony danych przed
          nieuprawnionym dostępem, utratą lub zniszczeniem, w tym szyfrowanie połączenia (HTTPS)
          oraz zahaszowane przechowywanie haseł.
        </p>
      </section>

      <section>
        <h2>9. Kontakt</h2>
        <p>
          W sprawach związanych z ochroną danych osobowych skontaktuj się z nami pod adresem:
          <strong>[adres e-mail kontaktowy]</strong>.
        </p>
      </section>
    </div>
  </AppLayout>
</template>

<style scoped>
.policy {
  max-width: 760px;
  margin: 0 auto;
  padding: 2rem;
  line-height: 1.6;
}

.policy h1 {
  color: var(--color-primary);
  margin-bottom: 0.25rem;
}

.updated {
  color: var(--color-text-muted);
  font-size: 0.875rem;
  margin-bottom: 1.5rem;
}

.policy section {
  margin-top: 1.75rem;
}

.policy h2 {
  font-size: 1.0625rem;
  color: var(--color-text);
  margin-bottom: 0.5rem;
}

.policy p,
.policy li {
  color: var(--color-text-muted);
  font-size: 0.9375rem;
}

.policy ul {
  padding-left: 1.25rem;
  display: flex;
  flex-direction: column;
  gap: 0.375rem;
}

.policy code {
  background: rgba(255, 255, 255, 0.08);
  padding: 0.1rem 0.35rem;
  border-radius: 4px;
}
</style>
