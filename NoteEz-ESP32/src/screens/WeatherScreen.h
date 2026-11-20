#pragma once

// Ekran prognozy pogody (Open-Meteo, bez klucza API) + aktualny odczyt z SHT40.
// Wspolrzedne na sztywno w Config.h (WEATHER_LAT/WEATHER_LON) - docelowo do wyboru
// w portalu konfiguracyjnym WiFi. Prognoza obejmuje dzisiaj + WEATHER_FORECAST_DAYS-1 dni
// w przod, przelaczane strzalkami </> na gorze ekranu. Dane rysowane jako dwa wykresy
// (temperatura - linia, opady - slupki) dla 13-godzinnego okna przesuwanego dolnymi przyciskami.

// rysuje przycisk "Pogoda" w naglowku ekranu listy notatek
void drawWeatherButton();

// pobiera prognoze z API i renderuje ekran (pokazuje "Pobieranie..." w trakcie)
void fetchWeather();

void renderWeatherScreen();
void changeWeatherDay(int delta);
// przesuwa okno godzinowe wykresow o deltaHours (clamp do 0..WEATHER_WINDOW_MAX_START)
void shiftWeatherWindow(int deltaHours);
