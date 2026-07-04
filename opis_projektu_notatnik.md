# Opis projektu — Notatnik online z ESP32

## Cel projektu
Aplikacja do notatek online z trzema klientami:
1. **Vue.js PWA** — główny interfejs na telefon/tablet z Androidem, instalowany jako aplikacja na ekranie głównym.
2. **ESP32 z wyświetlaczem TFT** — fizyczne urządzenie (np. na lodówce) do przeglądania, usuwania notatek i szybkich akcji, sterowane joystickiem i/lub ekranem dotykowym, budzone czujnikiem PIR.
3. **Dostęp przez API** — ten sam REST API obsługuje oba klienty.

---

## Stack technologiczny

### Backend
- **ASP.NET Core** — REST API
- **Azure App Service (Linux, F1 free tier)** — hosting backendu
- **Azure SQL Database (free offer)** — baza danych (notatki, rysunki jako JSON, metadane głosówek)
- **Azure Blob Storage (5GB free)** — pliki audio (głosówki)
- **Azure for Students** — 100 USD kredytu, aktywowane przez GitHub Student Developer Pack

### Frontend
- **Vue.js** jako PWA (Progressive Web App)
- **Azure Static Web Apps** lub GitHub Pages — hosting frontendu
- Rysowanie notatek: Canvas API + dane wektorowe (JSON ze ścieżkami/strokes), zapisywane w SQL Database (nie Blob Storage)
- Głosówki: nagrywanie przez `MediaRecorder` (format WebM/Opus, ~16-24 kbps mono), upload do Blob Storage przez backend lub SAS token

### Uwierzytelnianie
- **JWT (krótkotrwały access token ~15-60 min + długotrwały refresh token ~7 dni)** dla użytkowników Vue/PWA
  - Rotacja refresh tokena przy każdym użyciu
  - Refresh token w httpOnly + Secure cookie (ochrona przed XSS)
  - Możliwość podglądu aktywnych sesji i zdalnego wylogowania
- **Stały API key per urządzenie ESP32** — osobny schemat uwierzytelniania w ASP.NET Core (`AddScheme`), przechowywany jako hash (SHA-256) w bazie, powiązany z kontem użytkownika i ID urządzenia
- Flow parowania ESP32: krótkotrwały jednorazowy kod parowania (5-10 min) generowany przez Vue → ESP32 odbiera przez SoftAP/captive portal (biblioteka **WiFiManager**) → wysyła do endpointu `POST /api/devices/claim` → dostaje klucz API i zapisuje w NVS (`Preferences.h`)

### Endpointy API (przykładowe)
- `GET /api/notes` — lista notatek
- `GET /api/notes/lite` — uproszczona lista dla ESP32 (tylko id + tytuł)
- `GET /api/notes/{id}` — pełna notatka
- `POST /api/notes` — nowa notatka
- `PUT /api/notes/{id}` — edycja
- `DELETE /api/notes/{id}` — usunięcie
- `PUT /api/notes/{id}/drawing` — zapis rysunku (JSON ze strokes)
- `POST /api/notes/{id}/audio` — upload głosówki
- `POST /api/devices/pair-init` — inicjacja parowania ESP32 (wymaga JWT)
- `POST /api/devices/claim` — odbiór klucza API przez ESP32 (otwarty, chroniony kodem parowania)

---

## Notatki — typy zawartości
1. **Tekst** — zwykłe notatki tekstowe
2. **Rysunek wektorowy** — Canvas API w Vue, dane jako JSON (lista strokes z punktami, kolorem, grubością), z throttlingiem punktów (co ~16ms) i opcjonalną redukcją algorytmem Ramer-Douglas-Peucker. Renderowanie po stronie klienta (nie serwera). Rozmiar: 1-5 KB na szkic.
3. **Głosówki** — nagranie audio w formacie Opus/WebM, ~16-24 kbps mono (~120-180 KB/min). Plik w Azure Blob Storage, URL + metadane w bazie SQL.

---

## Urządzenie ESP32 — hardware

