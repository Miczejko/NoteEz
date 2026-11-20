# NoteEz-ESP32

Firmware ESP32 (Arduino) — parowanie urządzenia, notatki (lista, szczegóły, checkboxy), rysunki (podgląd i rysowanie),
prognoza pogody i minutnik na ekranie dotykowym TFT ST7789 (320×240, dotyk XPT2046).

Bazuje na `sketch_jul16a.ino` (konfiguracja LovyanGFX + WiFiManager + kalibracja dotyku). Płytka: **ESP32-C6, 4 MB flash,
schemat partycji `huge_app` (brak OTA)**. Piny i ograniczenia sprzętowe: [../../docs/ESP32_WIRING.md](../../docs/ESP32_WIRING.md)
i [../../docs/ESP32_SESSION_SUMMARY.md](../../docs/ESP32_SESSION_SUMMARY.md). Endpointy backendu używane przez urządzenie:
[../../docs/ESP32_DEVICE_API.md](../../docs/ESP32_DEVICE_API.md).

## Funkcje

- pole na kod parowania i adres serwera w portalu konfiguracyjnym WiFiManager; po połączeniu z Wi-Fi `POST /api/devices/claim`,
  a `apiKey` i adres serwera zapisują się w NVS (`Preferences`) — parowanie tylko raz,
- lista notatek (`GET /api/device-notes/lite`) z przyciskiem „Odśwież” i przewijaniem „Góra”/„Dół”,
- szczegóły notatki z zawijaniem tekstu, **klikalnymi checkboxami**, podglądem rysunków i **rysowaniem nowych**,
- **pogoda** (Open-Meteo) jako wykresy temperatury i opadów z przesuwanym oknem godzinowym,
- minutnik z alarmem (blokuje usypianie, dopóki odlicza i dopóki alarm nie zostanie zatwierdzony),
- pomiar temperatury/wilgotności (SHT40) i baterii, brzęczyk, deep sleep po 60 s bezczynności.

## Ekran szczegółów notatki

Dotknięcie notatki na liście pobiera `GET /api/device-notes/{id}`. Tekst jest zawinięty do szerokości ekranu i pisany
powiększoną czcionką (`DETAIL_TEXT_SCALE 1.5`: znak 9×12 px, wiersz 16 px, ok. 10 linii na stronę). Przyciski „Góra”/„Dół”
przewijają o pół strony; „< Wstecz” wraca do listy. Głosówki nie są odtwarzane (brak głośnika).

**Checkboxy.** Zadanie (`taskItem`) jest rysowane jako kwadrat 14×14 z wcięciem; zaznaczone ma wypełnienie i ptaszek, a tekst
jest przekreślony. Cały „blok” zadania (min. 2 wiersze = 32 px) jest obszarem dotyku. Dotknięcie:
1. zmienia stan od razu na ekranie (optymistycznie) i pokazuje „Zapis...”,
2. wysyła `PUT /api/device-notes/{id}/tasks/{taskIndex}` z `{"checked":true|false}` (docelowy stan, nie przełączanie;
   timeout 4,5 s, bez automatycznych ponowień),
3. po błędzie (status inny niż 2xx, timeout, brak sieci) cofa stan, dwa piski i „Blad zapisu” na 1,5 s.

Reaguje tylko „świeże” dotknięcie (pierwsza pętla po zetknięciu palca), więc przytrzymanie palca ani dotyk z poprzedniego
ekranu niczego nie przełączają. Zadania zagnieżdżone są klikalne, ale bez wizualnego wcięcia.

## Rysunki

Przycisk „Rys.” (prawy górny róg szczegółów) jest **zawsze widoczny**. Ekran rysunków pokazuje podgląd (linie
przeskalowane pod ekran; dotknięcie lewej/prawej połowy poniżej paska przełącza rysunki) albo napis „Brak rysunkow”.
W górnym pasku jest „< Wstecz” i **„Nowy rysunek”** (zawsze, także gdy rysunków nie ma).

**Edytor rysunku** (`DrawingEditScreen`, biały obszar 320×172 px pod paskiem narzędzi):
- 5 kolorów (czarny, czerwony, niebieski, zielony, pomarańczowy), grubość 3 px,
- Cofnij / Przywróć / Wyczyść / Zapisz / Anuluj (Anuluj przy niepustym rysunku wymaga drugiego dotknięcia),
- limity w pamięci: **40 linii i 800 punktów** (statyczne tablice ok. 3,4 KB, zero alokacji przy rysowaniu); punkt bliższy
  niż 4 px od poprzedniego jest odrzucany; chwilowy zanik dotyku krótszy niż 80 ms nie kończy linii; przekroczenie limitu
  pokazuje czerwony komunikat i piszczy (nie gubi punktów po cichu),
- Cofnij/Przywróć nie zużywają dodatkowej pamięci: linie leżą w tablicy ciągle, cofnięcie przesuwa tylko licznik;
  rozpoczęcie nowej linii kasuje możliwość przywracania.

**Zapis** wysyła `POST /api/device-notes/{id}/drawings` do notatki, z której wszedłeś w szczegóły (timeout 7 s, bez ponowień).
JSON jest budowany strumieniowo w jednym `String` z `reserve()` (max ok. 20 KB), bez `JsonDocument`. Po sukcesie rysunek
trafia do lokalnych danych notatki (bez ponownego pobierania) i jest wyświetlany. Po błędzie rysunek **zostaje na ekranie**,
a ponowienie robisz ręcznie przyciskiem Zapisz. Po 60 s bez dotyku urządzenie zasypia i niezapisany rysunek przepada.
Serwer dopisuje do notatki węzeł `drawingBlock`, więc rysunek pojawia się na końcu tekstu w aplikacji webowej.

