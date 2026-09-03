# Podsumowanie sesji — NoteEz

Jeden, chronologiczny zapis zmian wprowadzonych w kolejnych konwersacjach z asystentem —
scalony z wcześniej rozrzuconych plików (`SESSION_SUMMARY.md` w katalogu głównym +
`docs/ESP32_SESSION_SUMMARY.md`), żeby historia była w jednym miejscu.

## Sesja: edytor TipTap, rysunki w treści, motyw, strona główna

### 1. Bogaty edytor tekstu (TipTap)

**Backend** (`NoteEz-Server`):
- `Note.TextContent` przechowuje teraz JSON z TipTap zamiast czystego tekstu (bez zmiany schematu bazy — nadal `string`).
- `Services/TipTapPlainTextExtractor.cs` — spłaszcza TipTap JSON do czystego tekstu (używane dla ESP32 i fallbacku).
- `Services/TipTapBlockExtractor.cs` — spłaszcza TipTap JSON do listy bloków `{type, text, checked, color, bold}` do renderowania na urządzeniu.
- `DeviceNotesController` — endpoint `/api/device-notes/{id}` zwraca `DeviceNoteDto`: `textContent` (plain-text fallback) + `textBlocks` (lista bloków) + `drawings` (bez zmian, już zawierały `strokesJson`).

**Frontend** (`NoteEz-Frontend`):
- Zainstalowano `@tiptap/vue-3`, `@tiptap/pm`, `@tiptap/starter-kit`, `@tiptap/extension-text-style` (Color+TextStyle), `@tiptap/extension-task-list`, `@tiptap/extension-task-item`.
- `components/NoteEditor.vue` — edytor TipTap z toolbarem: pogrubienie, kursywa, przekreślenie, nagłówek, listy punktowane/numerowane, checkboxy, cytat, blok kodu, wstawianie rysunku, wybór koloru tekstu (`<input type="color">`).
- `components/ToolbarIcon.vue` — ręcznie rysowane ikony SVG (bez zewnętrznej biblioteki) zastępujące tekstowe/emoji przyciski toolbara.
- `utils/tiptapText.js` — ekstraktor czystego tekstu z TipTap JSON (do podglądów na liście notatek).
- `components/NoteCard.vue` — podgląd notatki wyciąga czysty tekst z JSON zamiast wyświetlać surowy JSON.
- `views/NoteDetailView.vue` — textarea zastąpiona przez `<NoteEditor>`.
- Stare notatki z czystym tekstem są automatycznie opakowywane w pojedynczy akapit przy pierwszym otwarciu (kompatybilność wsteczna, bez migracji danych).

### 2. Rysunki wstawiane w treści notatki (nie osobna zakładka)

- `tiptap/drawingBlock.js` — customowy węzeł TipTap `drawingBlock` (atrybut `drawingId` → istniejący rekord `NoteDrawing`).
- `components/DrawingNodeView.vue` — node view renderujący `DrawingCanvas` bezpośrednio w treści: nowy rysunek startuje w edycji, zapis/edycja/usuwanie przez istniejące store'owe `addDrawing`/`updateDrawing`/`deleteDrawing`.
- `NoteDetailView.vue` — usunięta osobna zakładka „Rysunki”; `NoteEditor` dostaje `:key="note.id"` i `note-id`, żeby węzeł miał dostęp do właściwego `noteId`.
- Backend/format przechowywania rysunków bez zmian — nadal osobne rekordy `NoteDrawing` z `strokesJson` (wektorowe pociągnięcia, nie bitmapy).

### 3. Bugfix: przycinanie/skalowanie canvasu rysunku

- `DrawingCanvas.vue`: canvas miał `min-width` wymuszający pełną szerokość rysunku, co wychodziło poza ekran telefonu. Naprawione: `max-width: 100%` + `width:100%; height:auto; aspect-ratio` (canvas skaluje się proporcjonalnie).
- Dalszy bug: canvas był przycinany do bounding-boxa rysunku, ale punkty pociągnięć nie były przesuwane względem przycięcia. Naprawione przez `offsetX`/`offsetY` (translate przy renderze + korekta w `getPos()` dla nowych punktów).

