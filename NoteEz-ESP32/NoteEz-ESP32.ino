#define LGFX_USE_V1
#include <LovyanGFX.hpp>
#include <WiFiManager.h>   // https://github.com/tzapu/WiFiManager
#include <HTTPClient.h>
#include <ArduinoJson.h>   // https://arduinojson.org
#include <Preferences.h>
#include <vector>
#include <esp_sleep.h>
#include <driver/gpio.h>

// ---- usypianie (deep sleep) po bezczynnosci ----
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

// ---- kolorystyka dopasowana do motywu strony (NoteEz-Frontend/src/assets/main.css) ----
// UWAGA: musza to byc stale typu uint16_t (nie #define/int), bo LovyanGFX przeciażza
// funkcje rysujace wg typu argumentu - "int" trafia w przeciażenie dla surowego RGB888
// i przekopakowuje juz spakowane bity 565, co psuje kolory (czerwony wychodzi zielony itp.)
constexpr uint16_t RGB565(uint8_t r, uint8_t g, uint8_t b) {
  return (uint16_t)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
}

const uint16_t COLOR_BG          = RGB565(0, 5, 1);       // --color-bg / --black
const uint16_t COLOR_SURFACE     = RGB565(12, 21, 18);    // --color-surface
const uint16_t COLOR_BORDER      = RGB565(30, 47, 40);    // --color-border
const uint16_t COLOR_TEXT        = RGB565(234, 245, 240); // --color-text
const uint16_t COLOR_TEXT_MUTED  = RGB565(147, 172, 162); // --color-text-muted
const uint16_t COLOR_PRIMARY     = RGB565(121, 199, 197); // --color-primary / pearl-aqua
const uint16_t COLOR_SECONDARY   = RGB565(115, 171, 132); // --color-secondary / muted-teal
const uint16_t COLOR_ACCENT_MINT = RGB565(153, 209, 156); // --color-accent-mint / celadon
const uint16_t COLOR_DANGER      = RGB565(224, 118, 110); // --color-danger
const uint16_t COLOR_ON_ACCENT   = COLOR_BG;              // --color-on-accent (ciemny tekst na jasnym przycisku)
const uint16_t COLOR_RESET       = RGB565(235, 5, 5);     // czerwony przycisk resetu WiFi
const uint16_t COLOR_BRIGHT_TEXT = RGB565(255, 255, 255); // jasniejszy naglowek "NOTATNIK" / tytuly notatek
const uint16_t COLOR_CARD_GREY   = RGB565(52, 52, 56);    // szare tlo kafelka notatki na liscie

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
  String color; // np. "#8963ba", puste gdy notatka nie ma wybranego koloru
};

#define MAX_NOTES 20
NoteLite notesList[MAX_NOTES];
int notesCount = 0;

#define LIST_START_Y 44
#define LIST_ROW_HEIGHT 44
#define LIST_CONTENT_BOTTOM 200 // ponizej tego zaczynaja sie przyciski scrolla listy
int listScrollRow = 0; // indeks pierwszej widocznej notatki na liscie

unsigned long lastActivityMillis = 0; // czas ostatniego dotkniecia - do usypiania po bezczynnosci

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
void showMessage(const char* line1, const char* line2 = "", uint32_t color = COLOR_TEXT) {
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

// callback wywoływany, gdy WiFiManager wchodzi w tryb konfiguracji (AP)
void configModeCallback(WiFiManager* myWiFiManager) {
  display.fillScreen(COLOR_BG);
  display.setTextColor(COLOR_PRIMARY);
  display.setTextSize(2);
  display.setCursor(10, 20);
  display.println("KONFIGURACJA WIFI");
  display.setTextColor(COLOR_TEXT);
  display.setTextSize(1);
  display.setCursor(10, 60);
  display.println("Polacz sie z siecia:");
  display.setTextColor(COLOR_ACCENT_MINT);
  display.setTextSize(2);
  display.setCursor(10, 75);
  display.println(myWiFiManager->getConfigPortalSSID());
  display.setTextColor(COLOR_TEXT);
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
    showMessage("Blad parowania", response.c_str(), COLOR_DANGER);
    Serial.printf("claim status=%d body=%s\n", status, response.c_str());
    delay(3000);
    return false;
  }

  JsonDocument resDoc;
  if (deserializeJson(resDoc, response) != DeserializationError::Ok) {
    showMessage("Blad parowania", "Nieprawidlowa odpowiedz serwera", COLOR_DANGER);
    delay(3000);
    return false;
  }

  apiKey = resDoc["apiKey"].as<String>();
  prefs.putString("apiKey", apiKey);
  prefs.putString("apiHost", apiHost);

  showMessage("Sparowano!", "", COLOR_ACCENT_MINT);
  delay(1500);
  return true;
}

