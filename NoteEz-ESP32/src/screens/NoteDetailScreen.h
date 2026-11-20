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

// dotkniecie obszaru tresci na wysokosci y: jesli trafilo w task, optymistycznie przelacza
// checkbox i wysyla PUT /api/device-notes/{id}/tasks/{index}; przy bledzie cofa stan
void tapDetailContent(int32_t y);

// ---- pobiera pelna tresc notatki (GET /api/device-notes/{id}) i pokazuje ekran szczegolow ----
void selectNote(int index);
