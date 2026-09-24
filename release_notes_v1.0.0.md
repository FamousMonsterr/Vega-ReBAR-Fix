# Vega-ReBAR-Fix v1.0.0

One-click Resizable BAR (SAM) patcher & autostart guard for unsupported AMD GPUs (RX Vega / Polaris).

First release.

## What's inside

- `VegaReBARFix.exe` — portable, self-contained (no .NET installation required, ~50 MB). Run, press **Patch**, reboot. Tick the autostart checkbox to survive Windows Updates.
- `Vega-ReBAR-Fix-v1.0.0.zip` — the same exe + README, zipped.

## Requirements (BIOS side, one-time)

- Above 4G Decoding = **Enabled**
- Re-Size BAR Support = **Enabled** / Auto
- CSM = **Disabled**

## Quick check

```
VegaReBARFix.exe -status
```
exit code `0` = registry patch present; `1` = wiped (run `-patch`).

---

Одноклик-патчер и страж Resizable BAR (SAM) для неподдерживаемых карт AMD (RX Vega / Polaris). `VegaReBARFix.exe` самодостаточен (.NET не нужен): запустить → **Пропатчить** → перезагрузка; галочка автозапуска переживёт обновления Windows. BIOS: Above 4G Decoding + Re-Size BAR = Enabled, CSM = Disabled.
