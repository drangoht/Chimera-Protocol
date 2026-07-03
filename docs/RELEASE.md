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

## Publier une nouvelle version

1. **Incrémente la version** dans `project.godot` : `config/version="1.1.0"` (semver).
   C'est ce numéro qui s'affiche sur itch et sert de référence à l'auto-update.
2. Lance le script de release :
   ```
   powershell -File tools/release_itch.ps1
   ```
   Il enchaîne : vérif `.sln` → **export release Godot .NET** → dossier de distribution propre
   (`build/dist_windows/` = exe + runtime `data_*`, sans les artefacts parasites) → **`butler push`
   versionné** vers `drangoht/chimera-protocol:windows` → affiche l'état des channels.

   Options utiles :
   - `-Version 1.1.0` force la version (sinon lue depuis `project.godot`).
   - `-Channel windows` (défaut) — un channel par plateforme.
   - `-Itch user/slug` si ton slug itch diffère du défaut.
   - `-SkipExport` réutilise le build existant dans `build/` (itère plus vite).

3. Vérifie sur la page itch que la nouvelle version est en ligne. Terminé — les joueurs de l'app
   la reçoivent automatiquement.

---

## Notes

- **Un channel = une plateforme.** Si un build macOS/Linux est ajouté plus tard : `:osx`, `:linux`.
- Butler ne ré-uploade que les **fichiers modifiés** (diff wharf) : les pushes suivants sont rapides
  et légers, même si le build fait ~250 Mo décompressé.
- L'historique des versions est consultable : `butler status drangoht/chimera-protocol`.
- Le ZIP manuel `build/ChimeraProtocol_windows.zip` (généré à part) reste utile pour une
  distribution hors itch, mais **n'est pas nécessaire** au workflow Butler ci-dessus.
