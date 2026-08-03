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

# DLL qui DOIVENT etre presentes dans le runtime .NET expedie (sinon build amputee).
# Godot 4.7 .NET rend la main a PowerShell AVANT que `dotnet publish` ait fini d'ecrire
# le runtime (course connue) -> un staging premature peut omettre des DLL sans erreur.
$CriticalDlls = @("ChimeraProtocol.dll", "GodotSharp.dll", "DiscordRPC.dll", "Newtonsoft.Json.dll")

# Attend que le dossier soit STABLE (nb de fichiers + taille totale inchanges sur
# plusieurs sondages consecutifs) — garde-fou contre la course d'ecriture de dotnet publish.
function Wait-DirStable($dir, $stableReads = 3, $intervalMs = 800, $timeoutSec = 90) {
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    $last = ""; $streak = 0
    while ((Get-Date) -lt $deadline) {
        $files = Get-ChildItem -Path $dir -Recurse -File -ErrorAction SilentlyContinue
        $sig = "{0}:{1}" -f $files.Count, (($files | Measure-Object -Property Length -Sum).Sum)
        if ($sig -eq $last) { $streak++ } else { $streak = 0; $last = $sig }
        if ($streak -ge $stableReads) {
            Write-Host "Runtime stable ($($files.Count) fichiers)." -ForegroundColor DarkGray
            return
        }
        Start-Sleep -Milliseconds $intervalMs
    }
    Write-Host "AVERTISSEMENT : stabilite du runtime non confirmee apres $timeoutSec s (on continue, la verif DLL tranchera)." -ForegroundColor Yellow
}

# Attend que l'exe ET l'assembly C# aient ete REECRITS depuis le debut de l'export.
#
# ⚠ Ce garde-fou existe parce que son absence a pousse une build PERIMEE en ligne (2026-08-03,
# release 1.26.0) : Godot rend la main immediatement, `Wait-DirStable` a constate un runtime
# "stable" AVANT que dotnet publish n'ait commence a ecrire, les DLL critiques du build precedent
# etaient toutes la — et butler a expedie le binaire de la version d'avant, sans une erreur. Le
# gameplay etait bon, mais l'exe s'annoncait 1.25.1 face a un version.json a 1.26.0 (bandeau MAJ
# perpetuel pour les joueurs web).
#
# La stabilite ne prouve rien : un artefact perime est parfaitement stable. Seule la FRAICHEUR
# distingue un export reussi d'un export fantome. On attend donc, au lieu de renoncer a verifier
# (ce que faisait le commentaire d'origine pour eviter un faux negatif) — et on echoue dur, car
# expedier l'ancienne version est strictement pire que ne rien expedier.
function Wait-FreshArtifacts($exe, $dataDir, $since, $timeoutSec = 1200) {
    $probe = Join-Path $dataDir "ChimeraProtocol.dll"   # ecrit par dotnet publish, le plus tardif
    $deadline = (Get-Date).AddSeconds($timeoutSec)
    while ((Get-Date) -lt $deadline) {
        $exeOk = (Test-Path $exe)   -and ((Get-Item $exe).LastWriteTime   -ge $since)
        $dllOk = (Test-Path $probe) -and ((Get-Item $probe).LastWriteTime -ge $since)
        if ($exeOk -and $dllOk) {
            Write-Host "Artefacts frais (exe + assembly reecrits pendant cet export)." -ForegroundColor DarkGray
            return
        }
        Start-Sleep -Milliseconds 1000
    }
    Fail ("Export fantome : l'exe ou l'assembly n'a pas ete reecrit en $timeoutSec s (exe = " +
          "$(if (Test-Path $exe) { (Get-Item $exe).LastWriteTime } else { 'absent' }), export lance a $since). " +
          "Rien n'a ete pousse. Verifie que le projet est indexe (--headless --path . --import) puis relance.")
}

# Verifie la presence des DLL critiques dans un dossier runtime donne ; Fail sinon.
function Assert-CriticalDlls($dataDir, $label) {
    foreach ($dll in $CriticalDlls) {
        if (-not (Test-Path (Join-Path $dataDir $dll))) {
            Fail "$dll absente de $label — export incomplet (course dotnet publish ?). Relance l'export."
        }
    }
    Write-Host "DLL critiques presentes ($label)." -ForegroundColor DarkGray
}

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
# On NE regenere PAS le tampon en -SkipExport : le binaire reutilise embarque deja le SHA
# du commit sur lequel il a ete exporte. Le reecrire ici sur le HEAD courant desynchroniserait
# le stamp source du binaire expedie (le tampon mentirait). Il n'est pertinent qu'avant un export.
if (-not $SkipExport) {
    & (Join-Path $PSScriptRoot "gen_build_info.ps1")
} else {
    Write-Host "SkipExport : tampon BuildInfo laisse tel quel (SHA du binaire reutilise)." -ForegroundColor DarkGray
}

