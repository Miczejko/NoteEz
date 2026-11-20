#include "NoteDetailScreen.h"
#include <HTTPClient.h>
#include <WiFiClientSecure.h>
#include <ArduinoJson.h>
#include "../display/Display.h"
#include "../Colors.h"
#include "../../Config.h"
#include "../State.h"
#include "../UiHelpers.h"
#include "NotesScreen.h"
#include "../buzzer/Buzzer.h"

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

  // "Rys." zawsze widoczny - ekran rysunkow pokazuje liste albo "Brak rysunkow" + "Nowy rysunek"
  display.fillRoundRect(DRAWING_BTN_X + 4, DRAWING_BTN_Y + 2, DRAWING_BTN_W - 8, DRAWING_BTN_H - 4, 6, COLOR_ACCENT_MINT);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(DRAWING_BTN_X + 9, DRAWING_BTN_Y + 12);
  display.print("Rys.");

  drawScrollButtons(detailScrollLine > 0, detailScrollLine < detailMaxScroll());
}

static void drawCheckbox(int x, int y, bool checked) {
  const int s = DETAIL_TASK_BOX;
  if (checked) {
    display.fillRoundRect(x, y, s, s, 3, COLOR_ACCENT_MINT);
    // znacznik "ptaszek" (dwie linie, druga przesunieta o 1 px dla grubosci)
    display.drawLine(x + 3, y + 7, x + 6, y + 10, COLOR_ON_ACCENT);
    display.drawLine(x + 3, y + 8, x + 6, y + 11, COLOR_ON_ACCENT);
    display.drawLine(x + 6, y + 10, x + 11, y + 4, COLOR_ON_ACCENT);
    display.drawLine(x + 6, y + 11, x + 11, y + 5, COLOR_ON_ACCENT);
  } else {
    display.drawRoundRect(x, y, s, s, 3, COLOR_PRIMARY);
    display.drawRoundRect(x + 1, y + 1, s - 2, s - 2, 2, COLOR_PRIMARY);
  }
}

// rysuje jeden wiersz (slot 0..detailLinesPerPage()-1) obszaru tresci - dzieki temu zmiana
// checkboxa przerysowuje tylko swoje wiersze, bez migania calej strony
static void drawDetailRow(int slot) {
  int y = DETAIL_CONTENT_TOP + slot * DETAIL_LINE_HEIGHT;
  display.fillRect(0, y, display.width(), DETAIL_LINE_HEIGHT, COLOR_BG);

  int lineIdx = detailScrollLine + slot;
  if (lineIdx >= (int)detailLines.size()) return;

  const DetailLine& line = detailLines[lineIdx];
  int textX = DETAIL_MARGIN_X;
  int textY = y + (DETAIL_LINE_HEIGHT - 12) / 2; // glyph 12 px wysokosci w wierszu 16 px

  if (line.taskIndex >= 0) {
    textX += DETAIL_TASK_INDENT;
    if (line.taskStart) {
      drawCheckbox(DETAIL_MARGIN_X, y + (DETAIL_LINE_HEIGHT - DETAIL_TASK_BOX) / 2, line.checked);
    }
  }

  display.setTextSize(DETAIL_TEXT_SCALE);
  display.setTextColor(line.color);
  display.setCursor(textX, textY);
  display.print(line.text);
  if (line.bold) {
    // brak fontu pogrubionego w bibliotece - tani trik: drugi wydruk przesuniety o 1px
    display.setCursor(textX + 1, textY);
    display.print(line.text);
  }
  if (line.taskIndex >= 0 && line.checked && line.text.length() > 0) {
    // zaznaczony task: przekreslony tekst
    display.drawFastHLine(textX, textY + 6, line.text.length() * DETAIL_CHAR_W, line.color);
  }
}

void drawDetailContent() {
  // rysuje tylko obszar tresci, zeby nie przerysowywac naglowka/przyciskow przy scrollu
  int perPage = detailLinesPerPage();
  for (int slot = 0; slot < perPage; slot++) drawDetailRow(slot);
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

  WiFiClientSecure secureClient;
  secureClient.setInsecure();
  HTTPClient http;
  if (isApiHostRawIp(apiHost)) {
    http.begin("http://" + apiHost + "/api/device-notes/" + notesList[index].id);
  } else {
    http.begin(secureClient, "https://" + apiHost + "/api/device-notes/" + notesList[index].id);
  }
  http.addHeader("X-Api-Key", apiKey);
  int status = http.GET();
  String response = http.getString();
  http.end();

  if (status != 200) {
    buzzError();
    Serial.printf("device-notes/{id} status=%d body=%s\n", status, response.c_str());
    showMessage("Blad wczytywania", "Sprobuj ponownie", COLOR_DANGER);
    delay(2000);
    renderNotesList();
    return;
  }

  detailDoc.clear();
  if (deserializeJson(detailDoc, response) != DeserializationError::Ok) {
    buzzError();
    showMessage("Blad wczytywania", "Nieprawidlowa odpowiedz", COLOR_DANGER);
    delay(2000);
    renderNotesList();
    return;
  }


  detailTitle = String((const char*)(detailDoc["title"] | "(bez tytulu)"));
  detailHasAudio = detailDoc["audioClips"].as<JsonArray>().size() > 0;
  detailDrawingIndex = 0;

  // szerokosc linii liczona z fontu skali DETAIL_TEXT_SCALE (znak DETAIL_CHAR_W px) i marginesow;
  // linie taska maja mniej miejsca przez wciecie za kwadratem checkboxa
  int charsPerLine = (display.width() - 2 * DETAIL_MARGIN_X) / DETAIL_CHAR_W;
  int taskCharsPerLine = (display.width() - 2 * DETAIL_MARGIN_X - DETAIL_TASK_INDENT) / DETAIL_CHAR_W;
  detailLines.clear();

  // Kolejne bloki z tym samym taskIndex to jeden task (kilka paragrafow). Task, ktory zajalby
  // mniej niz DETAIL_TASK_MIN_LINES wierszy, dostaje pusty wiersz dopelniajacy (ten sam taskIndex),
  // zeby obszar dotyku mial min. ~32 px.
  int curTask = -1;
  int curTaskLines = 0;
  bool curTaskChecked = false;
  auto flushTask = [&]() {
    while (curTask >= 0 && curTaskLines < DETAIL_TASK_MIN_LINES) {
      detailLines.push_back({ "", COLOR_TEXT, false, (int16_t)curTask, curTaskChecked, false });
      curTaskLines++;
    }
    curTask = -1;
    curTaskLines = 0;
  };

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

    int taskIndex = (type == "taskItem") ? (block["taskIndex"] | -1) : -1;
    if (taskIndex != curTask) {
      flushTask();
      if (taskIndex >= 0) {
        curTask = taskIndex;
        curTaskChecked = block["checked"] | false;
      }
    }

    std::vector<String> wrapped = wrapText(text, taskIndex >= 0 ? taskCharsPerLine : charsPerLine);
    for (auto& w : wrapped) {
      bool start = (taskIndex >= 0 && curTaskLines == 0);
      detailLines.push_back({ w, color, bold, (int16_t)taskIndex, taskIndex >= 0 ? curTaskChecked : false, start });
      if (taskIndex >= 0) curTaskLines++;
    }
  }
  flushTask();

  if (detailLines.empty()) {
    detailLines.push_back({ "(notatka nie zawiera tekstu)", COLOR_TEXT_MUTED, false, -1, false, false });
  }
  detailScrollLine = 0;

  renderNoteDetail();
}

