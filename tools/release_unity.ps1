<#
.SYNOPSIS
    Publie une version du jeu (moteur Unity) sur itch.io.

.DESCRIPTION
    Enchaine : numero de version pose dans le projet -> tampon de build (SHA du commit publie)
    -> build Unity -> dossier de distribution propre -> `butler push` -> manifeste version.json
    pousse sur GitHub (c'est lui que lit le bandeau "nouvelle version" du menu).

    Remplace `release_itch.ps1`, qui pilotait l'export Godot. Les garde-fous de ce dernier ne se
    transposent pas tels quels : Godot rendait la main AVANT que `dotnet publish` ait fini d'ecrire
    le runtime, d'ou une detection de stabilite puis de fraicheur des artefacts. Unity construit de
    facon synchrone et rend un rapport. La lecon, elle, reste : on verifie que le binaire pousse est
    bien celui qu'on vient de construire, parce qu'une release a deja expedie le binaire de la
    version precedente sans qu'aucune erreur ne soit levee.

.PARAMETER Version
    Numero affiche sur itch (ex. 1.27.0). Obligatoire : contrairement a Godot, rien ne le declare
    ailleurs dans le depot — le poser ici EST la decision de publier.

.PARAMETER SkipBuild
    Reutilise le binaire deja present dans unity/Build/game. A n'employer que si l'on vient de le
    construire soi-meme : le script verifie alors qu'il porte bien la version demandee.

.PARAMETER DryRun
    Va jusqu'au dossier de distribution et s'arrete AVANT butler, avant le manifeste et avant tout
    commit. C'est le seul moyen d'eprouver la chaine sans publier : un script de release qu'on ne
    peut essayer qu'en publiant ne se teste jamais qu'en production.

.EXAMPLE
    pwsh tools/release_unity.ps1 -Version 1.27.0 -DryRun
    pwsh tools/release_unity.ps1 -Version 1.27.0
#>

param(
    [Parameter(Mandatory = $true)][string]$Version,
    [ValidateSet("windows", "web")][string]$Target = "windows",
    [string]$Itch = "drangoht/chimera-protocol",
    [string]$Channel = "",
    [switch]$SkipBuild,
    [switch]$DryRun
)

# NB : PAS "Stop" — Unity et Butler ecrivent leur progression sur stderr, ce que PowerShell 5.1
# prend pour une erreur. Seul $LASTEXITCODE fait foi apres un executable natif.
$ErrorActionPreference = "Continue"

$ProjectRoot = Split-Path -Parent $PSScriptRoot
$UnityProject = Join-Path $ProjectRoot "unity"
$Unity = "C:\Program Files\Unity\Hub\Editor\6000.5.6f1\Editor\Unity.exe"

# --- Cible ------------------------------------------------------------------------
# Les deux cibles ne different que par CINQ choses : le dossier construit, la methode d'editeur qui
# le produit, ce qu'on exige d'y trouver, ce qu'on copie et le canal itch.
#
# ⚠ Le canal decide, cote itch.io, si le fichier est JOUABLE DANS LE NAVIGATEUR : un canal nomme
# `html5` (ou `html`, ou `web`) est reconnu comme tel, n'importe quel autre nom produit une archive a
# telecharger. Un build web pousse sur un canal mal nomme s'installe donc parfaitement — et ne se joue
# pas. Rien ne le signale.
if ($Target -eq "web") {
    $BuildDir      = Join-Path $UnityProject "Build\web"
    $BuildMethod   = "BuildBench.WebGame"
    $DefaultChannel = "html5"
    # index.html : la page elle-meme. Build\ : le wasm et le chargeur. StreamingAssets\ : le tuning
    # et les traductions, que le jeu web telecharge au demarrage — sans eux il demarre sur des tables
    # vides, ce qui ne ressemble pas a une donnee manquante mais a un jeu casse.
    $Required      = @("index.html", "Build", "StreamingAssets")
} else {
    $BuildDir      = Join-Path $UnityProject "Build\game"
    $BuildMethod   = "BuildBench.Windows64Game"
    $DefaultChannel = "windows"
    $Required      = @("ChimeraProtocol_Data\Managed", "ChimeraProtocol_Data\StreamingAssets", "UnityPlayer.dll")
}

if (-not $Channel) { $Channel = $DefaultChannel }

$Exe = Join-Path $BuildDir "ChimeraProtocol.exe"
$DataDir = Join-Path $BuildDir "ChimeraProtocol_Data"
$Staging = Join-Path $ProjectRoot "build\staging-unity"

function Fail($msg) { Write-Host "ERREUR : $msg" -ForegroundColor Red; exit 1 }

if (-not (Test-Path $Unity)) { Fail "Unity introuvable : $Unity" }
if ($Version -notmatch '^\d+\.\d+\.\d+$') { Fail "Version attendue au format x.y.z (recu : $Version)" }

# --- Butler ------------------------------------------------------------------------
$brothGlob = Join-Path $env:APPDATA "itch\broth\butler\versions\*\butler.exe"
$butler = Get-ChildItem -Path $brothGlob -ErrorAction SilentlyContinue |
          Sort-Object LastWriteTime -Descending | Select-Object -First 1
if (-not $butler) {
    Fail "butler.exe introuvable. Lance l'app itch.io une fois, ou installe butler depuis https://itchio.itch.io/butler"
}
$Butler = $butler.FullName
Write-Host "Butler  : $Butler" -ForegroundColor Cyan
Write-Host "Cible   : $Target ($BuildMethod)" -ForegroundColor Cyan
Write-Host "Version : $Version  ->  $Itch`:$Channel" -ForegroundColor Cyan

# --- 1. Version dans les reglages du projet ----------------------------------------
# C'est elle que lit `Application.version`, donc le tampon du menu ET la comparaison avec le
# manifeste. La laisser derriere ferait s'annoncer le binaire sous un ancien numero et declencherait
# le bandeau de mise a jour chez des joueurs deja a jour.
$settingsPath = Join-Path $UnityProject "ProjectSettings\ProjectSettings.asset"
$settings = Get-Content $settingsPath -Raw
$settings = $settings -replace '(?m)^(\s*bundleVersion:\s*).*$', "`${1}$Version"
Set-Content -Path $settingsPath -Value $settings -Encoding utf8 -NoNewline
Write-Host "bundleVersion pose a $Version." -ForegroundColor DarkGray

# --- 2. Tampon de build : le SHA du commit publie ----------------------------------
# Ecrit en RESSOURCE et non compile en dur : il doit designer le commit qu'on publie, connu au
# dernier moment. Sans lui, deux binaires portant la meme version sont indiscernables — et c'est
# exactement ce qui a permis d'expedier le mauvais.
#
# NOTE : depuis le 2026-08-10, `BuildBench.StampGitSha` REPOSE cette identite juste avant de
# construire, et c'est desormais elle qui fait foi. L'ecriture ci-dessous ne sert plus qu'au cas
# -SkipBuild (aucun build ne passera derriere) et a l'affichage. Le tampon n'appartenait pas ici :
# ecrit seulement a la publication, le fichier RESTAIT ensuite, et tout build local ulterieur
# affichait le SHA de la derniere release — un garde-fou de fraicheur qui se trompe est pire que pas
# de garde-fou, puisqu'on lui fait confiance.
Push-Location $ProjectRoot
$sha = (git rev-parse --short HEAD)
Pop-Location

# Pas d'avertissement « depot modifie » ici : a cet instant l'etape 1 vient elle-meme de modifier
# ProjectSettings.asset, et build_sha.txt suit ligne suivante — l'alerte se declenchait donc a CHAQUE
# release, y compris parfaitement propre, et on ne la lisait plus. Le constat honnete est fait par
# BuildBench, qui exclut ces deux fichiers, et se lit a l'etape 4 sur le tampon du binaire reellement
# produit.
$resDir = Join-Path $UnityProject "Assets\Resources"
New-Item -ItemType Directory -Force -Path $resDir | Out-Null
Set-Content -Path (Join-Path $resDir "build_sha.txt") -Value $sha -Encoding utf8 -NoNewline
Write-Host "Tampon de build : v$Version-$sha" -ForegroundColor DarkGray

# --- 3. Build ----------------------------------------------------------------------
if (-not $SkipBuild) {
    Remove-Item (Join-Path $UnityProject "Temp\UnityLockfile") -ErrorAction SilentlyContinue

    $log = Join-Path $env:TEMP "chimera-release-build.log"
    Write-Host "Build Unity en cours (log : $log)..." -ForegroundColor Yellow

    $buildStart = Get-Date

    # ⚠ Start-Process et non l'operateur d'appel `&`. Lance par `&`, Unity rend la main
    # IMMEDIATEMENT sans rien faire : pas de log, $LASTEXITCODE vide, et le script poursuit comme si
    # tout allait bien — c'est la garde de fraicheur, plus bas, qui a fini par le trahir. Un
    # lancement qui echoue en silence est pire qu'un lancement qui echoue.
    $proc = Start-Process -FilePath $Unity -PassThru -Wait -NoNewWindow -ArgumentList @(
        "-batchmode", "-quit",
        "-projectPath", $UnityProject,
        "-logFile", $log,
        "-executeMethod", $BuildMethod
    )

    if ($proc.ExitCode -ne 0) { Fail "Build Unity echoue (code $($proc.ExitCode)) - voir $log" }
    if (-not (Test-Path $log)) { Fail "Build Unity : aucun journal ecrit ($log) - Unity n'a pas demarre." }

    # Le rapport de build est dans le log : on exige la reussite EXPLICITE, un code retour nul ne
    # suffisant pas a distinguer « construit » de « rien a faire ».
    if (-not (Select-String -Path $log -Pattern 'resultat=Succeeded' -Quiet)) {
        Fail "Build Unity : aucune reussite confirmee dans $log"
    }

    if ($Target -eq "windows" -and -not (Test-Path $Exe)) { Fail "Executable absent apres le build : $Exe" }

    # ⚠ La DATE de l'executable ne prouve rien sous Unity : le build est incremental, et un binaire
    # deja identique n'est pas reecrit — un horodatage anterieur est donc normal. La premiere version
    # de ce script echouait la-dessus sur un build parfaitement valide.
    # Ce qui tranche est la VERSION EMBARQUEE, verifiee plus bas : posee juste avant le build, elle ne
    # peut correspondre que si le binaire a bien ete reconstruit avec elle.
    if ($Target -eq "windows" -and (Get-Item $Exe).LastWriteTime -lt $buildStart) {
        Write-Host "Binaire non reecrit (build incremental) - la version embarquee tranchera." -ForegroundColor DarkGray
    }
} else {
    Write-Host "SkipBuild : binaire existant reutilise." -ForegroundColor DarkGray
    if (-not (Test-Path $BuildDir)) { Fail "SkipBuild demande mais aucun build : $BuildDir" }
}

# --- 4. Verification du binaire ----------------------------------------------------
# Ce qui part doit contenir de quoi tourner. Un dossier de donnees incomplet ne se voit qu'au
# lancement, c'est-a-dire chez le joueur.
foreach ($required in $Required) {
    $path = Join-Path $BuildDir $required
    if (-not (Test-Path $path)) { Fail "Element manquant dans le build : $required" }
}

# Le TAMPON produit par le build, dernier point ou l'on peut constater qu'on s'apprete a publier
# autre chose que ce qu'on croit.
# ⚠ Ne PAS interroger les metadonnees Windows de l'executable : celles d'un binaire Unity decrivent
# le MOTEUR (« 6000.5.6f1 »), pas le jeu. La premiere version de ce controle comparait la version de
# release au numero d'Unity, et echouait donc toujours.
$stampPath = Join-Path $BuildDir "build_stamp.json"
if (-not (Test-Path $stampPath)) { Fail "Tampon de build absent ($stampPath) - build incomplet ou trop ancien." }

$stamp = Get-Content $stampPath -Raw | ConvertFrom-Json
if ($stamp.version -ne $Version) {
    Fail "Le binaire porte la version '$($stamp.version)' alors qu'on publie '$Version' - build perime."
}
Write-Host "Binaire verifie : v$($stamp.version)-$($stamp.sha) (construit le $($stamp.date))." -ForegroundColor DarkGray

# Le suffixe « + » est pose par BuildBench quand l'arbre de travail portait des modifications autres
# que celles que cette release ecrit elle-meme : le binaire ne correspond alors A AUCUN COMMIT, et le
# tampon affiche en jeu ne permettra pas de rejouer un rapport de bug. Averti ici et pas plus tot,
# parce que c'est le seul endroit ou le constat porte sur le binaire REELLEMENT produit.
if ($stamp.sha -like "*+") {
    Write-Host "AVERTISSEMENT : binaire construit depuis un arbre modifie ($($stamp.sha)) — il ne correspond a aucun commit." -ForegroundColor Yellow
} elseif ($stamp.sha -eq "dev") {
    Write-Host "AVERTISSEMENT : le build n'a pas pu lire git — le tampon dira 'dev' aux joueurs." -ForegroundColor Yellow
}

# --- 5. Dossier de distribution propre ---------------------------------------------
# Butler diffe fichier par fichier : on pousse un DOSSIER, sans les artefacts parasites du dossier
# de build (captures, journaux, anciennes archives).
if (Test-Path $Staging) { Remove-Item $Staging -Recurse -Force }
New-Item -ItemType Directory -Path $Staging -Force | Out-Null

if ($Target -eq "web") {
    # Tout le dossier, moins ce qu'Unity nomme lui-meme « DoNotShip » : des symboles de debogage
    # Burst qui n'ont rien a faire chez un joueur et qui alourdiraient la page pour rien.
    Copy-Item (Join-Path $BuildDir "*") -Destination $Staging -Recurse -Force -Exclude "*BurstDebugInformation*"
    Get-ChildItem $Staging -Directory -Filter "*BurstDebugInformation*" |
        Remove-Item -Recurse -Force -ErrorAction SilentlyContinue
} else {
    Copy-Item $Exe -Destination $Staging
    Copy-Item $DataDir -Destination $Staging -Recurse
    foreach ($file in Get-ChildItem $BuildDir -File | Where-Object { $_.Extension -in @(".dll", ".json") }) {
        Copy-Item $file.FullName -Destination $Staging
    }
}
Write-Host "Staging pret : $Staging" -ForegroundColor Cyan

# --- 6. Push Butler ----------------------------------------------------------------
if ($DryRun) {
    Write-Host "`nA BLANC : tout est pret, rien n'a ete publie." -ForegroundColor Green
    # ⚠ Ce qu'on annonce doit exister : il n'y a pas d'executable dans un build web, et afficher son
    # chemin donnait une ligne parfaitement credible designant un fichier absent. Un essai a blanc
    # sert justement a verifier ce qu'on s'apprete a publier — il ne peut pas mentir dessus.
    Write-Host "  build    : $(if ($Target -eq 'web') { $BuildDir } else { $Exe })" -ForegroundColor DarkGray
    Write-Host "  staging  : $Staging" -ForegroundColor DarkGray
    Write-Host "  tampon   : v$Version-$sha" -ForegroundColor DarkGray
    Write-Host "Relance sans -DryRun pour pousser sur $Itch`:$Channel." -ForegroundColor Green
    exit 0
}

Write-Host "Push vers itch.io..." -ForegroundColor Yellow
& $Butler push $Staging "$Itch`:$Channel" --userversion $Version
if ($LASTEXITCODE -ne 0) {
    Fail "butler push echoue (code $LASTEXITCODE). Si 'not authorized', lance une fois : `"$Butler`" login"
}

# --- 7. Manifeste de version -------------------------------------------------------
# Les joueurs qui ont TELECHARGE le jeu n'ont pas l'auto-update de l'app itch : le menu lit ce fichier
# sur raw.githubusercontent et leur annonce la nouvelle version. Sans ce push, la release existe pour
# butler et pour personne d'autre.
#
# ⚠ Le manifeste decrit la version TELECHARGEABLE, donc il n'appartient qu'a la cible Windows. Un
# joueur web est toujours sur la derniere version — la page sert le build courant — et le bandeau y
# est desactive. Le pousser depuis une release web annoncerait donc, a tous les joueurs Windows, une
# mise a jour qui n'existe pas : ils iraient telecharger le binaire qu'ils ont deja. C'est la meme
# famille de defaut que le tampon de build qui survivait a sa release et mentait aux builds suivants.
if ($Target -eq "web") {
    Write-Host "Manifeste inchange : une release web n'annonce rien aux joueurs Windows." -ForegroundColor DarkGray
} else {
    $parts = $Itch.Split("/")
    $itchUrl = "https://$($parts[0]).itch.io/$($parts[1])"
    $manifest = [ordered]@{ version = $Version; url = $itchUrl }
    ($manifest | ConvertTo-Json) | Out-File -FilePath (Join-Path $ProjectRoot "version.json") -Encoding utf8
}

Push-Location $ProjectRoot
# build_sha.txt n'est PLUS commite : depuis qu'il est repose a chaque build, c'est un artefact et non
# une source — le versionner rendait le depot modifie apres chaque compilation. Il est ignore.
git add version.json "unity/ProjectSettings/ProjectSettings.asset"
git diff --cached --quiet
if ($LASTEXITCODE -ne 0) {
    # Le message doit dire ce qui est REELLEMENT commite : une release web ne touche pas au manifeste,
    # et annoncer le contraire rendrait l'historique faux la ou on vient le consulter.
    $what = if ($Target -eq "web") { "version du projet (canal web)" }
            else { "manifeste, version du projet, tampon de build" }
    git commit -m "chore(release): $Version ($what)"
    # ⚠ Ne PAS tester $? apres un exe natif : git ecrit sa progression sur stderr meme quand tout
    # va bien, ce qui met $? a faux. Seul $LASTEXITCODE fait foi.
    if ($LASTEXITCODE -eq 0) {
        git push
        if ($LASTEXITCODE -ne 0) {
            Write-Host "AVERTISSEMENT : git push echoue — pousse version.json a la main, sinon le bandeau reste muet." -ForegroundColor Yellow
        }
    }
} else {
    Write-Host "Rien a committer (version.json inchange)." -ForegroundColor DarkGray
}
Pop-Location

# --- 8. Etat ------------------------------------------------------------------------
& $Butler status $Itch
Write-Host "`nPublication OK — version $Version poussee sur $Itch`:$Channel" -ForegroundColor Green

# ⚠ Ce qu'on annonce doit etre vrai pour la cible qu'on vient de publier. Le message d'origine —
# auto-update de l'app itch et bandeau au prochain lancement — ne decrit QUE la version telechargeable :
# une page web n'a pas d'auto-update (elle sert toujours le build courant) et son bandeau est
# desactive. L'afficher apres un push web decrivait une mecanique qui n'existe pas.
if ($Target -eq "web") {
    Write-Host "La page sert le nouveau build des qu'itch a fini de le traiter." -ForegroundColor Green
    Write-Host "⚠ Prerequis cote itch.io, a faire UNE fois : « Kind of project » = HTML." -ForegroundColor Yellow
    Write-Host "  Tant que le projet est « Downloadable », ce build se telecharge au lieu de se jouer." -ForegroundColor Yellow
} else {
    Write-Host "Les joueurs de l'app itch.io recevront la mise a jour automatiquement ;" -ForegroundColor Green
    Write-Host "les autres verront le bandeau au prochain lancement." -ForegroundColor Green
}
