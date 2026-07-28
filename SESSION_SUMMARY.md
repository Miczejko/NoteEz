# Podsumowanie sesji — NoteEz

Zapis wszystkich zmian wprowadzonych w tej konwersacji, w kolejności chronologicznej.

## 1. Bogaty edytor tekstu (TipTap)

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

## 2. Rysunki wstawiane w treści notatki (nie osobna zakładka)

- `tiptap/drawingBlock.js` — customowy węzeł TipTap `drawingBlock` (atrybut `drawingId` → istniejący rekord `NoteDrawing`).
- `components/DrawingNodeView.vue` — node view renderujący `DrawingCanvas` bezpośrednio w treści: nowy rysunek startuje w edycji, zapis/edycja/usuwanie przez istniejące store'owe `addDrawing`/`updateDrawing`/`deleteDrawing`.
- `NoteDetailView.vue` — usunięta osobna zakładka „Rysunki”; `NoteEditor` dostaje `:key="note.id"` i `note-id`, żeby węzeł miał dostęp do właściwego `noteId`.
- Backend/format przechowywania rysunków bez zmian — nadal osobne rekordy `NoteDrawing` z `strokesJson` (wektorowe pociągnięcia, nie bitmapy).

## 3. Bugfix: przycinanie/skalowanie canvasu rysunku

- `DrawingCanvas.vue`: canvas miał `min-width` wymuszający pełną szerokość rysunku, co wychodziło poza ekran telefonu. Naprawione: `max-width: 100%` + `width:100%; height:auto; aspect-ratio` (canvas skaluje się proporcjonalnie).
- Dalszy bug: canvas był przycinany do bounding-boxa rysunku, ale punkty pociągnięć nie były przesuwane względem przycięcia. Naprawione przez `offsetX`/`offsetY` (translate przy renderze + korekta w `getPos()` dla nowych punktów).

## 4. Więcej kolorów w rysunkach

- `DrawingCanvas.vue`: paleta rozszerzona z 5 do 10 kolorów + przycisk „własny kolor” (`<input type="color">`) z podglądem wybranego koloru.

## 5. Kolor kafelka notatki (akcent na liście)

**Backend**: nowe pole `Note.Color` (nullable hex), przepuszczone przez `NoteDto`/`CreateNoteRequest`/`UpdateNoteRequest`/`NoteService`. Migracja EF `AddNoteColor` (zastosowana na bazie w kontenerze Docker).

**Frontend**:
- `NoteDetailView.vue` — rząd kółek-swatchy „Kolor kafelka” + przycisk usunięcia koloru.
- `NoteCard.vue` — gdy notatka ma kolor, w rogu kafelka pojawia się ukośnie przycięty gradientowy kawałek (`clip-path` + `linear-gradient`), nie cała karta.

## 6. Logo NoteEz

- Źródłowy plik `NoteEz-Logo.png` (2000×2000) przetworzony narzędziem `sharp` (doinstalowanym tymczasowo w scratchpadzie, nie w zależnościach projektu) — usunięcie białego tła metodą chroma-key.
- Wygenerowane warianty w `NoteEz-Logo/` i skopiowane do `NoteEz-Frontend/public/`:
  - `noteez-logo.png` — pełne logo (czarne, przezroczyste tło).
  - `noteez-logo-tealtext.png` / `noteez-logo-square-tealtext.png` — bez tła, grafika w kolorze `#79c7c5`.
  - `noteez-logo-tealbg.png` / `noteez-logo-square-tealbg.png` — zaokrąglone rogi, tło `#79c7c5`.
  - `noteez-logo-blue.png`, `favicon-32/192/512.png` — warianty z niebieskim tłem (favicon).
  - **Uwaga**: favicon w `index.html` był później ręcznie podmieniony przez użytkownika na `noteez-logo-square-tealbg.png` (turkusowe tło) — zostawione bez zmian.
- Logo użyte w: nagłówku (`AppLayout.vue`), stronie logowania/rejestracji/głównej — bez tekstu „NoteEz” obok (usunięty, logo powiększone).

## 7. Ciemny motyw kolorystyczny

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

## 8. Strona główna (landing page)

