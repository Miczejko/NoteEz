# Notatki z sesji debugowania usypiania ESP32-C6 (deep/light sleep + wybudzanie dotykiem)

Stan na koniec sesji — do wznowienia w nowej konwersacji. Wklej ten plik / podlinkuj go na starcie nowej rozmowy.

## Cel

Urządzenie ma usypiać (docelowo `deep sleep`) po **1 minucie bezczynności** i wybudzać się **dotknięciem ekranu** (linia `T_IRQ` z kontrolera dotyku XPT2046).

## Sprzęt

- Chip: **ESP32-C6 Dev Module**
- Board package w Arduino IDE: **"esp32 by Espressif Systems"** (potwierdzone jako używany, obsługuje C6). Uwaga: na komputerze jest zainstalowany też inny pakiet "Arduino ESP32 Boards" 2.0.18, ale to NIE jest ten używany do kompilacji tego szkicu.
- `T_IRQ` fizycznie podłączony do **GPIO3** (potwierdzone przez użytkownika, przełutowane z pierwotnego GPIO22 — GPIO22 nie jest pinem domeny LP-IO/RTC na C6, więc nie mógł wybudzać z deep sleep).
- Podświetlenie (BLK/LED modułu wyświetlacza) przełutowane z szyny 3.3V na **GPIO10**, żeby dało się je programowo wyłączać przed snem.
- Piny już zajęte (SPI + wyświetlacz): sclk=6, mosi=7, miso=2, dc=19, cs=18, rst=20, touch_cs=21.
- GPIO4/5 to piny strappingowe na C6 — świadomie pominięte przy wyborze wolnego pinu.

## Co już potwierdzone jako DZIAŁAJĄCE (nie szukać tam dalej)

1. **Wiring T_IRQ jest w 100% sprawny.** Surowy odczyt `digitalRead(GPIO3)` pokazuje `1` w spoczynku i `0` podczas dotyku — potwierdzone bezpośrednim testem w `loop()` bez udziału jakiegokolwiek trybu snu.
2. **`esp_deep_sleep_enable_gpio_wakeup(1ULL << 3, ESP_GPIO_WAKEUP_GPIO_LOW)` zwraca `ESP_OK` (0).**
3. **Urządzenie NIE robi pełnego resetu/restartu w pętli.** Log `[boot] przyczyna wybudzenia: X` (drukowany na starcie `setup()`) pojawia się **tylko raz**, mimo że ekran wielokrotnie gaśnie i zapala się — czyli `loop()` faktycznie normalnie wraca do działania między cyklami, to nie jest crash-reboot loop.
4. **Test na powerbanku (bez PC) dał identyczny efekt wizualny** — czyli obecność aktywnego Serial Monitora / hosta USB nie jest przyczyną (choć ten test nie dał logów, więc jest tylko częściowo rozstrzygający).
5. Poprawka "wyłącz WiFi przed snem" (`WiFi.disconnect(true); WiFi.mode(WIFI_OFF);` przed `esp_light_sleep_start()`/`esp_deep_sleep_start()`) została dodana — **podświetlenie faktycznie zapala się po dotyku**, co dowodzi, że `esp_light_sleep_start()` TERAZ poprawnie wraca po wybudzeniu (wcześniej, przed tą poprawką, nie było tego widać).

## Aktualny, nierozwiązany problem

Po dotknięciu uśpionego ekranu:
- Podświetlenie **zapala się** (dowód że `esp_light_sleep_start()` wrócił).
- Ale **ekran całkowicie zamarza — nie reaguje na żadne dalsze dotknięcia.**
- Linijka `Serial.println("[light-sleep-test] wybudzono dotknieciem!")` (drukowana zaraz PO `esp_light_sleep_start()`, PRZED zapaleniem podświetlenia) **nigdy się nie pojawia w logu**, mimo dokładnego przeszukania (Ctrl+F) całego outputu Seriala.
- To jest sprzeczne pozornie (backlight HIGH jest kilka linijek PO tym printcie w kodzie, więc skoro podświetlenie się zapala, print powinien był wykonać się wcześniej) — możliwe że to konkretny `Serial.println()` ginie z powodu niestabilności USB-CDC dokładnie w momencie wybudzenia, ALBO że coś innego dzieje się nie tak jak zakładamy.

## Ostatnia podjęta akcja (NIEZWERYFIKOWANA przez użytkownika)

Dodano granularne znaczniki diagnostyczne `[T1]` do `[T6]` bezpośrednio po `esp_light_sleep_start()` w funkcji `enterLightSleepTest()`, żeby dokładnie zlokalizować, na którym kroku następuje zamrożenie:

```
esp_light_sleep_start();
Serial.println("[T1] po esp_light_sleep_start()");

digitalWrite(BACKLIGHT_PIN, HIGH);
Serial.println("[T2] po digitalWrite BACKLIGHT HIGH");

display.wakeup();
Serial.println("[T3] po display.wakeup()");

WiFi.mode(WIFI_STA);
Serial.println("[T4] po WiFi.mode(WIFI_STA)");

WiFi.begin();
Serial.println("[T5] po WiFi.begin()");

renderNotesList();
Serial.println("[T6] po renderNotesList() - koniec funkcji");
```

**Czekamy na wynik: który to jest ostatni znacznik `[Tx]` widoczny w logu przed zamrożeniem.**

### Robocza hipoteza (do zweryfikowania po wyniku [T1]-[T6])

- Jeśli **`[T1]` się NIE pojawi** → sam `esp_light_sleep_start()` jednak nie wraca poprawnie mimo pozornego zapalenia podświetlenia (byłoby to zaskakujące, bo digitalWrite(HIGH) jest PO tym poincie w kodzie — czyli oznaczałoby że coś inne zapala podświetlenie, nie nasz kod).
- Jeśli zatrzyma się między **`[T3]` a `[T4]`** → `WiFi.mode(WIFI_STA)` się wiesza (możliwe, że wyłączenie WiFi przez `WiFi.mode(WIFI_OFF)` tuż przed snem na tym rdzeniu/chipie robi coś, co blokuje późniejsze włączenie z powrotem).
- Jeśli zatrzyma się między **`[T4]` a `[T5]`** → `WiFi.begin()` się wiesza (blocking call czekający na coś, co nigdy nie nadejdzie, skoro dane WiFi zostały "wyczyszczone" przez `WiFi.mode(WIFI_OFF)`).
- Jeśli dojdzie do **`[T6]`** ale ekran i tak jest zamrożony → problem jest gdzie indziej, prawdopodobnie w `renderNotesList()` / SPI do wyświetlacza po `display.sleep()`+`display.wakeup()` cyklu, albo w głównej pętli `loop()` już PO powrocie z `enterLightSleepTest()` (np. `display.getTouch()` przestaje działać po cyklu sleep/wakeup panelu).

## Stan kodu w pliku `NoteEz-ESP32.ino` (WAŻNE: dużo tymczasowego kodu testowego!)

- `loop()` **tymczasowo wywołuje `enterLightSleepTest()`** zamiast docelowego `enterDeepSleep()` — do przywrócenia po znalezieniu i naprawieniu przyczyny.
- Funkcja `enterDeepSleep()` (docelowa, prawdziwy deep sleep) **istnieje i jest gotowa**, ale nieużywana w `loop()` na czas testów — ma tę samą poprawkę WiFi.disconnect() przed snem.
- Funkcja `enterLightSleepTest()` to **tymczasowy diagnostyczny wariant** na light sleep (łatwiejszy do debugowania, bo wraca zamiast resetować całe RAM jak deep sleep) — zawiera teraz znaczniki `[T1]`-`[T6]`.
- `setup()` zawiera tymczasowy log `[boot] przyczyna wybudzenia: X` (`esp_sleep_get_wakeup_cause()`) — warto zostawić na stałe, to tanie i przydatne.
- Backlight (`BACKLIGHT_PIN` = GPIO10) inicjalizowany w `setup()`: `gpio_hold_dis()` + `pinMode(OUTPUT)` + `digitalWrite(HIGH)`.
- `TIRQ_PIN` (GPIO3) konfigurowany jako `INPUT_PULLUP` już w `setup()` (nie tylko tuż przed snem) — potrzebne dla wiarygodnego odczytu w diagnostyce.
- `IDLE_SLEEP_MS` = 60000 (1 minuta).

## Co zrobić po wznowieniu

1. Poproś użytkownika o wynik testu z `[T1]`-`[T6]` (jeśli jeszcze go nie ma).
2. Napraw konkretny hang wskazany przez ten test.
3. Po naprawieniu **usuń cały kod diagnostyczny** (`[T1]`-`[T6]`, ewentualnie surowy `[gpio-test]` jeśli gdzieś jeszcze zostały resztki, print stanu T_IRQ przed uśnięciem) — zostawić tylko finalną, czystą logikę.
4. **Zdecydować: `enterDeepSleep()` (prawdziwy deep sleep, mniejsze zużycie energii, ale RAM/WiFi/stan resetuje się całkowicie po każdym wybudzeniu) czy zostać na `enterLightSleepTest()`/light sleep na stałe (nie resetuje stanu, ale mniejsza oszczędność energii).** To decyzja do podjęcia z użytkownikiem, gdy już wiadomo że mechanizm w ogóle działa stabilnie.
5. Podmienić wywołanie w `loop()` z `enterLightSleepTest()` na docelową funkcję.
6. Zaktualizować `README.md` w `NoteEz-ESP32/` o opis funkcji usypiania (obecnie nieopisana).
