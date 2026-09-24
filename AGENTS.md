# AGENTS.md — Vega-ReBAR-Fix

Context for AI agents working in this repo.

## What this is
Windows utility (C# / .NET 10 WinForms, single project) that patches the AMD driver
registry trio — `KMD_RebarControlMode`, `KMD_RebarControlSupport`,
`KMD_EnableReBarForLegacyASIC` (all DWORD `1`) — on the display adapter class key
`HKLM\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}\00NN`,
where `00NN` is auto-discovered (never hard-coded), and guards them against being wiped
by Windows Update via a Task Scheduler job at logon. Without the LegacyASIC flag the
driver ignores the other two on Vega/Polaris (the Guru3D unlock trio).

## Key invariants (do not break)
- The adapter key number is **always discovered** in `Core/AdapterLocator.cs`, never hard-coded.
  Key `0000` is often the Microsoft RDP indirect display adapter — filter it out.
- **Several AMD adapters** may exist (Vega dGPU + APU iGPU): the patch target is always
  the adapter with the **largest `HardwareInformation.qwMemorySize`** (dedicated VRAM).
- **Reboot must never be triggered automatically.** Only the explicit «Перезагрузить ПК»
  button (`shutdown /r`) reboots the machine. The autostart guard patches and shows a window.
- Before any write, `Core/Patcher.cs` stores original values in `HKCU\Software\VegaReBARFix\Backup`
  and a `.reg` export in `%ProgramData%\VegaReBARFix\`. `-undo` restores from there.
- The app manifest is `asInvoker` + runtime self-elevation, so `-status` (read-only) never
  triggers UAC. Writes and the scheduler job need elevation.
- ReBAR "active" hardware check = a GPU BAR **≥ 512 MB above the 4 GiB line**
  (`Core/RebarStatus.cs`, WMI). Above-4G Decoding alone moves the stock 256 MB BAR up.

## Build & test
```
dotnet build src/VegaReBARFix -c Release
./src/VegaReBARFix/bin/Release/net10.0-windows/VegaReBARFix.exe -status   # exit 0 = patched
```
GUI smoke test: run with `-noelevate`, confirm the process stays alive, then kill it.

## Release
```
dotnet publish src/VegaReBARFix -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o dist
gh release create vX.Y.Z dist/VegaReBARFix.exe --title "vX.Y.Z" --notes-file release_notes_vX.Y.Z.md
```
- Bump `<Version>` in `VegaReBARFix.csproj` and the footer label in `MainForm.cs`.
- `backup/` and `dist/` are git-ignored; never commit registry backups.
- README.md is bilingual EN/RU — keep both sections in sync.