### 4. Więcej kolorów w rysunkach

- `DrawingCanvas.vue`: paleta rozszerzona z 5 do 10 kolorów + przycisk „własny kolor” (`<input type="color">`) z podglądem wybranego koloru.

### 5. Kolor kafelka notatki (akcent na liście)

**Backend**: nowe pole `Note.Color` (nullable hex), przepuszczone przez `NoteDto`/`CreateNoteRequest`/`UpdateNoteRequest`/`NoteService`. Migracja EF `AddNoteColor` (zastosowana na bazie w kontenerze Docker).

**Frontend**:
- `NoteDetailView.vue` — rząd kółek-swatchy „Kolor kafelka” + przycisk usunięcia koloru.
- `NoteCard.vue` — gdy notatka ma kolor, w rogu kafelka pojawia się ukośnie przycięty gradientowy kawałek (`clip-path` + `linear-gradient`), nie cała karta.

### 6. Logo NoteEz

- Źródłowy plik `NoteEz-Logo.png` (2000×2000) przetworzony narzędziem `sharp` (doinstalowanym tymczasowo w scratchpadzie, nie w zależnościach projektu) — usunięcie białego tła metodą chroma-key.
- Wygenerowane warianty w `NoteEz-Logo/` i skopiowane do `NoteEz-Frontend/public/`:
  - `noteez-logo.png` — pełne logo (czarne, przezroczyste tło).
  - `noteez-logo-tealtext.png` / `noteez-logo-square-tealtext.png` — bez tła, grafika w kolorze `#79c7c5`.
  - `noteez-logo-tealbg.png` / `noteez-logo-square-tealbg.png` — zaokrąglone rogi, tło `#79c7c5`.
  - `noteez-logo-blue.png`, `favicon-32/192/512.png` — warianty z niebieskim tłem (favicon).
  - **Uwaga**: favicon w `index.html` był później ręcznie podmieniony przez użytkownika na `noteez-logo-square-tealbg.png` (turkusowe tło) — zostawione bez zmian.
- Logo użyte w: nagłówku (`AppLayout.vue`), stronie logowania/rejestracji/głównej — bez tekstu „NoteEz” obok (usunięty, logo powiększone).

### 7. Ciemny motyw kolorystyczny

`assets/main.css` — paleta zmieniona na:
```
--black: #000501       (tło strony)
--pearl-aqua: #79c7c5  (primary)
--muted-teal: #73ab84  (secondary)
--celadon: #99d19c     (akcent)
```
- Dodano `--color-on-accent` (czarny) — jasne akcenty (pearl-aqua/muted-teal) wymagają ciemnego tekstu na przyciskach/nagłówku.
- Hover-y na przyciskach liczone przez `color-mix()` zamiast twardych heksów.
- Zaktualizowane twarde `rgba(...)` w odznakach (`NoteCard`, `DevicesView`) i tle toolbara rysunku.
- `AppLayout.vue` — nagłówek: tekst/logo na `--color-on-accent`; logo linkuje do `/notes` (zalogowany) lub `/` (gość).
- `theme-color` w `index.html` zmieniony na `#000501`.

### 8. Strona główna (landing page)

- `views/HomeView.vue` — nowa publiczna strona pod `/`: opis projektu, 4 kafelki funkcji (edytor, rysunki, głosówki, ESP32), duże przyciski CTA „Zacznij za darmo” / „Mam już konto”.
- `router/index.js` — lista notatek przeniesiona z `/` na `/notes` (nazwa trasy `notes` bez zmian). Trasa `home` dodana na `/`.
- Poprawione twarde odnośniki `to="/"` w `DevicesView.vue` i `NoteDetailView.vue` na `{ name: 'notes' }`.
- `LoginView.vue` / `RegisterView.vue` — dodany przycisk „← Wróć” (do strony głównej), logo zmienione na `noteez-logo-tealtext.png`.

### 9. Poprawki mobilne

