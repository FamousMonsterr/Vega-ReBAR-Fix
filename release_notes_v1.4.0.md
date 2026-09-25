# Vega-ReBAR-Fix v1.4.0

## New

- **vBIOS knowledge base.** Built-in table of known ROM IDs (stock Vega 56 `113-D050…` / Vega 64 `113-D046…` = no ReBAR capability) — the verdict now comes from the KB; a resized BAR remains the hard "proven working" evidence. The table grows from field reports: run `-status`, [open an Issue](https://github.com/FamousMonsterr/Vega-ReBAR-Fix/issues) with your vBIOS line, it ships in the next release.
- **Fast Startup detection.** When the vBIOS verdict is not green and `HiberbootEnabled=1`, the tool (GUI log + `-status`) warns: "Shut down" hibernates and the card keeps its old ROM — after flipping the Dual BIOS switch do a FULL power-off (`shutdown /s /full /t 0`), then power on. This is the most common reason a switch flip "does nothing".
- **ReBarUEFI link.** While the vBIOS verdict is not green, the Diagnostics block shows a clickable link to the ReBarUEFI project (board-firmware route).
- **Live vBIOS change tracking.** The driver re-reads the ROM at every real power-on; when the ID changes, the tool logs the old → new transition and recomputes the verdict on the spot (no restart needed).

---

# Vega-ReBAR-Fix v1.4.0

## Новое

- **База знаний vBIOS.** Встроенная таблица известных ID прошивок (стоковые Vega 56 `113-D050…` / Vega 64 `113-D046…` = нет возможности ReBAR) — вердикт теперь берётся из базы; ресайзнутый BAR остаётся жёстким доказательством «работает». База пополняется отчётами: запустите `-status`, [откройте Issue](https://github.com/FamousMonsterr/Vega-ReBAR-Fix/issues) со своей строкой vBIOS — она попадёт в следующий релиз.
- **Определение Fast Startup.** Если вердикт vBIOS не зелёный и `HiberbootEnabled=1`, утилита (лог ГУИ + `-status`) предупреждает: «Завершение работы» уходит в гибернацию, и карта продолжает со старым ROM — после переключения тумблера Dual BIOS нужно ПОЛНОЕ выключение (`shutdown /s /full /t 0`), затем включение ПК. Это самая частая причина «тумблер не сработал».
- **Ссылка на ReBarUEFI.** Пока вердикт vBIOS не зелёный, в блоке «Диагностика» показана кликабельная ссылка на проект ReBarUEFI (маршрут мода прошивки платы).
- **Живой трекинг смены vBIOS.** Драйвер перечитывает ROM при каждом настоящем включении; при смене ID утилита логирует переход «старый → новый» и тут же пересчитывает вердикт (без перезапуска).
