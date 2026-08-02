#include "WeatherScreen.h"
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include "Display.h"
#include "Colors.h"
#include "Config.h"
#include "State.h"
#include "UiHelpers.h"
#include "ClimateSensor.h"

struct WeatherHour {
  String label; // "HH:MM" wyciete z ISO8601 zwracanego przez API
  float temp;
  float precip;
};

static WeatherHour weatherHours[MAX_WEATHER_ENTRIES];
static int weatherCount = 0;      // ile godzin faktycznie wczytano (<= MAX_WEATHER_ENTRIES)
static String weatherDayDates[WEATHER_FORECAST_DAYS]; // "DD.MM" per dzien
static int weatherDaysLoaded = 0; // ile pelnych/czesciowych dni jest w danych
static int selectedDay = 0;
static int weatherScrollRow = 0;
static bool weatherLoaded = false;

void drawWeatherButton() {
  display.fillRoundRect(WEATHER_BTN_X + 4, WEATHER_BTN_Y + 2, WEATHER_BTN_W - 8, WEATHER_BTN_H - 4, 6, COLOR_SECONDARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(WEATHER_BTN_X + 6, WEATHER_BTN_Y + 12);
  display.print("Pogoda");
}

// ile wpisow godzinowych ma wybrany dzien (ostatni dzien moze byc niepelny)
static int currentDayEntryCount() {
  int n = weatherCount - selectedDay * WEATHER_HOURS_PER_DAY;
  if (n < 0) n = 0;
  if (n > WEATHER_HOURS_PER_DAY) n = WEATHER_HOURS_PER_DAY;
  return n;
}

int weatherListRowsPerPage() {
  return (WEATHER_CONTENT_BOTTOM - WEATHER_LIST_START_Y) / WEATHER_ROW_HEIGHT;
}

int weatherListMaxScroll() {
  int maxScroll = currentDayEntryCount() - weatherListRowsPerPage();
  return maxScroll > 0 ? maxScroll : 0;
}

void scrollWeather(int deltaRows) {
  int maxScroll = weatherListMaxScroll();
  int newScroll = weatherScrollRow + deltaRows;
  if (newScroll < 0) newScroll = 0;
  if (newScroll > maxScroll) newScroll = maxScroll;
  if (newScroll == weatherScrollRow) return;

  weatherScrollRow = newScroll;
  renderWeatherScreen();
}

void changeWeatherDay(int delta) {
  int newDay = selectedDay + delta;
  if (newDay < 0) newDay = 0;
  if (newDay > weatherDaysLoaded - 1) newDay = weatherDaysLoaded - 1;
  if (newDay < 0) newDay = 0;
  if (newDay == selectedDay) return;

  selectedDay = newDay;
  weatherScrollRow = 0;
  renderWeatherScreen();
}

// z "2024-01-01T14:00" wyciaga "14:00"
static String extractHourLabel(const String& iso) {
  int tPos = iso.indexOf('T');
  if (tPos == -1 || tPos + 6 > (int)iso.length()) return iso;
  return iso.substring(tPos + 1, tPos + 6);
}

// z "2024-01-01T14:00" wyciaga "01.01"
static String extractDateLabel(const String& iso) {
  if (iso.length() < 10) return iso;
  return iso.substring(8, 10) + "." + iso.substring(5, 7);
}

static String dayLabel(int day) {
  if (day == 0) return "Dzis";
  if (day == 1) return "Jutro";
  if (day == 2) return "Pojutrze";
  if (day >= 0 && day < WEATHER_FORECAST_DAYS) return weatherDayDates[day];
  return "?";
}

static void drawDayNav() {
  bool canPrev = selectedDay > 0;
  bool canNext = selectedDay < weatherDaysLoaded - 1;

  display.fillRoundRect(WEATHER_DAY_PREV_BTN_X, WEATHER_DAY_NAV_Y, WEATHER_DAY_PREV_BTN_W, WEATHER_DAY_NAV_H, 6, canPrev ? COLOR_PRIMARY : COLOR_BORDER);
  display.setTextColor(canPrev ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  display.setTextSize(2);
  display.setCursor(WEATHER_DAY_PREV_BTN_X + 11, WEATHER_DAY_NAV_Y + 3);
  display.print("<");

  display.fillRoundRect(WEATHER_DAY_NEXT_BTN_X, WEATHER_DAY_NAV_Y, WEATHER_DAY_PREV_BTN_W, WEATHER_DAY_NAV_H, 6, canNext ? COLOR_PRIMARY : COLOR_BORDER);
  display.setTextColor(canNext ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  display.setCursor(WEATHER_DAY_NEXT_BTN_X + 11, WEATHER_DAY_NAV_Y + 3);
  display.print(">");

  String label = dayLabel(selectedDay);
  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  int textW = label.length() * 12; // przyblizona szerokosc znaku dla textSize(2)
  display.setCursor((display.width() - textW) / 2, WEATHER_DAY_NAV_Y + 3);
  display.print(label);
}

void renderWeatherScreen() {
  currentScreen = SCREEN_WEATHER;
  display.fillScreen(COLOR_BG);

  display.fillRoundRect(BACK_BTN_X + 4, BACK_BTN_Y + 2, BACK_BTN_W - 8, BACK_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 10, BACK_BTN_Y + 12);
  display.print("< Wstecz");

  display.fillRoundRect(WEATHER_REFRESH_BTN_X + 4, WEATHER_REFRESH_BTN_Y + 2, WEATHER_REFRESH_BTN_W - 8, WEATHER_REFRESH_BTN_H - 4, 6, COLOR_ACCENT_MINT);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setCursor(WEATHER_REFRESH_BTN_X + 3, WEATHER_REFRESH_BTN_Y + 12);
  display.print("Odsw.");

  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  display.setCursor(BACK_BTN_W + 10, 6);
  display.println("POGODA");

  if (!weatherLoaded || weatherCount == 0) {
    display.setTextSize(1);
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, WEATHER_DAY_NAV_Y + 4);
    display.println(weatherLoaded ? "Brak danych prognozy." : "Dotknij Odsw., zeby pobrac prognoze.");
    return;
  }

  drawDayNav();

  // cienka linia oddzielajaca nawigacje dni od reszty tresci
  display.drawFastHLine(10, WEATHER_DAY_NAV_Y + WEATHER_DAY_NAV_H + 4, display.width() - 20, COLOR_BORDER);

  // aktualne dane z wlasnego czujnika SHT40 (niezalezne od API) - tylko przy "Dzis"
  display.setTextSize(1);
  if (selectedDay == 0) {
    display.setTextColor(COLOR_ACCENT_MINT);
    display.setCursor(10, WEATHER_CLIMATE_Y);
    if (sht4Ready && !isnan(currentTempC)) {
      display.printf("Teraz (czujnik): %.1f C   wilg. %.0f %%", currentTempC, currentHumidityPct);
    } else {
      display.print("Teraz (czujnik): brak danych");
    }
  } else {
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, WEATHER_CLIMATE_Y);
    display.print("Prognoza Open-Meteo");
  }

  int dayCount = currentDayEntryCount();
  if (dayCount == 0) {
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, WEATHER_LIST_START_Y);
    display.println("Brak danych dla tego dnia.");
    return;
  }

  int dayStart = selectedDay * WEATHER_HOURS_PER_DAY;
  int perPage = weatherListRowsPerPage();
  int rows = min(perPage, dayCount - weatherScrollRow);
  int y = WEATHER_LIST_START_Y;

  for (int i = 0; i < rows; i++) {
    int idx = dayStart + weatherScrollRow + i;
    const WeatherHour& h = weatherHours[idx];

    // delikatne naprzemienne tlo wierszy dla czytelnosci
    if (i % 2 == 1) {
      display.fillRect(6, y - 2, display.width() - 12, WEATHER_ROW_HEIGHT, COLOR_SURFACE);
    }

    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(12, y);
    display.print(h.label);

    display.setTextColor(COLOR_TEXT);
    display.setCursor(80, y);
    display.printf("%5.1f C", h.temp);

    // opady podswietlone kolorem akcentu, gdy faktycznie cos pada - latwiej wychwycic wzrokiem
    display.setTextColor(h.precip > 0.05f ? COLOR_PRIMARY : COLOR_TEXT_MUTED);
    display.setCursor(190, y);
    display.printf("%4.1f mm", h.precip);

    y += WEATHER_ROW_HEIGHT;
  }

  bool canUp = weatherScrollRow > 0;
  bool canDown = weatherScrollRow < weatherListMaxScroll();
  drawScrollButtons(canUp, canDown);
}

