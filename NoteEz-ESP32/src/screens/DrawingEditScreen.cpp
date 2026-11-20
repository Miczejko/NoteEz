#include "DrawingEditScreen.h"
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include "DrawingScreen.h"
#include "../display/Display.h"
#include "../Colors.h"
#include "../../Config.h"
#include "../State.h"
#include "../UiHelpers.h"
#include "../buzzer/Buzzer.h"

// ---- dane rysunku: kompaktowe, statyczne bufory (bez alokacji przy rysowaniu) ----
struct DrawPoint {
  int16_t x, y; // wzgledem lewego-gornego rogu obszaru rysowania
};
struct DrawStroke {
  uint16_t start; // indeks pierwszego punktu w tablicy punktow
  uint16_t count; // liczba punktow (>= 2)
  uint8_t color;  // indeks w PALETTE
};

static DrawPoint points[DRAW_MAX_POINTS];
static DrawStroke strokes[DRAW_MAX_STROKES];
static uint8_t strokeCount = 0;  // zatwierdzone linie: strokes[0..strokeCount)
static uint8_t redoCount = 0;    // linie cofniete: strokes[strokeCount..strokeCount+redoCount) - dane nadal w tablicach
static uint16_t pointsUsed = 0;  // punkty zatwierdzonych linii (koniec ostatniej linii)

// linia w trakcie rysowania (jeszcze niezatwierdzona): punkty od points[pointsUsed]
static bool penDown = false;
static uint16_t curCount = 0;
static unsigned long lastPenMs = 0;
static bool limitReported = false; // komunikat o limicie raz na dotkniecie, nie na kazdy punkt

static uint8_t selectedColor = 0;
static bool discardArmed = false; // pierwsze "Anuluj" przy niepustym rysunku tylko prosi o potwierdzenie
static unsigned long lastButtonMs = 0;

struct PaletteColor {
  uint8_t r, g, b;
};
static const PaletteColor PALETTE[DRAW_COLOR_COUNT] = {
  {0, 0, 0},       // czarny
  {214, 69, 69},   // czerwony
  {52, 104, 192},  // niebieski
  {47, 158, 68},   // zielony
  {224, 138, 44},  // pomaranczowy
};

static uint16_t colorOf(uint8_t i) {
  return RGB565(PALETTE[i].r, PALETTE[i].g, PALETTE[i].b);
}

// ---- rysowanie interfejsu ----

static void drawButton(int x, int y, int w, int h, const char* label, uint16_t bg, uint16_t fg) {
  display.fillRoundRect(x + 2, y + 2, w - 4, h - 4, 5, bg);
  display.setTextColor(fg);
  display.setTextSize(1);
  display.setCursor(x + (w - (int)strlen(label) * 6) / 2, y + h / 2 - 4);
  display.print(label);
}

