# Vega-ReBAR-Fix v1.1.0

## Fixes

- **The missing third flag: `KMD_EnableReBarForLegacyASIC=1`.** Without it the AMD driver silently ignores `KMD_RebarControlMode` / `KMD_RebarControlSupport` on Vega/Polaris — this is why the previous patch did not activate ReBAR even with correct values in the registry. The tool now always writes the full Guru3D trio (per [Guru3D](https://forums.guru3d.com) / [PCGamingWiki](https://www.pcgamingwiki.com/wiki/AMD_Radeon_Software)).
- **Multi-adapter systems**: when a Vega dGPU coexists with a Vega iGPU (APU), the tool now picks the adapter with the largest dedicated VRAM instead of the first AMD key found.
- **GUI alignment**: fixed status column layout (aligned rows, no clipped text), wider dialog, shortened status strings.

## Исправления

- **Недостающий третий флаг `KMD_EnableReBarForLegacyASIC=1`.** Без него драйвер AMD молча игнорирует `KMD_RebarControlMode` / `KMD_RebarControlSupport` на Vega/Polaris — из-за этого прежний патч не активировал ReBAR при верных значениях в реестре. Теперь утилита всегда пишет полный триплет (по [Guru3D](https://forums.guru3d.com) / [PCGamingWiki](https://www.pcgamingwiki.com/wiki/AMD_Radeon_Software)).
- **Системы с несколькими картами**: если рядом с Vega-дискреткой есть Vega-iGPU (APU), утилита выбирает адаптер с наибольшей собственной VRAM, а не первый попавшийся ключ AMD.
- **Выравнивание ГУИ**: исправлена колонка статусов (ровные строки, без обрезки текста), окно шире, тексты статусов компактнее.
