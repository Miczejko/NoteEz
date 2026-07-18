#define LGFX_USE_V1
#include <LovyanGFX.hpp>
#include <WiFiManager.h>   // https://github.com/tzapu/WiFiManager
#include <HTTPClient.h>
#include <ArduinoJson.h>   // https://arduinojson.org
#include <Preferences.h>

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

// przycisk "Reset WiFi" w prawym górnym rogu - przytrzymanie go kasuje
// zapisane dane WiFi + parowanie i wraca do portalu konfiguracyjnego
#define RESET_BTN_X 260
#define RESET_BTN_Y 0
#define RESET_BTN_W 60
#define RESET_BTN_H 32
#define RESET_HOLD_MS 2000

// przycisk "Odśwież" w lewym górnym rogu
#define REFRESH_BTN_X 0
#define REFRESH_BTN_Y 0
#define REFRESH_BTN_W 60
#define REFRESH_BTN_H 32

WiFiManagerParameter* pairingCodeParam = nullptr;
WiFiManagerParameter* apiHostParam = nullptr;

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

  HTTPClient http;
  http.begin(url);
  http.addHeader("Content-Type", "application/json");

  JsonDocument reqDoc;
  reqDoc["code"] = code;
  reqDoc["deviceName"] = "ESP32-" + WiFi.macAddress();
  String body;
  serializeJson(reqDoc, body);

  int status = http.POST(body);
  String response = http.getString();
  http.end();

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

// ---- pobiera i wyswietla liste notatek (GET /api/device-notes/lite) ----
void fetchNotesLite() {
  display.fillScreen(TFT_WHITE);
  display.setTextColor(TFT_BLACK);
  display.setTextSize(2);
  display.setCursor(70, 6);
  display.println("NOTATNIK");

  drawTopButtons();

  if (apiKey.length() == 0) {
    display.setTextSize(1);
    display.setCursor(10, 50);
    display.println("Urzadzenie niesparowane.");
    return;
  }

  HTTPClient http;
  http.begin("http://" + apiHost + "/api/device-notes/lite");
  http.addHeader("X-Api-Key", apiKey);
  int status = http.GET();
  String response = http.getString();
  http.end();

  display.setTextSize(1);

  if (status != 200) {
    display.setTextColor(TFT_RED);
    display.setCursor(10, 50);
    display.printf("Blad pobierania (HTTP %d)", status);
    Serial.printf("device-notes/lite status=%d body=%s\n", status, response.c_str());
    return;
  }

  JsonDocument doc;
  if (deserializeJson(doc, response) != DeserializationError::Ok) {
    display.setTextColor(TFT_RED);
    display.setCursor(10, 50);
    display.println("Blad odczytu listy notatek");
    return;
  }

  JsonArray notes = doc.as<JsonArray>();
  if (notes.size() == 0) {
    display.setTextColor(TFT_BLACK);
    display.setCursor(10, 50);
    display.println("Brak notatek.");
    return;
  }

  int y = 44;
  const int rowHeight = 22;
  const int maxRows = (display.height() - y) / rowHeight;
  int row = 0;

  for (JsonObject note : notes) {
    if (row >= maxRows) break;

    const char* title = note["title"] | "(bez tytulu)";
    bool hasDrawing = note["hasDrawing"] | false;
    bool hasAudio = note["hasAudio"] | false;

    display.setTextColor(TFT_BLACK);
    display.setCursor(10, y);
    display.print(title);

    String badges = "";
    if (hasDrawing) badges += "[R]";
    if (hasAudio) badges += "[A]";
    if (badges.length() > 0) {
      display.setTextColor(TFT_DARKGREEN);
      display.setCursor(display.width() - 10 - badges.length() * 6, y);
      display.print(badges);
    }

    y += rowHeight;
    row++;
  }
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

// usuwa przypadkowo wpisany prefiks "http(s)://" oraz koncowy "/" z pola adresu serwera,
// zeby nie zbudowac zdublowanego URL typu "http://http://..." (przyczyna bledu HTTPC -1)
String normalizeHost(String host) {
  host.trim();
  host.replace("http://", "");
  host.replace("https://", "");
  while (host.endsWith("/")) host.remove(host.length() - 1);
  return host;
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

  if (display.getTouch(&x, &y)) {
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
    } else {
      resetTouchStart = 0;
    }
  } else {
    resetTouchStart = 0;
  }

  delay(20);
}
