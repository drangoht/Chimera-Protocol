---
name: publier-itch
description: Publier une nouvelle version de Chimera Protocol sur itch.io (export Godot .NET → Butler push → mise à jour du manifeste version.json). À invoquer quand l'utilisateur demande de « publier », « release », « pousser sur itch », « sortir une nouvelle version ». Enchaîne le bump de version, les commits et le script tools/release_itch.ps1.
---

# Publier sur itch.io — Chimera Protocol

Distribution : **itch.io + Butler**. Un `butler push` = auto-update pour les joueurs de
l'app itch (patch différentiel wharf). Les joueurs web (ZIP) voient le bandeau « nouvelle
version » du menu (cf. skill `carte-projet` §MAJ). Runbook détaillé : `docs/RELEASE.md`.

## Procédure (dans l'ordre)

### 1. Choisir le numéro de version
Sémantique `MAJEUR.MINEUR.CORRECTIF` depuis `config/version` de `project.godot` :
- **correctif** (x.y.**Z**) : bugfix, ajustement mineur ;
- **mineur** (x.**Y**.0) : nouvelle fonctionnalité / contenu (défaut le plus courant ici) ;
- **majeur** (**X**.0.0) : refonte, rupture.

Si la nature n'est pas évidente, proposer le bump et continuer sans bloquer.

### 2. Bumper `project.godot` (⚠ le script NE le fait PAS)
Éditer `config/version="X.Y.Z"`. Puis committer + pousser :
```
git add project.godot
git commit -m "chore(release): bump version X.Y.Z (<résumé>)"
git push
```

### 3. Lancer le script de release
Depuis la racine du dépôt, **sans `-ExecutionPolicy Bypass`** (ce flag est refusé par le
classifier auto — l'ajouter fait échouer l'appel) :
```
& "tools/release_itch.ps1" -Version X.Y.Z
```
Timeout large (l'export release .NET est long — jusqu'à plusieurs minutes).

Le script enchaîne : export `build/ChimeraProtocol.exe` + runtime → staging propre →
`butler push …:windows --userversion X.Y.Z` → **régénère `version.json` (racine) et le
commit+push sur `main`** (source du bandeau MAJ, lu sur raw.githubusercontent).

Paramètres utiles : `-SkipExport` (re-push sans réexporter), `-Channel`, `-Itch user/slug`
(défaut `drangoht/chimera-protocol`).

### 4. Committer les artefacts générés restants
Un nouveau script C# génère un `.uid` Godot → le committer s'il apparaît en non-suivi :
```
git add -A ; git status --short   # committer les .uid/.import nouveaux
```

### 5. Vérifier
- Sortie du script : « Publication OK — version X.Y.Z poussée ». Le tableau `butler status`
  peut afficher l'ancienne version tant que le build est « processing » — c'est normal.
- `version.json` sur `main` = X.Y.Z (le CDN `raw.githubusercontent` a ~5 min de cache TTL,
  donc le `curl` direct peut retarder ; se fier au diff du commit poussé par le script).

## Prérequis / pièges
- **`ChimeraProtocol.sln` requis à la racine** (sinon l'export .NET crashe au lancement).
  Recréer : `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln … add ChimeraProtocol.csproj`.
- **Butler authentifié** : fourni par l'app itch (dossier `broth`, détecté auto). Si
  « not authorized », lancer une fois `"<butler.exe>" login` (chemin affiché par le script).
- Godot 4.7 .NET laisse souvent `$LASTEXITCODE` vide en fin d'export → le script ne faille
  que sur un code non-zéro explicite + vérifie l'existence de l'exe/runtime (ne pas « durcir »).
- **Doc de fin de release** (feedback projet) : MAJ `README.md`/`CLAUDE.md` + page store itch
  si la version introduit une phase ou un ajout majeur.
