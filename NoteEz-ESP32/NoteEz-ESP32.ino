#define LGFX_USE_V1
#include <LovyanGFX.hpp>
#include <WiFiManager.h>   // https://github.com/tzapu/WiFiManager
#include <HTTPClient.h>
#include <ArduinoJson.h>   // https://arduinojson.org
#include <Preferences.h>
#include <vector>

class LGFX : public lgfx::LGFX_Device {
  lgfx::Panel_ST7789 _panel_instance;
  lgfx::Bus_SPI       _bus_instance;
  lgfx::Touch_XPT2046 _touch_instance;

public:
  LGFX(void) {
    { auto cfg = _bus_instance.config();
      cfg.spi_host = SPI2_HOST;
      cfg.spi_mode = 0;
      cfg.freq_write = 40000000;
      cfg.freq_read  = 16000000;
      cfg.pin_sclk = 6;
      cfg.pin_mosi = 7;
      cfg.pin_miso = 2;
      cfg.pin_dc   = 19;
      _bus_instance.config(cfg);
      _panel_instance.setBus(&_bus_instance);
    }
    { auto cfg = _panel_instance.config();
      cfg.pin_cs   = 18;
      cfg.pin_rst  = 20;
      cfg.panel_width  = 240;
      cfg.panel_height = 320;
      _panel_instance.config(cfg);
    }
    { auto cfg = _touch_instance.config();
      cfg.x_min = 0; cfg.x_max = 4095;
      cfg.y_min = 0; cfg.y_max = 4095;
      cfg.pin_cs = 21;
      cfg.pin_int = -1;
      cfg.spi_host = SPI2_HOST;
      cfg.freq = 2500000;
      cfg.bus_shared = true;
      _touch_instance.config(cfg);
      _panel_instance.setTouch(&_touch_instance);
    }
    setPanel(&_panel_instance);
  }
};

LGFX display;
WiFiManager wm;
Preferences prefs;

// ---- konfiguracja zapisywana w NVS (Preferences) ----
String apiKey;      // klucz API urządzenia, pusty dopóki nie sparowane
String apiHost;      // np. "192.168.100.168:8080" - adres backendu w sieci lokalnej

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



WiFiManagerParameter* pairingCodeParam = nullptr;
WiFiManagerParameter* apiHostParam = nullptr;

// ---- lista notatek (GET /api/device-notes/lite) ----
struct NoteLite {
  String id;
  String title;
  bool hasDrawing;
  bool hasAudio;
};

#define MAX_NOTES 20
NoteLite notesList[MAX_NOTES];
int notesCount = 0;

#define LIST_START_Y 44
#define LIST_ROW_HEIGHT 44
#define LIST_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla listy
int listScrollRow = 0; // indeks pierwszej widocznej notatki na liscie

// ---- ekran szczegółów notatki ----
enum Screen { SCREEN_LIST, SCREEN_DETAIL, SCREEN_DRAWING };
Screen currentScreen = SCREEN_LIST;

// jedna zawinieta linia tresci notatki, z minimalnym formatowaniem jakie potrafi wyswietlic ESP32
struct DetailLine {
  String text;
  uint16_t color;
  bool bold; // "pogrubienie" robione tanim trikiem: dwukrotny wydruk przesuniety o 1px
};

String detailTitle;
bool detailHasDrawing = false;
bool detailHasAudio = false;
std::vector<DetailLine> detailLines;
int detailScrollLine = 0;

// pelny JSON ostatnio wczytanej notatki - trzymany zeby ekran rysunku mogl czytac drawings[]
// bez ponownego zapytania do serwera
JsonDocument detailDoc;
int detailDrawingIndex = 0;

#define DETAIL_CONTENT_TOP 40
#define DETAIL_LINE_HEIGHT 14
#define DETAIL_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla

// ---- proste komunikaty na ekranie ----
void showMessage(const char* line1, const char* line2 = "", uint32_t color = TFT_BLACK) {
  display.fillScreen(TFT_WHITE);
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

// callback wywoływany, gdy WiFiManager wchodzi w tryb konfiguracji (AP)
void configModeCallback(WiFiManager* myWiFiManager) {
  display.fillScreen(TFT_BLACK);
  display.setTextColor(TFT_WHITE);
  display.setTextSize(2);
  display.setCursor(10, 20);
  display.println("KONFIGURACJA WIFI");
  display.setTextSize(1);
  display.setCursor(10, 60);
  display.println("Polacz sie z siecia:");
  display.setTextSize(2);
  display.setCursor(10, 75);
  display.println(myWiFiManager->getConfigPortalSSID());
  display.setTextSize(1);
  display.setCursor(10, 110);
  display.println("Nastepnie otworz w przegladarce:");
  display.setCursor(10, 125);
  display.println("192.168.4.1");
  display.setCursor(10, 145);
  display.println("i wypelnij WiFi + kod parowania");
  display.setCursor(10, 160);
  display.println("(kod wygenerujesz w apce Vue,");
  display.setCursor(10, 172);
  display.println("zakladka Urzadzenia)");
}

// ---- parowanie: wysyla kod z formularza WiFiManagera do /api/devices/claim ----
bool claimDevice(const String& code) {
  if (code.length() == 0) return false;

  showMessage("Parowanie...", code.c_str());

  String url = "http://" + apiHost + "/api/devices/claim";
  Serial.println("claim URL: " + url);
  Serial.printf("WiFi status=%d mode=%d ip=%s\n", WiFi.status(), WiFi.getMode(), WiFi.localIP().toString().c_str());

  JsonDocument reqDoc;
  reqDoc["code"] = code;
  reqDoc["deviceName"] = "ESP32-" + WiFi.macAddress();
  String body;
  serializeJson(reqDoc, body);

  int status = -1;
  String response;

  // pierwsza proba tuz po polaczeniu z WiFi czasem konczy sie odmowa polaczenia
  // (stos IP jeszcze sie nie ustabilizowal) - probujemy ponownie zanim zglosimy blad
  for (int attempt = 0; attempt < 3 && status <= 0; attempt++) {
    if (attempt > 0) {
      Serial.printf("claim retry %d (poprzedni status=%d)\n", attempt, status);
      delay(1000);
    }

    HTTPClient http;
    http.setConnectTimeout(5000);
    http.begin(url);
    http.addHeader("Content-Type", "application/json");

    status = http.POST(body);
    response = http.getString();
    http.end();
  }

  if (status != 200) {
    showMessage("Blad parowania", response.c_str(), TFT_RED);
    Serial.printf("claim status=%d body=%s\n", status, response.c_str());
    delay(3000);
    return false;
  }

  JsonDocument resDoc;
  if (deserializeJson(resDoc, response) != DeserializationError::Ok) {
    showMessage("Blad parowania", "Nieprawidlowa odpowiedz serwera", TFT_RED);
    delay(3000);
    return false;
  }

  apiKey = resDoc["apiKey"].as<String>();
  prefs.putString("apiKey", apiKey);
  prefs.putString("apiHost", apiHost);

  showMessage("Sparowano!", "", TFT_DARKGREEN);
  delay(1500);
  return true;
}

// ---- kasuje WiFi + parowanie i wraca do portalu konfiguracyjnego ----
void resetWifiAndPairing() {
  showMessage("Resetowanie...", "Kasuje WiFi i parowanie", TFT_RED);
  delay(1000);

  prefs.remove("apiKey");
  prefs.remove("apiHost");
  wm.resetSettings(); // kasuje zapisane dane WiFi (NVS uzywane przez WiFiManager)

  delay(500);
  ESP.restart();
}

void drawTopButtons() {
  display.fillRect(REFRESH_BTN_X, REFRESH_BTN_Y, REFRESH_BTN_W, REFRESH_BTN_H, TFT_DARKGREEN);
  display.setTextColor(TFT_WHITE);
  display.setTextSize(1);
  display.setCursor(REFRESH_BTN_X + 6, REFRESH_BTN_Y + 12);
  display.print("Odswiez");

  display.fillRect(RESET_BTN_X, RESET_BTN_Y, RESET_BTN_W, RESET_BTN_H, TFT_RED);
  display.setCursor(RESET_BTN_X + 6, RESET_BTN_Y + 12);
  display.print("Reset");
}

bool pointInRect(int32_t x, int32_t y, int rx, int ry, int rw, int rh) {
  return x >= rx && x <= rx + rw && y >= ry && y <= ry + rh;
}

// przyciski przewijania (dol ekranu) - wspolne dla listy notatek i widoku szczegolow
void drawScrollButtons(bool canUp, bool canDown) {
  display.fillRect(SCROLL_UP_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H, canUp ? TFT_DARKGREEN : TFT_LIGHTGREY);
  display.setTextColor(TFT_WHITE);
  display.setTextSize(1);
  display.setCursor(SCROLL_UP_BTN_X + 14, SCROLL_BTN_Y + 11);
  display.print("Gora");

  display.fillRect(SCROLL_DOWN_BTN_X, SCROLL_BTN_Y, SCROLL_BTN_W, SCROLL_BTN_H, canDown ? TFT_DARKGREEN : TFT_LIGHTGREY);
  display.setCursor(SCROLL_DOWN_BTN_X + 16, SCROLL_BTN_Y + 11);
  display.print("Dol");
}

// usuwa przypadkowo wpisany prefiks "http(s)://" oraz koncowy "/" z pola adresu serwera,
// zeby nie zbudowac zdublowanego URL typu "http://http://..." (przyczyna bledu HTTPC -1)
String normalizeHost(String host) {
  host.trim();
  host.replace("http://", "");
  host.replace("https://", "");
  while (host.endsWith("/")) host.remove(host.length() - 1);
  return host;
}

// zamienia kolor tekstu "#rrggbb" (z edytora TipTap) na kolor RGB565 uzywany przez LovyanGFX
uint16_t hexToColor565(const String& hex) {
  if (hex.length() < 7 || hex[0] != '#') return TFT_BLACK;
  long rgb = strtol(hex.c_str() + 1, nullptr, 16);
  uint8_t r = (rgb >> 16) & 0xFF;
  uint8_t g = (rgb >> 8) & 0xFF;
  uint8_t b = rgb & 0xFF;
  return display.color565(r, g, b);
}

int listRowsPerPage() {
  return (LIST_CONTENT_BOTTOM - LIST_START_Y) / LIST_ROW_HEIGHT;
}

int listMaxScroll() {
  int maxScroll = notesCount - listRowsPerPage();
  return maxScroll > 0 ? maxScroll : 0;
}

void scrollList(int deltaRows) {
  int maxScroll = listMaxScroll();
  int newScroll = listScrollRow + deltaRows;
  if (newScroll < 0) newScroll = 0;
  if (newScroll > maxScroll) newScroll = maxScroll;
  if (newScroll == listScrollRow) return;

  listScrollRow = newScroll;
  renderNotesList();
}

// ---- pobiera liste notatek (GET /api/device-notes/lite) i renderuje ekran listy ----
void renderNotesList() {
  currentScreen = SCREEN_LIST;
  display.fillScreen(TFT_WHITE);
  display.setTextColor(TFT_BLACK);
  display.setTextSize(2);
  display.setCursor(70, 6);
  display.println("NOTATNIK");

  drawTopButtons();
  display.setTextSize(1);

  if (apiKey.length() == 0) {
    display.setCursor(10, 50);
    display.println("Urzadzenie niesparowane.");
    return;
  }

  if (notesCount == 0) {
    display.setTextColor(TFT_BLACK);
    display.setCursor(10, 50);
    display.println("Brak notatek.");
    return;
  }

  int perPage = listRowsPerPage();
  int rows = min(perPage, notesCount - listScrollRow);
  int y = LIST_START_Y;

  for (int i = 0; i < rows; i++) {
    int idx = listScrollRow + i;
    int cardH = LIST_ROW_HEIGHT - 6;

    // ramka wokol notatki, zeby bylo widac gdzie kliknac
    display.drawRoundRect(6, y, display.width() - 12, cardH, 5, TFT_DARKGREY);

    display.setTextColor(TFT_BLACK);
    display.setTextSize(2);
    display.setCursor(16, y + cardH / 2 - 8);
    String shownTitle = notesList[idx].title;
    if (shownTitle.length() > 18) shownTitle = shownTitle.substring(0, 17) + "..";
    display.print(shownTitle);

    String badges = "";
    if (notesList[idx].hasDrawing) badges += "[R]";
    if (notesList[idx].hasAudio) badges += "[A]";
    if (badges.length() > 0) {
      display.setTextSize(1);
      display.setTextColor(TFT_DARKGREEN);
      display.setCursor(display.width() - 16 - badges.length() * 6, y + cardH / 2 - 4);
      display.print(badges);
    }

    y += LIST_ROW_HEIGHT;
  }

  bool canUp = listScrollRow > 0;
  bool canDown = listScrollRow < listMaxScroll();
  drawScrollButtons(canUp, canDown);
}

void fetchNotesLite() {
  notesCount = 0;
  listScrollRow = 0;

  if (apiKey.length() == 0) {
    renderNotesList();
    return;
  }

  HTTPClient http;
  http.begin("http://" + apiHost + "/api/device-notes/lite");
  http.addHeader("X-Api-Key", apiKey);
  int status = http.GET();
  String response = http.getString();
  http.end();

  if (status != 200) {
    Serial.printf("device-notes/lite status=%d body=%s\n", status, response.c_str());
    renderNotesList();
    display.setTextColor(TFT_RED);
    display.setCursor(10, 50);
    display.printf("Blad pobierania (HTTP %d)", status);
    return;
  }

  JsonDocument doc;
  if (deserializeJson(doc, response) != DeserializationError::Ok) {
    renderNotesList();
    display.setTextColor(TFT_RED);
    display.setCursor(10, 50);
    display.println("Blad odczytu listy notatek");
    return;
  }

  JsonArray notes = doc.as<JsonArray>();
  for (JsonObject note : notes) {
    if (notesCount >= MAX_NOTES) break;
    notesList[notesCount].id = note["id"].as<String>();
    notesList[notesCount].title = String((const char*)(note["title"] | "(bez tytulu)"));
    notesList[notesCount].hasDrawing = note["hasDrawing"] | false;
    notesList[notesCount].hasAudio = note["hasAudio"] | false;
    notesCount++;
  }

  renderNotesList();
}

// ---- dzieli tekst na linie mieszczace sie w szerokosci ekranu (zawijanie po slowach) ----
std::vector<String> wrapText(const String& text, int charsPerLine) {
  std::vector<String> lines;
  int len = text.length();
  int pStart = 0;

  while (pStart <= len) {
    int nl = text.indexOf('\n', pStart);
    String paragraph = (nl == -1) ? text.substring(pStart) : text.substring(pStart, nl);

    if (paragraph.length() == 0) {
      lines.push_back("");
    } else {
      int i = 0;
      int plen = paragraph.length();
      while (i < plen) {
        int remaining = plen - i;
        int take = min(charsPerLine, remaining);
        if (take < remaining) {
          int breakAt = paragraph.lastIndexOf(' ', i + take - 1);
          if (breakAt > i) take = breakAt - i;
        }
        String chunk = paragraph.substring(i, i + take);
        chunk.trim();
        lines.push_back(chunk);
        i += take;
        while (i < plen && paragraph[i] == ' ') i++;
      }
    }

    if (nl == -1) break;
    pStart = nl + 1;
  }

  if (lines.empty()) lines.push_back("");
  return lines;
}

int detailLinesPerPage() {
  return (DETAIL_CONTENT_BOTTOM - DETAIL_CONTENT_TOP) / DETAIL_LINE_HEIGHT;
}

int detailMaxScroll() {
  int perPage = detailLinesPerPage();
  int maxScroll = (int)detailLines.size() - perPage;
  return maxScroll > 0 ? maxScroll : 0;
}

void drawDetailChrome() {
  display.fillScreen(TFT_WHITE);

  // naglowek z przyciskiem powrotu
  display.fillRect(BACK_BTN_X, BACK_BTN_Y, BACK_BTN_W, BACK_BTN_H, TFT_DARKGREEN);
  display.setTextColor(TFT_WHITE);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 6, BACK_BTN_Y + 12);
  display.print("< Wstecz");

  display.setTextColor(TFT_BLACK);
  display.setTextSize(2);
  display.setCursor(BACK_BTN_W + 10, 6);
  String shownTitle = detailTitle;
  if (shownTitle.length() > 12) shownTitle = shownTitle.substring(0, 11) + "..";
  display.println(shownTitle);

  if (detailHasDrawing) {
    display.fillRect(DRAWING_BTN_X, DRAWING_BTN_Y, DRAWING_BTN_W, DRAWING_BTN_H, TFT_DARKGREEN);
    display.setTextColor(TFT_WHITE);
    display.setTextSize(1);
    display.setCursor(DRAWING_BTN_X + 3, DRAWING_BTN_Y + 12);
    display.print("Rys.");
  }

  drawScrollButtons(detailScrollLine > 0, detailScrollLine < detailMaxScroll());
}

