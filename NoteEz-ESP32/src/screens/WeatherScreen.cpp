#include "WeatherScreen.h"
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include "../display/Display.h"
#include "../Colors.h"
#include "../../Config.h"
#include "../State.h"
#include "../UiHelpers.h"
#include "../sensors/ClimateSensor.h"

// kompaktowy wpis godzinowy (4 B); godzina wynika z indeksu (idx % 24)
struct WeatherHour {
  int16_t temp10;    // temperatura * 10 [C]
  uint16_t precip10; // opad * 10 [mm]
};

static WeatherHour weatherHours[MAX_WEATHER_ENTRIES];
static int weatherCount = 0;      // ile godzin faktycznie wczytano (<= MAX_WEATHER_ENTRIES)
static String weatherDayDates[WEATHER_FORECAST_DAYS]; // "DD.MM" per dzien
static int weatherDaysLoaded = 0; // ile pelnych/czesciowych dni jest w danych
static int selectedDay = 0;
static int windowStart = WEATHER_WINDOW_DEFAULT_START; // pierwsza godzina okna wykresow
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

void shiftWeatherWindow(int deltaHours) {
  int newStart = windowStart + deltaHours;
  if (newStart < 0) newStart = 0;
  if (newStart > WEATHER_WINDOW_MAX_START) newStart = WEATHER_WINDOW_MAX_START;
  if (newStart == windowStart) return;

  windowStart = newStart;
  renderWeatherScreen();
}

