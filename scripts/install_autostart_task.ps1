# Creates the VegaReBARFix autostart logon task (highest privileges, no UAC at logon).
$ErrorActionPreference = 'Stop'
$log = @()
$exe = 'C:\Users\famou\Tools\VegaReBARFix\VegaReBARFix.exe'

if (-not (Test-Path $exe)) { throw "Exe not found: $exe" }

schtasks /Create /F /TN 'VegaReBARFix_Autostart' /TR "`"$exe`" -autostart" /SC ONLOGON /RL HIGHEST | Out-Null
$rc = $LASTEXITCODE
$log += "schtasks create rc=$rc"

$q = schtasks /Query /TN 'VegaReBARFix_Autostart' 2>&1 | Out-String
$log += $q.Trim()

$log += if ($rc -eq 0) { 'RESULT: OK' } else { 'RESULT: FAILED' }
$log | Out-File "$env:TEMP\vega_rebar_task_result.txt" -Encoding utf8