- **Samoistny zoom na telefonie**: przyczyna — media query zmniejszała bazowy `font-size` do 15px, pola `input`/`textarea` i edytor TipTap dziedziczyły ten rozmiar; iOS Safari automatycznie przybliża stronę przy focusie na polu <16px. Naprawione: `font-size: max(1rem, 16px)` na inputach/textarea i `.ProseMirror`, plus `maximum-scale=1.0, user-scalable=no` w viewport meta, plus `overflow-x: hidden` na `html`/`body`.
- **Skok na górę po dojechaniu do dołu strony**: klasyczny mobilny efekt rubber-band/overscroll. Naprawione przez `overscroll-behavior-y` — domyślnie `auto` na `body` (pull-to-refresh u góry działa), ale `main.js` nasłuchuje scrolla i dodaje klasę `body.at-bottom` (→ `overscroll-behavior-y: none`) tylko gdy użytkownik jest dokładnie na końcu strony, blokując tylko odbicie na dole.

### 10. ESP32 firmware — bloki tekstu ze stylem + ekran rysunku

- **Backend**: `TipTapBlockExtractor` + `DeviceNoteDto.TextBlocks` (patrz sekcja 1) — dostarcza uproszczone bloki tekstu ze stylem.
- **Firmware** (`NoteEz-ESP32.ino`):
  - `DetailLine{text,color,bold}` zamiast czystego `String` — kolor z hex TipTap, pogrubienie tanim trikiem (podwójny wydruk przesunięty o 1px). Wysokość linii zostawiona stała (14px) celowo (uproszczenie).
  - Checkboxy jako prefiks `[x] `/`[ ] `.
  - Nowy ekran `SCREEN_DRAWING` — przycisk „Rys.” w widoku szczegółów (gdy notatka ma rysunki), renderuje wektorowe pociągnięcia (`drawLine`) przeskalowane pod ekran, bez dekodera obrazków (rysunki to dane wektorowe, nie bitmapy). Dotknięcie lewej/prawej połowy ekranu przełącza rysunki.
  - **Bugfix parowania** (`status=-1` z HTTPClient): `WiFi.mode(WIFI_STA)` wymuszony po `wm.autoConnect()` (WiFiManager czasem zostawia urządzenie w `WIFI_AP_STA`, co psuje wychodzące HTTP). `claimDevice()` robi teraz do 3 prób z sekundowym odstępem + `http.setConnectTimeout(5000)` + logi diagnostyczne na Serial.
  - Nie skompilowane lokalnie (brak `arduino-cli`/Arduino IDE w środowisku) — zweryfikowane ręcznie linia po linii, zalecane przetestowanie na fizycznym urządzeniu.

### Ważne uwagi / rzeczy do zapamiętania z tej sesji

- **Migracja EF `AddNoteColor` już zastosowana** na bazie w kontenerze Docker (`docker exec`/`dotnet ef database update` z `Server=localhost,1433` zamiast `sql-noteez,1433`, bo ten drugi to nazwa hosta widoczna tylko wewnątrz sieci Dockera).
- `dotnet ef` CLI nie wczytuje User Secrets domyślnie — trzeba `ASPNETCORE_ENVIRONMENT=Development` w zmiennej środowiskowej przy migracjach lokalnie.
- Backend connection string leży w User Secrets / `.env` (Docker Compose), nie w `appsettings.json`.
- Frontend dev server: `npm run dev -- --host` (lub stałe `host: true` w `vite.config.js`, już dodane) żeby testować z innych urządzeń w sieci LAN. API idzie przez proxy Vite (`/api` → `localhost:8080`), więc CORS nie przeszkadza przy testach LAN.
- Firewall Windows może wymagać ręcznego dodania reguły dla portu 5173, jeśli inne urządzenia nie widzą dev servera (nie zrobione — wymaga potwierdzenia, bo to zmiana systemowa).
- Kolory wyboru w edytorze tekstu/rysunku (paleta dla treści użytkownika) celowo NIE zostały zmienione przy przejściu na ciemny motyw — to inna, niezależna paleta.

## Sesja: ESP32 — naprawa dotyku, restrukturyzacja, czujniki, deep sleep (2026-08-02 → 2026-08-08)

### 1. Diagnoza i naprawa dotyku (biały/zamarznięty ekran)

- Pierwotny problem: biały ekran, czerniejący po dotyku. Przyczyna: brak opóźnienia (~120ms)
  po `display.wakeup()` — ST7789 potrzebuje czasu na stabilizację po SLPOUT.
