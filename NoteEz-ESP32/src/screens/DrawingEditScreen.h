#pragma once
#include <Arduino.h>

// ---- edytor nowego rysunku (SCREEN_DRAW_EDIT) ----
// Pusty bialy obszar + pasek narzedzi (5 kolorow, Cofnij, Przywroc, Wyczysc, Zapisz, Anuluj).
// Zapis: jeden POST /api/device-notes/{id}/drawings do aktualnie otwartej notatki (detailDoc["id"]).

// czysci bufor i pokazuje pusty edytor
void openDrawingEditor();

// dotyk na ekranie edytora: kreski w obszarze rysowania, przyciski w pasku (tylko "swiezy" dotyk)
void drawEditTouch(int32_t x, int32_t y, bool freshTouch);

// wolane z loop() gdy nie ma dotyku: po DRAW_PEN_UP_MS bez dotyku konczy biezaca linie
void drawEditPenUpTick();
