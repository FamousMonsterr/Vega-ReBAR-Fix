# Vega-ReBAR-Fix v1.6.1

## Fixed

- **Diagnostics captions were stacked on top of each other** (all rendered at the group's top-left corner) — captions now sit at their rows, and each ⓘ icon is placed **immediately after its own caption**, so it is unambiguous what it expands.
- **"Cards to patch" row**: the per-key patch-state label no longer overlaps long checkbox texts — it now follows the checkbox's right edge. Device-ID ampersands (`&`) are escaped so they are no longer swallowed as accelerator prefixes.

## Honest detection note

- The hardware BAR check reads what **Windows actually mapped** (WMI, plus a best-effort live resource-map parse). GPU-Z's "BAR Sizes" panel reads the PCIe aperture from the driver and can show the full 8 GB while Windows maps less — until a ReBAR-aware driver requests the full window. There is **no user-mode "speed ping"** for ReBAR; the practical proof is a BAR-heavy game benchmark (exactly the Gothic Remake test the author ran — ReBAR confirmed working in the field).

---

# Vega-ReBAR-Fix v1.6.1

## Исправлено

- **Подписи строк диагностики слипались в куче** (все рисовались в левом верхнем углу группы) — теперь каждая подпись на своей строке, а значок ⓘ стоит **сразу после своей подписи**, так что однозначно понятно, что он раскрывает.
- **Строка «Карты для патча»**: метка состояния патча больше не наезжает на длинный текст чекбокса — она следует за его правым краем. Амперсанды в device ID экранированы (`&&`) и больше не съедаются как акселераторы.

## Честное замечание по детекции

- Железная проверка BAR читает то, что **реально отображено в память Windows** (WMI плюс попытка разбора живой карты ресурсов). Панель «BAR Sizes» в GPU-Z читает PCIe-апертуру от драйвера и может показывать полные 8 ГБ, пока Windows отображает меньше — до тех пор, пока ReBAR-драйвер не запросит всё окно. **Пользовательского «пинга скорости» для ReBAR не существует**; практическое доказательство — бенчмарк в игре с тяжёлым BAR (ровно это и сделал автор с Gothic Remake — ReBAR подтверждён в бою).
