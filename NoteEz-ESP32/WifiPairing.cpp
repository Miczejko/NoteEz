#include "WifiPairing.h"
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include "Display.h"
#include "Colors.h"
#include "State.h"
#include "UiHelpers.h"
#include "Buzzer.h"

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
    buzzError();
    showMessage("Blad parowania", response.c_str(), COLOR_DANGER);
    Serial.printf("claim status=%d body=%s\n", status, response.c_str());
    delay(3000);
    return false;
  }

  JsonDocument resDoc;
  if (deserializeJson(resDoc, response) != DeserializationError::Ok) {
    buzzError();
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

void resetWifiAndPairing() {
  showMessage("Resetowanie...", "Kasuje WiFi i parowanie", COLOR_DANGER);
  delay(1000);

  prefs.remove("apiKey");
  prefs.remove("apiHost");
  wm.resetSettings(); // kasuje zapisane dane WiFi (NVS uzywane przez WiFiManager)

  delay(500);
  ESP.restart();
}