# --- 2. Export release Godot .NET ---------------------------------------------------
if (-not $SkipExport) {
    # Le .sln est CRITIQUE pour l'export .NET (sans lui, l'exe crashe au lancement).
    if (-not (Test-Path (Join-Path $ProjectRoot "ChimeraProtocol.sln"))) {
        Fail "ChimeraProtocol.sln absent a la racine — recree-le (cf. CLAUDE.md) avant d'exporter."
    }
    # Un export lance sur un projet jamais indexe echoue SANS RIEN DIRE (code 0, exe intact).
    # Un --import prealable rend l'echec impossible a manquer : cf. docs/PITFALLS.md §Export .NET.
    Write-Host "Indexation du projet..." -ForegroundColor Yellow
    & $Godot --headless --path $ProjectRoot --import
    $exportStart = Get-Date
    Write-Host "Export release en cours..." -ForegroundColor Yellow
    & $Godot --headless --export-release "Windows Desktop" $Exe
    # Godot 4.7 .NET laisse souvent $LASTEXITCODE VIDE/null en fin d'export headless
    # ($null -ne 0 -> faux echec). On ne fail que sur un code non-zero EXPLICITE ; la FRAICHEUR
    # de l'exe + du runtime .NET est verifiee juste apres (garde-fou reel contre un export
    # fantome, cf. Wait-FreshArtifacts) — Godot rend la main bien avant la fin de dotnet publish.
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { Fail "Export Godot echoue (code $LASTEXITCODE)" }
}
if (-not (Test-Path $Exe))     { Fail "Exe manquant : $Exe" }
if (-not (Test-Path $DataDir)) { Fail "Runtime .NET manquant : $DataDir" }

# Garde-fou course dotnet publish : on n'attend/verifie que si on vient d'exporter.
# L'ordre compte — FRAICHEUR d'abord (les artefacts ont-ils ete reecrits ?), stabilite ensuite
# (l'ecriture est-elle finie ?). Sonder la stabilite en premier la constate sur les artefacts de
# la release PRECEDENTE, avant meme que l'export courant n'ait commence a ecrire : c'est ce qui a
# laisse passer la build perimee du 2026-08-03.
if (-not $SkipExport) {
    Wait-FreshArtifacts $Exe $DataDir $exportStart
    Wait-DirStable $DataDir
}
Assert-CriticalDlls $DataDir "runtime source"

# --- 3. Dossier de distribution propre (exe + runtime uniquement) -------------------
# Butler diffe au niveau fichier : on pousse un DOSSIER (pas un zip), sans les
# artefacts parasites de build/ (covers, screenshots, anciens zips).
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item -ItemType Directory -Path $Staging | Out-Null
Copy-Item $Exe -Destination $Staging
Copy-Item $DataDir -Destination $Staging -Recurse
# Re-verif APRES copie : ce qui part chez butler doit contenir les DLL critiques.
Assert-CriticalDlls (Join-Path $Staging (Split-Path -Leaf $DataDir)) "staging"
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
    # PIEGE PowerShell 5.1 : ne PAS tester $? apres un exe natif. git ecrit sa progression sur
    # stderr meme quand tout va bien, ce qui met $? a $false alors que le code retour vaut 0 —
    # le script criait donc "git push echoue" a chaque release reussie. Seul $LASTEXITCODE fait foi.
    if ($LASTEXITCODE -eq 0) {
        git push
        if ($LASTEXITCODE -ne 0) { Write-Host "AVERTISSEMENT : git push echoue (code $LASTEXITCODE) — pousse version.json a la main pour activer le bandeau." -ForegroundColor Yellow }
    }
} else {
    Write-Host "version.json inchange — rien a pousser." -ForegroundColor DarkGray
}
Pop-Location

# --- 5. Etat des channels -----------------------------------------------------------
& $Butler status $Itch
Write-Host "`nPublication OK — version $Version poussee sur $Itch`:$Channel" -ForegroundColor Green
Write-Host "Les joueurs de l'app itch.io recevront la mise a jour automatiquement." -ForegroundColor Green
