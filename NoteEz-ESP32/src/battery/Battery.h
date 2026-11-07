#pragma once

// Pomiar poziomu baterii przez prosty dzielnik napiecia (2 rezystory) na GPIO1 (ADC1).
// Bez dedykowanego "fuel gauge" - tylko surowe napiecie przeliczone liniowo na procent,
// wystarczajaco dokladne do orientacyjnego wskaznika na ekranie.

extern float batteryVoltage;   // rzeczywiste napiecie ogniwa (po przeliczeniu przez dzielnik)
extern int batteryPercent;     // 0-100, przyciete do zakresu

void readBattery();
