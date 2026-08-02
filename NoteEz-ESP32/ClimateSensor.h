#pragma once

// Czujnik temperatury/wilgotnosci SHT40 (I2C, GPIO11=SDA, GPIO1=SCL - patrz Config.h).
// UWAGA: celowo NIE uzywa biblioteki Wire ani Adafruit_SHT4x - samo ich zlinkowanie do
// projektu psulo wspoldzielona magistrale SPI dotyku/wyswietlacza (zdiagnozowane empirycznie).
// Zamiast tego surowy protokol SHT40 zaimplementowany recznie na SoftI2C (bitbanging).

extern bool sht4Ready;
extern float currentTempC;
extern float currentHumidityPct;

// wywolac raz w setup()
void initClimateSensor();

// odswieza currentTempC/currentHumidityPct (no-op jesli czujnik sie nie zainicjalizowal)
void readClimate();