static void drawToolbar() {
  display.fillRect(0, 0, display.width(), DRAW_TOOLBAR_H, COLOR_BG);

  drawButton(0, 0, DRAW_CANCEL_BTN_W, DRAW_ROW_H, "Anuluj", COLOR_PRIMARY, COLOR_ON_ACCENT);
  for (int i = 0; i < DRAW_COLOR_COUNT; i++) {
    int x = DRAW_COLOR_X0 + i * DRAW_COLOR_PITCH;
    display.fillRoundRect(x, 3, DRAW_COLOR_W, DRAW_ROW_H - 6, 4, colorOf(i));
    display.drawRoundRect(x, 3, DRAW_COLOR_W, DRAW_ROW_H - 6, 4, i == selectedColor ? COLOR_BRIGHT_TEXT : COLOR_BORDER);
    if (i == selectedColor) display.drawRoundRect(x + 1, 4, DRAW_COLOR_W - 2, DRAW_ROW_H - 8, 3, COLOR_BRIGHT_TEXT);
  }
  drawButton(DRAW_SAVE_BTN_X, 0, DRAW_SAVE_BTN_W, DRAW_ROW_H, "Zapisz", COLOR_ACCENT_MINT, COLOR_ON_ACCENT);

  bool canUndo = strokeCount > 0;
  bool canRedo = redoCount > 0;
  drawButton(DRAW_UNDO_BTN_X, DRAW_ROW_H, DRAW_ROW2_BTN_W, DRAW_ROW_H, "Cofnij",
             canUndo ? COLOR_PRIMARY : COLOR_BORDER, canUndo ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  drawButton(DRAW_REDO_BTN_X, DRAW_ROW_H, DRAW_ROW2_BTN_W, DRAW_ROW_H, "Przywroc",
             canRedo ? COLOR_PRIMARY : COLOR_BORDER, canRedo ? COLOR_ON_ACCENT : COLOR_TEXT_MUTED);
  drawButton(DRAW_CLEAR_BTN_X, DRAW_ROW_H, DRAW_ROW2_BTN_W, DRAW_ROW_H, "Wyczysc", COLOR_PRIMARY, COLOR_ON_ACCENT);
}

// pasek statusu pod obszarem rysowania: komunikat albo (msg == nullptr) licznik uzycia limitow
static void drawStatus(const char* msg, uint16_t color) {
  display.fillRect(0, DRAW_STATUS_Y, display.width(), DRAW_STATUS_H, TFT_WHITE);
  display.drawFastHLine(0, DRAW_STATUS_Y, display.width(), TFT_LIGHTGREY);
  display.setTextSize(1);
  display.setTextColor(color);
  display.setCursor(4, DRAW_STATUS_Y + 3);
  if (msg != nullptr) {
    display.print(msg);
  } else {
    char buf[48];
    snprintf(buf, sizeof(buf), "Linie %d/%d  Punkty %d/%d", strokeCount, DRAW_MAX_STROKES, pointsUsed, DRAW_MAX_POINTS);
    display.print(buf);
  }
}

static void drawCounterStatus() {
  drawStatus(nullptr, TFT_DARKGREY);
}

// kreska ok. DRAW_PEN_WIDTH px: glowna linia + dwie przesuniete o 1 px + kropka na koncu
// (punkty sa przycinane do obszaru z marginesem 1 px, wiec nie wychodzi poza obszar)
static void drawSegment(const DrawPoint& a, const DrawPoint& b, uint16_t color) {
  int ax = DRAW_AREA_X + a.x, ay = DRAW_AREA_Y + a.y;
  int bx = DRAW_AREA_X + b.x, by = DRAW_AREA_Y + b.y;
  display.drawLine(ax, ay, bx, by, color);
  display.drawLine(ax + 1, ay, bx + 1, by, color);
  display.drawLine(ax, ay + 1, bx, by + 1, color);
  display.fillCircle(bx, by, 1, color);
}

static void drawStroke(const DrawStroke& s) {
  uint16_t color = colorOf(s.color);
  for (uint16_t i = 1; i < s.count; i++) {
    drawSegment(points[s.start + i - 1], points[s.start + i], color);
  }
}

static void redrawCanvas() {
  display.fillRect(DRAW_AREA_X, DRAW_AREA_Y, DRAW_AREA_W, DRAW_AREA_H, TFT_WHITE);
  for (uint8_t i = 0; i < strokeCount; i++) drawStroke(strokes[i]);
}

// ---- logika linii ----

static void resetDrawing() {
  strokeCount = 0;
  redoCount = 0;
  pointsUsed = 0;
  penDown = false;
  curCount = 0;
  limitReported = false;
  discardArmed = false;
}

static void reportLimit(const char* msg) {
  if (limitReported) return;
  limitReported = true;
  buzzError();
  drawStatus(msg, COLOR_DANGER);
}

// zatwierdza biezaca linie (po uniesieniu palca albo gdy palec wjechal na pasek narzedzi)
static void finishStroke() {
  if (!penDown) return;
  penDown = false;
  if (curCount == 1) {
    // stuk bez ruchu = kropka; serwer wymaga >= 2 punktow w linii, wiec powielamy punkt
    // (miejsce jest zagwarantowane - patrz sprawdzenie przy starcie linii)
    points[pointsUsed + 1] = points[pointsUsed];
    curCount = 2;
  }
  strokes[strokeCount] = { pointsUsed, curCount, selectedColor };
  strokeCount++;
  pointsUsed += curCount;
  curCount = 0;
  drawToolbar();         // Cofnij staje sie aktywne
  if (!limitReported) drawCounterStatus();
}

static void addPoint(int32_t x, int32_t y) {
  int16_t lx = constrain(x - DRAW_AREA_X, 1, DRAW_AREA_W - 2);
  int16_t ly = constrain(y - DRAW_AREA_Y, 1, DRAW_AREA_H - 2);

  if (!penDown) {
    if (strokeCount >= DRAW_MAX_STROKES) {
      reportLimit("Limit linii - zapisz lub cofnij");
      return;
    }
    // miejsce na min. 2 punkty (kropka z jednego dotkniecia)
    if (pointsUsed + 2 > DRAW_MAX_POINTS) {
      reportLimit("Limit punktow - zapisz lub cofnij");
      return;
    }
    penDown = true;
    curCount = 0;
    redoCount = 0; // nowa linia kasuje stos Przywroc (jego dane za pointsUsed zostana nadpisane)
    discardArmed = false;
    limitReported = false;
    drawToolbar(); // Przywroc staje sie nieaktywne
    drawCounterStatus();
  }

  uint16_t idx = pointsUsed + curCount;
  if (curCount > 0) {
    const DrawPoint& last = points[idx - 1];
    int dx = lx - last.x, dy = ly - last.y;
    if (dx * dx + dy * dy < DRAW_MIN_POINT_DIST * DRAW_MIN_POINT_DIST) return; // probkowanie
  }
  if (idx >= DRAW_MAX_POINTS) {
    reportLimit("Limit punktow - zapisz rysunek");
    return;
  }

  points[idx] = { lx, ly };
  drawSegment(curCount > 0 ? points[idx - 1] : points[idx], points[idx], colorOf(selectedColor));
  curCount++;
}

static void undoStroke() {
  if (strokeCount == 0) return;
  strokeCount--;
  redoCount++;
  pointsUsed = strokeCount > 0 ? strokes[strokeCount - 1].start + strokes[strokeCount - 1].count : 0;
  redrawCanvas();
}

static void redoStroke() {
  if (redoCount == 0) return;
  strokeCount++;
  redoCount--;
  pointsUsed = strokes[strokeCount - 1].start + strokes[strokeCount - 1].count;
  drawStroke(strokes[strokeCount - 1]);
}

// ---- serializacja i wysylka ----

// Cialo POST: {"strokesJson":"<strokesJson jako string - cudzyslowy z \>"}. Pisane strumieniowo przez
// put() - raz do zliczenia dlugosci (reserve), raz do wypelnienia String; bez JsonDocument.
#define Q "\\\""
template <typename Put>
static void emitBody(Put put) {
  char tmp[64];
  put("{\"strokesJson\":\"{" Q "strokes" Q ":[");
  for (uint8_t s = 0; s < strokeCount; s++) {
    const DrawStroke& st = strokes[s];
    const PaletteColor& c = PALETTE[st.color];
    snprintf(tmp, sizeof(tmp), "%s{" Q "color" Q ":" Q "#%02x%02x%02x" Q "," Q "width" Q ":%d," Q "points" Q ":[",
             s > 0 ? "," : "", c.r, c.g, c.b, DRAW_PEN_WIDTH);
    put(tmp);
    for (uint16_t i = 0; i < st.count; i++) {
      const DrawPoint& p = points[st.start + i];
      snprintf(tmp, sizeof(tmp), "%s{" Q "x" Q ":%d," Q "y" Q ":%d}", i > 0 ? "," : "", p.x, p.y);
      put(tmp);
    }
    put("]}");
  }
  snprintf(tmp, sizeof(tmp), "]," Q "canvasWidth" Q ":%d," Q "canvasHeight" Q ":%d}\"}", DRAW_AREA_W, DRAW_AREA_H);
  put(tmp);
}
#undef Q

static const size_t BODY_PREFIX_LEN = 16; // {"strokesJson":"
static const size_t BODY_SUFFIX_LEN = 2;  // "}

static bool buildBody(String& body) {
  size_t total = 0;
  emitBody([&](const char* s) { total += strlen(s); });
  if (!body.reserve(total)) return false;
  emitBody([&](const char* s) { body += s; });
  return body.length() == total;
}

// jedno krotkie zadanie POST, bez ponowien; true gdy status 2xx. Odpowiedz (echo rysunku, do ok. 20 KB)
// celowo nie jest czytana do pamieci - lista rysunkow dostaje wpis zbudowany z lokalnych danych.
static bool postDrawing(const String& noteId, const String& body) {
  String path = "/api/device-notes/" + noteId + "/drawings";

  WiFiClientSecure secureClient; // jeden naraz - niszczony po wyjsciu z funkcji
  secureClient.setInsecure();
  HTTPClient http;
  http.setConnectTimeout(DRAW_POST_TIMEOUT_MS);
  http.setTimeout(DRAW_POST_TIMEOUT_MS);
  if (isApiHostRawIp(apiHost)) {
    http.begin("http://" + apiHost + path);
  } else {
    http.begin(secureClient, "https://" + apiHost + path);
  }
  http.addHeader("X-Api-Key", apiKey);
  http.addHeader("Content-Type", "application/json");
  int status = http.POST((uint8_t*)body.c_str(), body.length()); // bez kopii body
  http.end();

  Serial.printf("POST drawing len=%d -> status=%d\n", (int)body.length(), status);
  return status >= 200 && status < 300;
}

// dopisuje zapisany rysunek do detailDoc["drawings"] (strokesJson odzyskany z ciala POST przez zdjecie
// prefiksu/sufiksu i znakow ucieczki przed cudzyslowami) i zwraca jego indeks
static int appendToDetailDoc(String& body) {
  char* buf = (char*)body.c_str();
  size_t len = body.length();
  size_t w = 0;
  for (size_t r = BODY_PREFIX_LEN; r + BODY_SUFFIX_LEN < len; r++) {
    if (buf[r] == '\\' && r + 1 < len - BODY_SUFFIX_LEN) r++; // w tresci wystepuje tylko \"
    buf[w++] = buf[r];
  }
  buf[w] = '\0';
  body.remove(w);

  if (!detailDoc["drawings"].is<JsonArray>()) detailDoc["drawings"].to<JsonArray>();
  JsonArray arr = detailDoc["drawings"].as<JsonArray>();
  JsonObject entry = arr.add<JsonObject>();
  entry["strokesJson"] = body;
  entry["sortOrder"] = (int)arr.size() - 1;
  return (int)arr.size() - 1;
}

static void saveDrawing() {
  finishStroke();
  if (strokeCount == 0) {
    drawStatus("Narysuj cos przed zapisem", COLOR_DANGER);
    return;
  }

  String noteId = String((const char*)(detailDoc["id"] | ""));
  String body;
  if (noteId.length() == 0 || !buildBody(body)) {
    buzzError();
    drawStatus("Blad zapisu - sprobuj ponownie", COLOR_DANGER);
    return;
  }

  drawStatus("Zapis...", TFT_DARKGREY);
  if (!postDrawing(noteId, body)) {
    // rysunek zostaje w buforach - uzytkownik moze dotknac Zapisz ponownie
    buzzError();
    drawStatus("Blad zapisu - sprobuj ponownie", COLOR_DANGER);
    return;
  }

  int newIndex = appendToDetailDoc(body);
  body = String(); // zwolnij bufor przed rysowaniem listy

  // lista notatek ma ikone [R] z GET /lite - odswiez lokalnie, zeby byla spojna bez ponownego pobierania
  for (int i = 0; i < notesCount; i++) {
    if (notesList[i].id == noteId) notesList[i].hasDrawing = true;
  }

  detailDrawingIndex = newIndex;
  renderDrawingScreen(detailDrawingIndex);
}

// ---- wejscie / dotyk ----

void openDrawingEditor() {
  resetDrawing();
  selectedColor = 0;
  currentScreen = SCREEN_DRAW_EDIT;
  display.fillScreen(TFT_WHITE);
  drawToolbar();
  drawCounterStatus();
}

static void handleToolbarTap(int32_t x, int32_t y) {
  if (millis() - lastButtonMs < DRAW_BTN_DEBOUNCE_MS) return;

  if (y < DRAW_ROW_H) {
    if (x < DRAW_CANCEL_BTN_W) {
      lastButtonMs = millis();
      if (strokeCount > 0 && !discardArmed) {
        discardArmed = true;
        drawStatus("Anuluj jeszcze raz - odrzuci rysunek", COLOR_DANGER);
        return;
      }
      resetDrawing();
      int count = detailDoc["drawings"].as<JsonArray>().size();
      detailDrawingIndex = constrain(detailDrawingIndex, 0, max(count - 1, 0));
      renderDrawingScreen(detailDrawingIndex);
      return;
    }
    if (x >= DRAW_SAVE_BTN_X) {
      lastButtonMs = millis();
      discardArmed = false;
      saveDrawing();
      return;
    }
    for (int i = 0; i < DRAW_COLOR_COUNT; i++) {
      int cx = DRAW_COLOR_X0 + i * DRAW_COLOR_PITCH;
      if (x >= cx && x < cx + DRAW_COLOR_PITCH) {
        lastButtonMs = millis();
        selectedColor = i;
        drawToolbar();
        return;
      }
    }
    return;
  }

  // wiersz 2
  if (x < DRAW_REDO_BTN_X) {
    lastButtonMs = millis();
    discardArmed = false;
    undoStroke();
  } else if (x < DRAW_CLEAR_BTN_X) {
    lastButtonMs = millis();
    discardArmed = false;
    redoStroke();
  } else {
    lastButtonMs = millis();
    discardArmed = false;
    resetDrawing();
    redrawCanvas();
  }
  drawToolbar();
  drawCounterStatus();
}

void drawEditTouch(int32_t x, int32_t y, bool freshTouch) {
  lastPenMs = millis();

  if (y >= DRAW_AREA_Y) {
    if (y < DRAW_STATUS_Y) addPoint(x, y);
    return; // pasek statusu nic nie robi
  }

  // pasek narzedzi: palec wjechal na przyciski - zakoncz linie; przyciski reaguja tylko na swiezy dotyk
  finishStroke();
  if (freshTouch) handleToolbarTap(x, y);
}

void drawEditPenUpTick() {
  if (millis() - lastPenMs < DRAW_PEN_UP_MS) return;
  finishStroke();
  limitReported = false; // palec uniesiony - kolejny dotyk moze znowu zglosic limit
}
