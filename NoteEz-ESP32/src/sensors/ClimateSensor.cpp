#include "ClimateSensor.h"
#include "Config.h"
#include "SoftI2C.h"

#define SHT40_I2C_ADDR 0x44
#define SHT40_CMD_MEASURE_HIGH_PRECISION 0xFD

static SoftI2C i2c(SHT40_SDA_PIN, SHT40_SCL_PIN);

bool sht4Ready = false;
float currentTempC = NAN;
float currentHumidityPct = NAN;

// CRC-8 Sensirion (polynomial 0x31, init 0xFF) - uzywane przez SHT4x do weryfikacji kazdych 2 bajtow danych
static uint8_t crc8(const uint8_t* data, int len) {
  uint8_t crc = 0xFF;
  for (int i = 0; i < len; i++) {
    crc ^= data[i];
    for (int b = 0; b < 8; b++) {
      crc = (crc & 0x80) ? (crc << 1) ^ 0x31 : (crc << 1);
    }
  }
  return crc;
}

void initClimateSensor() {
  i2c.begin();

  // krotki "ping" - probujemy wyslac komende pomiaru i sprawdzamy czy urzadzenie odpowiada ACK-iem
  uint8_t cmd = SHT40_CMD_MEASURE_HIGH_PRECISION;
  sht4Ready = i2c.writeBytes(SHT40_I2C_ADDR, &cmd, 1);
  Serial.printf("[boot] SHT40 (soft I2C) ping = %d\n", sht4Ready);

  if (sht4Ready) {
    delay(10); // czas na pomiar przed odrzuceniem pierwszego wyniku
    readClimate();
  }
}

void readClimate() {
  if (!sht4Ready) return;

  uint8_t cmd = SHT40_CMD_MEASURE_HIGH_PRECISION;
  if (!i2c.writeBytes(SHT40_I2C_ADDR, &cmd, 1)) return;

  delay(10); // wg datasheet pomiar w trybie high precision trwa do ~8.3ms

  uint8_t buf[6];
  if (!i2c.readBytes(SHT40_I2C_ADDR, buf, 6)) return;

  if (crc8(buf, 2) != buf[2] || crc8(buf + 3, 2) != buf[5]) {
    Serial.println("[SHT40] blad CRC odczytu - pomijam");
    return;
  }

  uint16_t rawT = (buf[0] << 8) | buf[1];
  uint16_t rawRH = (buf[3] << 8) | buf[4];

  currentTempC = -45.0f + 175.0f * (rawT / 65535.0f);
  currentHumidityPct = -6.0f + 125.0f * (rawRH / 65535.0f);
  if (currentHumidityPct < 0) currentHumidityPct = 0;
  if (currentHumidityPct > 100) currentHumidityPct = 100;
}