- Wyłączyliśmy usypianie na jakiś czas, bo psuło się przy kolejnych eksperymentach.
- **Kluczowe odkrycie:** biblioteka `Wire.h`/`Adafruit_SHT4x` **psuje dzieloną magistralę SPI
  dotyku/wyświetlacza na tym rdzeniu ESP32-C6, nawet gdy nigdy nie jest realnie używana** —
  samo zlinkowanie jej do binarki wystarczy (Arduino kompiluje/linkuje KAŻDY `.cpp` w folderze
  szkicu, niezależnie od tego czy jest gdziekolwiek `#include`owany).
- Rozwiązanie: własna, bitbangowana implementacja I2C (`SoftI2C.h/.cpp`) na zwykłych
  `digitalWrite`/`pinMode`, całkowicie bez `Wire.h`.
- Późniejsze losowe zawieszenia (biały ekran, ustępujący po replug USB) — to był osobny
  problem: niestabilne zasilanie z portu USB (spadek napięcia pod obciążeniem).

### 2. Restrukturyzacja kodu (1 plik `.ino` → moduły)

Podzielony na: `Config.h` (piny/stałe), `Colors.h/.cpp`, `Display.h/.cpp` (klasa LGFX),
`State.h/.cpp` (współdzielony stan), `UiHelpers.h/.cpp`, `WifiPairing.h/.cpp`,
`SleepMode.h/.cpp`, `NotesScreen.h/.cpp`, `NoteDetailScreen.h/.cpp`, `DrawingScreen.h/.cpp`.
Arduino kompiluje wszystkie `.h`/`.cpp` w folderze szkicu automatycznie — nie trzeba nic
konfigurować, wystarczy że leżą obok `.ino`.

### 3. Czujnik SHT40 (temperatura/wilgotność)

- `ClimateSensor.h/.cpp` — własny, ręczny protokół SHT40 (adres `0x44`, komenda `0xFD`,
  weryfikacja CRC-8) na `SoftI2C`, bez żadnej biblioteki Adafruit/Wire.
- Piny: **SDA = GPIO11, SCL = GPIO22** (SCL przeniesiony z GPIO1 później, żeby zwolnić GPIO1
  pod pomiar baterii — patrz sekcja 7).
- Odczyt wyświetlany na ekranie listy notatek, odświeżany przy każdym `fetchNotesLite()`.

### 4. Ekran pogody (Open-Meteo)

- `WeatherScreen.h/.cpp` — nowy ekran z prognozą godzinową (temperatura + opady) na 4 dni
  (dziś + 3 w przód), z nawigacją strzałkami `</>`.
- API: Open-Meteo (bez klucza), lokalizacja na sztywno: Białystok (`53.1325, 23.1688`) —
  celowo bez wyboru miasta (urządzenie zostaje w jednym miejscu).
- Wymaga HTTPS → `WiFiClientSecure` z `setInsecure()` (bez weryfikacji certyfikatu — świadomy
  kompromis, publiczne, nieistotne dane).
- Przycisk "Pogoda" w nagłówku ekranu listy notatek.

### 5. Usypianie (deep sleep) — pełna diagnoza

Kilka nawarstwionych problemów, po kolei:
1. **T_IRQ "pływał" podczas snu** — `pinMode(INPUT_PULLUP)` konfiguruje pull-up tylko na tryb
   aktywny; ESP-IDF domyślnie izoluje część obwodu pinu na czas snu. Fix: `gpio_sleep_sel_dis()`
   (funkcje `gpio_sleep_pullup_en/pulldown_dis` niedostępne w tej wersji pakietu płytek).
2. **Domena zasilania RTC/LP** bywa wyłączana w deep sleep, gasząc obwód "czujący" stan pinu
   mimo poprawnej konfiguracji wakeup. Fix: `esp_sleep_pd_config(ESP_PD_DOMAIN_RTC_PERIPH, ESP_PD_OPTION_ON)`.
