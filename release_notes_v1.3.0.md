# Vega-ReBAR-Fix v1.3.0

## New

- **Card vBIOS row.** The diagnostics block now shows the GPU ROM ID with a ReBAR verdict: a resized BAR proves support (green); AMD reference stock Vega ROMs (`113-D05…`, e.g. `113-D0500350-102`) are flagged **red** with the message that the patch will not take effect until the card runs a ReBAR-capable BIOS (switch via the Dual BIOS switch) or ReBarUEFI is added to the board. Also printed by `-status`.
- **Resizable window** with a wider default size; all blocks track the window, the log grows with it.
- **DPI-scaling fix** (text no longer clips on >100 % display scaling): captions auto-size and the value column starts after the widest caption.
- Footer shows only the numeric version (the `+commit` suffix from SourceLink no longer pushes into the language selector).

## Новое

- **Строка «vBIOS карты».** В диагностике теперь показывается ID ROM видеокарты с вердиктом по ReBAR: ресайзнутый BAR доказывает поддержку (зелёный); стоковые reference-ROM Vega (`113-D05…`, например `113-D0500350-102`) помечаются **красным** с сообщением, что патч не сработает, пока карта не работает на BIOS с ReBAR (переключение тумблером Dual BIOS) или в плату не добавлен ReBarUEFI. Строка также выводится в `-status`.
- **Растягиваемое окно** с большим размером по умолчанию; все блоки следуют за размером, лог растёт вместе с окном.
- **Исправлено масштабирование DPI** (текст больше не обрезается при масштабе >100 %): подписи автоширины, колонка значений начинается после самой широкой подписи.
- В футере только числовая версия (суффикс `+commit` больше не наезжает на переключатель языка).
