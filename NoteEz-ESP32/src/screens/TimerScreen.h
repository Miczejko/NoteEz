#pragma once

// ---- minutnik: wybor minut -> odliczanie -> alarm dzwiekowy (Buzzer.h) ----
// Trzy widoki w ramach jednego ekranu SCREEN_TIMER, rozroznione przez State.h::timerPhase.

void renderTimerSetup();   // wybor liczby minut + przycisk "Start"
void renderTimerRunning(); // odliczanie MM:SS + "Pauza"/"Reset"
void renderTimerPaused();  // zatrzymane odliczanie + "Wznow"/"Reset"
void renderTimerDone();    // ekran po uplynieciu czasu + przycisk "OK"

void adjustTimerMinutes(int delta); // zmiana wybranej liczby minut na ekranie ustawiania

void startTimer();  // TIMER_SETUP -> TIMER_RUNNING
void pauseTimer();  // TIMER_RUNNING -> TIMER_PAUSED (zapamietuje pozostaly czas)
void resumeTimer(); // TIMER_PAUSED -> TIMER_RUNNING (kontynuuje od zapamietanego czasu)
void resetTimer();  // dowolna faza -> z powrotem do TIMER_SETUP, mozna wybrac minuty od nowa

// wywolywane w kazdej iteracji loop() (niezaleznie od dotyku) - odswieza wyswietlany czas
// raz na sekunde i wykrywa koniec odliczania (wtedy odpala alarm dzwiekowy)
void timerTick();