3. **Największe odkrycie:** CS dotyku (GPIO21) "pływał" podczas deep sleep (piny bez jawnego
   `gpio_hold_en` wracają do stanu domyślnego, gdy domena cyfrowa traci zasilanie). XPT2046
   wystawia T_IRQ/PENIRQ **tylko gdy jego CS jest w stanie wysokim** — pływające CS = chip
   "wybrany" przez SPI = PENIRQ całkowicie wyłączone, niezależnie od dotyku. Zdiagnozowane
   testem: ręczne zwarcie T_IRQ→GND budziło urządzenie, prawdziwy dotyk — nie.
   Fix: `digitalWrite(TOUCH_CS_PIN, HIGH); gpio_hold_en((gpio_num_t)TOUCH_CS_PIN);` przed
   `esp_deep_sleep_start()`, plus `gpio_hold_dis()` na starcie w `setup()` (tak jak już było
   dla podświetlenia).
- **Deep sleep teraz działa poprawnie** (`enterDeepSleep()` aktywne w `loop()`).
- `enterLightSleepTest()` zostaje w kodzie jako nieużywana (ale sprawna) alternatywa.
- Zaktualizowano pakiet płytek ESP32 w Arduino IDE po drodze (do testów deep sleep).

### 6. Dioda RGB → usunięta

- Dodana, potem przez brak wolnych pinów (tylko GPIO22/23 wolne, GPIO10 zarezerwowany pod
  backlight/sleep) zredukowana do 2 kolorów (R+B), **finalnie całkiem usunięta**, żeby zrobić
  miejsce pod SCL SHT40 (GPIO22) i pomiar baterii (zwolnienie GPIO1).

### 7. Pomiar baterii

- `Battery.h/.cpp` — dzielnik napięcia (2× 100kΩ, dzielenie przez 2) na **GPIO1** (jedyny wolny
  pin z ADC1 na tej płytce — ADC1 działa tylko na GPIO0-6, wszystkie inne z tego zakresu zajęte).
- `analogReadMilliVolts()` (kalibrowany odczyt ESP32) × `BATTERY_DIVIDER_RATIO` (2.0) → napięcie.
- Napięcie → procent: liniowa interpolacja 3.0V (0%) – 4.2V (100%) — przybliżenie, nie
  dedykowany fuel gauge, ale wystarczające. Zmierzona dokładność: ~3% odchyłki od multimetru,
  uznane za akceptowalne.
- Odczyt i wyświetlanie na ekranie listy notatek, odświeżane przy `fetchNotesLite()`.
- **Nie testowane jeszcze na realnym zasilaniu bateryjnym** — płytka wciąż zasilana z PC,
  zmieni się po lutowaniu (to czysto softwareowa gotowość, nic do zmiany w kodzie).

### 8. Brzęczyk (buzzer)

- `Buzzer.h/.cpp` — pasywny brzęczyk na **GPIO23** (ostatni wolny pin), sterowany `tone()`.
- `buzzWake()` — pik przy wybudzeniu dotykiem z deep sleep.
- `buzzError()` — dwa piski przy błędach (WiFi, parowanie, pobieranie notatek/pogody/szczegółów).
- ~~TODO na przyszłość: krótki timer z melodią powiadomienia~~ — **zaimplementowane** w sesji 2026-09-03, patrz niżej (`buzzTimerDone()`).

### Finalna rozpiska pinów (do lutowania)

| GPIO | Funkcja |
|---|---|
| 6 | Wyświetlacz SCLK (SPI, dzielone z dotykiem) |
| 7 | Wyświetlacz MOSI (SPI, dzielone z dotykiem) |
| 2 | Wyświetlacz MISO (SPI, dzielone z dotykiem) |
| 19 | Wyświetlacz DC |
| 18 | Wyświetlacz CS |
| 20 | Wyświetlacz RST |
| 21 | Dotyk XPT2046 CS (osobny od CS panelu!) |
| 3 | Dotyk T_IRQ (budzi z deep sleep; rezystor 10kΩ pull-up do 3.3V — opcjonalny, działa bez niego) |
| 10 | Podświetlenie (BLK), sterowane programowo pod usypianie |
| 11 | SHT40 SDA (SoftI2C) |
| 22 | SHT40 SCL (SoftI2C) |
| 1 | Bateria — środek dzielnika 100kΩ+100kΩ (ADC) |
| 23 | Brzęczyk pasywny (`tone()`) |

