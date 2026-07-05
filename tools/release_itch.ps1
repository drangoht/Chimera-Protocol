<#
.SYNOPSIS
    Publie Chimera Protocol sur itch.io via Butler (auto-update natif de l'app itch).

.DESCRIPTION
    Enchaine : export release Godot .NET -> dossier de distribution propre
    (ChimeraProtocol.exe + data_ChimeraProtocol_windows_x86_64) -> `butler push`
    versionne vers le channel Windows. Un seul push couvre les deux publics :
      - joueurs de l'app itch.io : mise a jour AUTOMATIQUE (detection + patch
        differentiel wharf + relance), sans aucun code dans le jeu ;
      - joueurs qui telechargent le ZIP depuis la page web : itch reconditionne
        l'upload en telechargement direct.

    La version est lue depuis project.godot (application/config/version) sauf si
    -Version est passe explicitement. Incremente-la avant chaque release.

.PARAMETER Version
    Numero de version affiche sur itch (ex. 1.1.0). Defaut : valeur de project.godot.

.PARAMETER Channel
    Channel Butler. Defaut : "windows".

.PARAMETER Itch
    Cible itch "user/game-slug". Defaut : "drangoht/chimera-protocol".
    IMPORTANT : le game-slug doit correspondre EXACTEMENT a l'URL de ta page itch
    (itch.io/<user>/<game-slug>). A ajuster si ta page a un autre slug.

.PARAMETER SkipExport
    Reutilise le build existant dans build/ au lieu de re-exporter (debug/rapidite).

.EXAMPLE
    powershell -File tools/release_itch.ps1 -Version 1.1.0
#>
param(
    [string]$Version,
    [string]$Channel = "windows",
    [string]$Itch    = "drangoht/chimera-protocol",
    [switch]$SkipExport
)

# NB : PAS "Stop" — Godot et Butler ecrivent leur progression sur stderr, ce que PS 5.1
# convertit en erreurs terminantes sous "Stop" (fausse la detection de $LASTEXITCODE).
# On verifie explicitement les codes de sortie a la place.
$ErrorActionPreference = "Continue"
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$Godot   = "C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
$BuildDir = Join-Path $ProjectRoot "build"
$Exe      = Join-Path $BuildDir "ChimeraProtocol.exe"
$DataDir  = Join-Path $BuildDir "data_ChimeraProtocol_windows_x86_64"
$Staging  = Join-Path $BuildDir "dist_windows"

function Fail($msg) { Write-Host "ERREUR : $msg" -ForegroundColor Red; exit 1 }

# --- 0. Localise Butler (fourni par l'app itch.io, dossier broth versionne) ---------
$brothGlob = Join-Path $env:APPDATA "itch\broth\butler\versions\*\butler.exe"
$butler = Get-ChildItem -Path $brothGlob -ErrorAction SilentlyContinue |
          Sort-Object { [version]($_.Directory.Name) } -ErrorAction SilentlyContinue |
          Select-Object -Last 1
if (-not $butler) {
    Fail "butler.exe introuvable. Installe/lance l'app itch.io une fois, ou telecharge butler depuis https://itchio.itch.io/butler"
}
$Butler = $butler.FullName
Write-Host "Butler : $Butler" -ForegroundColor Cyan

# --- 1. Version (project.godot si non fournie) --------------------------------------
if (-not $Version) {
    $line = Select-String -Path (Join-Path $ProjectRoot "project.godot") -Pattern '^config/version="(.+)"'
    if ($line) { $Version = $line.Matches[0].Groups[1].Value }
    if (-not $Version) { Fail "Version absente : passe -Version x.y.z ou ajoute config/version a project.godot" }
}
Write-Host "Version : $Version  ->  $Itch`:$Channel" -ForegroundColor Cyan

# --- 1b. Tampon de build (SHA du commit publie, affiche bas-droite + statut Discord) --
& (Join-Path $PSScriptRoot "gen_build_info.ps1")

