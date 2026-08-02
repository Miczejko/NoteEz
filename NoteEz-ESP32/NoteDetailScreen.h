#pragma once
#include <Arduino.h>
#include <vector>

// ---- dzieli tekst na linie mieszczace sie w szerokosci ekranu (zawijanie po slowach) ----
std::vector<String> wrapText(const String& text, int charsPerLine);

int detailLinesPerPage();
int detailMaxScroll();

void drawDetailChrome();
void drawDetailContent();
void renderNoteDetail();
void scrollDetail(int deltaLines);

// ---- pobiera pelna tresc notatki (GET /api/device-notes/{id}) i pokazuje ekran szczegolow ----
void selectNote(int index);
