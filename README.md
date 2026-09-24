# Vega-ReBAR-Fix

<div align="center">

![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue)
![Framework](https://img.shields.io/badge/.NET-10%20WinForms-512BD4)
![GPU](https://img.shields.io/badge/GPU-AMD%20Radeon%20RX%20Vega%20%2F%20Polaris-red)
![License](https://img.shields.io/badge/license-MIT-green)

**EN** | [Русский ↓](#русский)

One-click Resizable BAR (Smart Access Memory) patcher & guard for AMD GPUs that AMD itself does not support — inspired by the UX of [RDP_CnC](https://github.com/sebaxakerhtc/rdpwrap) (live green/red diagnostics) and packaged like a portable one-click fix.

</div>

## What it does

AMD Adrenalin gates ReBAR (SAM) behind a whitelist: newer drivers read two registry values on the display adapter class key — `KMD_RebarControlMode` and `KMD_RebarControlSupport` — and only enable Resizable BAR when both are `1` **and** the GPU is on the supported list. On unsupported cards (RX Vega 56/64, Polaris, …) they stay `0` and the SAM toggle is hidden.

**The values get wiped by every Windows Update / driver reinstall** (the class key is recreated from the INF defaults). This tool puts them back and watches for it:

- **Diagnostics** — live green/red status, refreshed on a 2 s timer:
  - *Registry patch* — are both `KMD_*` values `1`?
  - *BAR above 4 GB* — queries WMI for the GPU's mapped memory ranges; ReBAR counts as active only when a BAR **≥ 512 MB sits above the 4 GiB boundary** (Above-4G Decoding alone just moves the stock 256 MB BAR up). This is the same evidence Device Manager shows as "Large Memory Range".
- **Patch / Undo** — writes or reverts the two values, with a full `.reg` backup saved to `%ProgramData%\VegaReBARFix\` and originals stored in `HKCU\Software\VegaReBARFix\Backup` before touching anything.
- **Autostart guard** (optional, checkbox) — creates a Task Scheduler job (`VegaReBARFix_Autostart`, runs at logon with highest privileges, **no UAC prompt**). At every logon it checks the flags; if a Windows Update wiped them, it silently re-patches and shows a window with a **Reboot** button. Reboot **never** happens by itself.
- Adapter key (`0000`, `0001`, …) is auto-discovered at runtime — it shifts when the driver is reinstalled, so it is never hard-coded.

## Requirements

- Windows 10 / 11 x64, admin rights at least once (for the patch itself)
- AMD Radeon GPU + Adrenalin drivers
- **BIOS side (cannot be patched from Windows):**
  - `Above 4G Decoding` = **Enabled**
  - `Re-Size BAR Support` = **Enabled** (try `Auto` too)
  - `CSM Support` = **Disabled** (UEFI boot)
- No .NET installation needed — the release exe is self-contained (~50 MB)

## Usage

1. Download `VegaReBARFix.exe` from the [latest release](../../releases).
2. Run it — it self-elevates (one UAC prompt).
3. Press **Пропатчить / Patch**, then **Перезагрузить ПК / Reboot**.
4. Tick **Проверять при входе в Windows** to install the autostart guard.
5. Verify: the *BAR above 4 GB* row must turn green, and [GPU-Z](https://www.techpowerup.com/gpuz/) should show `Resizable BAR: Enabled`.

### CLI

| Command | Effect | Exit codes |
|---|---|---|
| `VegaReBARFix.exe -status` | print patch + BAR status, no changes | `0` = patched, `1` = wiped, `4` = no AMD GPU |
| `VegaReBARFix.exe -patch` | write `KMD_RebarControlMode=1`, `KMD_RebarControlSupport=1` (self-elevates) | `0` / `1` |
| `VegaReBARFix.exe -undo` | restore pre-patch values from backup | `0` / `1` |
| `VegaReBARFix.exe -autostart` | logon guard: check → silently re-patch → show window only if a reboot is needed | — |

## Honest disclaimer

- RX Vega / Polaris are **not officially supported** by AMD Smart Access Memory. This is a community-known registry unlock: the driver *can* drive ReBAR on these ASICs, but nothing is guaranteed.
- Expect **modest** gains (a few percent, sometimes zero or negative in individual titles). Measure your own games.
- After every Windows Update / driver reinstall the flags may be wiped — that is exactly what the autostart guard re-fixes.
- The registry side is only half of ReBAR. If *BAR above 4 GB* stays red after a reboot, fix the BIOS first (see below).

## Troubleshooting

| Symptom | Fix |
|---|---|
| *Registry patch* red after an update | Press **Patch** again (or let the autostart guard do it) + reboot |
| *BAR above 4 GB* red after reboot | Check BIOS: Above 4G Decoding = Enabled, Re-Size BAR = Enabled/Auto, CSM = Disabled |
| BIOS is right, still red | The vBIOS may not advertise the PCIe ReBAR capability. Firmware-side fix: [ReBarUEFI](https://github.com/xCuri0/ReBarUEFI) (adds a ReBarDxe module to the board's UEFI; flashing a modded BIOS carries a brick risk — know what you are doing) |
| GPU-Z shows ReBAR but games unchanged | Normal on some titles; the win depends on the game's BAR utilization |

## Building from source

```
dotnet publish src/VegaReBARFix -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

## Credits

- [RDP_CnC / rdpwrap by sebaxakerhtc](https://github.com/sebaxakerhtc/rdpwrap) — the diagnostics-UI pattern this tool mimics
- [ReBarUEFI by xCuri0](https://github.com/xCuri0/ReBarUEFI) — firmware-side ReBAR for boards that lack it
- [Guru3D: "Unlocking Resizable Bar for unsupported AMD GPUs"](https://forums.guru3d.com) — the community research behind `KMD_RebarControlMode`

## License

[MIT](LICENSE)

---

<a name="русский"></a>

# Русский

Одноклик-патчер и «страж» Resizable BAR (Smart Access Memory) для видеокарт AMD, которые AMD официально не поддерживает. ГУИ сделан по образцу RDP_CnC (живая диагностика «зелёный/красный»), распространяется как портативный exe.

## Что делает

Драйвер AMD держит ReBAR (SAM) за whitelist: в ключе класса видеоадаптера он читает два значения — `KMD_RebarControlMode` и `KMD_RebarControlSupport` — и включает Resizable BAR только если оба равны `1` **и** карта в списке поддерживаемых. На неподдерживаемых картах (RX Vega 56/64, Polaris и т.д.) значения остаются `0`, а переключатель SAM скрыт.

**После каждого обновления Windows / переустановки драйвера значения слетают** (ключ класса пересоздаётся из дефолтов INF). Утилита возвращает их обратно и следит за этим:

- **Диагностика** — живой статус «зелёный/красный», обновление по таймеру 2 с:
  - *Патч реестра* — оба ли значения `KMD_*` равны `1`;
  - *BAR выше 4 ГБ* — через WMI опрашивает диапазоны памяти GPU; ReBAR считается активным, только если BAR **≥ 512 МБ лежит выше границы 4 ГиБ** (одно Above 4G Decoding просто поднимает штатный BAR на 256 МБ). Тот же признак, что Device Manager показывает как «Большой диапазон памяти».
- **Пропатчить / Откатить** — запись или откат двух значений; перед изменением полный бэкап `.reg` в `%ProgramData%\VegaReBARFix\` и оригиналы в `HKCU\Software\VegaReBARFix\Backup`.
- **Автозапуск-страж** (опционально, галочка) — создаёт задачу Планировщика (`VegaReBARFix_Autostart`, при входе в Windows с максимальными правами, **без UAC-запроса**). При каждом входе проверяет флаги; если обновление их стёрло — тихо патчит и показывает окно с кнопкой **Перезагрузить**. Перезагрузка **никогда** не происходит сама.
- Номер ключа адаптера (`0000`, `0001`, …) определяется автоматически при запуске — он меняется при переустановке драйвера.

## Требования

- Windows 10 / 11 x64, один раз нужны права администратора (для самого патча)
- Видеокарта AMD Radeon + драйверы Adrenalin
- **Со стороны BIOS (из Windows это не пропатчить):**
  - `Above 4G Decoding` = **Enabled**
  - `Re-Size BAR Support` = **Enabled** (можно попробовать и `Auto`)
  - `CSM Support` = **Disabled** (загрузка UEFI)
- .NET устанавливать не нужно — exe в релизе самодостаточный (~50 МБ)

## Использование

1. Скачайте `VegaReBARFix.exe` из [последнего релиза](../../releases).
2. Запустите — утилита сама попросит права администратора (один UAC).
3. Нажмите **Пропатчить**, затем **Перезагрузить ПК**.
4. Поставьте галочку **Проверять при входе в Windows** — включит страж автозапуска.
5. Проверка: строка *BAR выше 4 ГБ* должна стать зелёной, а [GPU-Z](https://www.techpowerup.com/gpuz/) — показывать `Resizable BAR: Enabled`.

### Командная строка

| Команда | Действие | Коды выхода |
|---|---|---|
| `VegaReBARFix.exe -status` | показать статус патча и BAR, ничего не меняя | `0` = патч есть, `1` = слетел, `4` = GPU не найден |
| `VegaReBARFix.exe -patch` | записать `KMD_RebarControlMode=1`, `KMD_RebarControlSupport=1` (сам запросит права) | `0` / `1` |
| `VegaReBARFix.exe -undo` | откат к значениям из бэкапа | `0` / `1` |
| `VegaReBARFix.exe -autostart` | страж при входе: проверка → тихий патч → окно только если нужна перезагрузка | — |

## Честное предупреждение

- RX Vega / Polaris **официально не поддерживаются** AMD Smart Access Memory. Это известное community-решение: драйвер умеет ReBAR на этих чипах, но гарантий нет.
- Прирост **скромный**: единицы процентов, местами ноль или минус в отдельных играх. Замеряйте на своих.
- После каждого обновления Windows / переустановки драйвера флаги могут слетать — именно это и перечиняет страж автозапуска.
- Реестр — только половина ReBAR. Если после перезагрузки *BAR выше 4 ГБ* остаётся красным — сначала чините BIOS (см. ниже).

## Решение проблем

| Симптом | Что делать |
|---|---|
| *Патч реестра* красный после обновления | Нажать **Пропатчить** снова (или дождаться стража) + перезагрузка |
| *BAR выше 4 ГБ* красный после перезагрузки | Проверить BIOS: Above 4G Decoding = Enabled, Re-Size BAR = Enabled/Auto, CSM = Disabled |
| BIOS верный, всё равно красный | Возможно, vBIOS не анонсирует ReBAR-возможность PCIe. Решение на стороне прошивки: [ReBarUEFI](https://github.com/xCuri0/ReBarUEFI) (добавляет ReBarDxe в UEFI платы; прошивка модифицированного BIOS — риск брика, действуйте осознанно) |
| GPU-Z показывает ReBAR, а игры не изменились | Нормально: выигрыш зависит от того, использует ли игра BAR |

## Сборка из исходников

```
dotnet publish src/VegaReBARFix -c Release -r win-x64 --self-contained true ^
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o dist
```

## Благодарности

- [RDP_CnC / rdpwrap (sebaxakerhtc)](https://github.com/sebaxakerhtc/rdpwrap) — образец диагностического ГУИ
- [ReBarUEFI (xCuri0)](https://github.com/xCuri0/ReBarUEFI) — ReBAR на уровне прошивки для плат без него
- [Guru3D: «Unlocking Resizable Bar for unsupported AMD GPUs»](https://forums.guru3d.com) — исследования по `KMD_RebarControlMode`

## Лицензия

[MIT](LICENSE)
