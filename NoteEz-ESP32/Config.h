#pragma once

// ---- usypianie (deep sleep) po bezczynnosci - OBECNIE WYLACZONE w loop(), patrz SleepMode.h ----
// T_IRQ (linia przerwania dotyku z XPT2046, aktywna stanem niskim) - musi byc podpieta pod
// pin z domeny LP-IO, bo tylko takie piny moga wybudzic ESP32-C6 z deep sleep. Na tej plytce
// GPIO0/2/6/7 sa zajete przez magistrale SPI, a GPIO4/5 to piny strappingowe (nie ruszac) -
// GPIO3 to najbezpieczniejszy wolny pin w zakresie 0-7.
#define TIRQ_PIN 3
#define IDLE_SLEEP_MS 60000 // 1 minuta bezczynnosci -> deep sleep

// CS dotyku XPT2046 (musi sie zgadzac z cfg.pin_cs w konfiguracji Touch_XPT2046 w Display.h) -
// trzeba go jawnie trzymac na HIGH w deep sleep, inaczej PENIRQ/T_IRQ przestaje dzialac.
#define TOUCH_CS_PIN 21

// Brzeczyk pasywny (tone()/PWM) - ostatni wolny pin na tej plytce.
#define BUZZER_PIN 23

// Podswietlenie (BLK/LED modulu) przepiete z 3.3V na GPIO10, zeby dalo sie je zgasic
// programowo przed usypianiem. GPIO10 nie koliduje z SPI (0/2/6/7) ani ze strappingiem (4/5),
// wiec jest bezpiecznym wyborem do zwyklego sterowania cyfrowego (nie musi byc pinem LP-IO,
// bo tylko GO/wylaczamy je - nie budzimy sie przez niego).
#define BACKLIGHT_PIN 10

// Czujnik temperatury/wilgotnosci SHT40 (I2C bitbangowane, SoftI2C - patrz ClimateSensor.h).
// Biblioteka Wire psula dzielona magistrale SPI dotyku/wyswietlacza (nawet nieuzywana!),
// dlatego I2C jest tu zaimplementowane recznie na zwyklych GPIO, bez Wire.h.
// SCL przeniesiony z GPIO1 na GPIO22 (dawny pin diody, ktora zostala usunieta), zeby zwolnic
// GPIO1 pod pomiar baterii - to jedyny "ADC-owy" wolny pin na tej plytce (ADC1 dziala tylko
// na GPIO0-6, a wszystkie inne z tego zakresu sa juz zajete przez SPI/dotyk/strapping).
#define SHT40_SDA_PIN 11
#define SHT40_SCL_PIN 22

// Pomiar napiecia baterii przez dzielnik rezystorowy (2x rezystor, np. 100k+100k = dzielenie
// przez 2) na GPIO1 (jedyny wolny pin z ADC1 na tej plytce). Patrz Battery.h.
#define BATTERY_ADC_PIN 1
// Odwrotnosc dzielnika napiecia - dla 100k+100k (dzielenie przez 2) to 2.0. Jesli dobierzesz
// inne rezystory, przelicz: BATTERY_DIVIDER_RATIO = (R1+R2) / R2 (R2 to ten od strony GND/ADC).
#define BATTERY_DIVIDER_RATIO 2.0f
// Typowa krzywa rozladowania LiPo 1S - napiecia dla 0% i 100% (przyblizenie liniowe,
// wystarczajace bez dedykowanego "fuel gauge")
#define BATTERY_VOLTAGE_EMPTY 3.0f
#define BATTERY_VOLTAGE_FULL  4.2f

// domyślny host - produkcyjna wersja hostowana (Cloudflare Worker + Azure backend za nim).
// Nadal mozna go nadpisac w portalu WiFiManager (np. na lokalny adres IP:port do testow dev),
// ale po fabrycznym resecie/pierwszej konfiguracji urzadzenie od razu laczy sie z produkcja.
#define DEFAULT_API_HOST "noteez.online"

// przycisk "Odśwież" w lewym górnym rogu (ekran listy)
#define REFRESH_BTN_X 0
#define REFRESH_BTN_Y 0
#define REFRESH_BTN_W 60
#define REFRESH_BTN_H 32

// przycisk "Timer" w prawym gornym rogu (ekran listy) - dawne miejsce przycisku "Reset",
// ktory przeniesiony zostal w mniej eksponowane miejsce (patrz RESET_BTN_* nizej)
#define TIMER_BTN_X 260
#define TIMER_BTN_Y 0
#define TIMER_BTN_W 60
#define TIMER_BTN_H 32

// przycisk "Wstecz" w lewym górnym rogu (ekran szczegółów notatki / timera)
#define BACK_BTN_X 0
#define BACK_BTN_Y 0
#define BACK_BTN_W 70
#define BACK_BTN_H 32

