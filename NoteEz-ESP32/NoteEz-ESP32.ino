#include <WiFiManager.h>   // https://github.com/tzapu/WiFiManager
#include <Preferences.h>
#include <esp_sleep.h>
#include <driver/gpio.h>

#include "Config.h"
#include "Colors.h"
#include "Display.h"
#include "State.h"
#include "UiHelpers.h"
#include "WifiPairing.h"
#include "SleepMode.h"
#include "NotesScreen.h"
#include "NoteDetailScreen.h"
#include "DrawingScreen.h"
#include "ClimateSensor.h"
#include "WeatherScreen.h"

void setup() {
  Serial.begin(115200);
  delay(200); // czas dla monitora Serial, zeby zdazyl sie podlaczyc po restarcie z deep sleep

  // Diagnostyka: pokazuje czy ten start to powrot z deep sleep (i przez co), czy zwykly reset/wgranie.
  // ESP_SLEEP_WAKEUP_GPIO (touch zadzialal) / ESP_SLEEP_WAKEUP_UNDEFINED (zwykly reset/power-on).
  Serial.printf("[boot] przyczyna wybudzenia: %d\n", (int)esp_sleep_get_wakeup_cause());

  // Zwalnia "zatrzask" (gpio_hold) z ewentualnego poprzedniego deep sleep i wlacza
  // podswietlenie na nowo - bez tego pin zostalby zablokowany na stanie niskim z ostatniego snu.
  gpio_hold_dis((gpio_num_t)BACKLIGHT_PIN);
  pinMode(BACKLIGHT_PIN, OUTPUT);
  digitalWrite(BACKLIGHT_PIN, HIGH);

  // TYMCZASOWA DIAGNOSTYKA: konfigurujemy T_IRQ od razu przy starcie (nie dopiero przed
  // usnieciem), zeby test w loop() mial wiarygodny, zdefiniowany odczyt od pierwszej sekundy.
  pinMode(TIRQ_PIN, INPUT_PULLUP);

  display.init();
  display.setRotation(1);
  display.fillScreen(COLOR_BG);

  // kalibracja dotyku (jak w oryginalnym szkicu)
  uint16_t calData[8] = {3826, 308, 3886, 3793, 361, 252, 359, 3741};
  display.setTouchCalibrate(calData);

  // Bitbangowane I2C (SoftI2C) - celowo bez biblioteki Wire, ktora psula dotyk na tej plytce.
  initClimateSensor();

  prefs.begin("noteez", false);
  apiKey = prefs.getString("apiKey", "");
  apiHost = normalizeHost(prefs.getString("apiHost", DEFAULT_API_HOST));

  showMessage("Laczenie z WiFi...", "");

  // --- WiFiManager: pola dodatkowe w portalu konfiguracyjnym ---
  // kod parowania wygenerowany w apce Vue (zakladka Urzadzenia -> Sparuj urzadzenie)
  pairingCodeParam = new WiFiManagerParameter("code", "Kod parowania z apki Vue", "", 8);
  // adres backendu w sieci lokalnej, np. 192.168.1.50:8080
  apiHostParam = new WiFiManagerParameter("host", "Adres serwera (IP:port)", apiHost.c_str(), 40);

  wm.addParameter(pairingCodeParam);
  wm.addParameter(apiHostParam);
  wm.setAPCallback(configModeCallback);
  wm.setConfigPortalTimeout(180); // po 3 min bez konfiguracji restart i kolejna proba

  bool connected = wm.autoConnect("Notatnik-ESP32");

  if (!connected) {
    showMessage("Brak polaczenia", "Restart za 3s...", COLOR_DANGER);
    delay(3000);
    ESP.restart();
  }

  // WiFiManager po konfiguracji przez portal (AP) czasem zostawia urzadzenie w trybie
  // WIFI_AP_STA (softAP + stacja jednoczesnie), co potrafi psuc wychodzace polaczenia
  // HTTPClient (blad -1 "connection refused") mimo ze sieć jest ok. Wymuszamy czysty tryb STA.
  WiFi.mode(WIFI_STA);
  delay(200);

  // host mogl zostac zmieniony w portalu konfiguracyjnym
  String hostFromPortal = normalizeHost(String(apiHostParam->getValue()));
  if (hostFromPortal.length() > 0) {
    apiHost = hostFromPortal;
  }
  Serial.println("apiHost: " + apiHost);

  showMessage("Polaczono!", WiFi.localIP().toString().c_str(), COLOR_ACCENT_MINT);
  delay(1000);

  // jesli w portalu wpisano kod parowania - sparuj urzadzenie zanim wejdziemy do glownego ekranu
  String enteredCode = String(pairingCodeParam->getValue());
  if (enteredCode.length() > 0) {
    claimDevice(enteredCode);
  } else if (apiKey.length() == 0) {
    showMessage("Brak parowania", "Uzyj portalu WiFi, by wpisac kod", COLOR_DANGER);
    delay(3000);
  }

  fetchNotesLite();

  lastActivityMillis = millis(); // liczy sie tez czas od ostatniego uzycia przed usypianiem
}