// ---- TYMCZASOWY TEST DIAGNOSTYCZNY: light sleep zamiast deep sleep ----
// Light sleep budzi sie z DOWOLNEGO GPIO (gpio_wakeup_enable), bez ograniczenia do domeny
// LP-IO i bez niskopoziomowych zaleznosci ktore w deep sleep mogly nie byc poprawnie
// obslugiwane przez pakiet plytek "Arduino ESP32 Boards" 2.0.18. Jesli TO zadziala,
// wiemy ze wiring/dotyk sa ok, a problem siedzi konkretnie w deep sleep na tym rdzeniu.
// Po teście podmien z powrotem wywolanie w loop() na enterDeepSleep().
void enterLightSleepTest() {
  showMessage("Usypianie (TEST light sleep)", "Dotknij ekranu, aby wybudzic", COLOR_TEXT_MUTED);
  delay(300);

  display.sleep();
  digitalWrite(BACKLIGHT_PIN, LOW);

  pinMode(TIRQ_PIN, INPUT_PULLUP);
  delay(10);
  Serial.printf("[light-sleep-test] T_IRQ (GPIO%d) stan przed usnieciem: %d\n", TIRQ_PIN, digitalRead(TIRQ_PIN));
  Serial.flush();

  // Aktywne polaczenie WiFi STA potrafi kolidowac z reczmym wejsciem w sleep (modem
  // proby utrzymania polaczenia moga przerywac/destabilizowac cykl usypiania). Wylaczamy
  // radio calkowicie na czas testu - i tak zaraz zestawiamy je od nowa po wybudzeniu.
  WiFi.disconnect(true);
  WiFi.mode(WIFI_OFF);
  delay(100);

  gpio_wakeup_enable((gpio_num_t)TIRQ_PIN, GPIO_INTR_LOW_LEVEL);
  esp_sleep_enable_gpio_wakeup();

  esp_light_sleep_start(); // W ODROZNIENIU OD deep sleep - TO WRACA po wybudzeniu, kod leci dalej

  // TYMCZASOWA DIAGNOSTYKA: krok po kroku, zeby zlokalizowac dokladnie gdzie sie wiesza.
  Serial.println("[T1] po esp_light_sleep_start()");
  Serial.flush();

  digitalWrite(BACKLIGHT_PIN, HIGH);
  Serial.println("[T2] po digitalWrite BACKLIGHT HIGH");
  Serial.flush();

  display.wakeup();
  Serial.println("[T3] po display.wakeup()");
  Serial.flush();

  WiFi.mode(WIFI_STA);
  Serial.println("[T4] po WiFi.mode(WIFI_STA)");
  Serial.flush();

  WiFi.begin(); // wraca do ostatnio zapamietanej sieci (dane trzyma WiFiManager/NVS)
  Serial.println("[T5] po WiFi.begin()");
  Serial.flush();

  lastActivityMillis = millis();
  renderNotesList();
  Serial.println("[T6] po renderNotesList() - koniec funkcji");
  Serial.flush();
}

