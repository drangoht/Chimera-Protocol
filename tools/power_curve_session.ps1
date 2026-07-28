<#
.SYNOPSIS
    Session de mesure de la COURBE DE PUISSANCE d'une run, jouée par un HUMAIN.

.DESCRIPTION
    Complément de `boss_ttk_session.ps1` : celui-ci ne relève qu'un instantané par combat de boss,
    alors que la question ouverte porte sur l'ÉVOLUTION du rapport puissance/menace — le DPS mesuré
    contre le boss passe de 617 à 2322 en deux minutes d'overtime alors que toutes les cartes sont
    déjà au plafond L20 (cf. docs/TEST_REPORT.md, run du 2026-07-28).

    Le script lance une run normale avec `--power-curve` : `PowerTelemetry` échantillonne toutes les
    15 s de jeu (DPS infligé, dégâts subis, population d'ennemis, indice de puissance du loadout,
    build complet) dans `user://power_curve.log`, écrit au fil de l'eau. Joue normalement, pousse
    l'overtime aussi loin que possible, puis meurs ou ferme la fenêtre : le journal est déjà sur
    disque dans les deux cas.

    Le tableau affiché en fin de session résume l'écart entre ce que le joueur inflige et ce que le
    contenu lui oppose, minute par minute.

.PARAMETER Biome
    Biome à jouer : sanctuaire, aether, givre, fournaise, neon. Détermine le palier de menace.
    Défaut : fournaise (celui du relevé de référence).