void changeWeatherDay(int delta) {
  int newDay = selectedDay + delta;
  if (newDay < 0) newDay = 0;
  if (newDay > weatherDaysLoaded - 1) newDay = weatherDaysLoaded - 1;
  if (newDay < 0) newDay = 0;
  if (newDay == selectedDay) return;

  selectedDay = newDay;
  windowStart = WEATHER_WINDOW_DEFAULT_START;
  renderWeatherScreen();
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

// przyciski przesuwania okna godzinowego (pozycje jak SCROLL_UP/DOWN_BTN), szare na krancach
static void drawHourButtons() {
  bool canLeft = windowStart > 0;
  bool canRight = windowStart < WEATHER_WINDOW_MAX_START;

  display.fillRoundRect(SCROLL_UP_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H, 6, canLeft ? COLOR_PRIMARY : COLOR_BORDER);
  display.setTextColor(canLeft ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  display.setTextSize(2);
  display.setCursor(SCROLL_UP_BTN_X + 21, SCROLL_BTN_Y + 7);
  display.print("<");

  display.fillRoundRect(SCROLL_DOWN_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H, 6, canRight ? COLOR_PRIMARY : COLOR_BORDER);
  display.setTextColor(canRight ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  display.setCursor(SCROLL_DOWN_BTN_X + 21, SCROLL_BTN_Y + 7);
  display.print(">");

  // zakres okna po lewej od przyciskow, np. "Okno: 06-18h"
  display.setTextSize(1);
  display.setTextColor(COLOR_TEXT_MUTED);
  display.setCursor(10, SCROLL_BTN_Y + 11);
  display.printf("Okno: %02d-%02dh", windowStart, windowStart + WEATHER_WINDOW_HOURS);
}

// i-ty punkt okna (0..WEATHER_WINDOW_HOURS) -> wspolrzedna X na wykresie
static int hourToX(int i) {
  return WEATHER_CHART_X0 + (i * (WEATHER_CHART_X1 - WEATHER_CHART_X0)) / WEATHER_WINDOW_HOURS;
}

// podpis wartosci osi Y wyrownany do prawej przy lewej krawedzi pola wykresu
static void drawYLabel(int value, int y) {
  char buf[8];
  int len = snprintf(buf, sizeof(buf), "%d", value);
  display.setTextColor(COLOR_TEXT_MUTED);
  display.setCursor(WEATHER_CHART_X0 - 4 - len * 6, y - 3);
  display.print(buf);
}

static int floorDiv10(int v10) { // floor(v10 / 10) takze dla ujemnych
  return v10 >= 0 ? v10 / 10 : -((-v10 + 9) / 10);
}

// wykres temperatury: linia, os Y w pelnych stopniach (3 podzialki: min, srodek, max)
static void drawTempChart(int dayStart, int pts) {
  display.setTextSize(1);
  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setCursor(WEATHER_CHART_X0, WEATHER_TEMP_TITLE_Y);
  display.print("Temperatura [C]");

  int mn10 = 32767, mx10 = -32768;
  for (int i = 0; i < pts; i++) {
    int t = weatherHours[dayStart + windowStart + i].temp10;
    if (t < mn10) mn10 = t;
    if (t > mx10) mx10 = t;
  }
  int lo = floorDiv10(mn10);
  int hi = -floorDiv10(-mx10); // ceil
  while (hi - lo < WEATHER_TEMP_MIN_SPAN) { // min. zakres, rozszerzany na zmiane w obie strony
    hi++;
    if (hi - lo < WEATHER_TEMP_MIN_SPAN) lo--;
  }
  if ((hi - lo) & 1) hi++; // parzysty zakres -> srodkowa podzialka tez jest cala liczba
  int span10 = (hi - lo) * 10;
  int h = WEATHER_TEMP_BOTTOM - WEATHER_TEMP_TOP;

  for (int k = 0; k < 3; k++) {
    int y = WEATHER_TEMP_BOTTOM - h * k / 2;
    display.drawFastHLine(WEATHER_CHART_X0, y, WEATHER_CHART_X1 - WEATHER_CHART_X0 + 1, COLOR_BORDER);
    drawYLabel(lo + (hi - lo) * k / 2, y);
  }

  int px = 0, py = 0;
  for (int i = 0; i < pts; i++) {
    int t10 = weatherHours[dayStart + windowStart + i].temp10;
    int x = hourToX(i);
    int y = WEATHER_TEMP_BOTTOM - ((t10 - lo * 10) * h) / span10;
    if (i > 0) {
      display.drawLine(px, py, x, y, COLOR_PRIMARY);
      display.drawLine(px, py + 1, x, y + 1, COLOR_PRIMARY); // pogrubienie linii
    }
    display.fillRect(x - 1, y - 1, 3, 3, COLOR_ACCENT_MINT);
    px = x;
    py = y;
  }
}

// wykres opadow: slupki, os Y w mm (0..max, minimum WEATHER_PRECIP_MIN_AXIS) + podpisy godzin
static void drawPrecipChart(int dayStart, int pts) {
  display.setTextSize(1);
  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setCursor(WEATHER_CHART_X0, WEATHER_PRECIP_TITLE_Y);
  display.print("Opady [mm]");

  int mx10 = 0;
  for (int i = 0; i < pts; i++) {
    int p = weatherHours[dayStart + windowStart + i].precip10;
    if (p > mx10) mx10 = p;
  }
  int axis = (mx10 + 9) / 10; // ceil do pelnych mm
  if (axis < WEATHER_PRECIP_MIN_AXIS) axis = WEATHER_PRECIP_MIN_AXIS;
  if (axis & 1) axis++;       // parzysta os -> srodkowa podzialka calkowita
  int h = WEATHER_PRECIP_BOTTOM - WEATHER_PRECIP_TOP;

  for (int k = 0; k < 3; k++) {
    int y = WEATHER_PRECIP_BOTTOM - h * k / 2;
    display.drawFastHLine(WEATHER_CHART_X0, y, WEATHER_CHART_X1 - WEATHER_CHART_X0 + 1, COLOR_BORDER);
    drawYLabel(axis * k / 2, y);
  }

  for (int i = 0; i < pts; i++) {
    int p10 = weatherHours[dayStart + windowStart + i].precip10;
    int x = hourToX(i);
    if (p10 > 0) {
      int bh = (p10 * h) / (axis * 10);
      if (bh < 1) bh = 1; // nawet drobny opad jest widoczny
      if (bh > h) bh = h;
      display.fillRect(x - 6, WEATHER_PRECIP_BOTTOM - bh, 13, bh, COLOR_PRIMARY);
    }
    // podpis co 3 h (godziny podzielne przez 3) + znacznik
    int hour = windowStart + i;
    if (hour % 3 == 0) {
      display.drawFastVLine(x, WEATHER_PRECIP_BOTTOM + 1, 2, COLOR_TEXT_MUTED);
      display.setTextColor(COLOR_TEXT_MUTED);
      display.setCursor(x - 6, WEATHER_XLABEL_Y);
      display.printf("%02d", hour);
    }
  }
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

  int dayStart = selectedDay * WEATHER_HOURS_PER_DAY;
  // ile punktow okna ma dane (ostatni dzien moze byc niepelny)
  int pts = min(WEATHER_WINDOW_HOURS + 1, currentDayEntryCount() - windowStart);
  if (pts < 2) {
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, WEATHER_TEMP_TITLE_Y);
    display.println("Brak danych dla tego okna.");
  } else {
    drawTempChart(dayStart, pts);
    drawPrecipChart(dayStart, pts);
  }

  drawHourButtons();
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
  windowStart = WEATHER_WINDOW_DEFAULT_START;
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

  // filtr: parsujemy tylko hourly.time / temperature_2m / precipitation
  JsonDocument filter;
  filter["hourly"]["time"] = true;
  filter["hourly"]["temperature_2m"] = true;
  filter["hourly"]["precipitation"] = true;

  JsonDocument doc;
  if (deserializeJson(doc, response, DeserializationOption::Filter(filter)) != DeserializationError::Ok) {
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
    float t = temps[i] | 0.0f;
    float p = precs[i] | 0.0f;
    if (p < 0) p = 0;
    weatherHours[weatherCount].temp10 = (int16_t)lroundf(t * 10.0f);
    weatherHours[weatherCount].precip10 = (uint16_t)lroundf(p * 10.0f);

    if (weatherCount % WEATHER_HOURS_PER_DAY == 0) {
      int day = weatherCount / WEATHER_HOURS_PER_DAY;
      if (day < WEATHER_FORECAST_DAYS) weatherDayDates[day] = extractDateLabel(String(times[i].as<const char*>()));
    }

    weatherCount++;
  }

  weatherDaysLoaded = (weatherCount + WEATHER_HOURS_PER_DAY - 1) / WEATHER_HOURS_PER_DAY;
  weatherLoaded = true;
  renderWeatherScreen();
}
