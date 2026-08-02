#pragma once

// ---- usypianie (deep sleep) po bezczynnosci - OBECNIE WYLACZONE w loop(), patrz SleepMode.h ----
// T_IRQ (linia przerwania dotyku z XPT2046, aktywna stanem niskim) - musi byc podpieta pod
// pin z domeny LP-IO, bo tylko takie piny moga wybudzic ESP32-C6 z deep sleep. Na tej plytce
// GPIO0/2/6/7 sa zajete przez magistrale SPI, a GPIO4/5 to piny strappingowe (nie ruszac) -
// GPIO3 to najbezpieczniejszy wolny pin w zakresie 0-7.
#define TIRQ_PIN 3
#define IDLE_SLEEP_MS 60000 // 1 minuta bezczynnosci -> deep sleep

// Podswietlenie (BLK/LED modulu) przepiete z 3.3V na GPIO10, zeby dalo sie je zgasic
// programowo przed usypianiem. GPIO10 nie koliduje z SPI (0/2/6/7) ani ze strappingiem (4/5),
// wiec jest bezpiecznym wyborem do zwyklego sterowania cyfrowego (nie musi byc pinem LP-IO,
// bo tylko GO/wylaczamy je - nie budzimy sie przez niego).
#define BACKLIGHT_PIN 10

// Czujnik temperatury/wilgotnosci SHT40 (I2C bitbangowane, SoftI2C - patrz ClimateSensor.h).
// Biblioteka Wire psula dzielona magistrale SPI dotyku/wyswietlacza (nawet nieuzywana!),
// dlatego I2C jest tu zaimplementowane recznie na zwyklych GPIO, bez Wire.h.
#define SHT40_SDA_PIN 11
#define SHT40_SCL_PIN 1

// domyślny host używany tylko przy pierwszej konfiguracji (potem nadpisywany przez portal WiFiManager)
#define DEFAULT_API_HOST "192.168.100.168:8080"

// przycisk "Reset WiFi" w prawym górnym rogu (ekran listy) - przytrzymanie go kasuje
// zapisane dane WiFi + parowanie i wraca do portalu konfiguracyjnego
#define RESET_BTN_X 260
#define RESET_BTN_Y 0
#define RESET_BTN_W 60
#define RESET_BTN_H 32
#define RESET_HOLD_MS 2000

// przycisk "Odśwież" w lewym górnym rogu (ekran listy)
#define REFRESH_BTN_X 0
#define REFRESH_BTN_Y 0
#define REFRESH_BTN_W 60
#define REFRESH_BTN_H 32

// przycisk "Wstecz" w lewym górnym rogu (ekran szczegółów notatki)
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

// przycisk "Rysunek" w prawym gornym rogu ekranu szczegolow (widoczny gdy notatka ma rysunki)
#define DRAWING_BTN_X 270
#define DRAWING_BTN_Y 0
#define DRAWING_BTN_W 50
#define DRAWING_BTN_H 32

#define MAX_NOTES 20

#define LIST_START_Y 44
#define LIST_ROW_HEIGHT 44
#define LIST_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla listy

#define DETAIL_CONTENT_TOP 40
#define DETAIL_LINE_HEIGHT 14
#define DETAIL_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla
