#include "SoftI2C.h"

// polowka okresu zegara ~100kHz (5us high + 5us low = 10us = 100kHz)
static const uint16_t HALF_PERIOD_US = 5;

SoftI2C::SoftI2C(int sdaPin, int sclPin) : _sda(sdaPin), _scl(sclPin) {}

void SoftI2C::begin() {
  pinMode(_sda, INPUT_PULLUP);
  pinMode(_scl, INPUT_PULLUP);
}

void SoftI2C::sdaRelease() { pinMode(_sda, INPUT_PULLUP); }
void SoftI2C::sdaLow()     { pinMode(_sda, OUTPUT); digitalWrite(_sda, LOW); }
void SoftI2C::sclRelease() { pinMode(_scl, INPUT_PULLUP); }
void SoftI2C::sclLow()     { pinMode(_scl, OUTPUT); digitalWrite(_scl, LOW); }
int  SoftI2C::sdaRead()    { return digitalRead(_sda); }

void SoftI2C::start() {
  sdaRelease();
  sclRelease();
  delayMicroseconds(HALF_PERIOD_US);
  sdaLow();
  delayMicroseconds(HALF_PERIOD_US);
  sclLow();
}

void SoftI2C::stop() {
  sdaLow();
  delayMicroseconds(HALF_PERIOD_US);
  sclRelease();
  delayMicroseconds(HALF_PERIOD_US);
  sdaRelease();
  delayMicroseconds(HALF_PERIOD_US);
}

bool SoftI2C::writeByte(uint8_t b) {
  for (int i = 7; i >= 0; i--) {
    if (b & (1 << i)) sdaRelease(); else sdaLow();
    delayMicroseconds(HALF_PERIOD_US);
    sclRelease();
    // clock stretching - urzadzenie moze trzymac SCL nisko, czekamy az samo puosci
    uint32_t waitStart = micros();
    while (digitalRead(_scl) == LOW) {
      if (micros() - waitStart > 20000) break; // zabezpieczenie przed zawieszeniem
    }
    delayMicroseconds(HALF_PERIOD_US);
    sclLow();
  }

  // 9. takt - odczyt ACK (urzadzenie sciaga SDA w dol)
  sdaRelease();
  delayMicroseconds(HALF_PERIOD_US);
  sclRelease();
  delayMicroseconds(HALF_PERIOD_US / 2);
  bool ack = (sdaRead() == LOW);
  delayMicroseconds(HALF_PERIOD_US / 2);
  sclLow();

  return ack;
}

uint8_t SoftI2C::readByte(bool ack) {
  uint8_t b = 0;
  sdaRelease(); // master zwalnia SDA, zeby slave mogl nadawac

  for (int i = 7; i >= 0; i--) {
    sclRelease();
    uint32_t waitStart = micros();
    while (digitalRead(_scl) == LOW) {
      if (micros() - waitStart > 20000) break;
    }
    delayMicroseconds(HALF_PERIOD_US);
    if (sdaRead() == HIGH) b |= (1 << i);
    sclLow();
    delayMicroseconds(HALF_PERIOD_US);
  }

  // master wysyla ACK/NACK
  if (ack) sdaLow(); else sdaRelease();
  delayMicroseconds(HALF_PERIOD_US);
  sclRelease();
  delayMicroseconds(HALF_PERIOD_US);
  sclLow();
  sdaRelease();

  return b;
}

bool SoftI2C::writeBytes(uint8_t addr7, const uint8_t* data, size_t len) {
  start();
  bool ok = writeByte((addr7 << 1) | 0); // write bit = 0
  for (size_t i = 0; ok && i < len; i++) {
    ok = writeByte(data[i]);
  }
  stop();
  return ok;
}

bool SoftI2C::readBytes(uint8_t addr7, uint8_t* data, size_t len) {
  start();
  bool ok = writeByte((addr7 << 1) | 1); // read bit = 1
  if (ok) {
    for (size_t i = 0; i < len; i++) {
      data[i] = readByte(i < len - 1); // ACK na wszystkich oprocz ostatniego bajtu
    }
  }
  stop();
  return ok;
}