void loop() {
  int32_t x, y;
  static uint32_t resetTouchStart = 0;
  static uint32_t lastScrollTap = 0;

  bool touched = display.getTouch(&x, &y);

  if (!touched) {
    resetTouchStart = 0;
    // usypianie wylaczone - nie dzialalo poprawnie (biale/zamarzniete ekrany po wybudzeniu)
    delay(20);
    return;
  }

  lastActivityMillis = millis();

  if (currentScreen == SCREEN_LIST) {
    if (pointInRect(x, y, REFRESH_BTN_X, REFRESH_BTN_Y, REFRESH_BTN_W, REFRESH_BTN_H)) {
      fetchNotesLite();
      delay(300); // debounce
      return;
    }

    if (pointInRect(x, y, RESET_BTN_X, RESET_BTN_Y, RESET_BTN_W, RESET_BTN_H)) {
      if (resetTouchStart == 0) resetTouchStart = millis();
      if (millis() - resetTouchStart >= RESET_HOLD_MS) {
        resetWifiAndPairing();
      }
      delay(20);
      return;
    }
    resetTouchStart = 0;

    if (pointInRect(x, y, WEATHER_BTN_X, WEATHER_BTN_Y, WEATHER_BTN_W, WEATHER_BTN_H)) {
      fetchWeather();
      delay(300); // debounce
      return;
    }

    if (millis() - lastScrollTap > 180) { // debounce przewijania, ale pozwala trzymac palec
      if (pointInRect(x, y, SCROLL_UP_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H)) {
        scrollList(-1);
        lastScrollTap = millis();
        return;
      } else if (pointInRect(x, y, SCROLL_DOWN_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H)) {
        scrollList(1);
        lastScrollTap = millis();
        return;
      }
    }

    // dotkniecie wiersza notatki (karty z ramka) -> szczegoly
    if (y >= LIST_START_Y && y < LIST_CONTENT_BOTTOM) {
      int rowInPage = (y - LIST_START_Y) / LIST_ROW_HEIGHT;
      int index = listScrollRow + rowInPage;
      if (rowInPage < listRowsPerPage() && index < notesCount) {
        selectNote(index);
        delay(300); // debounce
        return;
      }
    }
  } else if (currentScreen == SCREEN_DETAIL) {
    if (pointInRect(x, y, BACK_BTN_X, BACK_BTN_Y, BACK_BTN_W, BACK_BTN_H)) {
      renderNotesList();
      delay(300); // debounce
      return;
    }

    if (detailHasDrawing && pointInRect(x, y, DRAWING_BTN_X, DRAWING_BTN_Y, DRAWING_BTN_W, DRAWING_BTN_H)) {
      detailDrawingIndex = 0;
      renderDrawingScreen(detailDrawingIndex);
      delay(300); // debounce
      return;
    }

    if (millis() - lastScrollTap > 180) { // debounce przewijania, ale pozwala trzymac palec
      if (pointInRect(x, y, SCROLL_UP_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H)) {
        scrollDetail(-detailLinesPerPage() / 2 - 1);
        lastScrollTap = millis();
      } else if (pointInRect(x, y, SCROLL_DOWN_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H)) {
        scrollDetail(detailLinesPerPage() / 2 + 1);
        lastScrollTap = millis();
      }
    }
  } else if (currentScreen == SCREEN_WEATHER) {
    if (pointInRect(x, y, BACK_BTN_X, BACK_BTN_Y, BACK_BTN_W, BACK_BTN_H)) {
      renderNotesList();
      delay(300); // debounce
      return;
    }

    if (pointInRect(x, y, WEATHER_REFRESH_BTN_X, WEATHER_REFRESH_BTN_Y, WEATHER_REFRESH_BTN_W, WEATHER_REFRESH_BTN_H)) {
      fetchWeather();
      delay(300); // debounce
      return;
    }

    if (pointInRect(x, y, WEATHER_DAY_PREV_BTN_X, WEATHER_DAY_NAV_Y, WEATHER_DAY_PREV_BTN_W, WEATHER_DAY_NAV_H)) {
      changeWeatherDay(-1);
      delay(250); // debounce
      return;
    }

    if (pointInRect(x, y, WEATHER_DAY_NEXT_BTN_X, WEATHER_DAY_NAV_Y, WEATHER_DAY_PREV_BTN_W, WEATHER_DAY_NAV_H)) {
      changeWeatherDay(1);
      delay(250); // debounce
      return;
    }

    if (millis() - lastScrollTap > 180) { // debounce przewijania, ale pozwala trzymac palec
      if (pointInRect(x, y, SCROLL_UP_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H)) {
        scrollWeather(-1);
        lastScrollTap = millis();
      } else if (pointInRect(x, y, SCROLL_DOWN_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H)) {
        scrollWeather(1);
        lastScrollTap = millis();
      }
    }
  } else { // SCREEN_DRAWING
    if (pointInRect(x, y, BACK_BTN_X, BACK_BTN_Y, BACK_BTN_W, BACK_BTN_H)) {
      renderNoteDetail();
      delay(300); // debounce
      return;
    }

    int drawingCount = detailDoc["drawings"].as<JsonArray>().size();
    if (drawingCount > 1 && y > BACK_BTN_H && millis() - lastScrollTap > 300) {
      // dotkniecie lewej/prawej polowy ekranu (ponizej gornego paska) przelacza rysunek
      detailDrawingIndex = (x < display.width() / 2)
        ? (detailDrawingIndex - 1 + drawingCount) % drawingCount
        : (detailDrawingIndex + 1) % drawingCount;
      renderDrawingScreen(detailDrawingIndex);
      lastScrollTap = millis();
    }
  }
}