- `views/HomeView.vue` — nowa publiczna strona pod `/`: opis projektu, 4 kafelki funkcji (edytor, rysunki, głosówki, ESP32), duże przyciski CTA „Zacznij za darmo” / „Mam już konto”.
- `router/index.js` — lista notatek przeniesiona z `/` na `/notes` (nazwa trasy `notes` bez zmian). Trasa `home` dodana na `/`.
- Poprawione twarde odnośniki `to="/"` w `DevicesView.vue` i `NoteDetailView.vue` na `{ name: 'notes' }`.
- `LoginView.vue` / `RegisterView.vue` — dodany przycisk „← Wróć” (do strony głównej), logo zmienione na `noteez-logo-tealtext.png`.

## 9. Poprawki mobilne

- **Samoistny zoom na telefonie**: przyczyna — media query zmniejszała bazowy `font-size` do 15px, pola `input`/`textarea` i edytor TipTap dziedziczyły ten rozmiar; iOS Safari automatycznie przybliża stronę przy focusie na polu <16px. Naprawione: `font-size: max(1rem, 16px)` na inputach/textarea i `.ProseMirror`, plus `maximum-scale=1.0, user-scalable=no` w viewport meta, plus `overflow-x: hidden` na `html`/`body`.
- **Skok na górę po dojechaniu do dołu strony**: klasyczny mobilny efekt rubber-band/overscroll. Naprawione przez `overscroll-behavior-y` — domyślnie `auto` na `body` (pull-to-refresh u góry działa), ale `main.js` nasłuchuje scrolla i dodaje klasę `body.at-bottom` (→ `overscroll-behavior-y: none`) tylko gdy użytkownik jest dokładnie na końcu strony, blokując tylko odbicie na dole.

## 10. ESP32 firmware

- **Backend**: `TipTapBlockExtractor` + `DeviceNoteDto.TextBlocks` (patrz sekcja 1) — dostarcza uproszczone bloki tekstu ze stylem.
- **Firmware** (`NoteEz-ESP32.ino`):
  - `DetailLine{text,color,bold}` zamiast czystego `String` — kolor z hex TipTap, pogrubienie tanim trikiem (podwójny wydruk przesunięty o 1px). Wysokość linii zostawiona stała (14px) celowo (uproszczenie).
  - Checkboxy jako prefiks `[x] `/`[ ] `.
  - Nowy ekran `SCREEN_DRAWING` — przycisk „Rys.” w widoku szczegółów (gdy notatka ma rysunki), renderuje wektorowe pociągnięcia (`drawLine`) przeskalowane pod ekran, bez dekodera obrazków (rysunki to dane wektorowe, nie bitmapy). Dotknięcie lewej/prawej połowy ekranu przełącza rysunki.
  - **Bugfix parowania** (`status=-1` z HTTPClient): `WiFi.mode(WIFI_STA)` wymuszony po `wm.autoConnect()` (WiFiManager czasem zostawia urządzenie w `WIFI_AP_STA`, co psuje wychodzące HTTP). `claimDevice()` robi teraz do 3 prób z sekundowym odstępem + `http.setConnectTimeout(5000)` + logi diagnostyczne na Serial.
  - Nie skompilowane lokalnie (brak `arduino-cli`/Arduino IDE w środowisku) — zweryfikowane ręcznie linia po linii, zalecane przetestowanie na fizycznym urządzeniu.

## Ważne uwagi / rzeczy do zapamiętania

- **Migracja EF `AddNoteColor` już zastosowana** na bazie w kontenerze Docker (`docker exec`/`dotnet ef database update` z `Server=localhost,1433` zamiast `sql-noteez,1433`, bo ten drugi to nazwa hosta widoczna tylko wewnątrz sieci Dockera).
- `dotnet ef` CLI nie wczytuje User Secrets domyślnie — trzeba `ASPNETCORE_ENVIRONMENT=Development` w zmiennej środowiskowej przy migracjach lokalnie.
- Backend connection string leży w User Secrets / `.env` (Docker Compose), nie w `appsettings.json`.
- Frontend dev server: `npm run dev -- --host` (lub stałe `host: true` w `vite.config.js`, już dodane) żeby testować z innych urządzeń w sieci LAN. API idzie przez proxy Vite (`/api` → `localhost:8080`), więc CORS nie przeszkadza przy testach LAN.
- Firewall Windows może wymagać ręcznego dodania reguły dla portu 5173, jeśli inne urządzenia nie widzą dev servera (nie zrobione — wymaga potwierdzenia, bo to zmiana systemowa).
- Kolory wyboru w edytorze tekstu/rysunku (paleta dla treści użytkownika) celowo NIE zostały zmienione przy przejściu na ciemny motyw — to inna, niezależna paleta.
