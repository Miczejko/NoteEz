#pragma once
#define LGFX_USE_V1
#include <LovyanGFX.hpp>

class LGFX : public lgfx::LGFX_Device {
  lgfx::Panel_ST7789 _panel_instance;
  lgfx::Bus_SPI       _bus_instance;
  lgfx::Touch_XPT2046 _touch_instance;

public:
  LGFX(void) {
    { auto cfg = _bus_instance.config();
      cfg.spi_host = SPI2_HOST;
      cfg.spi_mode = 0;
      cfg.freq_write = 40000000;
      cfg.freq_read  = 16000000;
      cfg.pin_sclk = 6;
      cfg.pin_mosi = 7;
      cfg.pin_miso = 2;
      cfg.pin_dc   = 19;
      _bus_instance.config(cfg);
      _panel_instance.setBus(&_bus_instance);
    }
    { auto cfg = _panel_instance.config();
      cfg.pin_cs   = 18;
      cfg.pin_rst  = 20;
      cfg.panel_width  = 240;
      cfg.panel_height = 320;
      _panel_instance.config(cfg);
    }
    { auto cfg = _touch_instance.config();
      cfg.x_min = 0; cfg.x_max = 4095;
      cfg.y_min = 0; cfg.y_max = 4095;
      cfg.pin_cs = 21;
      cfg.pin_int = -1;
      cfg.spi_host = SPI2_HOST;
      cfg.freq = 2500000;
      cfg.bus_shared = true;
      _touch_instance.config(cfg);
      _panel_instance.setTouch(&_touch_instance);
    }
    setPanel(&_panel_instance);
  }
};

extern LGFX display;
