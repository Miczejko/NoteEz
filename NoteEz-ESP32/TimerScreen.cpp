#include "TimerScreen.h"
#include "Display.h"
#include "Colors.h"
#include "Config.h"
#include "State.h"
#include "UiHelpers.h"
#include "Buzzer.h"

static void drawTimerHeader(const char* title) {
  display.fillScreen(COLOR_BG);
  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(2);
  display.setCursor((display.width() - (int)strlen(title) * 12) / 2, 6);
  display.println(title);
}

static void drawBackButton() {
  display.fillRoundRect(BACK_BTN_X + 4, BACK_BTN_Y + 2, BACK_BTN_W - 8, BACK_BTN_H - 4, 6, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(1);
  display.setCursor(BACK_BTN_X + 8, BACK_BTN_Y + 12);
  display.print("Wstecz");
}

void renderTimerSetup() {
  currentScreen = SCREEN_TIMER;
  timerPhase = TIMER_SETUP;
  drawTimerHeader("TIMER");
  drawBackButton();

  display.fillRoundRect(TIMER_SETUP_MINUS_X, TIMER_SETUP_ADJ_Y, TIMER_SETUP_ADJ_W, TIMER_SETUP_ADJ_H, 8, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(3);
  display.setCursor(TIMER_SETUP_MINUS_X + 18, TIMER_SETUP_ADJ_Y + 12);
  display.print("-");

  display.fillRoundRect(TIMER_SETUP_PLUS_X, TIMER_SETUP_ADJ_Y, TIMER_SETUP_ADJ_W, TIMER_SETUP_ADJ_H, 8, COLOR_PRIMARY);
  display.setCursor(TIMER_SETUP_PLUS_X + 14, TIMER_SETUP_ADJ_Y + 12);
  display.print("+");

  display.setTextColor(COLOR_BRIGHT_TEXT);
  display.setTextSize(4);
  char buf[10];
  snprintf(buf, sizeof(buf), "%d min", timerMinutes);
  int textW = (int)strlen(buf) * 24;
  display.setCursor((display.width() - textW) / 2, TIMER_SETUP_ADJ_Y + 8);
  display.print(buf);

  display.fillRoundRect(TIMER_START_BTN_X, TIMER_START_BTN_Y, TIMER_START_BTN_W, TIMER_START_BTN_H, 8, COLOR_ACCENT_MINT);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setTextSize(2);
  display.setCursor(TIMER_START_BTN_X + 18, TIMER_START_BTN_Y + 13);
  display.print("Start");
}

void adjustTimerMinutes(int delta) {
  timerMinutes += delta;
  if (timerMinutes < TIMER_MIN_MINUTES) timerMinutes = TIMER_MIN_MINUTES;
  if (timerMinutes > TIMER_MAX_MINUTES) timerMinutes = TIMER_MAX_MINUTES;
  renderTimerSetup();
}

static void drawCountdownFace(unsigned long remainingMs, bool paused) {
  drawTimerHeader(paused ? "TIMER (pauza)" : "TIMER");

  unsigned long remainingSec = (remainingMs + 999) / 1000;
  int mm = (int)(remainingSec / 60);
  int ss = (int)(remainingSec % 60);
  char buf[8];
  snprintf(buf, sizeof(buf), "%02d:%02d", mm, ss);

  display.setTextColor(paused ? COLOR_TEXT_MUTED : COLOR_BRIGHT_TEXT);
  display.setTextSize(6);
  int textW = 5 * 36; // "MM:SS" - 5 znakow
  display.setCursor((display.width() - textW) / 2, 80);
  display.print(buf);

  display.setTextSize(2);
  if (paused) {
    display.fillRoundRect(TIMER_PAUSE_BTN_X, TIMER_PAUSE_BTN_Y, TIMER_PAUSE_BTN_W, TIMER_PAUSE_BTN_H, 8, COLOR_ACCENT_MINT);
    display.setTextColor(COLOR_ON_ACCENT);
    display.setCursor(TIMER_PAUSE_BTN_X + 6, TIMER_PAUSE_BTN_Y + 13);
    display.print("Wznow");
  } else {
    display.fillRoundRect(TIMER_PAUSE_BTN_X, TIMER_PAUSE_BTN_Y, TIMER_PAUSE_BTN_W, TIMER_PAUSE_BTN_H, 8, COLOR_PRIMARY);
    display.setTextColor(COLOR_ON_ACCENT);
    display.setCursor(TIMER_PAUSE_BTN_X + 10, TIMER_PAUSE_BTN_Y + 13);
    display.print("Pauza");
  }

  display.fillRoundRect(TIMER_RESET_BTN_X, TIMER_RESET_BTN_Y, TIMER_RESET_BTN_W, TIMER_RESET_BTN_H, 8, COLOR_RESET);
  display.setTextColor(COLOR_TEXT);
  display.setCursor(TIMER_RESET_BTN_X + 12, TIMER_RESET_BTN_Y + 13);
  display.print("Reset");
}

void renderTimerRunning() {
  currentScreen = SCREEN_TIMER;
  unsigned long remainingMs = (timerEndMillis > millis()) ? (timerEndMillis - millis()) : 0;
  drawCountdownFace(remainingMs, false);
}

void renderTimerPaused() {
  currentScreen = SCREEN_TIMER;
  drawCountdownFace(timerRemainingMs, true);
}

void startTimer() {
  timerPhase = TIMER_RUNNING;
  timerEndMillis = millis() + (unsigned long)timerMinutes * 60000UL;
  timerLastDisplayedSec = -1;
  renderTimerRunning();
}

void pauseTimer() {
  timerRemainingMs = (timerEndMillis > millis()) ? (timerEndMillis - millis()) : 0;
  timerPhase = TIMER_PAUSED;
  renderTimerPaused();
}

void resumeTimer() {
  timerEndMillis = millis() + timerRemainingMs;
  timerPhase = TIMER_RUNNING;
  timerLastDisplayedSec = -1;
  renderTimerRunning();
}

void resetTimer() {
  renderTimerSetup();
}

void renderTimerDone() {
  currentScreen = SCREEN_TIMER;
  drawTimerHeader("Czas minal!");
  display.setTextColor(COLOR_ACCENT_MINT);
  display.setTextSize(2);
  display.setCursor(50, 90);
  display.println("Koniec odliczania");

  display.fillRoundRect(TIMER_START_BTN_X, TIMER_START_BTN_Y, TIMER_START_BTN_W, TIMER_START_BTN_H, 8, COLOR_PRIMARY);
  display.setTextColor(COLOR_ON_ACCENT);
  display.setCursor(TIMER_START_BTN_X + 42, TIMER_START_BTN_Y + 13);
  display.print("OK");
}

void timerTick() {
  if (timerPhase != TIMER_RUNNING) return;

  long remainingMs = (long)(timerEndMillis - millis());
  if (remainingMs <= 0) {
    timerPhase = TIMER_DONE;
    renderTimerDone();
    buzzTimerDone(); // blokuje na czas melodii - zamierzone, to alarm konca odliczania
    return;
  }

  int remainingSec = (int)((remainingMs + 999) / 1000);
  if (remainingSec != timerLastDisplayedSec) {
    timerLastDisplayedSec = remainingSec;
    renderTimerRunning();
  }
}
