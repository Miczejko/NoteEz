#pragma once
#include <Arduino.h>
#include <WiFiManager.h>
#include <Preferences.h>
#include <ArduinoJson.h>
#include <vector>
#include "Config.h"

extern WiFiManager wm;
extern Preferences prefs;

// ---- konfiguracja zapisywana w NVS (Preferences) ----
extern String apiKey;   // klucz API urządzenia, pusty dopóki nie sparowane
extern String apiHost;  // np. "192.168.100.168:8080" - adres backendu w sieci lokalnej

extern WiFiManagerParameter* pairingCodeParam;
extern WiFiManagerParameter* apiHostParam;

// ---- lista notatek (GET /api/device-notes/lite) ----
struct NoteLite {
  String id;
  String title;
  bool hasDrawing;
  bool hasAudio;
  String color; // np. "#8963ba", puste gdy notatka nie ma wybranego koloru
};

extern NoteLite notesList[MAX_NOTES];
extern int notesCount;
extern int listScrollRow; // indeks pierwszej widocznej notatki na liscie

extern unsigned long lastActivityMillis; // czas ostatniego dotkniecia - do usypiania po bezczynnosci

// ---- ekran szczegółów notatki ----
enum Screen { SCREEN_LIST, SCREEN_DETAIL, SCREEN_DRAWING, SCREEN_WEATHER, SCREEN_TIMER };
extern Screen currentScreen;

// ---- minutnik (patrz TimerScreen.h) ----
enum TimerPhase { TIMER_SETUP, TIMER_RUNNING, TIMER_PAUSED, TIMER_DONE };
extern TimerPhase timerPhase;
extern int timerMinutes;              // wybrana liczba minut na ekranie ustawiania
extern unsigned long timerEndMillis;  // millis() docelowy koniec odliczania - wazny gdy TIMER_RUNNING
extern unsigned long timerRemainingMs; // pozostaly czas w ms - wazny gdy TIMER_PAUSED
extern int timerLastDisplayedSec;     // ostatnia narysowana sekunda - zeby nie przerysowywac co petle loop()

// jedna zawinieta linia tresci notatki, z minimalnym formatowaniem jakie potrafi wyswietlic ESP32
struct DetailLine {
  String text;
  uint16_t color;
  bool bold; // "pogrubienie" robione tanim trikiem: dwukrotny wydruk przesuniety o 1px
};

extern String detailTitle;
extern bool detailHasDrawing;
extern bool detailHasAudio;
extern std::vector<DetailLine> detailLines;
extern int detailScrollLine;

// pelny JSON ostatnio wczytanej notatki - trzymany zeby ekran rysunku mogl czytac drawings[]
// bez ponownego zapytania do serwera
extern JsonDocument detailDoc;
extern int detailDrawingIndex;
