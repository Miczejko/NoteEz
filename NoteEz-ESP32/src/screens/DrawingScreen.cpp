#include "DrawingScreen.h"
#include <ArduinoJson.h>
#include "Display.h"
#include "Colors.h"
#include "Config.h"
#include "State.h"
#include "UiHelpers.h"

// rysunki sa przechowywane jako lista pociagniec (linie miedzy punktami), nie jako bitmapy,
// wiec renderujemy je wprost jako polaczone odcinki przeskalowane pod rozmiar ekranu
void renderDrawingScreen(int index) {
  currentScreen = SCREEN_DRAWING;
  // tlo rysunku zostaje jasne - pociagniecia sa zapisane z zalozeniem bialego
  // canvasu (tak jak w edytorze na stronie), wiec ciemny motyw psulby ich czytelnosc
  display.fillScreen(TFT_WHITE);

  display.fillRoundRect(BACK_BTN_X + 4, BACK_BTN_Y + 2, BACK_BTN_W - 8, BACK_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 10, BACK_BTN_Y + 12);
  display.print("< Wstecz");

  JsonArray drawings = detailDoc["drawings"].as<JsonArray>();
  if (index < 0 || index >= (int)drawings.size()) return;

  if (drawings.size() > 1) {
    display.setTextColor(TFT_DARKGREY);
    display.setCursor(display.width() - 60, 12);
    display.printf("%d/%d (dotknij)", index + 1, (int)drawings.size());
  }

  String strokesJson = String((const char*)(drawings[index]["strokesJson"] | ""));
  if (strokesJson.length() == 0) return;

  JsonDocument strokesDoc;
  if (deserializeJson(strokesDoc, strokesJson) != DeserializationError::Ok) return;
  JsonArray strokes = strokesDoc["strokes"].as<JsonArray>();

  // granice rysunku w oryginalnych wspolrzednych (przestrzen canvasu w przegladarce)
  float minX = 1e9, minY = 1e9, maxX = -1e9, maxY = -1e9;
  for (JsonObject stroke : strokes) {
    for (JsonObject pt : stroke["points"].as<JsonArray>()) {
      float px = pt["x"] | 0.0f;
      float py = pt["y"] | 0.0f;
      minX = min(minX, px); maxX = max(maxX, px);
      minY = min(minY, py); maxY = max(maxY, py);
    }
  }
  if (maxX < minX) return; // brak punktow

  float srcW = max(1.0f, maxX - minX);
  float srcH = max(1.0f, maxY - minY);

  int areaTop = 40;
  int areaBottom = display.height() - 10;
  int areaW = display.width() - 20;
  int areaH = areaBottom - areaTop;

  float scale = min((float)areaW / srcW, (float)areaH / srcH);
  float offsetX = 10 + (areaW - srcW * scale) / 2 - minX * scale;
  float offsetY = areaTop + (areaH - srcH * scale) / 2 - minY * scale;

  for (JsonObject stroke : strokes) {
    JsonArray points = stroke["points"].as<JsonArray>();
    if (points.size() < 2) continue;

    uint16_t color = hexToColor565(String((const char*)(stroke["color"] | "#000000")));

    float prevX = 0, prevY = 0;
    bool hasPrev = false;
    for (JsonObject pt : points) {
      float px = (float)(pt["x"] | 0.0f) * scale + offsetX;
      float py = (float)(pt["y"] | 0.0f) * scale + offsetY;
      if (hasPrev) {
        display.drawLine((int)prevX, (int)prevY, (int)px, (int)py, color);
      }
      prevX = px;
      prevY = py;
      hasPrev = true;
    }
  }
}
