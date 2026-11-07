#include "Battery.h"
#include <Arduino.h>
#include "Config.h"

float batteryVoltage = 0.0f;
int batteryPercent = 0;

void readBattery() {
  // analogReadMilliVolts() korzysta z fabrycznej kalibracji ADC (dokladniejsze niz surowe
  // analogRead() przeliczane recznie przez stala) - dostepne na ESP32 Arduino core.
  uint32_t milliVolts = analogReadMilliVolts(BATTERY_ADC_PIN);
  batteryVoltage = (milliVolts / 1000.0f) * BATTERY_DIVIDER_RATIO;

  float pct = (batteryVoltage - BATTERY_VOLTAGE_EMPTY) / (BATTERY_VOLTAGE_FULL - BATTERY_VOLTAGE_EMPTY) * 100.0f;
  if (pct < 0.0f) pct = 0.0f;
  if (pct > 100.0f) pct = 100.0f;
  batteryPercent = (int)(pct + 0.5f);
}
