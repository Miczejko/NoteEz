#include "Buzzer.h"
#include <Arduino.h>
#include "Config.h"

void initBuzzer() {
  pinMode(BUZZER_PIN, OUTPUT);
  noTone(BUZZER_PIN);
}

void buzzWake() {
  tone(BUZZER_PIN, 2000, 80);
}

void buzzError() {
  tone(BUZZER_PIN, 1200, 100);
  delay(150); // przerwa miedzy piskami - krotkie zablokowanie petli, akceptowalne przy sygnale bledu
  tone(BUZZER_PIN, 1200, 100);
}