// ---- usypia urzadzenie po minucie bezczynnosci; budzi je dotkniecie ekranu (T_IRQ na GPIO3) ----
// Deep sleep resetuje cala pamiec RAM - po wybudzeniu kod zaczyna sie od nowa od setup(),
// ktory i tak od razu laczy sie z zapisanym WiFi i odswieza liste notatek, wiec efekt dla
// uzytkownika jest taki, jakby urzadzenie po prostu "obudzilo sie" na ekranie listy.
void enterDeepSleep() {
  showMessage("Usypianie...", "Dotknij ekranu, aby wybudzic", COLOR_TEXT_MUTED);
  delay(300);

  display.sleep(); // usypia sam panel (ST7789 SLPIN)

  // Gasimy podswietlenie i "zatrzaskujemy" ten stan na czas snu (gpio_hold) - bez tego
  // pad wraca do stanu domyslnego po wejsciu w deep sleep i podswietlenie zapala sie z powrotem.
  // (ESP32-C6 nie ma oddzielnej domeny RTC GPIO jak klasyczny ESP32, wiec nie ma tu globalnego
  // przelacznika gpio_deep_sleep_hold_en/dis - samo gpio_hold_en na pinie wystarczy.)
  digitalWrite(BACKLIGHT_PIN, LOW);
  gpio_hold_en((gpio_num_t)BACKLIGHT_PIN);

  // XPT2046 IRQ jest open-drain (aktywne niskim) - INPUT_PULLUP zapewnia stan wysoki w spoczynku,
  // bez tego pin moglby "plywac" i wybudzanie byloby niewiarygodne/nie dzialaloby wcale.
  pinMode(TIRQ_PIN, INPUT_PULLUP);
  delay(10); // czas na ustalenie sie stanu linii po zmianie pinMode

  // Diagnostyka: 1 = spoczynek (dobrze), 0 = albo dotkniete w tej chwili, albo pin
  // nie ma podciagniecia i "plywa" na LOW - jesli to drugie, wybudzanie nigdy sie nie uda.
  Serial.printf("[sleep] T_IRQ (GPIO%d) stan przed usnieciem: %d\n", TIRQ_PIN, digitalRead(TIRQ_PIN));

  esp_err_t wakeErr = esp_deep_sleep_enable_gpio_wakeup(1ULL << TIRQ_PIN, ESP_GPIO_WAKEUP_GPIO_LOW);
  Serial.printf("[sleep] esp_deep_sleep_enable_gpio_wakeup zwrocilo: %d (0 = ESP_OK)\n", (int)wakeErr);
  Serial.flush();

  // Aktywne polaczenie WiFi STA tuz przed wejsciem w sleep potrafi destabilizowac sam moment
  // usypiania - czyste rozlaczenie przed esp_deep_sleep_start() jest zalecana praktyka.
  WiFi.disconnect(true);
  WiFi.mode(WIFI_OFF);
  delay(100);

  esp_deep_sleep_start(); // nie wraca - reset i ponowne setup() po wybudzeniu
}

// ---- kasuje WiFi + parowanie i wraca do portalu konfiguracyjnego ----
void resetWifiAndPairing() {
  showMessage("Resetowanie...", "Kasuje WiFi i parowanie", COLOR_DANGER);
  delay(1000);

  prefs.remove("apiKey");
  prefs.remove("apiHost");
  wm.resetSettings(); // kasuje zapisane dane WiFi (NVS uzywane przez WiFiManager)

  delay(500);
  ESP.restart();
}

