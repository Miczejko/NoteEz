#pragma once
#include <Arduino.h>
#include "Colors.h"

// ---- proste komunikaty na ekranie ----
void showMessage(const char* line1, const char* line2 = "", uint32_t color = COLOR_TEXT);

void drawTopButtons();
void drawResetButton();
bool pointInRect(int32_t x, int32_t y, int rx, int ry, int rw, int rh);

// przyciski przewijania (dol ekranu) - wspolne dla listy notatek i widoku szczegolow
void drawScrollButtons(bool canUp, bool canDown);

// usuwa przypadkowo wpisany prefiks "http(s)://" oraz koncowy "/" z pola adresu serwera,
// zeby nie zbudowac zdublowanego URL typu "http://http://..." (przyczyna bledu HTTPC -1)
String normalizeHost(String host);

// true, jesli apiHost wyglada na "goly" adres IP (ew. z portem) - takie adresy to zawsze
// lokalny/dev backend po http. Wszystko inne (domena, np. domyslne "noteez.online") to
// hostowana produkcja, ktora dziala tylko po https (za Cloudflare Workerem).
bool isApiHostRawIp(const String& host);

// zamienia kolor tekstu "#rrggbb" (z edytora TipTap) na kolor RGB565 uzywany przez LovyanGFX
uint16_t hexToColor565(const String& hex);
