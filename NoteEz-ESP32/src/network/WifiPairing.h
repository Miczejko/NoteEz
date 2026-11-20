#pragma once
#include <Arduino.h>
#include <WiFiManager.h>

// callback wywoływany, gdy WiFiManager wchodzi w tryb konfiguracji (AP)
void configModeCallback(WiFiManager* myWiFiManager);

// ---- parowanie: wysyla kod z formularza WiFiManagera do /api/devices/claim ----
bool claimDevice(const String& code);

// ---- kasuje WiFi + parowanie i wraca do portalu konfiguracyjnego ----
void resetWifiAndPairing();