**Zarezerwowane/omijane:** GPIO4/5/8/9/15 (strapping), 12/13 (USB-JTAG), 16/17 (SPI flash),
GPIO0 (empirycznie zarezerwowany przez SPI2_HOST — psuje dotyk mimo braku jawnej konfiguracji).

**Zero wolnych pinów nie zostało** — płytka w pełni wykorzystana.

### Ważne lekcje / rzeczy do pamiętania z tej sesji

- **Nigdy nie dodawać biblioteki `Wire.h` do tego projektu** — psuje dzieloną magistralę SPI
  nawet nieużywana. Zawsze `SoftI2C`.
- Arduino kompiluje/linkuje każdy `.cpp` w folderze szkicu niezależnie od `#include` — usuwanie
  nieużywanego kodu wymaga fizycznego usunięcia pliku, nie tylko przestania go używać.
- Piny w domenie snu (T_IRQ, CS dotyku) potrzebują jawnego `gpio_hold_en`/`gpio_sleep_sel_dis`,
  inaczej "pływają" podczas deep/light sleep.
- Diagnostyka sprzętowa (T_IRQ przez `digitalRead`, `esp_sleep_get_wakeup_cause()`, ręczne
  zwarcie pinu do GND) była kluczowa do znalezienia realnych przyczyn — nie zgadywanie.
- Projekt jest **skończony pod względem liczby pinów** — każdy kolejny czujnik/peryferal
  wymaga zwolnienia jakiegoś istniejącego (jak SHT40 SCL → zwolnienie GPIO1 pod baterię).

## Sesja: produkcyjny adres API (HTTPS) + minutnik na ESP32 (2026-09-03)

- **`DEFAULT_API_HOST`** w `Config.h` zmieniony z lokalnego IP dev na `noteez.online`
  (produkcja, Cloudflare Worker + Azure App Service za nim) — po fabrycznym resecie WiFi
  urządzenie od razu łączy się z produkcją, dalej można nadpisać w portalu WiFiManager
  lokalnym `IP:port` do testów.
- Nowy helper `isApiHostRawIp()` (`UiHelpers.h/.cpp`) rozróżnia surowy adres IP (dev, `http://`)
  od domeny (produkcja, `https://` + `WiFiClientSecure` z `setInsecure()`, jak w ekranie pogody).
  Zastosowane we wszystkich 3 miejscach wywołań API (`NotesScreen.cpp`, `NoteDetailScreen.cpp`,
  `WifiPairing.cpp`).
- Świadomie **nie dodano** automatycznego retry/dłuższego timeoutu na cold-start Azure F1 —
  blokujący retry zamroziłby dotyk i uniemożliwił korzystanie z innych ekranów (np. Pogoda)
  w czasie budzenia się serwera. Szybki fail + ręczne "Odśwież" po ~20-30s jest tu lepsze.
- **Nowy ekran minutnika** (`TimerScreen.h/.cpp`) — przycisk "Timer" w prawym górnym rogu
  (dawne miejsce przycisku Reset), 4 fazy (`TIMER_SETUP/RUNNING/PAUSED/DONE` w `State.h`):
  wybór minut (1-90, +/-) → Start → odliczanie MM:SS (odświeżane co sekundę w `timerTick()`,
  niezależnie od dotyku) z Pauzą/Resetem → po dojściu do zera gra melodyjka `buzzTimerDone()`
  (`Buzzer.h/.cpp`, ~3.5s) i pokazuje się ekran "Czas minął!" z przyciskiem OK.
  Usypianie zablokowane, gdy `timerPhase == TIMER_RUNNING` **lub** `TIMER_DONE` (pierwsza
  wersja blokowała tylko RUNNING, więc ekran gasł zaraz po melodii, zanim ktokolwiek zdążył
  kliknąć OK).
- Przycisk "Reset WiFi" przeniesiony z prawego górnego rogu na mały przycisk w lewym dolnym
  rogu (wyrównany do rzędu przycisków scrolla) — używany bardzo rzadko, nie zasługuje na
  eksponowane miejsce.
