#pragma once

// Brzeczyk pasywny (GPIO23) sterowany przez tone()/PWM (LEDC pod spodem).

void initBuzzer(); // wywolac raz w setup()

void buzzWake();  // krotki pik przy wybudzeniu dotykiem
void buzzError();  // dwa krotkie piski - blad (pobieranie, parowanie, WiFi)

// TODO: melodia po uplynieciu krotkiego timera - do dodania pozniej
