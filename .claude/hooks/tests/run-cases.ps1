$guard = 'C:\CODE\JEUX\chimera-protocol\.claude\hooks\guard.ps1'
$cases = Get-Content 'C:\CODE\JEUX\chimera-protocol\.claude\hooks\tests\cases.txt'
$fails = 0
foreach ($line in $cases) {
    if (-not $line.Trim()) { continue }
    $parts = $line -split '\|', 3
    $id = $parts[0]; $expected = $parts[1] -replace 'attendu=', ''
    $json = $parts[2] -replace '\\n', "`n"
    $out = $json | & powershell -NoProfile -ExecutionPolicy Bypass -File $guard | Out-String
    $actual = if ($out -match 'permissionDecision') { 'DENY' } else { 'OK' }
    $verdict = if ($actual -eq $expected) { 'PASS' } else { $fails++; 'ECHEC' }
    Write-Host ("{0} cas {1} : attendu={2} obtenu={3}" -f $verdict, $id, $expected, $actual)
}
Write-Host "---"
Write-Host "$fails echec(s)"
