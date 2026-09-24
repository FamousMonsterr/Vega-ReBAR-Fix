# Vega-ReBAR-Fix: enables the AMD ReBAR registry flag trio for the AMD display
# adapter with the largest dedicated VRAM (the discrete card, even when an APU
# iGPU is present). Run elevated. Result goes to $env:TEMP\vega_rebar_patch_result.txt
$ErrorActionPreference = 'Stop'
$log = @()

$classRoot = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
$target = $null
$targetName = $null
$targetVram = -1

Get-ChildItem $classRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if ($p.MatchingDeviceId -and $p.MatchingDeviceId -like 'PCI\VEN_1002*' `
        -and $p.DriverDesc -and $p.DriverDesc -notlike '*Remote Display*')
    {
        $vram = 0
        if ($p.'HardwareInformation.qwMemorySize' -is [long]) { $vram = $p.'HardwareInformation.qwMemorySize' }
        if ($vram -ge $targetVram) { $script:target = $_.PSPath; $script:targetName = $_.PSChildName; $script:targetVram = $vram }
    }
}

if (-not $target) { throw "AMD display adapter class key not found under $classRoot" }

$log += "Target key: $targetName (VRAM: $([math]::Round($targetVram/1MB)) MB)"

$old = Get-ItemProperty $target
$log += "Before: Mode=$($old.KMD_RebarControlMode) Support=$($old.KMD_RebarControlSupport) LegacyASIC=$($old.KMD_EnableReBarForLegacyASIC)"

Set-ItemProperty $target -Name 'KMD_RebarControlMode'          -Type DWord -Value 1
Set-ItemProperty $target -Name 'KMD_RebarControlSupport'       -Type DWord -Value 1
Set-ItemProperty $target -Name 'KMD_EnableReBarForLegacyASIC'  -Type DWord -Value 1

$new = Get-ItemProperty $target
$ok = ($new.KMD_RebarControlMode -eq 1) -and ($new.KMD_RebarControlSupport -eq 1) -and ($new.KMD_EnableReBarForLegacyASIC -eq 1)
$log += "After:  Mode=$($new.KMD_RebarControlMode) Support=$($new.KMD_RebarControlSupport) LegacyASIC=$($new.KMD_EnableReBarForLegacyASIC)"
$log += if ($ok) { 'RESULT: OK' } else { 'RESULT: FAILED' }
$log | Out-File "$env:TEMP\vega_rebar_patch_result.txt" -Encoding utf8
exit $(if ($ok) { 0 } else { 1 })
