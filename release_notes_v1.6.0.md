# Vega-ReBAR-Fix v1.6.0

## New

- **Force patch button («Принудительно»).** Patches EVERY AMD adapter key found, unconditionally — for "just make sure" moments after BIOS changes or device re-enumerations (state and checkboxes are ignored).
- **Compact rows + ⓘ details.** The long explanations moved behind an ⓘ icon at the right edge of the Registry patch / BAR / vBIOS rows: hover for a tooltip, click for the full text. No more wall of text in the diagnostics block.

## Field report baked in

- After disabling CSM the card re-enumerated on its original ROM (`113-D0500350-102`), Above-4G placement came alive (the "Large Memory Range" finally sits above 4 GB) and GPU-Z reports **BAR0 = 8192 MB / Resizable BAR: Enabled** — the firmware/PCIe resize WORKS on this stock setup, confirming the Guru3D approach. With the old Windows Update driver (Adrenalin 22.20, "Graphics Driver Support: Unsupported GPU") the resized BAR is not utilized — install the current Polaris/Vega Adrenalin branch so the driver actually drives it.

---

# Vega-ReBAR-Fix v1.6.0

## Новое

- **Кнопка «Принудительно».** Патчит ВСЕ найденные ключи AMD безусловно — для моментов «просто убедиться» после изменений BIOS или пере-перечисления устройств (состояние и галочки игнорируются).
- **Компактные строки + ⓘ.** Длинные пояснения переехали под значок ⓘ у правого края строк «Патч реестра» / «BAR выше 4 ГБ» / «vBIOS карты»: наведение — подсказка, клик — полный текст. Больше нет простыней в блоке диагностики.

## Полевой кейс вшит в релиз

- После отключения CSM карта пере-перечислилась на исходный ROM (`113-D0500350-102`), Above-4G-размещение ожило («Большой диапазон памяти» наконец выше 4 ГБ), а GPU-Z показывает **BAR0 = 8192 МБ / Resizable BAR: Enabled** — прошивочный/PCIe-ресайз на стоковом сетапе РАБОТАЕТ, подход Guru3D подтверждён. Со старым драйвером из Windows Update (Adrenalin 22.20, «Graphics Driver Support: Unsupported GPU») ресайзнутый BAR не используется — установите актуальную ветку Adrenalin для Polaris/Vega, чтобы драйвер начал им пользоваться.
