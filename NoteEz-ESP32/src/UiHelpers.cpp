#include "UiHelpers.h"
#include "Display.h"
#include "Config.h"

void showMessage(const char* line1, const char* line2, uint32_t color) {
  display.fillScreen(COLOR_BG);
  display.setTextColor(color);
  display.setTextSize(2);
  display.setCursor(10, 40);
  display.println(line1);
  if (strlen(line2) > 0) {
    display.setCursor(10, 70);
    display.setTextSize(1);
    display.println(line2);
  }
}

void drawTopButtons() {
  display.fillRoundRect(REFRESH_BTN_X + 4, REFRESH_BTN_Y + 2, REFRESH_BTN_W - 8, REFRESH_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(REFRESH_BTN_X + 10, REFRESH_BTN_Y + 12);
  display.print("Odswiez");

  display.fillRoundRect(TIMER_BTN_X + 4, TIMER_BTN_Y + 2, TIMER_BTN_W - 8, TIMER_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setCursor(TIMER_BTN_X + 12, TIMER_BTN_Y + 12);
  display.print("Timer");
}

// przycisk resetu WiFi/parowania - malutki, w lewym dolnym rogu (patrz komentarz przy
// RESET_BTN_* w Config.h - uzywany bardzo rzadko, wiec nie zasluguje na eksponowane miejsce)
void drawResetButton() {
  display.fillRoundRect(RESET_BTN_X + 2, RESET_BTN_Y + 2, RESET_BTN_W - 4, RESET_BTN_H - 4, 5, COLOR_RESET);
  display.setTextColor(COLOR_TEXT);
  display.setTextSize(1);
  display.setCursor(RESET_BTN_X + 5, RESET_BTN_Y + RESET_BTN_H / 2 - 4);
  display.print("Reset");
}

bool pointInRect(int32_t x, int32_t y, int rx, int ry, int rw, int rh) {
  return x >= rx && x <= rx + rw && y >= ry && y <= ry + rh;
}

void drawScrollButtons(bool canUp, bool canDown) {
  display.fillRoundRect(SCROLL_UP_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H, 6, canUp ? COLOR_PRIMARY : COLOR_BORDER);
  display.setTextColor(canUp ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  display.setTextSize(1);
  display.setCursor(SCROLL_UP_BTN_X + 14, SCROLL_BTN_Y + 11);
  display.print("Gora");

  display.fillRoundRect(SCROLL_DOWN_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H, 6, canDown ? COLOR_PRIMARY : COLOR_BORDER);
  display.setTextColor(canDown ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  display.setCursor(SCROLL_DOWN_BTN_X + 16, SCROLL_BTN_Y + 11);
  display.print("Dol");
}

String normalizeHost(String host) {
  host.trim();
  host.replace("http://", "");
  host.replace("https://", "");
  while (host.endsWith("/")) host.remove(host.length() - 1);
  return host;
}

bool isApiHostRawIp(const String& host) {
  for (size_t i = 0; i < host.length(); i++) {
    char c = host[i];
    if (!isDigit(c) && c != '.' && c != ':') return false;
  }
  return host.length() > 0;
}

uint16_t hexToColor565(const String& hex) {
  if (hex.length() < 7 || hex[0] != '#') return TFT_BLACK;
  long rgb = strtol(hex.c_str() + 1, nullptr, 16);
  uint8_t r = (rgb >> 16) & 0xFF;
  uint8_t g = (rgb >> 8) & 0xFF;
  uint8_t b = rgb & 0xFF;
  return display.color565(r, g, b);
}