// przyciski przewijania tresci notatki (dol ekranu)
#define SCROLL_UP_BTN_X 200
#define SCROLL_DOWN_BTN_X 260
#define SCROLL_BTN_Y 206
#define SCROLL_BTN_W 55
#define SCROLL_BTN_H 30

// przycisk "Reset WiFi" w lewym dolnym rogu (ekran listy) - celowo maly, bo uzywany bardzo
// rzadko (tylko przy zmianie sieci/sparowania). Przytrzymanie go kasuje zapisane dane WiFi +
// parowanie i wraca do portalu konfiguracyjnego. Wyrownany do rzedu przyciskow scrolla.
#define RESET_BTN_X 4
#define RESET_BTN_Y SCROLL_BTN_Y
#define RESET_BTN_W 46
#define RESET_BTN_H SCROLL_BTN_H
#define RESET_HOLD_MS 2000

// przycisk "Rysunek" w prawym gornym rogu ekranu szczegolow (widoczny gdy notatka ma rysunki)
#define DRAWING_BTN_X 270
#define DRAWING_BTN_Y 0
#define DRAWING_BTN_W 50
#define DRAWING_BTN_H 32

#define MAX_NOTES 20

#define LIST_START_Y 54
#define LIST_ROW_HEIGHT 44
#define LIST_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla listy

#define DETAIL_CONTENT_TOP 40
#define DETAIL_LINE_HEIGHT 14
#define DETAIL_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla

// ---- ekran pogody (Open-Meteo, https://open-meteo.com - bez klucza API) ----
// Na sztywno Bialystok - docelowo do wyboru w portalu konfiguracyjnym WiFi
#define WEATHER_LAT "53.1325"
#define WEATHER_LON "23.1688"
#define WEATHER_FORECAST_DAYS 4 // dzisiaj + 3 dni w przod
#define WEATHER_HOURS_PER_DAY 24
#define MAX_WEATHER_ENTRIES (WEATHER_FORECAST_DAYS * WEATHER_HOURS_PER_DAY)

// przycisk "Pogoda" w naglowku ekranu listy notatek (miedzy tytulem a przyciskiem Reset)
#define WEATHER_BTN_X 195
#define WEATHER_BTN_Y 0
#define WEATHER_BTN_W 60
#define WEATHER_BTN_H 32

// przycisk "Odswiez" na ekranie pogody (ta sama pozycja co przycisk "Rysunek" na ekranie
// szczegolow notatki - ekrany sie nie nakladaja, wiec bezpiecznie recyklingujemy geometrie)
#define WEATHER_REFRESH_BTN_X DRAWING_BTN_X
#define WEATHER_REFRESH_BTN_Y DRAWING_BTN_Y
#define WEATHER_REFRESH_BTN_W DRAWING_BTN_W
#define WEATHER_REFRESH_BTN_H DRAWING_BTN_H

// nawigacja dnia (strzalki < / > wokol etykiety "Dzis" / "Jutro" / daty)
#define WEATHER_DAY_NAV_Y 34
#define WEATHER_DAY_NAV_H 22
#define WEATHER_DAY_PREV_BTN_X 4
#define WEATHER_DAY_PREV_BTN_W 34
#define WEATHER_DAY_NEXT_BTN_X 282
#define WEATHER_DAY_NEXT_BTN_W 34

#define WEATHER_CLIMATE_Y 62

#define WEATHER_LIST_START_Y 80
#define WEATHER_ROW_HEIGHT 18
#define WEATHER_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla

// ---- ekran timera (minutnik, patrz TimerScreen.h) ----
#define TIMER_MIN_MINUTES 1
#define TIMER_MAX_MINUTES 90
#define TIMER_DEFAULT_MINUTES 5

// przyciski -/+ do wyboru liczby minut (ekran ustawiania timera)
#define TIMER_SETUP_MINUS_X 50
#define TIMER_SETUP_PLUS_X 220
#define TIMER_SETUP_ADJ_Y 90
#define TIMER_SETUP_ADJ_W 50
#define TIMER_SETUP_ADJ_H 50

// przycisk "Start" (ustawianie) / "OK" (po zakonczeniu odliczania) - ta sama geometria,
// bo oba ekrany sie nie nakladaja w czasie
#define TIMER_START_BTN_X 100
#define TIMER_START_BTN_Y 180
#define TIMER_START_BTN_W 120
#define TIMER_START_BTN_H 42

// przyciski "Pauza"/"Wznow" i "Reset" (ekran odliczania)
#define TIMER_PAUSE_BTN_X 50
#define TIMER_PAUSE_BTN_Y 180
#define TIMER_PAUSE_BTN_W 100
#define TIMER_PAUSE_BTN_H 42

#define TIMER_RESET_BTN_X 170
#define TIMER_RESET_BTN_Y 180
#define TIMER_RESET_BTN_W 100
#define TIMER_RESET_BTN_H 42