## Pogoda

Dane z Open-Meteo (`hourly=temperature_2m,precipitation`, Białystok, `WEATHER_FORECAST_DAYS` dni). Strzałki `<` `>` na górze
zmieniają dzień. Dwa wykresy rysowane bezpośrednio prymitywami LovyanGFX (bez sprite'a): temperatura w °C (linia, oś
w pełnych stopniach, min. zakres 4) i opady w mm (słupki, min. oś 0–2 mm). Domyślne okno godzinowe to **06:00–18:00**
(13 punktów: dzień aktywny i szczyt temperatury); strzałki na dole przesuwają je o 3 godziny w zakresie startu 0…11,
a zmiana dnia przywraca okno domyślne. Dane są trzymane w 4-bajtowych wpisach (`int16` temperatura×10, `uint16` opad×10),
a `fetchWeather()` filtruje JSON (`DeserializationOption::Filter`).

## Wymagane biblioteki (Arduino Library Manager)

- `LovyanGFX`
- `WiFiManager` (tzapu)
- `ArduinoJson` (≥ 7.x, używa API `JsonDocument`)
- `Preferences` (wbudowana w rdzeń ESP32)

## Kompilacja i wgrywanie

`scripts/flash.ps1` (PowerShell) kompiluje i wgrywa firmware przez `arduino-cli` z Arduino IDE:
```
.\scripts\flash.ps1 -Port COM5          # kompiluje i wgrywa (bez -Port wykrywa jedyny port szeregowy)
.\scripts\flash.ps1 -NoUpload           # tylko kompilacja
.\scripts\flash.ps1 -Worktree <katalog> # szkic z innego worktree (domyślnie ten, w którym leży skrypt)
```
Opcje płytki: `esp32:esp32:esp32c6:CDCOnBoot=cdc,CPUFreq=160,FlashFreq=80,FlashMode=qio,FlashSize=4M,PartitionScheme=huge_app,DebugLevel=none`
(w Arduino IDE ustaw dokładnie te same). Katalog budowania jest stały (`%LOCALAPPDATA%\NoteEz-build\esp32`); po zmianie
katalogu, opcji, a także po zmianie zestawu plików (nowe/usunięte `.cpp`, przełączenie gałęzi) kompilacja bywa pełna
(zmierzone 4–8 min); zwykła edycja istniejącego pliku, także `Config.h`, trwa kilkanaście sekund (zmierzone 13–16 s). Przed wgrywaniem zamknij monitor portu szeregowego (otwarcie go resetuje płytkę).

Rozmiar (build z 2026-11-20, rysunek włączony): flash 1 404 704 B z 3 145 728 B (44%), RAM statyczny 51 904 B z 327 680 B (15%).
Zmierzona sterta po przeklikaniu funkcji z internetem (przed dodaniem rysunku): wolne 261–297 KB, min ok. 181 KB, największy
ciągły blok ok. 135 KB — stąd nie alokujemy dużych buforów, filtrujemy JSON i nie trzymamy dwóch `WiFiClientSecure` naraz.

## Flow parowania

1. Pierwsze uruchomienie (brak zapisanych danych Wi-Fi) → ESP32 startuje jako access point `Notatnik-ESP32`.
2. Połącz się telefonem/laptopem z tą siecią → otwiera się portal konfiguracyjny (`192.168.4.1`).
3. W aplikacji Vue: zakładka **Urządzenia** → „Sparuj urządzenie” → skopiuj 8-znakowy kod (ważny 10 min).
4. W portalu wypełnij: SSID + hasło Wi-Fi, adres serwera (domyślnie `noteez.online`; do testów lokalnych `IP:port`, np.
   `192.168.100.168:5112`) i kod parowania.
5. ESP32 łączy się z Wi-Fi i wywołuje `/api/devices/claim` — po sukcesie zapisuje klucz API i przechodzi do listy notatek.

## Reset / ponowne parowanie

Na liście notatek przytrzymaj mały przycisk „Reset WiFi” w lewym dolnym rogu przez ~2 sekundy — urządzenie skasuje zapisane
dane Wi-Fi i parowania, zrestartuje się i ponownie wystawi portal konfiguracyjny. Klucz API jest powiązany z serwerem, z którym
sparowano urządzenie: po testach z lokalnym backendem trzeba sparować je ponownie z produkcją (`noteez.online`).

## Uwagi

- Adres serwera jest zapamiętywany w NVS — zmiana wymaga resetu i ponownego wpisania w portalu.
- Do testów lokalnych backend uruchom skryptem `scripts/run-local-backend.sh` (patrz [../../docs/DEPLOY_BACKEND.md](../../docs/DEPLOY_BACKEND.md)),
  a w portalu podaj `IP_komputera:5112` (HTTP, nie HTTPS).
- Zasada: blokujące wywołania HTTP blokują dotyk, więc żądania są krótkie i **bez automatycznych ponowień** (szybki błąd +
  ręczne ponowienie). Pozostajemy na deep sleep (eksperyment z light sleep został porzucony).
- **Nie dodawaj `Wire.h`** (psuje dzieloną magistralę SPI dotyku/wyświetlacza); I2C tylko przez `SoftI2C`. Arduino kompiluje
  każdy `.cpp` w folderze szkicu, więc nieużywany kod trzeba usuwać.
- Kalibracja dotyku (`calData`) jest przepisana z oryginalnego szkicu — jeśli dotyk jest niedokładny, skalibruj ponownie.
