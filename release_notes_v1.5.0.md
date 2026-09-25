# Vega-ReBAR-Fix v1.5.0

## Fixed: the multi-key trap (Dual BIOS re-enumeration)

A field report caught the exact failure mode: after flipping the card's Dual-BIOS switch, Windows re-enumerates the **same physical card under a NEW class key** — the ROM reports its own subsystem ID and revision (`…SUBSYS_0B361002&REV_C3` became `…REV_C1`, then the live device showed a third variant, `SUBSYS_6B761002&REV_C1`). The old "patch the best adapter" logic kept the trio on the stale key while the live one had none.

- The GUI now has a **"Cards to patch"** group listing **every** AMD adapter key, each with its device ID, VRAM, an **АКТИВНАЯ/ACTIVE** mark (resolved authoritatively via the device Enum → `Driver` registry value) and per-key patch state. **All keys are checked by default** — the trio is inert on stale keys and must exist on whichever key Windows binds. A checkbox excludes a key; exclusions are remembered by device ID.
- `Patch`/`Undo`, `-patch`/`-undo` and the logon guard apply to all selected keys; `-status` prints a line per key.
- Diagnostics rows (vBIOS, driver) now always describe the **live** key, so the shown ROM ID tracks the actually running firmware (it changed `113-D0500350-102` → `113-D0501400-101` after the switch — both stock, both without ReBAR capability).

---

# Vega-ReBAR-Fix v1.5.0

## Исправлено: ловушка нескольких ключей (перечисление при переключении Dual BIOS)

Полевой репорт поймал точный сценарий отказа: после переключения тумблера Dual BIOS Windows перечисляет **ту же физическую карту под НОВЫМ ключом класса** — ROM отдаёт свои SUBSYS и ревизию (`…SUBSYS_0B361002&REV_C3` сменилось на `…REV_C1`, а живое устройство показало третий вариант `SUBSYS_6B761002&REV_C1`). Старая логика «патч лучшей карты» держала триплет на устаревшем ключе, пока живой оставался пустым.

- В ГУИ появилась группа **«Карты для патча»** со **всеми** ключами AMD: device ID, VRAM, метка **АКТИВНАЯ/ACTIVE** (авторитетно — через значение `Driver` в ветке Enum устройства) и состояние патча по каждому ключу. **По умолчанию отмечены все** — триплет безвреден на устаревших ключах и обязан быть на том, который выберет Windows. Галочка исключает ключ; исключения запоминаются по device ID.
- «Пропатчить»/«Откатить», `-patch`/`-undo` и страж автозапуска работают по всем выбранным ключам; `-status` печатает строку на каждый ключ.
- Строки диагностики (vBIOS, драйвер) теперь всегда описывают **живой** ключ: показываемый ROM отслеживает реально работающую прошивку (после переключения он сменился с `113-D0500350-102` на `113-D0501400-101` — оба стоковые, обе без ReBAR).
