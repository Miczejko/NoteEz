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
enum Screen { SCREEN_LIST, SCREEN_DETAIL, SCREEN_DRAWING };
extern Screen currentScreen;

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
