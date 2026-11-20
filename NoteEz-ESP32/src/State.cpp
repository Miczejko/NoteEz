#include "State.h"

WiFiManager wm;
Preferences prefs;

String apiKey;
String apiHost;

WiFiManagerParameter* pairingCodeParam = nullptr;
WiFiManagerParameter* apiHostParam = nullptr;

NoteLite notesList[MAX_NOTES];
int notesCount = 0;
int listScrollRow = 0;

unsigned long lastActivityMillis = 0;

Screen currentScreen = SCREEN_LIST;

TimerPhase timerPhase = TIMER_SETUP;
int timerMinutes = TIMER_DEFAULT_MINUTES;
unsigned long timerEndMillis = 0;
unsigned long timerRemainingMs = 0;
int timerLastDisplayedSec = -1;

String detailTitle;
bool detailHasDrawing = false;
bool detailHasAudio = false;
std::vector<DetailLine> detailLines;
int detailScrollLine = 0;

JsonDocument detailDoc;
int detailDrawingIndex = 0;
