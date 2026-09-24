# Vega-ReBAR-Fix v1.3.1

## Fixed

- **Proper DPI scaling (best practice).** The app now runs `PerMonitorV2` (explicit `ApplicationHighDpiMode` in the csproj) and scales its whole hand-laid-out layout by the actual monitor DPI: window default/minimum size, group boxes, row heights, buttons, log and footer all multiply by `DeviceDpi/96`. At 150 % scaling the window is 1.5× larger, text stays crisp (GDI renders point sizes at the monitor DPI), and long status rows (BAR above 4 GB, card vBIOS) are two lines tall and wrap instead of clipping. Moving the window to a monitor with another DPI re-layouts on the fly (`DpiChanged`).

## Исправлено

- **Корректное масштабирование DPI (best practice).** Приложение работает в режиме `PerMonitorV2` (явно задан `ApplicationHighDpiMode` в csproj) и масштабирует всю ручную раскладку под реальный DPI монитора: размер окна по умолчанию и минимальный, группы, высоты строк, кнопки, лог и футер умножаются на `DeviceDpi/96`. При масштабе 150 % окно в 1,5 раза больше, текст резкий (GDI рендерит пункты по DPI монитора), а длинные строки статуса (BAR выше 4 ГБ, vBIOS карты) занимают две строки и переносятся вместо обрезки. При переносе окна на монитор с другим DPI раскладка пересчитывается на лету (`DpiChanged`).
