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

  // patrz komentarz w enterDeepSleep() - bez tego pull-up "znika" na czas snu i linia plywa,
  // co tlumaczy sporadyczne samoistne wybudzanie sie w tym tescie.
  gpio_sleep_sel_dis((gpio_num_t)TIRQ_PIN);

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
  esp_sleep_enable_timer_wakeup(3000000); // dodatkowe, "awaryjne" wybudzenie co 3s - diagnostyka:
                                           // jesli TO tez nigdy sie nie odpali, esp_light_sleep_start()
                                           // sam w sobie sie wiesza, a nie problem lezy w GPIO/dotyku

  bool wokenByTouch = false;
  int iter = 0;
  while (!wokenByTouch && iter < 50) { // limit iteracji na wszelki wypadek, zeby nie petlic w nieskonczonosc
    Serial.printf("[light-sleep-test] wchodze w sleep (proba %d)...\n", iter);
    Serial.flush();

    esp_light_sleep_start(); // W ODROZNIENIU OD deep sleep - TO WRACA po wybudzeniu, kod leci dalej

    esp_sleep_wakeup_cause_t cause = esp_sleep_get_wakeup_cause();
    Serial.printf("[light-sleep-test] wybudzono, przyczyna=%d (2=GPIO,4=TIMER), T_IRQ=%d\n",
                  (int)cause, digitalRead(TIRQ_PIN));
    Serial.flush();

    if (cause == ESP_SLEEP_WAKEUP_GPIO || digitalRead(TIRQ_PIN) == LOW) {
      wokenByTouch = true;
    }
    iter++;
  }

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

  // XPT2046 wystawia T_IRQ (PENIRQ) tylko gdy jego CS jest w stanie WYSOKIM (niewybrany).
  // W deep sleep piny bez jawnego gpio_hold_en "plyna" do stanu domyslnego - gdyby CS dotyku
  // (GPIO21) popłynal w dol, chip zostalby "wybrany" i calkowicie przestalby wystawiac T_IRQ,
  // niezaleznie od dotyku (zdiagnozowane empirycznie: reczne zwarcie T_IRQ do GND budzilo
  // urzadzenie, ale prawdziwy dotyk juz nie). Wymuszamy i przytrzymujemy CS dotyku na HIGH.
  pinMode(TOUCH_CS_PIN, OUTPUT);
  digitalWrite(TOUCH_CS_PIN, HIGH);
  gpio_hold_en((gpio_num_t)TOUCH_CS_PIN);

  // XPT2046 IRQ jest open-drain (aktywne niskim) - INPUT_PULLUP zapewnia stan wysoki w spoczynku,
  // bez tego pin moglby "plywac" i wybudzanie byloby niewiarygodne/nie dzialaloby wcale.
  pinMode(TIRQ_PIN, INPUT_PULLUP);

  // pinMode() konfiguruje pull-up tylko dla trybu AKTYWNEGO. ESP-IDF domyslnie "izoluje"
  // czesc obwodu pinu na czas snu (oszczednosc energii), co realnie wylacza ten pull-up
  // podczas snu - linia zaczyna "plywac" i wybudzanie staje sie niewiarygodne (albo w ogole
  // nie dziala, albo budzi sie losowo samo z siebie). gpio_sleep_sel_dis() wylacza ta izolacje,
  // zeby zwykly pull-up z pinMode() realnie przetrwal na czas snu (gpio_sleep_pullup_en/
  // gpio_sleep_pulldown_dis nie sa dostepne w tej wersji pakietu plytek ESP32).
  gpio_sleep_sel_dis((gpio_num_t)TIRQ_PIN);

  delay(10); // czas na ustalenie sie stanu linii po zmianie pinMode

  Serial.printf("[sleep] T_IRQ (GPIO%d) stan przed usnieciem: %d\n", TIRQ_PIN, digitalRead(TIRQ_PIN));

  // Domena zasilania peryferiow RTC/LP bywa domyslnie wylaczana w deep sleep (oszczednosc
  // energii), co gasi tez obwod "czujacy" stan pinu potrzebny do wybudzenia - nawet gdy
  // esp_deep_sleep_enable_gpio_wakeup() zwraca ESP_OK. Wymuszamy jej wlaczenie na czas snu.
  esp_sleep_pd_config(ESP_PD_DOMAIN_RTC_PERIPH, ESP_PD_OPTION_ON);

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
