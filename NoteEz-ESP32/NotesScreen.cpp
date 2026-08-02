#include "NotesScreen.h"
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include "Display.h"
#include "Colors.h"
#include "Config.h"
#include "State.h"
#include "UiHelpers.h"
#include "ClimateSensor.h"

int listRowsPerPage() {
  return (LIST_CONTENT_BOTTOM - LIST_START_Y) / LIST_ROW_HEIGHT;
}

int listMaxScroll() {
  int maxScroll = notesCount - listRowsPerPage();
  return maxScroll > 0 ? maxScroll : 0;
}

void scrollList(int deltaRows) {
  int maxScroll = listMaxScroll();
  int newScroll = listScrollRow + deltaRows;
  if (newScroll < 0) newScroll = 0;
  if (newScroll > maxScroll) newScroll = maxScroll;
  if (newScroll == listScrollRow) return;

  listScrollRow = newScroll;
  renderNotesList();
}

void renderNotesList() {
  currentScreen = SCREEN_LIST;
  display.fillScreen(COLOR_BG);
  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  display.setCursor(70, 6);
  display.println("NOTATNIK");

  drawTopButtons();
  display.setTextSize(1);

  // TEST: odczyt SHT40 - do usuniecia/przeniesienia po sprawdzeniu, ze czujnik dziala
  display.setTextColor(COLOR_TEXT_MUTED);
  display.setCursor(70, 28);
  if (sht4Ready && !isnan(currentTempC)) {
    display.printf("%.1f C   %.0f %%", currentTempC, currentHumidityPct);
  } else {
    display.print("SHT40: brak danych");
  }

  if (apiKey.length() == 0) {
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, 50);
    display.println("Urzadzenie niesparowane.");
    return;
  }

  if (notesCount == 0) {
    display.setTextColor(COLOR_TEXT_MUTED);
    display.setCursor(10, 50);
    display.println("Brak notatek.");
    return;
  }

  int perPage = listRowsPerPage();
  int rows = min(perPage, notesCount - listScrollRow);
  int y = LIST_START_Y;

  for (int i = 0; i < rows; i++) {
    int idx = listScrollRow + i;
    int cardH = LIST_ROW_HEIGHT - 6;

    // karta notatki - szare tlo, z boku pasek w kolorze wybranym przez uzytkownika
    // (odpowiednik .note-accent z NoteCard.vue na stronie)
    display.fillRoundRect(6, y, display.width() - 12, cardH, 6, COLOR_CARD_GREY);
    if (notesList[idx].color.length() > 0) {
      uint16_t accent = hexToColor565(notesList[idx].color);
      display.fillRoundRect(6, y, 8, cardH, 3, accent);
    }
    display.drawRoundRect(6, y, display.width() - 12, cardH, 6, COLOR_BORDER);

    display.setTextColor(COLOR_BRIGHT_TEXT);
    display.setTextSize(2);
    display.setCursor(16, y + cardH / 2 - 8);
    String shownTitle = notesList[idx].title;
    if (shownTitle.length() > 18) shownTitle = shownTitle.substring(0, 17) + "..";
    display.print(shownTitle);

    String badges = "";
    if (notesList[idx].hasDrawing) badges += "[R]";
    if (notesList[idx].hasAudio) badges += "[A]";
    if (badges.length() > 0) {
      display.setTextSize(1);
      display.setTextColor(COLOR_ACCENT_MINT);
      display.setCursor(display.width() - 16 - badges.length() * 6, y + cardH / 2 - 4);
      display.print(badges);
    }

    y += LIST_ROW_HEIGHT;
  }

  bool canUp = listScrollRow > 0;
  bool canDown = listScrollRow < listMaxScroll();
  drawScrollButtons(canUp, canDown);
}

void fetchNotesLite() {
  readClimate();
  notesCount = 0;
  listScrollRow = 0;

  if (apiKey.length() == 0) {
    renderNotesList();
    return;
  }

  HTTPClient http;
  http.begin("http://" + apiHost + "/api/device-notes/lite");
  http.addHeader("X-Api-Key", apiKey);
  int status = http.GET();
  String response = http.getString();
  http.end();

  if (status != 200) {
    Serial.printf("device-notes/lite status=%d body=%s\n", status, response.c_str());
    renderNotesList();
    display.setTextColor(COLOR_DANGER);
    display.setCursor(10, 50);
    display.printf("Blad pobierania (HTTP %d)", status);
    return;
  }

  JsonDocument doc;
  if (deserializeJson(doc, response) != DeserializationError::Ok) {
    renderNotesList();
    display.setTextColor(COLOR_DANGER);
    display.setCursor(10, 50);
    display.println("Blad odczytu listy notatek");
    return;
  }

  JsonArray notes = doc.as<JsonArray>();
  for (JsonObject note : notes) {
    if (notesCount >= MAX_NOTES) break;
    notesList[notesCount].id = note["id"].as<String>();
    notesList[notesCount].title = String((const char*)(note["title"] | "(bez tytulu)"));
    notesList[notesCount].hasDrawing = note["hasDrawing"] | false;
    notesList[notesCount].hasAudio = note["hasAudio"] | false;
    notesList[notesCount].color = note["color"].isNull() ? "" : String((const char*)note["color"]);
    notesCount++;
  }

  renderNotesList();
}