void drawDetailContent() {
  // czysci tylko obszar tresci, zeby nie przerysowywac naglowka/przyciskow przy scrollu
  display.fillRect(0, DETAIL_CONTENT_TOP, display.width(), DETAIL_CONTENT_BOTTOM - DETAIL_CONTENT_TOP, TFT_WHITE);

  display.setTextSize(1);

  int perPage = detailLinesPerPage();
  int y = DETAIL_CONTENT_TOP;

  for (int i = 0; i < perPage; i++) {
    int lineIdx = detailScrollLine + i;
    if (lineIdx >= (int)detailLines.size()) break;

    const DetailLine& line = detailLines[lineIdx];
    display.setTextColor(line.color);
    display.setCursor(10, y);
    display.print(line.text);
    if (line.bold) {
      // brak fontu pogrubionego w bibliotece - tani trik: drugi wydruk przesuniety o 1px
      display.setCursor(11, y);
      display.print(line.text);
    }
    y += DETAIL_LINE_HEIGHT;
  }
}

void renderNoteDetail() {
  currentScreen = SCREEN_DETAIL;
  drawDetailChrome();
  drawDetailContent();
}

void scrollDetail(int deltaLines) {
  int maxScroll = detailMaxScroll();
  int newScroll = detailScrollLine + deltaLines;
  if (newScroll < 0) newScroll = 0;
  if (newScroll > maxScroll) newScroll = maxScroll;
  if (newScroll == detailScrollLine) return;

  detailScrollLine = newScroll;
  drawDetailContent();
  // przyciski gora/dol moga zmienic kolor (aktywny/nieaktywny), wiec odswiez tez naglowek
  drawDetailChrome();
  drawDetailContent();
}

