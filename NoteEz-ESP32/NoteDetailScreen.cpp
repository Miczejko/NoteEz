#include "NoteDetailScreen.h"
#include <HTTPClient.h>
#include <ArduinoJson.h>
#include "Display.h"
#include "Colors.h"
#include "Config.h"
#include "State.h"
#include "UiHelpers.h"
#include "NotesScreen.h"

std::vector<String> wrapText(const String& text, int charsPerLine) {
  std::vector<String> lines;
  int len = text.length();
  int pStart = 0;

  while (pStart <= len) {
    int nl = text.indexOf('\n', pStart);
    String paragraph = (nl == -1) ? text.substring(pStart) : text.substring(pStart, nl);

    if (paragraph.length() == 0) {
      lines.push_back("");
    } else {
      int i = 0;
      int plen = paragraph.length();
      while (i < plen) {
        int remaining = plen - i;
        int take = min(charsPerLine, remaining);
        if (take < remaining) {
          int breakAt = paragraph.lastIndexOf(' ', i + take - 1);
          if (breakAt > i) take = breakAt - i;
        }
        String chunk = paragraph.substring(i, i + take);
        chunk.trim();
        lines.push_back(chunk);
        i += take;
        while (i < plen && paragraph[i] == ' ') i++;
      }
    }

    if (nl == -1) break;
    pStart = nl + 1;
  }

  if (lines.empty()) lines.push_back("");
  return lines;
}

int detailLinesPerPage() {
  return (DETAIL_CONTENT_BOTTOM - DETAIL_CONTENT_TOP) / DETAIL_LINE_HEIGHT;
}

int detailMaxScroll() {
  int perPage = detailLinesPerPage();
  int maxScroll = (int)detailLines.size() - perPage;
  return maxScroll > 0 ? maxScroll : 0;
}

void drawDetailChrome() {
  display.fillScreen(COLOR_BG);

  // naglowek z przyciskiem powrotu
  display.fillRoundRect(BACK_BTN_X + 4, BACK_BTN_Y + 2, BACK_BTN_W - 8, BACK_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 10, BACK_BTN_Y + 12);
  display.print("< Wstecz");

  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  display.setCursor(BACK_BTN_W + 10, 6);
  String shownTitle = detailTitle;
  if (shownTitle.length() > 12) shownTitle = shownTitle.substring(0, 11) + "..";
  display.println(shownTitle);

  if (detailHasDrawing) {
    display.fillRoundRect(DRAWING_BTN_X + 4, DRAWING_BTN_Y + 2, DRAWING_BTN_W - 8, DRAWING_BTN_H - 4, 6, COLOR_ACCENT_MINT);
    display.setTextColor(COLOR_ON_ACCENT);
    display.setTextSize(1);
    display.setCursor(DRAWING_BTN_X + 9, DRAWING_BTN_Y + 12);
    display.print("Rys.");
  }

  drawScrollButtons(detailScrollLine > 0, detailScrollLine < detailMaxScroll());
}

void drawDetailContent() {
  // czysci tylko obszar tresci, zeby nie przerysowywac naglowka/przyciskow przy scrollu
  display.fillRect(0, DETAIL_CONTENT_TOP, display.width(), DETAIL_CONTENT_BOTTOM - DETAIL_CONTENT_TOP, COLOR_BG);

  display.setTextSize(1);

  int perPage = detailLinesPerPage();
  int y = DETAIL_CONTENT_TOP;

  for (int i = 0; i < perPage; i++) {
    int lineIdx = detailScrollLine + i;
    if (lineIdx >= (int)detailLines.size()) break;

    const DetailLine& line = detailLines[lineIdx];
    display.setTextColor(line.color);
    display.setCursor(10, y);
    display.print(line.text);
    if (line.bold) {
      // brak fontu pogrubionego w bibliotece - tani trik: drugi wydruk przesuniety o 1px
      display.setCursor(11, y);
      display.print(line.text);
    }
    y += DETAIL_LINE_HEIGHT;
  }
}

void renderNoteDetail() {
  currentScreen = SCREEN_DETAIL;
  drawDetailChrome();
  drawDetailContent();
}

void scrollDetail(int deltaLines) {
  int maxScroll = detailMaxScroll();
  int newScroll = detailScrollLine + deltaLines;
  if (newScroll < 0) newScroll = 0;
  if (newScroll > maxScroll) newScroll = maxScroll;
  if (newScroll == detailScrollLine) return;

  detailScrollLine = newScroll;
  drawDetailContent();
  // przyciski gora/dol moga zmienic kolor (aktywny/nieaktywny), wiec odswiez tez naglowek
  drawDetailChrome();
  drawDetailContent();
}

void selectNote(int index) {
  if (index < 0 || index >= notesCount) return;

  showMessage("Wczytywanie...", notesList[index].title.c_str());

  HTTPClient http;
  http.begin("http://" + apiHost + "/api/device-notes/" + notesList[index].id);
  http.addHeader("X-Api-Key", apiKey);
  int status = http.GET();
  String response = http.getString();
  http.end();

  if (status != 200) {
    Serial.printf("device-notes/{id} status=%d body=%s\n", status, response.c_str());
    showMessage("Blad wczytywania", "Sprobuj ponownie", COLOR_DANGER);
    delay(2000);
    renderNotesList();
    return;
  }

  detailDoc.clear();
  if (deserializeJson(detailDoc, response) != DeserializationError::Ok) {
    showMessage("Blad wczytywania", "Nieprawidlowa odpowiedz", COLOR_DANGER);
    delay(2000);
    renderNotesList();
    return;
  }

  detailTitle = String((const char*)(detailDoc["title"] | "(bez tytulu)"));
  detailHasDrawing = detailDoc["drawings"].as<JsonArray>().size() > 0;
  detailHasAudio = detailDoc["audioClips"].as<JsonArray>().size() > 0;
  detailDrawingIndex = 0;

  // przy tekscie 1 (font ~6px szerokosci znaku) i marginesie 10px z kazdej strony
  int charsPerLine = (display.width() - 20) / 6;
  detailLines.clear();

  JsonArray blocksArr = detailDoc["textBlocks"].as<JsonArray>();
  for (JsonObject block : blocksArr) {
    String text = String((const char*)(block["text"] | ""));
    if (text.length() == 0) continue;

    String type = String((const char*)(block["type"] | "paragraph"));
    bool bold = block["bold"] | false;

    uint16_t color = COLOR_TEXT;
    if (!block["color"].isNull()) {
      String hex = String((const char*)block["color"]);
      // czarny tekst z edytora (domyslny kolor na jasnym tle strony) zamieniamy na
      // jasny tekst motywu, zeby byl czytelny na ciemnym tle ekranu - inne kolory (akcenty
      // wybrane recznie przez uzytkownika) zostawiamy bez zmian
      color = (hex == "#000000") ? COLOR_TEXT : hexToColor565(hex);
    } else if (type == "blockquote") {
      color = COLOR_TEXT_MUTED;
    } else if (type == "codeBlock") {
      color = COLOR_ACCENT_MINT;
    }

    String prefix = "";
    if (type == "taskItem") {
      bool checked = block["checked"] | false;
      prefix = checked ? "[x] " : "[ ] ";
    }

    std::vector<String> wrapped = wrapText(prefix + text, charsPerLine);
    for (auto& w : wrapped) {
      detailLines.push_back({ w, color, bold });
    }
  }

  if (detailLines.empty()) {
    detailLines.push_back({ "(notatka nie zawiera tekstu)", COLOR_TEXT_MUTED, false });
  }
  detailScrollLine = 0;

  renderNoteDetail();
}
