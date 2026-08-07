# NoteEz — przegląd jakości kodu (2026-08-02)

Notatka robocza z przeglądu kodu pod kątem realnych błędów/luk, nie stylu. Ma służyć jako lista
"co poprawić" przed pokazaniem projektu w portfolio — nie wszystko trzeba robić od razu.

## Backend (`NoteEz-Server`)

### Wysoki priorytet
1. **Brak unikalnego constraintu na `Users.Username`/`Email` w bazie** — kontrolery sprawdzają
   duplikat przez `AnyAsync` przed insertem (classic check-then-act race). Dwie równoczesne
   rejestracje na ten sam login mogą obie przejść walidację i wybuchnąć później na
   `SingleOrDefaultAsync` przy logowaniu (500). Dodać unique index migracją.
2. **Brak globalnego middleware do obsługi wyjątków** — `Program.cs` nie ma
   `UseExceptionHandler`/structured loggera (Serilog itp.). Każdy nieobsłużony wyjątek ucieka do
   domyślnego handlera ASP.NET bez logowania — trudne do zdiagnozowania w produkcji.
3. **Usuwanie danych nie jest transakcyjne, blob kasowany przed commitem DB** —
   `NoteService.cs:103-113`, `NoteAudioService.cs:70-78`, `AccountController.cs:38-51`. Jeśli
   proces padnie między usunięciem blob a `SaveChangesAsync()`, zostają rekordy DB wskazujące na
   nieistniejące pliki (albo częściowo skasowane konto).

### Średni priorytet
4. `SortOrder` liczony z `Count()` kolekcji (`NoteService.cs:128`, `NoteAudioService.cs:41`) —
   race przy dwóch równoczesnych requestach dodających drawing/audio do tej samej notatki.
5. Logowanie ma rate-limiting tylko per-IP, nie per-konto — rozproszony atak rotujący IP może
   brute-forcować jedno konto bez ograniczeń.
6. `GetAudioStreamAsync` deklaruje `enableRangeProcessing: true`, ale strumień z
   `blobClient.DownloadStreamingAsync()` nie wspiera seekowania — przewijanie audio we
   frontendzie prawdopodobnie nie działa tak jak powinno.
7. Brak testów kontrolerów dla `AccountController` (usuwanie konta — najwyższa stawka, RODO),
   `DeviceNotesController`, `DevicesController`, `DeviceClaimController`, `NotesController`
   (testowany tylko `NoteService`, nie warstwa HTTP/autoryzacji).

### Niski priorytet
8. Mylący, nieaktualny komentarz w `DeviceNotesController.cs:55` sugerujący brak autoryzacji —
   w praktyce endpoint jest poprawnie zabezpieczony, ale komentarz wprowadza w błąd.
9. `Guid.Parse` na claimie `UserId` bez guardu, powtórzone w wielu kontrolerach — dziś bezpieczne,
   ale przy zmianie schematu auth rzuci nieobsłużony wyjątek zamiast czystego 401.
10. Brak jawnego `ClockSkew` w konfiguracji JWT (domyslne 5 min, dość liberalne wobec 30-min
    ważności tokenu).

### Co jest zrobione dobrze
BCrypt, rotacja refresh-tokenów z wykrywaniem powtórnego użycia, timing-safe dummy-hash login,
ochrona przed enumeracją e-maili, Turnstile, ciasteczka HttpOnly/Secure/SameSite=Strict,
walidacja audio po magic bytes, pełne czyszczenie danych przy usuwaniu konta (RODO), rate
limiting, nagłówki bezpieczeństwa, konsekwentna filtracja zapytań po `UserId` (brak IDOR).

## Frontend (`NoteEz-Frontend`)

### Wysoki priorytet
1. **Debounced zapis notatki nigdy nie jest czyszczony przy odmontowaniu/zmianie trasy**
   (`NoteDetailView.vue:59-68`, `setTimeout(saveNote, 800)`) — przejście na inną notatkę w ciągu
   800ms po wpisaniu tekstu może nadpisać złą notatkę starą treścią. Realne ryzyko utraty danych.
2. **Listenery `mousemove`/`mouseup`/`touchmove`/`touchend` na `window` przy resize canvasu**
   (`DrawingCanvas.vue:308-346`) nie są usuwane, jeśli komponent zniknie w trakcie przeciągania —
   wyciek pamięci/referencji do zniszczonej instancji.

### Średni priorytet
3. Surowy obiekt błędu z API (`error.value = e.response?.data`) jest wyświetlany wprost w
   szablonach (`DevicesView.vue:97`, `CalendarView.vue:181`) — użytkownik widzi `[object Object]`
   zamiast komunikatu.
