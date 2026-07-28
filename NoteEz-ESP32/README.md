# NoteEz-ESP32

Firmware ESP32 (Arduino) — parowanie urządzenia + wyświetlanie listy notatek (`GET /api/device-notes/lite`) na ekranie dotykowym TFT ST7789.

Bazuje na `sketch_jul16a.ino` (konfiguracja LovyanGFX + WiFiManager + kalibracja dotyku), rozszerzony o:
- pole na kod parowania i adres serwera bezpośrednio w portalu konfiguracyjnym WiFiManager,
- automatyczne wywołanie `POST /api/devices/claim` po połączeniu z Wi-Fi, jeśli kod został wpisany,
- zapis `apiKey` i adresu serwera w NVS (`Preferences`), więc parowanie trzeba zrobić tylko raz,
- ekran główny z listą notatek pobraną przez `X-Api-Key`,
- przycisk „Odśwież” (odpytuje `/api/device-notes/lite` ponownie),
- przycisk „Reset” (przytrzymanie ~2s kasuje zapisane Wi-Fi + parowanie i wraca do portalu konfiguracyjnego),
- dotknięcie notatki na liście otwiera jej pełną treść (`GET /api/device-notes/{id}`) z zawijaniem tekstu i przewijaniem przyciskami „Góra”/„Dół”, gdy treść nie mieści się na ekranie.

## Ekran szczegółów notatki

Dotknięcie tytułu na liście pobiera pełną notatkę i pokazuje jej treść, zawiniętą do szerokości ekranu. Rysunki i głosówki nie są renderowane/odtwarzane na urządzeniu (zbyt mały ekran, brak głośnika) — zamiast tego widoczna jest informacja `[Rysunek - podglad tylko w apce]` / `[Glosowka - odtwarzanie tylko w apce]`, zgodnie z założeniem z [opis_projektu_notatnik.md](../opis_projektu_notatnik.md) („Rysunki wyświetlane jako 🖊 rysunek bez renderowania na ESP32”).

Przyciski „Góra”/„Dół” w prawym dolnym rogu przewijają treść o pół strony; szarzeją, gdy przewijanie w danym kierunku nie jest już możliwe. Przycisk „< Wstecz” w lewym górnym rogu wraca do listy (bez ponownego pobierania — lista zostaje w pamięci do czasu ręcznego „Odśwież”).

## Wymagane biblioteki (Arduino Library Manager / PlatformIO)

- `LovyanGFX`
- `WiFiManager` (tzapu)
- `ArduinoJson` (≥ 7.x, używa API `JsonDocument`)
- `Preferences` (wbudowana w rdzeń ESP32)

## Flow parowania

1. Pierwsze uruchomienie (brak zapisanych danych Wi-Fi) → ESP32 startuje jako access point `Notatnik-ESP32`.
2. Połącz się telefonem/laptopem z tą siecią → automatycznie otwiera się portal konfiguracyjny (`192.168.4.1`).
3. W aplikacji Vue: zakładka **Urządzenia** → „Sparuj urządzenie” → skopiuj wygenerowany 8-znakowy kod (ważny 10 min).
4. W portalu na ESP32 wypełnij: SSID + hasło Wi-Fi domowej sieci, adres serwera (`IP:port`, np. `192.168.1.50:8080`), kod parowania.
5. ESP32 łączy się z Wi-Fi i od razu wywołuje `/api/devices/claim` — po sukcesie zapisuje klucz API i przechodzi do ekranu z listą notatek.

## Reset / ponowne parowanie

Przytrzymaj czerwony przycisk „Reset” w prawym górnym rogu ekranu przez ~2 sekundy — urządzenie skasuje zapisane dane Wi-Fi i parowania, zrestartuje się i ponownie wystawi portal konfiguracyjny (`Notatnik-ESP32`).

## Uwagi

- Adres serwera (`IP:port`) wpisywany w portalu jest zapamiętywany w NVS i używany też przy kolejnych uruchomieniach — jeśli backend zmieni adres, trzeba przejść przez reset (patrz wyżej) i wpisać go ponownie.
- Do testów lokalnych używaj `http://IP:port` (nie `https`) — patrz [README.md](../README.md) w katalogu głównym, sekcja o testowaniu z poziomu ESP32.
- Kalibracja dotyku (`calData`) jest przepisana z oryginalnego szkicu — jeśli dotyk jest niedokładny na Twoim egzemplarzu wyświetlacza, przeprowadź kalibrację ponownie (przykłady w dokumentacji `LovyanGFX`).
