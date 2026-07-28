<#
.SYNOPSIS
    Session de mesure du TTK du boss de fin, jouée par un HUMAIN.

.DESCRIPTION
    Le bot `tools/boss_ttk_test.py` kite en cercle : ses relevés ne valent pas validation
    d'équilibrage (GDD §20.2, cible ~20-30 s pour un build de référence). Ce script fait le
    chronométrage à la place du testeur.

    Il lance le jeu avec le loadout de référence et le boss isolé (`--debug-boss`) sur le biome
    demandé, attend que tu fermes la fenêtre, puis affiche le relevé écrit par `BossTelemetry`
    dans `user://boss_ttk.log` (apparition, 1er dégât, bascules de phase, TTK, DPS moyen).

    Le combat commence dès le lancement : joue-le normalement, ne kite pas artificiellement,
    et ferme la fenêtre quand le boss est mort (ou quand tu meurs — le relevé est écrit aussi).

.PARAMETER Biome
    Biome à jouer : sanctuaire, aether, givre, fournaise, neon. Détermine l'incarnation du boss
    ET le palier de menace (donc ses PV). Défaut : sanctuaire.

.PARAMETER All
    Enchaîne les 5 biomes dans l'ordre de déblocage, une fenêtre après l'autre.

.PARAMETER Real
    Run normale (sans `--debug-boss`) : tu joues les ~13 minutes et construis ton propre build.
    Mesure la plus fidèle, mais la plus longue.

.PARAMETER Invuln
    Ajoute `--invuln` : le TTK reste valide, la survivabilité n'est plus mesurée. À n'utiliser
    que pour observer les trois phases sans mourir, jamais pour valider l'équilibrage.

.PARAMETER ReportOnly
    N'ouvre pas le jeu : affiche seulement le tableau des combats déjà enregistrés.

.EXAMPLE
    .\tools\boss_ttk_session.ps1 -Biome neon
.EXAMPLE
    .\tools\boss_ttk_session.ps1 -All
.EXAMPLE
    .\tools\boss_ttk_session.ps1 -ReportOnly
#>
[CmdletBinding()]
param(
    [ValidateSet('sanctuaire', 'aether', 'givre', 'fournaise', 'neon')]
    [string]$Biome = 'sanctuaire',
    [switch]$All,
    [switch]$Real,
    [switch]$Invuln,
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'

$Godot   = 'C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe'
$Project = Split-Path -Parent $PSScriptRoot
$LogPath = Join-Path $env:APPDATA 'Godot\app_userdata\Chimera Protocol\boss_ttk.log'

# Ordre de déblocage = ordre des paliers de menace (LevelThreat.Order).
$AllBiomes = @('sanctuaire', 'aether', 'givre', 'fournaise', 'neon')

function Show-Report {
    if (-not (Test-Path $LogPath)) {
        Write-Host "Aucun relevé : $LogPath n'existe pas encore." -ForegroundColor Yellow
        return
    }

    # -Encoding UTF8 : le journal est écrit en UTF-8 par Godot, la lecture ANSI par défaut de
    # PowerShell 5.1 mutilerait tous les accents.
    $rows = @()
    foreach ($line in (Get-Content $LogPath -Encoding UTF8 | Where-Object { $_ -like 'CSV;*' })) {
        $f = $line.Split(';')
        if ($f.Count -lt 11) { continue }
        # Les nombres du journal sont en culture invariante (point décimal) : les parser avec la
        # culture FR du poste transformerait « 44.3 » en 443.
        $inv = [Globalization.CultureInfo]::InvariantCulture
        $rows += [pscustomobject]@{
            Date       = $f[1]
            Biome      = $f[2]
            Palier     = [int]$f[3]
            Difficulte = $f[4]
            PV         = [double]::Parse($f[5], $inv)
            Duree      = [double]::Parse($f[6], $inv)
            DPS        = [double]::Parse($f[7], $inv)
            PhaseII    = $f[8]
            PhaseIII   = $f[9]
            Issue      = $f[10]
        }
    }

    if ($rows.Count -eq 0) {
        Write-Host 'Aucun combat enregistré pour le moment.' -ForegroundColor Yellow
        return
    }

    Write-Host ''
    Write-Host '=== Combats enregistrés (cible GDD §20.2 : TTK ~20-30 s) ===' -ForegroundColor Cyan
    $rows | Format-Table -AutoSize

    $kills = $rows | Where-Object { $_.Issue -eq 'kill' }
    if ($kills.Count -gt 0) {
        $avg = ($kills | Measure-Object -Property Duree -Average).Average
        Write-Host ("TTK moyen sur {0} victoire(s) : {1:N1} s" -f $kills.Count, $avg) -ForegroundColor Cyan
    }
}

function Start-Session {
    param([string]$BiomeId)

    # Un second Godot en parallèle sature le CPU : les deltas s'allongent, les projectiles
    # traversent le boss sans le toucher et le DPS mesuré s'effondre (relevé constaté à 271 DPS
    # au lieu de 628 sur le même biome). Une mesure n'est valable que seule sur la machine.
    $running = Get-Process -Name 'Godot*' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host 'ATTENTION : une autre instance de Godot tourne déjà — le relevé sera faussé.' -ForegroundColor Red
        Write-Host '           Ferme-la avant de mesurer.' -ForegroundColor Red
    }

    $userArgs = @("--biome=$BiomeId")
    if (-not $Real)  { $userArgs += '--debug-boss' }
    if ($Invuln)     { $userArgs += '--invuln' }

    Write-Host ''
    Write-Host "--- Biome $BiomeId : $($userArgs -join ' ')" -ForegroundColor Cyan
    if ($Real) {
        Write-Host '    Run normale : le boss arrive vers 13 min. Ferme la fenêtre après le combat.'
    } else {
        Write-Host '    Boss isolé + loadout de référence. Joue le combat, puis ferme la fenêtre.'
    }

    $gameArgs = @('--path', $Project, '--rendering-driver', 'd3d12', 'res://scenes/Game.tscn', '--') + $userArgs
    $proc = Start-Process -FilePath $Godot -ArgumentList $gameArgs -PassThru
    $proc.WaitForExit()
}

if ($ReportOnly) {
    Show-Report
    return
}

if (-not (Test-Path $Godot)) {
    throw "Godot introuvable : $Godot"
}

$targets = @($Biome)
if ($All) { $targets = $AllBiomes }

foreach ($b in $targets) { Start-Session -BiomeId $b }

Show-Report
Write-Host ''
Write-Host "Journal complet : $LogPath"
