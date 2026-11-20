#pragma once
#include <Arduino.h>

// ---- kolorystyka dopasowana do motywu strony (NoteEz-Frontend/src/assets/main.css) ----
// UWAGA: musza to byc stale typu uint16_t (nie #define/int), bo LovyanGFX przeciażza
// funkcje rysujace wg typu argumentu - "int" trafia w przeciażenie dla surowego RGB888
// i przekopakowuje juz spakowane bity 565, co psuje kolory (czerwony wychodzi zielony itp.)
constexpr uint16_t RGB565(uint8_t r, uint8_t g, uint8_t b) {
  return (uint16_t)(((r & 0xF8) << 8) | ((g & 0xFC) << 3) | (b >> 3));
}

extern const uint16_t COLOR_BG;          // --color-bg / --black
extern const uint16_t COLOR_SURFACE;     // --color-surface
extern const uint16_t COLOR_BORDER;      // --color-border
extern const uint16_t COLOR_TEXT;        // --color-text
extern const uint16_t COLOR_TEXT_MUTED;  // --color-text-muted
extern const uint16_t COLOR_PRIMARY;     // --color-primary / pearl-aqua
extern const uint16_t COLOR_SECONDARY;   // --color-secondary / muted-teal
extern const uint16_t COLOR_ACCENT_MINT; // --color-accent-mint / celadon
extern const uint16_t COLOR_DANGER;      // --color-danger
extern const uint16_t COLOR_ON_ACCENT;   // --color-on-accent (ciemny tekst na jasnym przycisku)
extern const uint16_t COLOR_RESET;       // czerwony przycisk resetu WiFi
extern const uint16_t COLOR_BRIGHT_TEXT; // jasniejszy naglowek "NOTATNIK" / tytuly notatek
extern const uint16_t COLOR_CARD_GREY;   // szare tlo kafelka notatki na liscie