// ---- pobiera pelna tresc notatki (GET /api/device-notes/{id}) i pokazuje ekran szczegolow ----
void selectNote(int index) {
  if (index < 0 || index >= notesCount) return;

  showMessage("Wczytywanie...", notesList[index].title.c_str());

  HTTPClient http;
  http.begin("http://" + apiHost + "/api/device-notes/" + notesList[index].id);
  http.addHeader("X-Api-Key", apiKey);
  int status = http.GET();
  String response = http.getString();
  http.end();

  if (status != 200) {
    Serial.printf("device-notes/{id} status=%d body=%s\n", status, response.c_str());
    showMessage("Blad wczytywania", "Sprobuj ponownie", TFT_RED);
    delay(2000);
    renderNotesList();
    return;
  }

  detailDoc.clear();
  if (deserializeJson(detailDoc, response) != DeserializationError::Ok) {
    showMessage("Blad wczytywania", "Nieprawidlowa odpowiedz", TFT_RED);
    delay(2000);
    renderNotesList();
    return;
  }

  detailTitle = String((const char*)(detailDoc["title"] | "(bez tytulu)"));
  detailHasDrawing = detailDoc["drawings"].as<JsonArray>().size() > 0;
  detailHasAudio = detailDoc["audioClips"].as<JsonArray>().size() > 0;
  detailDrawingIndex = 0;

  // przy tekscie 1 (font ~6px szerokosci znaku) i marginesie 10px z kazdej strony
  int charsPerLine = (display.width() - 20) / 6;
  detailLines.clear();

  JsonArray blocksArr = detailDoc["textBlocks"].as<JsonArray>();
  for (JsonObject block : blocksArr) {
    String text = String((const char*)(block["text"] | ""));
    if (text.length() == 0) continue;

    String type = String((const char*)(block["type"] | "paragraph"));
    bool bold = block["bold"] | false;

    uint16_t color = TFT_BLACK;
    if (!block["color"].isNull()) {
      color = hexToColor565(String((const char*)block["color"]));
    } else if (type == "blockquote") {
      color = TFT_DARKGREY;
    } else if (type == "codeBlock") {
      color = TFT_NAVY;
    }

    String prefix = "";
    if (type == "taskItem") {
      bool checked = block["checked"] | false;
      prefix = checked ? "[x] " : "[ ] ";
    }

    std::vector<String> wrapped = wrapText(prefix + text, charsPerLine);
    for (auto& w : wrapped) {
      detailLines.push_back({ w, color, bold });
    }
  }

  if (detailLines.empty()) {
    detailLines.push_back({ "(notatka nie zawiera tekstu)", TFT_BLACK, false });
  }
  detailScrollLine = 0;

  renderNoteDetail();
}