# --- 2. Export release Godot .NET ---------------------------------------------------
if (-not $SkipExport) {
    # Le .sln est CRITIQUE pour l'export .NET (sans lui, l'exe crashe au lancement).
    if (-not (Test-Path (Join-Path $ProjectRoot "ChimeraProtocol.sln"))) {
        Fail "ChimeraProtocol.sln absent a la racine — recree-le (cf. CLAUDE.md) avant d'exporter."
    }
    Write-Host "Export release en cours..." -ForegroundColor Yellow
    & $Godot --headless --export-release "Windows Desktop" $Exe
    # Godot 4.7 .NET laisse souvent $LASTEXITCODE VIDE/null en fin d'export headless
    # ($null -ne 0 -> faux echec). On ne fail que sur un code non-zero EXPLICITE ; l'existence
    # de l'exe + du runtime .NET est verifiee juste apres (garde-fou reel contre un echec).
    # NB : pas de comparaison de timestamp — Godot rend la main a PowerShell avant d'avoir
    # flush l'exe (course), ce qui provoquait un faux "echec silencieux".
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { Fail "Export Godot echoue (code $LASTEXITCODE)" }
}
if (-not (Test-Path $Exe))     { Fail "Exe manquant : $Exe" }
if (-not (Test-Path $DataDir)) { Fail "Runtime .NET manquant : $DataDir" }

# --- 3. Dossier de distribution propre (exe + runtime uniquement) -------------------
# Butler diffe au niveau fichier : on pousse un DOSSIER (pas un zip), sans les
# artefacts parasites de build/ (covers, screenshots, anciens zips).
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item -ItemType Directory -Path $Staging | Out-Null
Copy-Item $Exe -Destination $Staging
Copy-Item $DataDir -Destination $Staging -Recurse
Write-Host "Staging pret : $Staging" -ForegroundColor Cyan

# --- 4. Push Butler (versionne) -----------------------------------------------------
Write-Host "Push vers itch.io..." -ForegroundColor Yellow
& $Butler push $Staging "$Itch`:$Channel" --userversion $Version
if ($LASTEXITCODE -ne 0) {
    Fail "butler push echoue (code $LASTEXITCODE). Si 'not authorized', lance une fois : `"$Butler`" login"
}

# --- 4b. Manifeste de version (bandeau "nouvelle version" cote jeu) ------------------
# Les joueurs qui telechargent le ZIP via le web n'ont PAS l'auto-update de l'app itch.
# Le menu principal lit ce fichier sur raw.githubusercontent pour afficher un bandeau
# "nouvelle version dispo -> itch.io". On le regenere et on le pousse sur GitHub (main).
$parts   = $Itch.Split("/")
$itchUrl = "https://$($parts[0]).itch.io/$($parts[1])"
$manifest = [ordered]@{ version = $Version; url = $itchUrl }
$manifestPath = Join-Path $ProjectRoot "version.json"
($manifest | ConvertTo-Json) | Out-File -FilePath $manifestPath -Encoding utf8
Write-Host "version.json regenere : $Version" -ForegroundColor Cyan

Push-Location $ProjectRoot
git add version.json
# Ne commit/push que si version.json a reellement change.
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    git commit -m "chore(release): version.json -> $Version (bandeau MAJ web)"
    if ($?) {
        git push
        if (-not $?) { Write-Host "AVERTISSEMENT : git push echoue — pousse version.json a la main pour activer le bandeau." -ForegroundColor Yellow }
    }
} else {
    Write-Host "version.json inchange — rien a pousser." -ForegroundColor DarkGray
}
Pop-Location

# --- 5. Etat des channels -----------------------------------------------------------
& $Butler status $Itch
Write-Host "`nPublication OK — version $Version poussee sur $Itch`:$Channel" -ForegroundColor Green
Write-Host "Les joueurs de l'app itch.io recevront la mise a jour automatiquement." -ForegroundColor Green
