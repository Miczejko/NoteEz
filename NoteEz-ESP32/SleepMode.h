#pragma once

// UWAGA: usypianie jest OBECNIE WYLACZONE (nigdy nie wywolywane w loop()) - nie dzialalo
// poprawnie na tym rdzeniu (bialy/zamarzniety ekran po wybudzeniu, patrz historia w git log).
// Funkcje zostaly zeby bylo od czego zaczac, jesli ktos bedzie chcial to naprawic.

// ---- TYMCZASOWY TEST DIAGNOSTYCZNY: light sleep zamiast deep sleep ----
void enterLightSleepTest();

// ---- usypia urzadzenie po minucie bezczynnosci; budzi je dotkniecie ekranu (T_IRQ na GPIO3) ----
void enterDeepSleep();