### Komponenty (finalnie wybrane/rozważane)
- **Mikrokontroler:** ESP32-WROOM-32 DevKit 30-pin z układem CP2102 (klasyczny, popularny klon)
- **Wyświetlacz:** LCD TFT 2.8" 240×320 ST7789 SPI z obsługą dotyku + slot microSD
- **Czujnik ruchu:** HC-SR505 mini PIR (lub AM312) — podłączony do RTC GPIO ESP32, budzi urządzenie z deep sleep
- **Joystick:** analogowy 2-osiowy + przycisk (ADC + 1 pin cyfrowy)
- **Zasilanie:** bateria LiPo/Li-Ion 3.7V 1S ze złączem JST-PH 2.0mm + osobny moduł ładowania **TP4056 z zabezpieczeniem (with protection)** — chroni przed przeładowaniem, rozładowaniem i zwarciem
- **Biblioteki firmware:** `TFT_eSPI` (wyświetlacz), `ArduinoJson` (parsowanie JSON), `HTTPClient` + `WiFiClientSecure` (HTTPS), `WiFiManager` (konfiguracja Wi-Fi + parowanie), `Preferences.h` (zapis klucza API w NVS)

### Schemat zasilania
```
Bateria LiPo JST-PH 2.0 → TP4056 (BAT)
TP4056 OUT+ → ESP32 VIN
TP4056 OUT- → ESP32 GND
USB-C do TP4056 → ładowanie baterii
```

### Oszczędność energii
- ESP32 w **deep sleep** (µA) przez większość czasu
- **PIR budzi ESP32** przez zbocze HIGH na RTC GPIO (`esp_sleep_enable_ext0_wakeup`)
- Po 30-60s bezczynności (brak interakcji joystickiem/dotykiem) → powrót do deep sleep
- Opcjonalny fizyczny przełącznik na linii OUT+ TP4056 → ESP32 VIN jako "master off" na dłuższe wyłączenia

### Flow działania ESP32
1. Budzenie przez PIR (lub przycisk)
2. Połączenie z Wi-Fi
3. `GET /api/notes/lite` → lista tytułów na ekranie
4. Nawigacja joystickiem (lub dotykiem) po liście
5. Wybór notatki → `GET /api/notes/{id}` → pełna treść
6. Opcjonalnie: `DELETE /api/notes/{id}` po potwierdzeniu (long-press)
7. Bezczynność → deep sleep
8. Rysunki wyświetlane jako "🖊 rysunek" bez renderowania na ESP32 (za mały ekran)

---

## Wdrożenie (Azure)
- **App Service Linux F1** (darmowy) — cold starty po ~20 min bezczynności (kilka sekund opóźnienia); obejście: ping co 15-18 min przez GitHub Actions scheduled workflow lub cron-job.org
- **Azure SQL Database free offer** — 100k vCore-sekund/miesiąc, 32GB, do 10 baz per subskrypcja
- **Blob Storage 5GB** — always free, na głosówki skompresowane Opus
- **Własna domena** — z GitHub Student Developer Pack (Namecheap: darmowa .me + SSL na rok)
- **Alert budżetowy** w portalu Azure (np. na 5 USD) — zabezpieczenie przed niechcianymi kosztami
- Bezpłatne limity (SQL DB, Blob 5GB) działają niezależnie od statusu studenta i pakietu kredytowego

---

## Uwagi do dalszego rozwoju
- ESP32 nie obsługuje edycji tekstu (tylko joystick) — edycja zostaje po stronie Vue/PWA
- Głosówki na ESP32: możliwe technicznie (mikrofon I2S), ale złożone — zostawione na późniejszy etap
- Transkrypcja głosówek (speech-to-text): możliwe przez Azure OpenAI w przyszłości
- Lokalne cache notatek na karcie microSD (slot na wyświetlaczu): na wypadek braku Wi-Fi
- Możliwość dodania BLE jako alternatywnej metody parowania urządzenia (Web Bluetooth w Chrome Android)
