#pragma once

// Ekran prognozy pogody (Open-Meteo, bez klucza API) + aktualny odczyt z SHT40.
// Wspolrzedne na sztywno w Config.h (WEATHER_LAT/WEATHER_LON) - docelowo do wyboru
// w portalu konfiguracyjnym WiFi. Prognoza obejmuje dzisiaj + WEATHER_FORECAST_DAYS-1 dni
// w przod, przelaczane strzalkami </> na ekranie.

// rysuje przycisk "Pogoda" w naglowku ekranu listy notatek
void drawWeatherButton();

// pobiera prognoze z API i renderuje ekran (pokazuje "Pobieranie..." w trakcie)
void fetchWeather();

void renderWeatherScreen();
void scrollWeather(int deltaRows);
void changeWeatherDay(int delta);
int weatherListRowsPerPage();
int weatherListMaxScroll();