// ---- ekran podgladu rysunku (wektorowego) przypietego do notatki ----
// rysunki sa przechowywane jako lista pociagniec (linie miedzy punktami), nie jako bitmapy,
// wiec renderujemy je wprost jako polaczone odcinki przeskalowane pod rozmiar ekranu
void renderDrawingScreen(int index) {
  currentScreen = SCREEN_DRAWING;
  display.fillScreen(TFT_WHITE);

  display.fillRect(BACK_BTN_X, BACK_BTN_Y, BACK_BTN_W, BACK_BTN_H, TFT_DARKGREEN);
  display.setTextColor(TFT_WHITE);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 6, BACK_BTN_Y + 12);
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

void setup() {
  Serial.begin(115200);

  display.init();
  display.setRotation(1);
  display.fillScreen(TFT_BLACK);

  // kalibracja dotyku (jak w oryginalnym szkicu)
  uint16_t calData[8] = {3826, 308, 3886, 3793, 361, 252, 359, 3741};
  display.setTouchCalibrate(calData);

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
    showMessage("Brak polaczenia", "Restart za 3s...", TFT_RED);
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

  showMessage("Polaczono!", WiFi.localIP().toString().c_str(), TFT_DARKGREEN);
  delay(1000);

  // jesli w portalu wpisano kod parowania - sparuj urzadzenie zanim wejdziemy do glownego ekranu
  String enteredCode = String(pairingCodeParam->getValue());
  if (enteredCode.length() > 0) {
    claimDevice(enteredCode);
  } else if (apiKey.length() == 0) {
    showMessage("Brak parowania", "Uzyj portalu WiFi, by wpisac kod", TFT_RED);
    delay(3000);
  }

  fetchNotesLite();
}

void loop() {
  int32_t x, y;
  static uint32_t resetTouchStart = 0;
  static uint32_t lastScrollTap = 0;

  bool touched = display.getTouch(&x, &y);

  if (!touched) {
    resetTouchStart = 0;
    delay(20);
    return;
  }

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