void fetchWeather() {
  currentScreen = SCREEN_WEATHER;
  showMessage("Pobieranie pogody...", "");

  WiFiClientSecure client;
  client.setInsecure(); // upraszczamy: bez weryfikacji certyfikatu (publiczne dane pogodowe, niewrazliwe)

  String url = String("https://api.open-meteo.com/v1/forecast?latitude=") + WEATHER_LAT +
               "&longitude=" + WEATHER_LON +
               "&hourly=temperature_2m,precipitation&forecast_days=" + WEATHER_FORECAST_DAYS +
               "&timezone=auto";

  HTTPClient http;
  http.begin(client, url);
  int status = http.GET();
  String response = http.getString();
  http.end();

  weatherCount = 0;
  weatherDaysLoaded = 0;
  weatherScrollRow = 0;
  selectedDay = 0;

  if (status != 200) {
    Serial.printf("weather status=%d body=%s\n", status, response.c_str());
    weatherLoaded = false;
    renderWeatherScreen();
    display.setTextColor(COLOR_DANGER);
    display.setCursor(10, WEATHER_DAY_NAV_Y);
    display.printf("Blad pobierania (HTTP %d)", status);
    return;
  }

  JsonDocument doc;
  if (deserializeJson(doc, response) != DeserializationError::Ok) {
    weatherLoaded = false;
    renderWeatherScreen();
    display.setTextColor(COLOR_DANGER);
    display.setCursor(10, WEATHER_DAY_NAV_Y);
    display.println("Blad odczytu danych pogodowych");
    return;
  }

  JsonArray times = doc["hourly"]["time"].as<JsonArray>();
  JsonArray temps = doc["hourly"]["temperature_2m"].as<JsonArray>();
  JsonArray precs = doc["hourly"]["precipitation"].as<JsonArray>();

  for (size_t i = 0; i < times.size() && weatherCount < MAX_WEATHER_ENTRIES; i++) {
    String iso = String((const char*)times[i]);
    weatherHours[weatherCount].label = extractHourLabel(iso);
    weatherHours[weatherCount].temp = temps[i] | 0.0f;
    weatherHours[weatherCount].precip = precs[i] | 0.0f;

    if (weatherCount % WEATHER_HOURS_PER_DAY == 0) {
      int day = weatherCount / WEATHER_HOURS_PER_DAY;
      if (day < WEATHER_FORECAST_DAYS) weatherDayDates[day] = extractDateLabel(iso);
    }

    weatherCount++;
  }

  weatherDaysLoaded = (weatherCount + WEATHER_HOURS_PER_DAY - 1) / WEATHER_HOURS_PER_DAY;
  weatherLoaded = true;
  renderWeatherScreen();
}
