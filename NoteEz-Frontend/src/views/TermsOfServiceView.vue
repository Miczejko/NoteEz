<script setup>
import AppLayout from '../components/AppLayout.vue'
</script>

<template>
  <AppLayout>
    <div class="policy card">
      <h1>Regulamin serwisu NoteEz</h1>
      <p class="updated">Ostatnia aktualizacja: 1 sierpnia 2026 r.</p>

      <section>
        <h2>1. Postanowienia ogólne</h2>
        <p>
          Regulamin określa zasady korzystania z aplikacji NoteEz (dalej: „Usługa”), dostępnej pod
          adresem noteez.online oraz za pośrednictwem powiązanych urządzeń ESP32. Usługodawcą jest
          <strong>Kacper Miczejko</strong>, kontakt: <strong>noteez-kontakt@outlook.com</strong>.
          Usługa jest bezpłatna.
        </p>
      </section>

      <section>
        <h2>2. Warunki korzystania</h2>
        <ul>
          <li>Korzystanie z Usługi wymaga założenia konta i podania prawdziwego adresu e-mail.</li>
          <li>Konto jest przypisane do jednego użytkownika i nie może być udostępniane osobom trzecim.</li>
          <li>Użytkownik odpowiada za zachowanie poufności swojego hasła.</li>
          <li>Zabronione jest umieszczanie w notatkach treści niezgodnych z prawem.</li>
        </ul>
      </section>

      <section>
        <h2>3. Zakres usługi</h2>
        <p>
          Usługa umożliwia tworzenie, edycję i synchronizację notatek tekstowych, rysunków
          odręcznych oraz nagrań głosowych, a także parowanie i zarządzanie zgodnymi urządzeniami
          ESP32. Usługodawca dokłada starań, aby Usługa działała nieprzerwanie, ale nie gwarantuje
          jej stałej dostępności ani nie ponosi odpowiedzialności za przerwy wynikające z przyczyn
          niezależnych (np. awarie dostawców infrastruktury).
        </p>
      </section>

      <section>
        <h2>4. Odpowiedzialność</h2>
        <p>
          Usługa świadczona jest nieodpłatnie, w związku z czym odpowiedzialność Usługodawcy wobec
          użytkownika ograniczona jest do granic dopuszczalnych przez obowiązujące przepisy prawa.
          Usługodawca nie odpowiada za utratę danych spowodowaną działaniem siły wyższej, błędnym
          działaniem urządzenia użytkownika lub naruszeniem regulaminu przez użytkownika.
        </p>
      </section>

      <section>
        <h2>5. Urządzenia ESP32</h2>
        <p>
          Sparowanie urządzenia ESP32 z kontem odbywa się poprzez kod parowania generowany w
          aplikacji. Użytkownik może w dowolnym momencie odłączyć (odwołać) urządzenie w ustawieniach
          konta, co unieważnia jego dostęp do danych.
        </p>
      </section>

      <section>
        <h2>6. Reklamacje</h2>
        <p>
          Reklamacje dotyczące działania Usługi można zgłaszać na adres
          <strong>noteez-kontakt@outlook.com</strong>. Usługodawca rozpatruje reklamacje w miarę
          możliwości w terminie 14 dni od otrzymania zgłoszenia.
        </p>
      </section>

      <section>
        <h2>7. Rozwiązanie umowy i usunięcie konta</h2>
        <p>
          Użytkownik może w każdej chwili usunąć swoje konto w ustawieniach konta, co skutkuje
          trwałym usunięciem powiązanych danych zgodnie z
          <router-link to="/polityka-prywatnosci">polityką prywatności</router-link>.
          Usługodawca zastrzega sobie prawo do zablokowania lub usunięcia konta w przypadku
          rażącego naruszenia niniejszego regulaminu.
        </p>
      </section>

      <section>
        <h2>8. Postanowienia końcowe</h2>
        <p>
          W sprawach nieuregulowanych niniejszym regulaminem zastosowanie mają przepisy prawa
          polskiego, w szczególności Kodeksu cywilnego oraz ustawy o świadczeniu usług drogą
          elektroniczną. Usługodawca zastrzega sobie prawo do zmiany regulaminu; o istotnych
          zmianach użytkownicy zostaną poinformowani drogą mailową lub komunikatem w aplikacji.
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
</style>
