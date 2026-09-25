# Vega-ReBAR-Fix v1.5.1

## Corrected

- **Stock vBIOS is NOT treated as a blocker anymore.** The original Guru3D unlock (first post) states the trio works on **100 % stock motherboard BIOS + stock vBIOS** (tested by the author on Ryzen 5 5600 + B550M with an RX 470), and GPU-Z's "GPU hardware support: Unsupported" row is explicitly ignorable. Known stock ROM IDs (`113-D050…` / `113-D046…`) now get the "not determined" (orange) verdict pointing at the board BIOS first; a resized BAR remains the only hard evidence.
- Deep search confirmed: the Guru3D "REBAR Legacy On.reg" contains exactly the trio (`KMD_EnableReBarForLegacyASIC`, `KMD_RebarControlMode`, `KMD_RebarControlSupport` = 1) — **no fourth flag exists**; the ReBarUEFI discussion #274 (Vega 56 success) lists the same three values only.

---

# Vega-ReBAR-Fix v1.5.1

## Исправлено

- **Стоковый vBIOS больше не считается блокером.** Автор оригинального Guru3D-unlock (первый пост) пишет, что триплет работает на **100 % стоковых BIOS платы и vBIOS** (у автора Ryzen 5 5600 + B550M и RX 470), а строку «GPU hardware support: Unsupported» в GPU-Z можно игнорировать. Известные стоковые ROM (`113-D050…` / `113-D046…`) теперь получают вердикт «не определён» (оранжевый) с отсылкой к BIOS платы; ресайзнутый BAR — единственное железное доказательство.
- Глубокий поиск подтвердил: в «REBAR Legacy On.reg» с Guru3D ровно триплет (`KMD_EnableReBarForLegacyASIC`, `KMD_RebarControlMode`, `KMD_RebarControlSupport` = 1) — **четвёртого флага не существует**; обсуждение ReBarUEFI #274 (успех на Vega 56) называет те же три значения.