4. `AudioRecorder.vue:16-47` — jeśli `getUserMedia()` rozwiąże się po odmontowaniu komponentu,
   `MediaStream` nie trafia do cleanupu — mikrofon zostaje otwarty bez uchwytu do zamknięcia.
5. Natywny `confirm()` do akcji niszczących (`NoteDetailView.vue:85-101`) — niestylowany, można
   go łatwo pominąć w przeglądarce kiosku (Family Hub), niespójny z customowym potwierdzeniem
   przy usuwaniu konta.
6. `notes.js:56-65` — `update()` scala lokalny payload przez `Object.assign` zamiast użyć encji
   zwróconej przez serwer — lokalny stan może się rozjechać z tym, co faktycznie zapisano.

### Niski priorytet
7. `NoteEditor.vue:48-58` — `JSON.stringify` całego dokumentu TipTap przy każdej zmianie tylko
   do deduplikacji — koszt rośnie z rozmiarem notatki przy każdym znaku.
8. `TurnstileWidget.vue:33-41` — polling `setInterval` na `window.turnstile` bez timeoutu — jeśli
   skrypt nigdy się nie załaduje (adblock/CDN zablokowany), formularz cicho blokuje submit.

### Co jest zrobione dobrze
Brak `v-html` w całym froncie (TipTap renderuje przez ProseMirror, brak oczywistej powierzchni
XSS), dobrze zaprojektowany klient API (token w pamięci, mutex na odświeżanie, rozróżnienie
"trzeba odświeżyć" od realnego 401), router guards poprawnie czekają na `auth.initialize()`,
`$reset()` store'ów przy wylogowaniu. Brak testów frontendu to znany, niezamknięty temat.

## ESP32 (`NoteEz-ESP32`)

### Średni priorytet
1. **Brak reconnectu WiFi** — jeśli sieć padnie w trakcie działania, kolejne żądania HTTP po
   prostu zwracają błąd (obsłużone w UI), ale nic nie próbuje aktywnie odzyskać połączenia bez
   ręcznej akcji użytkownika (np. "Odśwież").
2. **Większość wywołań `HTTPClient` nie ma jawnego timeoutu** (poza `claimDevice`, które ma
   `setConnectTimeout(5000)`) — `fetchNotesLite`, `selectNote`, `fetchWeather` polegają na
   domyślnym zachowaniu biblioteki; przy niereagującym serwerze `loop()` może zamrozić UI na
   dłużej niż oczekiwane, bez komunikatu dla użytkownika w międzyczasie.
3. **Częste użycie `String`** (konkatenacje URL-i, etykiety) — przy urządzeniu działającym
   tygodniami bez restartu to klasyczne ryzyko fragmentacji sterty na ESP32. Nie jest to problem
   dziś, ale warto mieć z tyłu głowy przy dłuższych testach stabilności.
4. **`WiFiClientSecure::setInsecure()` w `WeatherScreen.cpp`** — świadomie pominięta weryfikacja
   certyfikatu TLS (uproszczenie zaakceptowane wcześniej w tej sesji dla publicznego API pogody
   bez wrażliwych danych) — zostawione jako świadomy kompromis, nie przeoczenie.

### Niski priorytet
5. **Martwy kod usypiania** (`enterDeepSleep()`, `enterLightSleepTest()` w `SleepMode.cpp`) —
   nigdy nie wywoływane z `loop()` (usypianie świadomie wyłączone, bo nie działało stabilnie).
   Zostawione jako punkt wyjścia na przyszłość — warto to dokończyć albo usunąć, żeby nie mylić
   przyszłego czytelnika kodu.
6. Trzy niemal identyczne wzorce "HTTP GET → parse JSON → obsłuż błąd → renderuj" w
   `NotesScreen.cpp`, `NoteDetailScreen.cpp`, `WeatherScreen.cpp` — teraz że kod jest już
   modularny, dałoby się to wydzielić do wspólnego helpera.
7. Dane kalibracji dotyku (`calData[8]` w `.ino`) są zahardkodowane pod konkretny, fizyczny
   egzemplarz wyświetlacza — oczywiste dla jednego urządzenia w domu, ale warto o tym pamiętać,
   gdyby kiedyś wymieniał się panel.

---

*Priorytety są orientacyjne — potraktuj to jako listę kandydatów do poprawy, nie wyrocznię.
Część "wysokich" pozycji backendu (transakcyjność usuwania, unique constraint) warto zrobić przed
pokazaniem projektu komuś technicznemu; reszta może poczekać.*
