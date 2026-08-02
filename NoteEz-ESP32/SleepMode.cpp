#include "SleepMode.h"
#include <esp_sleep.h>
#include <driver/gpio.h>
#include <WiFi.h>
#include "Display.h"
#include "Colors.h"
#include "Config.h"
#include "State.h"
#include "UiHelpers.h"
#include "NotesScreen.h"

// Light sleep budzi sie z DOWOLNEGO GPIO (gpio_wakeup_enable), bez ograniczenia do domeny
// LP-IO i bez niskopoziomowych zaleznosci ktore w deep sleep mogly nie byc poprawnie
// obslugiwane przez pakiet plytek "Arduino ESP32 Boards" 2.0.18. Jesli TO zadziala,
// wiemy ze wiring/dotyk sa ok, a problem siedzi konkretnie w deep sleep na tym rdzeniu.
void enterLightSleepTest() {
  showMessage("Usypianie (TEST light sleep)", "Dotknij ekranu, aby wybudzic", COLOR_TEXT_MUTED);
  delay(300);

  display.sleep();
  digitalWrite(BACKLIGHT_PIN, LOW);

  pinMode(TIRQ_PIN, INPUT_PULLUP);
  delay(10);
  Serial.printf("[light-sleep-test] T_IRQ (GPIO%d) stan przed usnieciem: %d\n", TIRQ_PIN, digitalRead(TIRQ_PIN));
  Serial.flush();

  // Aktywne polaczenie WiFi STA potrafi kolidowac z reczmym wejsciem w sleep (modem
  // proby utrzymania polaczenia moga przerywac/destabilizowac cykl usypiania). Wylaczamy
  // radio calkowicie na czas testu - i tak zaraz zestawiamy je od nowa po wybudzeniu.
  WiFi.disconnect(true);
  WiFi.mode(WIFI_OFF);
  delay(100);

  gpio_wakeup_enable((gpio_num_t)TIRQ_PIN, GPIO_INTR_LOW_LEVEL);
  esp_sleep_enable_gpio_wakeup();

  esp_light_sleep_start(); // W ODROZNIENIU OD deep sleep - TO WRACA po wybudzeniu, kod leci dalej

  digitalWrite(BACKLIGHT_PIN, HIGH);
  display.wakeup();
  delay(150); // ST7789 wymaga ok. 120ms po SLPOUT zanim stabilnie przyjmie kolejne dane

  WiFi.mode(WIFI_STA);
  WiFi.begin(); // wraca do ostatnio zapamietanej sieci (dane trzyma WiFiManager/NVS)

  lastActivityMillis = millis();
  renderNotesList();
}

// Deep sleep resetuje cala pamiec RAM - po wybudzeniu kod zaczyna sie od nowa od setup(),
// ktory i tak od razu laczy sie z zapisanym WiFi i odswieza liste notatek, wiec efekt dla
// uzytkownika jest taki, jakby urzadzenie po prostu "obudzilo sie" na ekranie listy.
void enterDeepSleep() {
  showMessage("Usypianie...", "Dotknij ekranu, aby wybudzic", COLOR_TEXT_MUTED);
  delay(300);

  display.sleep(); // usypia sam panel (ST7789 SLPIN)

  // Gasimy podswietlenie i "zatrzaskujemy" ten stan na czas snu (gpio_hold) - bez tego
  // pad wraca do stanu domyslnego po wejsciu w deep sleep i podswietlenie zapala sie z powrotem.
  // (ESP32-C6 nie ma oddzielnej domeny RTC GPIO jak klasyczny ESP32, wiec nie ma tu globalnego
  // przelacznika gpio_deep_sleep_hold_en/dis - samo gpio_hold_en na pinie wystarczy.)
  digitalWrite(BACKLIGHT_PIN, LOW);
  gpio_hold_en((gpio_num_t)BACKLIGHT_PIN);

  // XPT2046 IRQ jest open-drain (aktywne niskim) - INPUT_PULLUP zapewnia stan wysoki w spoczynku,
  // bez tego pin moglby "plywac" i wybudzanie byloby niewiarygodne/nie dzialaloby wcale.
  pinMode(TIRQ_PIN, INPUT_PULLUP);
  delay(10); // czas na ustalenie sie stanu linii po zmianie pinMode

  Serial.printf("[sleep] T_IRQ (GPIO%d) stan przed usnieciem: %d\n", TIRQ_PIN, digitalRead(TIRQ_PIN));

  esp_err_t wakeErr = esp_deep_sleep_enable_gpio_wakeup(1ULL << TIRQ_PIN, ESP_GPIO_WAKEUP_GPIO_LOW);
  Serial.printf("[sleep] esp_deep_sleep_enable_gpio_wakeup zwrocilo: %d (0 = ESP_OK)\n", (int)wakeErr);
  Serial.flush();

  // Aktywne polaczenie WiFi STA tuz przed wejsciem w sleep potrafi destabilizowac sam moment
  // usypiania - czyste rozlaczenie przed esp_deep_sleep_start() jest zalecana praktyka.
  WiFi.disconnect(true);
  WiFi.mode(WIFI_OFF);
  delay(100);

  esp_deep_sleep_start(); // nie wraca - reset i ponowne setup() po wybudzeniu
}
