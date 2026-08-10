# run-rules-tests.ps1 — hook PostToolUse : rejoue les tests unitaires quand la logique
# pure de unity/Assets/Scripts/Shared/ (ou les tests eux-memes) vient d'etre modifiee.
# Tourne en asynchrone ; sort en code 2 si les tests cassent, ce qui reveille
# Claude avec le detail de l'echec.

$ErrorActionPreference = 'Stop'

$raw = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($raw)) { exit 0 }

try { $data = $raw | ConvertFrom-Json } catch { exit 0 }

$path = $data.tool_input.file_path
if (-not $path) { $path = $data.tool_response.filePath }
if ([string]::IsNullOrWhiteSpace($path)) { exit 0 }

$norm = $path -replace '/', '\'
if ($norm -notmatch '(?i)\\unity\\Assets\\Scripts\\Shared\\.*\.cs$' -and
    $norm -notmatch '(?i)\\tests\\.*\.cs$') {
    exit 0
}

$projectRoot = 'C:\CODE\JEUX\chimera-protocol'
$csproj = Join-Path $projectRoot 'tests\ChimeraProtocol.Tests.csproj'
if (-not (Test-Path $csproj)) { exit 0 }

$output = & dotnet test $csproj --nologo --verbosity quiet 2>&1 | Out-String

if ($LASTEXITCODE -ne 0) {
    $tail = ($output -split "`n" | Select-Object -Last 25) -join "`n"
    [Console]::Error.WriteLine("Les tests unitaires echouent apres modification de $norm :`n$tail")
    exit 2
}

exit 0
