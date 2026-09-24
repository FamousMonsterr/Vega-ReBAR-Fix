# Vega-ReBAR-Fix: enables AMD ReBAR registry flags for the first AMD display adapter found.
# Run elevated. Result is written to $env:TEMP\vega_rebar_patch_result.txt
$ErrorActionPreference = 'Stop'
$log = @()

$classRoot = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
$target = $null

Get-ChildItem $classRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
    $mid = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).MatchingDeviceId
    if ($mid -and $mid -like 'PCI\VEN_1002*') {
        $desc = (Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue).DriverDesc
        if ($desc -and $desc -notlike '*Remote Display*') { $script:target = $_.PSPath; $script:targetName = $_.PSChildName }
    }
}

if (-not $target) { throw "AMD display adapter class key not found under $classRoot" }

$log += "Target key: $targetName"

$old = Get-ItemProperty $target
$log += "Before: KMD_RebarControlMode=$($old.KMD_RebarControlMode) KMD_RebarControlSupport=$($old.KMD_RebarControlSupport)"

Set-ItemProperty $target -Name 'KMD_RebarControlMode'    -Type DWord -Value 1
Set-ItemProperty $target -Name 'KMD_RebarControlSupport' -Type DWord -Value 1

$new = Get-ItemProperty $target
$ok = ($new.KMD_RebarControlMode -eq 1) -and ($new.KMD_RebarControlSupport -eq 1)
$log += "After:  KMD_RebarControlMode=$($new.KMD_RebarControlMode) KMD_RebarControlSupport=$($new.KMD_RebarControlSupport)"
$log += if ($ok) { 'RESULT: OK' } else { 'RESULT: FAILED' }
$log | Out-File "$env:TEMP\vega_rebar_patch_result.txt" -Encoding utf8
exit $(if ($ok) { 0 } else { 1 })
