# Podsumowanie sesji — ESP32 (2026-08-02 → 2026-08-08)

Zapis wszystkiego, co zrobiliśmy przy `NoteEz-ESP32` w tej konwersacji, żeby można było
bezpiecznie zresetować czat i wznowić pracę bez utraty kontekstu.

## 1. Diagnoza i naprawa dotyku (biały/zamarznięty ekran)

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

## 2. Restrukturyzacja kodu (1 plik `.ino` → moduły)

Podzielony na: `Config.h` (piny/stałe), `Colors.h/.cpp`, `Display.h/.cpp` (klasa LGFX),
`State.h/.cpp` (współdzielony stan), `UiHelpers.h/.cpp`, `WifiPairing.h/.cpp`,
`SleepMode.h/.cpp`, `NotesScreen.h/.cpp`, `NoteDetailScreen.h/.cpp`, `DrawingScreen.h/.cpp`.
Arduino kompiluje wszystkie `.h`/`.cpp` w folderze szkicu automatycznie — nie trzeba nic
konfigurować, wystarczy że leżą obok `.ino`.

## 3. Czujnik SHT40 (temperatura/wilgotność)

- `ClimateSensor.h/.cpp` — własny, ręczny protokół SHT40 (adres `0x44`, komenda `0xFD`,
  weryfikacja CRC-8) na `SoftI2C`, bez żadnej biblioteki Adafruit/Wire.
- Piny: **SDA = GPIO11, SCL = GPIO22** (SCL przeniesiony z GPIO1 później, żeby zwolnić GPIO1
  pod pomiar baterii — patrz sekcja 7).
- Odczyt wyświetlany na ekranie listy notatek, odświeżany przy każdym `fetchNotesLite()`.

## 4. Ekran pogody (Open-Meteo)

- `WeatherScreen.h/.cpp` — nowy ekran z prognozą godzinową (temperatura + opady) na 4 dni
  (dziś + 3 w przód), z nawigacją strzałkami `</>`.
- API: Open-Meteo (bez klucza), lokalizacja na sztywno: Białystok (`53.1325, 23.1688`) —
  celowo bez wyboru miasta (urządzenie zostaje w jednym miejscu).
- Wymaga HTTPS → `WiFiClientSecure` z `setInsecure()` (bez weryfikacji certyfikatu — świadomy
  kompromis, publiczne, nieistotne dane).
- Przycisk "Pogoda" w nagłówku ekranu listy notatek.

## 5. Usypianie (deep sleep) — pełna diagnoza

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

## 6. Dioda RGB → usunięta

- Dodana, potem przez brak wolnych pinów (tylko GPIO22/23 wolne, GPIO10 zarezerwowany pod
  backlight/sleep) zredukowana do 2 kolorów (R+B), **finalnie całkiem usunięta**, żeby zrobić
  miejsce pod SCL SHT40 (GPIO22) i pomiar baterii (zwolnienie GPIO1).

## 7. Pomiar baterii

- `Battery.h/.cpp` — dzielnik napięcia (2× 100kΩ, dzielenie przez 2) na **GPIO1** (jedyny wolny
  pin z ADC1 na tej płytce — ADC1 działa tylko na GPIO0-6, wszystkie inne z tego zakresu zajęte).
- `analogReadMilliVolts()` (kalibrowany odczyt ESP32) × `BATTERY_DIVIDER_RATIO` (2.0) → napięcie.
- Napięcie → procent: liniowa interpolacja 3.0V (0%) – 4.2V (100%) — przybliżenie, nie
  dedykowany fuel gauge, ale wystarczające. Zmierzona dokładność: ~3% odchyłki od multimetru,
  uznane za akceptowalne.
- Odczyt i wyświetlanie na ekranie listy notatek, odświeżane przy `fetchNotesLite()`.
- **Nie testowane jeszcze na realnym zasilaniu bateryjnym** — płytka wciąż zasilana z PC,
  zmieni się po lutowaniu (to czysto softwareowa gotowość, nic do zmiany w kodzie).

## 8. Brzęczyk (buzzer)

- `Buzzer.h/.cpp` — pasywny brzęczyk na **GPIO23** (ostatni wolny pin), sterowany `tone()`.
- `buzzWake()` — pik przy wybudzeniu dotykiem z deep sleep.
- `buzzError()` — dwa piski przy błędach (WiFi, parowanie, pobieranie notatek/pogody/szczegółów).
- **TODO na przyszłość:** krótki timer z melodią powiadomienia (zgłoszone, nie zaimplementowane).

## Finalna rozpiska pinów (do lutowania)

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

## Ważne lekcje / rzeczy do pamiętania

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
