# Vega-ReBAR-Fix v1.1.1

## Changes

- The "BAR above 4 GB" status now points at both possible blockers (BIOS **or** card vBIOS) when ReBAR is not active with the registry trio in place.
- README troubleshooting expanded with the vBIOS reality: stock Vega ROMs (e.g. `113-D0500350`) ship with the PCIe ReBAR capability flags unset (GPU-Z: "GPU hardware support: Unsupported"). Verified paths: the trio lives in the GPU's class key and is read dynamically by the driver (the driver INF declares no ReBAR values), no other registry locations are involved.

## Изменения

- Статус «BAR выше 4 ГБ» теперь указывает на оба возможных блокера (BIOS **или** vBIOS карты), когда реестровый триплет на месте, а ReBAR не активен.
- README дополнен фактом про vBIOS: стоковые ROM Vega (например, `113-D0500350`) идут с невыставленными флагами ReBAR-возможности PCIe (GPU-Z: «GPU hardware support: Unsupported»). Пути проверены: триплет лежит в ключе класса видеокарты и читается драйвером динамически (в INF драйвера нет ни одного rebar-значения), других реестровых мест нет.
