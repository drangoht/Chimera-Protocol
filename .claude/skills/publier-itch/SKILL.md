---
name: publier-itch
description: Publier une nouvelle version de Chimera Protocol sur itch.io (build Unity → Butler push → mise à jour du manifeste version.json). À invoquer quand l'utilisateur demande de « publier », « release », « pousser sur itch », « sortir une nouvelle version ». Enchaîne le build, le push et le commit du manifeste via tools/release_unity.ps1.
---

# Publier sur itch.io — Chimera Protocol

Distribution : **itch.io + Butler**. Un `butler push` = auto-update pour les joueurs de l'app itch
(patch différentiel wharf). Les joueurs web (ZIP) voient le bandeau « nouvelle version » du menu,
alimenté par `version.json` lu sur `raw.githubusercontent`. Runbook détaillé : `docs/RELEASE.md`.

## Procédure (dans l'ordre)

### 1. Choisir le numéro de version
Sémantique `MAJEUR.MINEUR.CORRECTIF`, lue dans `unity/ProjectSettings/ProjectSettings.asset`
(`bundleVersion`) — mais **ne pas l'éditer à la main** : le script la pose lui-même.
- **correctif** (x.y.**Z**) : bugfix, ajustement mineur ;
- **mineur** (x.**Y**.0) : nouvelle fonctionnalité / contenu (défaut le plus courant ici) ;
- **majeur** (**X**.0.0) : refonte, rupture.

Si la nature n'est pas évidente, proposer le bump et continuer sans bloquer.

### 2. Committer le travail à publier
Le tampon de build (`v<version>-<sha>`) désigne le commit publié : tout ce qui doit être dans la
release doit être commité **avant** de lancer le script. Un arbre modifié produit un tampon suffixé
`+`, qui ne correspond à aucun commit — le script le signale, il ne l'empêche pas.

### 3. Essai à blanc, puis publication
Depuis la racine, **sans `-ExecutionPolicy Bypass`** (ce flag est refusé par le classifier auto) :
```
& "tools/release_unity.ps1" -Version X.Y.Z -DryRun    # va jusqu'au staging, ne publie rien
& "tools/release_unity.ps1" -Version X.Y.Z
```
Timeout large : le build Unity prend plusieurs minutes.

Le script enchaîne : `bundleVersion` posée → tampon de build → build Unity
(`BuildBench.Windows64Game`) → vérification que le binaire porte **bien** la version demandée →
staging propre → `butler push …:windows --userversion X.Y.Z` → `version.json` régénéré, commité et
poussé sur `main`.

Paramètres utiles : `-SkipBuild` (re-push d'un binaire qu'on vient de construire soi-même — le
script vérifie alors sa version), `-Channel`, `-Itch user/slug` (défaut `drangoht/chimera-protocol`).

### 4. Vérifier
- Sortie : « Publication OK — version X.Y.Z poussée ». Le tableau `butler status` peut afficher
  l'ancienne version tant que le build est « processing » — c'est normal.
- `version.json` sur `main` = X.Y.Z (le CDN a ~5 min de cache ; se fier au diff du commit poussé).

## Prérequis / pièges
- **Butler authentifié** : fourni par l'app itch (dossier `broth`, détecté auto). Si
  « not authorized », lancer une fois `"<butler.exe>" login` (chemin affiché par le script).
- ⚠ **Une release a déjà expédié le binaire de la version précédente.** D'où la vérification du
  tampon : le script exige que `build_stamp.json` porte la version demandée. Ne pas la contourner.
- ⚠ **La date de l'exécutable ne prouve rien sous Unity** : le build est incrémental, un binaire
  identique n'est pas réécrit. Seule la version embarquée tranche.
- ⚠ **Ne jamais tester `$?` après un exe natif en PowerShell 5.1** : `git`, Unity et Butler écrivent
  leur progression sur stderr même quand tout va bien. Seul `$LASTEXITCODE` fait foi.
- **Doc de fin de release** : MAJ `README.md` / `CLAUDE.md` + page store itch si la version
  introduit une phase ou un ajout majeur ; devlog à coller sur itch (rédigé, jamais publié par
  l'agent).
