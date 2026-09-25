# Vega-ReBAR-Fix: enables the AMD ReBAR registry flag trio on EVERY AMD display
# adapter class key (the same physical card can own several keys after a driver
# reinstall or a Dual-BIOS switch — the trio is inert on stale keys).
# Run elevated. Result goes to $env:TEMP\vega_rebar_patch_result.txt
$ErrorActionPreference = 'Stop'
$log = @()

$classRoot = 'HKLM:\SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}'
$targets = @()

Get-ChildItem $classRoot -ErrorAction SilentlyContinue | Where-Object { $_.PSChildName -match '^\d{4}$' } | ForEach-Object {
    $p = Get-ItemProperty $_.PSPath -ErrorAction SilentlyContinue
    if ($p.MatchingDeviceId -and $p.MatchingDeviceId -like 'PCI\VEN_1002*' `
        -and $p.DriverDesc -and $p.DriverDesc -notlike '*Remote Display*')
    {
        $targets += @{ Path = $_.PSPath; Name = $_.PSChildName; Mid = $p.MatchingDeviceId }
    }
}

if ($targets.Count -eq 0) { throw "AMD display adapter class key not found under $classRoot" }
$log += "AMD adapter keys found: $($targets.Count)"

$okAll = $true
foreach ($t in $targets) {
    $old = Get-ItemProperty $t.Path
    Set-ItemProperty $t.Path -Name 'KMD_RebarControlMode'          -Type DWord -Value 1
    Set-ItemProperty $t.Path -Name 'KMD_RebarControlSupport'       -Type DWord -Value 1
    Set-ItemProperty $t.Path -Name 'KMD_EnableReBarForLegacyASIC'  -Type DWord -Value 1
    $new = Get-ItemProperty $t.Path
    $ok = ($new.KMD_RebarControlMode -eq 1) -and ($new.KMD_RebarControlSupport -eq 1) -and ($new.KMD_EnableReBarForLegacyASIC -eq 1)
    $okAll = $okAll -and $ok
    $log += ("{0} [{1}]: before M={2} S={3} L={4} -> after M={5} S={6} L={7} : {8}" -f `
        $t.Name, $t.Mid, $old.KMD_RebarControlMode, $old.KMD_RebarControlSupport, $old.KMD_EnableReBarForLegacyASIC, `
        $new.KMD_RebarControlMode, $new.KMD_RebarControlSupport, $new.KMD_EnableReBarForLegacyASIC, $(if ($ok) { 'OK' } else { 'FAILED' }))
}

$log += if ($okAll) { 'RESULT: OK' } else { 'RESULT: FAILED' }
$log | Out-File "$env:TEMP\vega_rebar_patch_result.txt" -Encoding utf8
exit $(if ($okAll) { 0 } else { 1 })
