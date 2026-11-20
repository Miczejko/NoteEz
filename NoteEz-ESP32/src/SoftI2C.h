#pragma once
#include <Arduino.h>

// Minimalna, bitbangowana implementacja I2C (master, tryb standard ~100kHz) na zwyklych GPIO.
// Powod istnienia: sprzetowa biblioteka Wire na tej plytce (ESP32-C6) psuje wspoldzielona
// magistrale SPI wyswietlacza/dotyku (LovyanGFX, bus_shared=true) - nawet gdy Wire.begin() nigdy
// sie nie wywoluje, samo zlinkowanie biblioteki wystarczy. SoftI2C nie dotyka zadnego sprzetowego
// peryferala I2C, wiec nie ma z czym kolidowac.
//
// Piny pracuja w trybie open-drain: do stanu niskiego sterujemy aktywnie (OUTPUT, LOW), do stanu
// wysokiego zwalniamy pin (INPUT_PULLUP) i pozwalamy podciagnieciu (na plytce SHT40 lub
// zewnetrznemu) podniesc linie - to standardowy, bezpieczny sposob bitbangowania I2C.
class SoftI2C {
public:
  SoftI2C(int sdaPin, int sclPin);

  void begin();

  // zwraca false, jesli urzadzenie nie potwierdzi adresu (brak ACK)
  bool writeBytes(uint8_t addr7, const uint8_t* data, size_t len);
  bool readBytes(uint8_t addr7, uint8_t* data, size_t len);

private:
  int _sda;
  int _scl;

  void sdaRelease();
  void sdaLow();
  void sclRelease();
  void sclLow();
  int  sdaRead();

  void start();
  void stop();
  bool writeByte(uint8_t b); // zwraca true jesli ACK
  uint8_t readByte(bool ack); // ack=true -> master wysyla ACK (beda kolejne bajty)
};
