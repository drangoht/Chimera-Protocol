# RELEASE — Publication & mises à jour automatiques (itch.io + Butler)

Chimera Protocol est distribué sur **itch.io**. Les mises à jour sont livrées via **Butler**,
l'outil CLI d'itch.io : un `push` suffit, et **l'app itch.io met à jour le jeu automatiquement**
chez les joueurs (détection de version, patch différentiel *wharf*, remplacement des fichiers,
relance) — **sans aucun code de mise à jour dans le jeu**.

Un seul push couvre les deux publics :
- **Joueurs via l'app itch.io** → mise à jour **automatique** au lancement / en arrière-plan.
- **Joueurs qui téléchargent le ZIP** depuis la page web → itch reconditionne l'upload poussé en
  téléchargement direct (ils re-téléchargent manuellement, comme d'habitude).

> Rappel technique : un `.exe` en cours d'exécution ne peut pas se remplacer lui-même sous Windows.
> C'est **l'app itch.io** (processus tiers) qui applique la mise à jour — d'où l'intérêt de ce
> workflow plutôt qu'un updater maison.
>
> Les joueurs venus du **web** n'ont aucune mise à jour automatique : pour eux, le menu principal
> interroge `version.json` sur le dépôt et affiche un bandeau. C'est le seul moyen qu'ils ont
> d'apprendre qu'un correctif est sorti — et il ne fonctionne que si le manifeste est poussé.

---

## Prérequis (une seule fois)

1. **Page itch.io créée** pour le jeu. Note son slug exact depuis l'URL :
   `itch.io/<user>/<game-slug>`. Par défaut le script vise `drangoht/chimera-protocol` —
   **ajuste `-Itch` si ton slug diffère**.
2. **Butler authentifié.** Il est déjà fourni par l'app itch.io (dossier `broth`), le script le
   localise automatiquement. S'il refuse le push avec *« not authorized »*, exécute une fois :
   ```
   ! & "$env:APPDATA\itch\broth\butler\versions\15.27.0\butler.exe" login
   ```
   (ouvre le navigateur pour lier ta clé API — action interactive, à lancer toi-même).
3. Sur la page itch, mets le prix / la visibilité comme voulu. Le fichier poussé par Butler apparaît
   coché comme plateforme **Windows** → l'app propose « Installer » puis l'auto-update.

---

## Publier une nouvelle version (moteur Unity)

Depuis la **2.0.0**, le jeu est construit avec Unity et se publie par `tools/release_unity.ps1`.
L'ancien `release_itch.ps1`, qui pilotait l'export Godot, a été supprimé avec le moteur
(2026-08-10) — ses garde-fous utiles ont été repris dans le script Unity, les autres ne se
transposaient pas.

1. **Essai à blanc** — la chaîne entière sans rien publier :
   ```
   powershell -File tools/release_unity.ps1 -Version 2.1.0 -DryRun
   ```
   Un script de release qu'on ne peut essayer qu'en publiant ne se teste jamais qu'en production.

2. **Régénère la galerie** si l'interface a bougé :
   ```
   py tools/capture_store.py
   ```
   Cinq tournées, une par biome, ~7 min. Les images atterrissent dans `docs/store_screens/` sous les
   noms attendus par `docs/ITCH_STORE_PAGE.md`, et les manquantes sont **annoncées**.

3. **Publie** :
   ```
   powershell -File tools/release_unity.ps1 -Version 2.1.0
   ```
   Il enchaîne : numéro de version posé dans le projet → tampon de build (SHA du commit) → build
   Unity → vérification du binaire → dossier de distribution propre → **`butler push`** versionné →
   `version.json` régénéré et poussé sur GitHub → état des channels.

   Options utiles :
   - `-Channel windows` (défaut) — un channel par plateforme.
   - `-Itch user/slug` si le slug diffère du défaut.
   - `-SkipBuild` réutilise le binaire déjà construit (le script vérifie qu'il porte la bonne version).

4. **Colle le devlog** sur itch depuis `docs/DEVLOG.md` (entrée la plus récente, EN puis FR).
   Le script ne pilote pas le navigateur.

### Ce que le script vérifie, et pourquoi

- **Le tampon de build** (`build_stamp.json`, écrit par le build lui-même) porte version et SHA. Les
  métadonnées Windows d'un exécutable Unity décrivent le **moteur** (« 6000.5.6f1 »), pas le jeu :
  les interroger ne dit rien. Et l'horodatage ne vaut pas mieux — le build est incrémental, donc un
  binaire identique n'est pas réécrit.
- ⚠ **Un `-DryRun` fait crier l'avertissement « arbre modifié » au run suivant.** L'essai à blanc
  pose `bundleVersion` dans `ProjectSettings.asset` et ne le remet pas : le vrai run construit donc
  depuis un arbre sale et tamponne `<sha>+`. **C'est bénin ici** — le seul écart est le numéro de
  version, que le script commite juste après (`chore(release): X.Y.Z`). Mais l'avertissement existe
  pour attraper un défaut réel (une release a déjà expédié le binaire de la version précédente) :
  **le vérifier au lieu de l'ignorer**, en confrontant le commit de release au `git status`. Un
  avertissement qui se déclenche à chaque publication est un avertissement qu'on cesse de lire.
- **Le journal de build** doit contenir une réussite explicite. ⚠ Unity lancé par l'opérateur d'appel
  `&` rend la main **immédiatement sans rien faire** : pas de log, pas de code retour. D'où
  `Start-Process -Wait`.
- **`version.json` est poussé sur GitHub** : c'est ce fichier que lit le bandeau « nouvelle version »
  du menu. Sans ce push, la release existe pour butler et pour personne d'autre.

---

## Notes

- **Un channel = une plateforme.** Si un build macOS/Linux est ajouté plus tard : `:osx`, `:linux`.
- Butler ne ré-uploade que les **fichiers modifiés** (diff wharf) : les pushes suivants sont rapides
  et légers, même si le build fait ~250 Mo décompressé.
- L'historique des versions est consultable : `butler status drangoht/chimera-protocol`.
- Le ZIP manuel `build/ChimeraProtocol_windows.zip` (généré à part) reste utile pour une
  distribution hors itch, mais **n'est pas nécessaire** au workflow Butler ci-dessus.