void drawTopButtons() {
  display.fillRoundRect(REFRESH_BTN_X + 4, REFRESH_BTN_Y + 2, REFRESH_BTN_W - 8, REFRESH_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(REFRESH_BTN_X + 10, REFRESH_BTN_Y + 12);
  display.print("Odswiez");

  display.fillRoundRect(RESET_BTN_X + 4, RESET_BTN_Y + 2, RESET_BTN_W - 8, RESET_BTN_H - 4, 6, COLOR_RESET);
  display.setTextColor(COLOR_TEXT);
  display.setCursor(RESET_BTN_X + 10, RESET_BTN_Y + 12);
  display.print("Reset");
}

bool pointInRect(int32_t x, int32_t y, int rx, int ry, int rw, int rh) {
  return x >= rx && x <= rx + rw && y >= ry && y <= ry + rh;
}

// przyciski przewijania (dol ekranu) - wspolne dla listy notatek i widoku szczegolow
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
  display.fillScreen(COLOR_BG);
  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  display.setCursor(70, 6);
  display.println("NOTATNIK");

  drawTopButtons();
  display.setTextSize(1);

  if (apiKey.length() == 0) {
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, 50);
    display.println("Urzadzenie niesparowane.");
    return;
  }

  if (notesCount == 0) {
    display.setTextColor(COLOR_TEXT_MUTED);
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

    // karta notatki - szare tlo, z boku pasek w kolorze wybranym przez uzytkownika
    // (odpowiednik .note-accent z NoteCard.vue na stronie)
    display.fillRoundRect(6, y, display.width() - 12, cardH, 6, COLOR_CARD_GREY);
    if (notesList[idx].color.length() > 0) {
      uint16_t accent = hexToColor565(notesList[idx].color);
      display.fillRoundRect(6, y, 8, cardH, 3, accent);
    }
    display.drawRoundRect(6, y, display.width() - 12, cardH, 6, COLOR_BORDER);

    display.setTextColor(COLOR_BRIGHT_TEXT);
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
      display.setTextColor(COLOR_ACCENT_MINT);
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
    display.setTextColor(COLOR_DANGER);
    display.setCursor(10, 50);
    display.printf("Blad pobierania (HTTP %d)", status);
    return;
  }

  JsonDocument doc;
  if (deserializeJson(doc, response) != DeserializationError::Ok) {
    renderNotesList();
    display.setTextColor(COLOR_DANGER);
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
    notesList[notesCount].color = note["color"].isNull() ? "" : String((const char*)note["color"]);
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
  display.fillScreen(COLOR_BG);

  // naglowek z przyciskiem powrotu
  display.fillRoundRect(BACK_BTN_X + 4, BACK_BTN_Y + 2, BACK_BTN_W - 8, BACK_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 10, BACK_BTN_Y + 12);
  display.print("< Wstecz");

  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  display.setCursor(BACK_BTN_W + 10, 6);
  String shownTitle = detailTitle;
  if (shownTitle.length() > 12) shownTitle = shownTitle.substring(0, 11) + "..";
  display.println(shownTitle);

  if (detailHasDrawing) {
    display.fillRoundRect(DRAWING_BTN_X + 4, DRAWING_BTN_Y + 2, DRAWING_BTN_W - 8, DRAWING_BTN_H - 4, 6, COLOR_ACCENT_MINT);
    display.setTextColor(COLOR_ON_ACCENT);
    display.setTextSize(1);
    display.setCursor(DRAWING_BTN_X + 9, DRAWING_BTN_Y + 12);
    display.print("Rys.");
  }

  drawScrollButtons(detailScrollLine > 0, detailScrollLine < detailMaxScroll());
}

void drawDetailContent() {
  // czysci tylko obszar tresci, zeby nie przerysowywac naglowka/przyciskow przy scrollu
  display.fillRect(0, DETAIL_CONTENT_TOP, display.width(), DETAIL_CONTENT_BOTTOM - DETAIL_CONTENT_TOP, COLOR_BG);

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
    showMessage("Blad wczytywania", "Sprobuj ponownie", COLOR_DANGER);
    delay(2000);
    renderNotesList();
    return;
  }

  detailDoc.clear();
  if (deserializeJson(detailDoc, response) != DeserializationError::Ok) {
    showMessage("Blad wczytywania", "Nieprawidlowa odpowiedz", COLOR_DANGER);
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

    uint16_t color = COLOR_TEXT;
    if (!block["color"].isNull()) {
      String hex = String((const char*)block["color"]);
      // czarny tekst z edytora (domyslny kolor na jasnym tle strony) zamieniamy na
      // jasny tekst motywu, zeby byl czytelny na ciemnym tle ekranu - inne kolory (akcenty
      // wybrane recznie przez uzytkownika) zostawiamy bez zmian
      color = (hex == "#000000") ? COLOR_TEXT : hexToColor565(hex);
    } else if (type == "blockquote") {
      color = COLOR_TEXT_MUTED;
    } else if (type == "codeBlock") {
      color = COLOR_ACCENT_MINT;
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
    detailLines.push_back({ "(notatka nie zawiera tekstu)", COLOR_TEXT_MUTED, false });
  }
  detailScrollLine = 0;

  renderNoteDetail();
}

// ---- ekran podgladu rysunku (wektorowego) przypietego do notatki ----
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
