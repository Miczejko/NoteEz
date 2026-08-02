#pragma once

int listRowsPerPage();
int listMaxScroll();
void scrollList(int deltaRows);

// ---- renderuje ekran listy notatek (z juz pobranych danych w notesList[]) ----
void renderNotesList();

// ---- pobiera liste notatek (GET /api/device-notes/lite) i renderuje ekran listy ----
void fetchNotesLite();