// ---- checkboxy (taski) ----

// ustawia stan taska lokalnie (linie na ekranie + detailDoc) i przerysowuje widoczne wiersze taska
static void setTaskCheckedLocal(int taskIndex, bool value) {
  for (auto& line : detailLines) {
    if (line.taskIndex == taskIndex) line.checked = value;
  }

  JsonArray blocksArr = detailDoc["textBlocks"].as<JsonArray>();
  for (JsonObject block : blocksArr) {
    if ((block["taskIndex"] | -1) == taskIndex) block["checked"] = value;
  }

  int perPage = detailLinesPerPage();
  for (int slot = 0; slot < perPage; slot++) {
    int lineIdx = detailScrollLine + slot;
    if (lineIdx >= (int)detailLines.size()) break;
    if (detailLines[lineIdx].taskIndex == taskIndex) drawDetailRow(slot);
  }
}

// pasek statusu zapisu w lewym dolnym rogu (obok przyciskow gora/dol, ktore zaczynaja sie od x=200)
static void drawSaveStatus(const char* msg, uint16_t color) {
  display.fillRect(0, SCROLL_BTN_Y, SCROLL_UP_BTN_X - 4, SCROLL_BTN_H, COLOR_BG);
  if (msg == nullptr) return;
  display.setTextSize(1);
  display.setTextColor(color);
  display.setCursor(DETAIL_MARGIN_X, SCROLL_BTN_Y + 11);
  display.print(msg);
}

// jedno krotkie zadanie PUT, bez ponowien; true gdy status 2xx
static bool putTaskChecked(const String& noteId, int taskIndex, bool value) {
  String path = "/api/device-notes/" + noteId + "/tasks/" + String(taskIndex);

  WiFiClientSecure secureClient; // jeden naraz - niszczony po wyjsciu z funkcji
  secureClient.setInsecure();
  HTTPClient http;
  http.setConnectTimeout(TASK_PUT_TIMEOUT_MS);
  http.setTimeout(TASK_PUT_TIMEOUT_MS);
  if (isApiHostRawIp(apiHost)) {
    http.begin("http://" + apiHost + path);
  } else {
    http.begin(secureClient, "https://" + apiHost + path);
  }
  http.addHeader("X-Api-Key", apiKey);
  http.addHeader("Content-Type", "application/json");
  int status = http.PUT(value ? "{\"checked\":true}" : "{\"checked\":false}");
  http.end();

  Serial.printf("PUT task %d -> %d status=%d\n", taskIndex, (int)value, status);
  return status >= 200 && status < 300;
}

void tapDetailContent(int32_t y) {
  if (y < DETAIL_CONTENT_TOP || y >= DETAIL_CONTENT_BOTTOM) return;

  int lineIdx = detailScrollLine + (y - DETAIL_CONTENT_TOP) / DETAIL_LINE_HEIGHT;
  if (lineIdx >= (int)detailLines.size()) return;

  const DetailLine& hit = detailLines[lineIdx];
  if (hit.taskIndex < 0) return;

  int taskIndex = hit.taskIndex;
  bool newValue = !hit.checked; // kopia przed zmiana (hit to referencja do elementu wektora)

  String noteId = String((const char*)(detailDoc["id"] | ""));
  if (noteId.length() == 0) return;

  setTaskCheckedLocal(taskIndex, newValue); // optymistycznie
  drawSaveStatus("Zapis...", COLOR_TEXT_MUTED);

  if (putTaskChecked(noteId, taskIndex, newValue)) {
    drawSaveStatus(nullptr, COLOR_TEXT);
    return;
  }

  setTaskCheckedLocal(taskIndex, !newValue); // cofniecie
  buzzError();
  drawSaveStatus("Blad zapisu", COLOR_DANGER);
  delay(1500);
  drawSaveStatus(nullptr, COLOR_TEXT);
}