.PARAMETER Bench
    Banc automatisé au lieu d'une session jouée : `--headless --auto-play --invuln --timescale=3`.
    Le bot ne se déplace PAS — sans `--invuln` il meurt en 20 s, et avec, la colonne des dégâts
    subis vaut zéro. Le DPS y est un DPS de terrain (il monte avec la population d'ennemis) : seule
    la colonne `indice_puissance` y est comparable dans le temps. Utile pour dégrossir, jamais pour
    trancher un équilibrage.

.PARAMETER Minutes
    Durée maximale de la run en minutes de jeu (`--run-limit`). Défaut : 25 (13 min + 12 d'overtime).
    En session jouée, la limite ne sert que de garde-fou.

.PARAMETER ReportOnly
    N'ouvre pas le jeu : affiche seulement le dernier relevé enregistré.

.EXAMPLE
    .\tools\power_curve_session.ps1 -Biome fournaise
.EXAMPLE
    .\tools\power_curve_session.ps1 -Bench -Biome neon
.EXAMPLE
    .\tools\power_curve_session.ps1 -ReportOnly
#>
[CmdletBinding()]
param(
    [ValidateSet('sanctuaire', 'aether', 'givre', 'fournaise', 'neon')]
    [string]$Biome = 'fournaise',
    [switch]$Bench,
    [int]$Minutes = 25,
    [switch]$ReportOnly
)

$ErrorActionPreference = 'Stop'

$Godot   = 'C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe'
$Project = Split-Path -Parent $PSScriptRoot
$LogPath = Join-Path $env:APPDATA 'Godot\app_userdata\Chimera Protocol\power_curve.log'

function Show-Report {
    if (-not (Test-Path $LogPath)) {
        Write-Host "Aucun relevé : $LogPath n'existe pas encore." -ForegroundColor Yellow
        return
    }

    # -Encoding UTF8 : le journal est écrit en UTF-8 par Godot ; la lecture ANSI par défaut de
    # PowerShell 5.1 mutilerait tous les accents.
    $lines = Get-Content $LogPath -Encoding UTF8

    # Dernier relevé seulement : le fichier s'accumule d'une session à l'autre.
    $starts = @()
    for ($i = 0; $i -lt $lines.Count; $i++) {
        if ($lines[$i] -like '=== Courbe de puissance*') { $starts += $i }
    }
    if ($starts.Count -eq 0) {
        Write-Host 'Aucun relevé exploitable dans le journal.' -ForegroundColor Yellow
        return
    }
    $lines = $lines[$starts[-1]..($lines.Count - 1)]

    $inv  = [Globalization.CultureInfo]::InvariantCulture
    $rows = @()
    foreach ($line in $lines) {
        if ($line -like '#*' -or $line -like '===*' -or $line -like 't_s;*' -or $line.Trim() -eq '') { continue }
        $f = $line.Split(';')
        if ($f.Count -lt 16) { continue }
        $rows += [pscustomobject]@{
            'min'       = [math]::Round([double]::Parse($f[0], $inv) / 60, 1)
            'phase'     = $f[1]
            'niv'       = [int]$f[2]
            'puissance' = [int]$f[3]
            'dps'       = [int]$f[4]
            'subis/s'   = [double]::Parse($f[5], $inv)
            'kills'     = [int]$f[6]
            'ennemis'   = [int]$f[7]
            'greffes'   = $f[15]
        }
    }

    if ($rows.Count -eq 0) {
        Write-Host 'Relevé vide (la run ne semble pas avoir démarré).' -ForegroundColor Yellow
        return
    }

    Write-Host ''
    foreach ($h in ($lines | Where-Object { $_ -like '# version*' -or $_ -like '# --*' })) {
        Write-Host $h -ForegroundColor DarkGray
    }
    Write-Host '=== Courbe de puissance (dernier relevé) ===' -ForegroundColor Cyan
    $rows | Format-Table -AutoSize

    # Ce qui intéresse le design : de combien la puissance grimpe APRÈS le passage en overtime.
    $run = $rows | Where-Object { $_.phase -eq 'run' }
    $ot  = $rows | Where-Object { $_.phase -eq 'OT'  }
    if ($run.Count -gt 0 -and $ot.Count -gt 0) {
        $lastRun = $run[-1]
        $lastOt  = $ot[-1]
        $ratioP  = if ($lastRun.puissance -gt 0) { $lastOt.puissance / $lastRun.puissance } else { 0 }
        $ratioE  = if ($lastRun.ennemis   -gt 0) { $lastOt.ennemis   / $lastRun.ennemis   } else { 0 }
        Write-Host ('Entrée en overtime → fin de run : puissance ×{0:N2} · population ×{1:N2}' -f $ratioP, $ratioE) -ForegroundColor Cyan
    }
}

function Start-Session {
    # Un second Godot en parallèle sature le CPU : les deltas s'allongent et toute mesure de DPS
    # devient fausse (cf. boss_ttk_session.ps1).
    $running = Get-Process -Name 'Godot*' -ErrorAction SilentlyContinue
    if ($running) {
        Write-Host 'ATTENTION : une autre instance de Godot tourne déjà — le relevé sera faussé.' -ForegroundColor Red
    }

    $userArgs = @("--biome=$Biome", '--power-curve', "--run-limit=$($Minutes * 60)")
    $engArgs  = @('--path', $Project)

    if ($Bench) {
        $userArgs += @('--auto-play', '--invuln', '--timescale=3')
        $engArgs  += '--headless'
        Write-Host ''
        Write-Host "--- Banc automatisé, biome $Biome, $Minutes min de jeu (~$([math]::Round($Minutes/3,1)) min réelles)" -ForegroundColor Cyan
        Write-Host '    Bot immobile et invulnérable : seule la colonne « puissance » est comparable.'
    } else {
        $engArgs += @('--rendering-driver', 'd3d12')
        Write-Host ''
        Write-Host "--- Session jouée, biome $Biome" -ForegroundColor Cyan
        Write-Host '    Joue normalement, pousse l''overtime le plus loin possible, puis meurs ou ferme la fenêtre.'
    }

    $gameArgs = $engArgs + @('res://scenes/Game.tscn', '--') + $userArgs
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

Start-Session
Show-Report
Write-Host ''
Write-Host "Journal complet : $LogPath"
