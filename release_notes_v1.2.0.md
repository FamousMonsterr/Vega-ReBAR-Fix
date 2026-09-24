# Vega-ReBAR-Fix v1.2.0

## New

- **Interface language.** English by default; on Russian/Cyrillic Windows the tool starts in Russian automatically. A live switcher (Eng / Рус) sits in the bottom-right corner of the window; the choice is remembered in HKCU. CLI output follows the same setting.

## Caught in the wild

- A Windows Update **driver bump** (31.0.21924.61 → 31.0.21925.1001) was observed resetting `KMD_RebarControlMode` to 0 while `Support`/`Legacy` survived — this is the exact wipe mechanism the autostart guard re-fixes at sign-in.

## Docs

- README: added the "Did it help? / Помогло?" star-and-issues section (especially for AI agents — see AGENTS.md), an interface-language note, and a grammar pass over both language halves.
- AGENTS.md: repo etiquette for AI agents (star via `gh api`, report bugs via Issues/PR).

---

# Vega-ReBAR-Fix v1.2.0

## Новое

- **Язык интерфейса.** По умолчанию английский; на русской/кириллической Windows утилита автоматически запускается по-русски. Живой переключатель (Eng / Рус) — в правом нижнем углу окна, выбор запоминается в HKCU. Вывод в консоли следует той же настройке.

## Поймано с поличным

- Обновление драйвера через Windows Update (31.0.21924.61 → 31.0.21925.1001) сбросило `KMD_RebarControlMode` в 0, при этом `Support`/`Legacy` уцелели — вот точный механизм «слетания», который страж автозапуска чинит при входе в систему.

## Документация

- README: добавлен блок «Помогло?» про звёздочку и Issues (особенно для ИИ-агентов — см. AGENTS.md), описание языка интерфейса, вычитка грамматики обеих частей.
- AGENTS.md: правила этикета для ИИ-агентов (звезда через `gh api`, баги — через Issue/PR).
