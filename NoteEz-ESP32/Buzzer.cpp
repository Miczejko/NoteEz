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

void buzzTimerDone() {
  // prosta melodyjka (2 przebiegi ~3.5s lacznie) - blokuje petle na czas grania, co jest
  // tu akceptowalne, bo to alarm konca odliczania, a nie coś w tle wymagajace responsywnosci dotyku
  const int melody[] = {1568, 1976, 1568, 1976, 2349, 2349, 1976, 1568};
  const int noteCount = sizeof(melody) / sizeof(melody[0]);
  const int noteMs = 180;

  for (int repeat = 0; repeat < 2; repeat++) {
    for (int i = 0; i < noteCount; i++) {
      tone(BUZZER_PIN, melody[i], noteMs);
      delay(noteMs + 40);
    }
  }
  noTone(BUZZER_PIN);
}
