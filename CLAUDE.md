# CLAUDE.md — Mémoire de projet

Chargé automatiquement au démarrage de chaque session : **rester court et stable**. Le détail vit
dans des fichiers chargés **à la demande** (pointés ci-dessous) pour limiter le contexte par session.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction (humains,
cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab. **Moteur : Unity 6.5** (C#,
URP 2D). Le dépôt ne contient plus qu'un moteur : Godot a été retiré le **2026-08-10**.

- **Design complet → `docs/GDD.md`** : le consulter avant toute tâche de design/implémentation, et le tenir à jour à chaque décision.
- **Localiser du code** (système, écran, arme, ennemi, données, outil) → invoquer le skill **`/carte-projet`** plutôt que Glob/Grep à froid : il indexe l'arborescence + les checklists de câblage. Le maintenir à jour dans le même commit qu'un changement structurel.
- **Avant de coder** dans un domaine (armes, ennemis, UI/focus, VFX, assets, tests headless) → lire **`docs/PITFALLS_UNITY.md`**. Y ajouter tout nouveau piège découvert.
- **Comprendre l'architecture du portage** (principe logique-pure/moteur, ponts `Platform/`, contrats d'entités, cycle de vie d'une run) → **`docs/UNITY_MIGRATION_PLAN.md`**.
- **État d'implémentation détaillé & version courante → `docs/PROJECT_STATE.md`**.
- **Interroger un fichier trop gros pour être lu** → **MCP local** `mcp__local-llm__local_digest` (un ou plusieurs fichiers → une synthèse) / `local_map` (même question sur N fichiers). Outils **différés** : `ToolSearch` d'abord. Le serveur lit les fichiers côté LM Studio — **le contenu brut n'entre jamais en contexte** (mesuré : 83 173 tokens lus en local → 675 renvoyés).
  - **Les cibles** : `docs/TEST_REPORT.md` (~290 Ko) · `docs/GDD.md` (~220 Ko) · `docs/PITFALLS_UNITY.md` (~120 Ko) · `docs/DEVLOG.md` · `unity/Assets/StreamingAssets/localization/ui.csv` · `.../data/*.json`.
  - ⚠ **Lent** : ~6-7 min pour 290 Ko. Le lancer et continuer à travailler. Prévoir `max_tokens` large (1500-2500 pour un inventaire ; 900 a déjà été tronqué **sans le signaler**).
  - ⚠ **Bon sur du TEXTE, à proscrire sur des CHIFFRES.** Un LLM qui « lit » un CSV de mesures produit des nombres plausibles et faux. Règle : *s'il existe un outil déterministe, il gagne* (`tools/power_loop.py` pour `power_curve.log`).
  - ⚠ **Jamais pour du code qu'on s'apprête à éditer** — là, il faut le contenu réel. Ni pour localiser (`Grep` est instantané et exact).

## Phase actuelle

**Migration Unity terminée. 2.1.0 publiée le 2026-08-13** (2.0.0 le 08-10, 2.0.1 et 2.0.2 le 08-11) —
build itch **#1880415**. Le jeu est jouable de bout en bout, avec son, validé en jouant.
**673 tests.** ▶ Toujours essayer `tools/release_unity.ps1 -DryRun` avant de publier pour de bon.
▶ **Reste côté utilisateur : coller le devlog 2.1.0 sur itch** (`docs/DEVLOG.md`, EN puis FR).
⚠ **Publiée sans mesure au banc du renforcement de 4 armes** (§36) — décision de l'auteur. La zone du
Champ de Surcharge et celle de la Singularité ne sont bornées par **aucun plafond** : premier endroit
à regarder si la fin de partie paraît plus facile.

**2.0.1 — la leçon du portage a frappé une dixième fois, sur le TEXTE.** Tout le contenu nommé
(armes, greffes, améliorations du Hub, ennemis) sortait **en français dans les trois langues** :
il venait des JSON, et `ui.csv` portait **109 clés traduites que rien ne lisait**. Corrigé par
`Platform/ContentText.cs` + `tools/audit_loc_keys.py`. Trouvé **en regardant les rushes du
trailer** — le pipeline vidéo a été reporté sous Unity dans la foulée (`Bench/TrailerRecorder.cs`,
`tools/record_trailer.py`, chaque plan mis en scène, timecodes stables d'une recapture à l'autre).
▶ **Reste à faire côté utilisateur : coller le devlog 2.0.1 sur itch** (`docs/DEVLOG.md`, EN puis FR).

**2026-08-12 — quatre défauts signalés en jouant, corrigés — publiés en 2.1.0.** (1) La **Lame Boomerang**
mettait 6 s à revenir face à un joueur rapide, et son arme cessait donc de tirer : la parade du
2026-08-11 (`PickupMagnet`) avait été appliquée au *site trouvé*, pas à la *classe de défauts* →
`BoomerangReturn`. (2) L'**Aimant n'existait pas** sous Unity, alors que `bonus_magnet` restait
achetable à 770 Échos — porté avec `MagnetSchedule` / `MagnetPickup` / `MagnetSpawner` /
`MagnetSprite`. (3) Les **Noyaux d'Aether s'aimantent** (renversement de design assumé) et
`core_magnetism` a été redirigé vers le rayon d'aimantation, sans quoi il devenait payant et sans
effet. (4) Le **menu de montée de niveau proposait 1 ou 2 cartes** en fin de run — un test
verrouillait même ce comportement. **673 tests, banc 271/0.**

**2026-08-13 — la rareté et la fusion se SENTENT enfin, publié en 2.1.0** (détail : `docs/GDD.md` §35).
Demandé en jouant. Toute la hiérarchie du jeu reposait sur des signaux **immobiles** : un cadre et un
mot, dans un écran qui met le jeu en pause au milieu d'une nuée. Ajouté : aura respirante par rareté
(`UiRarityFlare`), arrivée en cascade avec dépassement pour l'épique, étiquette colorée — et pour la
fusion, `FusionFanfare` (trois ondes + ralenti court), `FusionBanner` (titre/icône/nom, sans modale)
et une ligne d'arsenal dorée. **La commune ne reçoit rien : c'est l'écart qui informe.**
⚠ **Deux défauts trouvés à la CAPTURE, invisibles au code** : l'aura employait le dégradé *radial*
des effets, dont il ne reste rien à 85 % du rayon — elle ne brillait que sous la carte qui la cache
(→ halo 9-slice `UiPrimitives.GlowBox`) ; et la capture censée juger la hiérarchie photographiait la
main *précédente*, `Present` ne rouvrant pas une modale déjà ouverte. **Un outil de contrôle qui se
trompe de sujet valide ce qu'il n'a pas regardé.** **673 tests, banc 273/0.**
⚠ Un run de banc a rendu **9 échecs sur 261, tous faux** : le joueur y est mort tôt et quatre blocs se
sont sabordés en cascade. Le run suivant, **même binaire**, est passé à 273/0 — la survie du joueur au
banc n'est pas déterministe. **Rejouer avant de conclure.**

**2026-08-13 (2) — « l'overload field est trop discret », publié en 2.1.0** (détail : `docs/GDD.md` §36).
Deux causes : l'arme **ne grandissait pas** (`radius` 100→200 et `knockbackPx` 40→60 déclarés,
**lus par personne**) et n'existait à l'écran que 9 % du temps. Le croisement déclaré/consommé a sorti
**4 autres clés mortes** (Singularité, Lance Cryo, Flux de Braise) — toutes branchées. ⚠ **C'est un
renforcement de 4 armes** : 2 restent bornées par les plafonds existants, la zone du Champ et celle du
puits **restent à mesurer au banc**. Ajouté aussi : `FusionMark`, signature **dorée** au tir de toute
arme fusionnée (appelée depuis `WeaponBase`, au point du son de tir).
⚠ **Le banc est tombé à 9/263, et la RÉFÉRENCE reproduit les neuf lignes à l'identique** (`git stash`
+ rebuild de `HEAD`) — donc **rien à voir avec ces changements**. Il passait à 273/0 le matin même.
Seul changement entre les deux : **`settings.json` réécrit à 17:19 par la tournée de captures**
(`completions.sanctuaire = 1`, records). Le banc lit cet état → **l'outil de captures calibre le
banc**, ce que le piège « un outil ne laisse pas sa mise en scène dans la sauvegarde » interdit.
**À instruire séparément.**
⚠⚠ **`audit_json_keys.py` a raté ce défaut DEUX FOIS** — il existe pourtant pour ça. Comparer aux
littéraux *globaux* ne relie une clé ni à son fichier ni à son consommateur (`"knockbackPx"` lu pour
une **greffe** couvrait celui des **armes**) ; puis les `Shape()` globaux laissaient le `radius` de la
Singularité couvrir celui du Champ. Contrôle désormais **arme par arme**. **Un audit qui rate ce
qu'il vise est pire qu'absent : il rend un verdict rassurant. Tout durcissement se valide en
réintroduisant le défaut.**

**2026-08-13 (3) — entrées portées sur le paquet Input System, publié en 2.1.1** (build itch
#1880556 ; détail : `docs/PITFALLS_UNITY.md` §Entrées). L'Input Manager est marqué pour dépréciation :
`com.unity.inputsystem` 1.20.0, `activeInputHandler` à **1**, les trois `EventSystem` sur
`InputSystemUIInputModule`. Tout ce qui touche un périphérique tient désormais dans **deux
fichiers** — `Platform/InputRemap.cs` et `Platform/RawInput.cs` — au lieu de ~20 appels dispersés.
⚠ **Trois des quatre pièges du domaine ne lèvent rien** : un module d'entrée sans
`AssignDefaultActions()` est *inerte sans erreur* ; `Keyboard.current` est **nul** sans clavier ; et
un paquet installé reste invisible tant que l'`.asmdef` ne le référence pas.
⚠⚠ **Onzième « déclaré non consommé », trouvé en migrant : la visée était morte.**
`Player.UpdateAim()` lisait deux axes — `RightStickX`/`RightStickY` — **jamais déclarés** dans
`InputManager.asset` ; l'exception levée chaque frame **sautait la fin de la méthode**, donc la visée
souris et le réticule. ▶ **À valider en jouant : la Lance Vectorielle se vise-t-elle ?**
**673 tests, banc 273/0** (après 9/263 puis 1/273 sur le **même binaire** — instabilité connue).

**Le dépôt est mono-moteur depuis le 2026-08-10.** Ce qui a changé :
- `src/`, `scenes/`, `project.godot`, le `.csproj`/`.sln` Godot, `assets/`, `data/` et
  `localization/` racine ont été **supprimés** — tout vit sous `unity/Assets/`.
- **Source unique** pour les données (`StreamingAssets/data`, `.../localization/ui.csv`) et pour les
  assets : plus de copie racine, donc plus de dérive possible.
- Les générateurs Python écrivent **directement là où le jeu lit**, via **`tools/unity_paths.py`**
  (table de destination) et **`tools/spriteframes.py`** (manifestes d'animation, ex-`.tres`).
- Doc de l'ère Godot conservée sous **`docs/archive-godot/`** (fond valable, chemins périmés).

⚠ **La leçon du portage, toujours en vigueur : déclaré n'est pas consommé.** Dix fois une donnée,
une règle ou un système entier existait, était testé, et n'était appelé par rien — trouvé en jouant,
jamais par l'automatisation. Trois outils sont nés de là : `tools/audit_json_keys.py`,
`tools/audit_unused_members.py` et **`tools/audit_loc_keys.py`** (tout le contenu nommé était affiché
**en français dans les trois langues**, alors que 109 clés traduites dormaient dans `ui.csv`).
**Les lancer après tout ajout de données, de règle ou de texte affiché.**
⚠ **Aucun des trois n'aurait attrapé la dixième** (l'Aimant, 2026-08-12) : le système entier était
*absent*, pas déclaré-non-lu — rien à comparer. Le seul signal était côté **joueur**, qui pouvait
acheter `bonus_magnet` (770 Échos) pour étendre un objet inexistant. **Deuxième fois qu'une
amélioration du Hub se paie sur du vide** (cf. le cran IV de saturation). Contrôle à faire à la main :
toute entrée de `meta_upgrades.json` doit pointer un système qui **existe** côté Unity.

## Équipe d'agents

Agents dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`, `developpeur`,
`musicien`, `story-teller`, `marketing`, `game-tester`, `release-manager`. Déléguer proactivement à
l'agent compétent — **qui fait quoi et dans quel ordre : `GUIDE-CLAUDE-CODE.md`**.
⚠ **Un agent qui décrit un état périmé du projet donne des instructions fausses avec autorité** :
quand une phase se termine, relire les agents qu'elle concerne (dernière passe : 2026-08-10).

- **`game-tester`** : construit et lance le binaire Unity, joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.
- **`release-manager`** : publie la release binaire de bout en bout (semver, release notes, `tools/release_unity.ps1`, MAJ doc) puis **rédige le devlog** — l'utilisateur le publie lui-même sur itch (l'agent ne pilote pas le navigateur). Source des notes : `docs/DEVLOG.md`.

## Maintenance de la doc

- `README.md` (racine) — MAJ à chaque changement de phase / ajout majeur.
- `docs/PROJECT_STATE.md` + `docs/GDD.md` + `/carte-projet` + `docs/PITFALLS_UNITY.md` — MAJ dans le commit qui change ce qu'ils décrivent.

## Conventions

- Plateforme cible : Windows (.exe). Moteur : **Unity 6.5** (`6000.5.6f1`), C#, URP 2D.
- **Build** : `Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBench.Windows64Game`
  → `unity/Build/game/ChimeraProtocol.exe` (ignoré par git, régénéré).
- **Publication (itch.io + Butler)** : skill **`/publier-itch`** ou `tools/release_unity.ps1 -Version X.Y.Z`
  (essayer d'abord avec `-DryRun`), ou déléguer à l'agent **`release-manager`**. Le script pose lui-même
  `bundleVersion` — ne pas l'éditer à la main. Runbook : `docs/RELEASE.md` ; notes cumulées : `docs/DEVLOG.md`.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut.
- **Logique pure testable** : `unity/Assets/Scripts/Shared/Rules/` — classes statiques **sans
  dépendance moteur** (`XpCurve`, `EnemyScaling`, `SaturationTable`…). Les `MonoBehaviour` y délèguent.
  `Shared/PlatformCore/` porte le socle déterministe (`Pcg32`, `TimerWheel`, `Easing`).
- **Tests unitaires** : xUnit, `dotnet test tests/ChimeraProtocol.Tests.csproj` — **673 tests**.
  Ils compilent `Shared/` **par chemin** : aucun moteur, aucun build requis.
- ⚠ **`Art/` ≠ `Resources/`** : `Art/` est consommé par **GUID** (planches d'animation), `Resources/`
  **par chemin** (`Resources.Load`) et embarqué en entier dans le binaire. Se tromper de dossier ne
  lève rien — le jeu affiche l'ancienne image. La table qui fait autorité : `tools/unity_paths.py`.
- **Difficulté** : trois axes multiplicatifs — réglage du joueur (`DifficultyTuning`), **palier de
  menace du niveau joué** (`LevelThreat`, cf. `docs/GDD.md` §28) et escalade d'overtime — plus
  l'**échelle de saturation** (`SaturationTable`, §34), un cran = une règle nommée.
- Sauvegarde : `%USERPROFILE%\AppData\LocalLow\drangoht\Chimera Protocol\` (méta/Échos + préférences,
  records, complétions, découvertes). Les sauvegardes Godot des joueurs sont migrées (`GodotConfig`,
  `SaveMigration`) — **c'est le seul endroit du portage dont l'échec est irréversible pour le joueur**.
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48, mid-boss 72, boss 154). Import en
  `Point`, `spritePixelsPerUnit = 1` (1 px = 1 unité : toutes les valeurs du jeu se transposent telles
  quelles). Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py`
  — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()`.
- **Audio** : musique **générée sur Suno** (prompts : `docs/AUDIO_AI_PROMPTS.md`) — jamais éditer un
  `.ogg` à la main. Pour en remplacer une : regénérer, déposer dans `music_ai/`, puis
  `python tools/import_ai_music.py [--only <id>]`. Bande-son de secours synthétisée (sans contrainte
  de licence) : `tools/generate_music_v3.py`. SFX = WAV Kenney CC0. Crédits et **licence Suno (plan
  gratuit, non commercial)** : `docs/AUDIO_CREDITS.md`.
  ⚠ Une arme absente de la table **`WeaponSfx`** est **muette** — 14 l'ont été sans que rien ne le dise.
- Localisation EN/FR/ES : `unity/Assets/StreamingAssets/localization/ui.csv` → `Loc.T("CLÉ")`
  (lu tel quel, aucune étape d'import). Le **contenu nommé** (armes, greffes, améliorations, ennemis)
  passe par `Platform/ContentText.cs`, qui déduit la clé de l'identifiant (`tesla_coil` →
  `WPN_TESLA_COIL_NAME`) et **replie sur le texte français du JSON** si elle manque.
  ⚠ Ce repli est silencieux : le contrôle est `tools/audit_loc_keys.py`, à lancer après tout ajout
  de contenu. Il vérifie les deux sens — clé absente **et** clé orpheline.
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0,45 s (CRITIQUE pour les nuées).
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`.
  Police : Share Tech Mono ; VT323 en réserve.
- **Cadres & couleurs d'UI** : toujours via `Scripts/UI/UiPalette.cs` (couleurs) et `UiStyle.cs`
  (cadres « plaque blindée »). Jamais de couleur en dur. Parti pris → `docs/ART_BRIEF_UI_FRAMES.md` ;
  assets → `tools/generate_ui_frames.py`.
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH).

## Pièges critiques → `docs/PITFALLS_UNITY.md`

Tous les pièges non évidents (import d'assets et `.meta`, `Resources` vs `Art`, cycle de vie des
scènes, navigation clavier/manette, calques d'UI, VFX, tests headless, export .NET, tampon de build)
sont dans **`docs/PITFALLS_UNITY.md`**. **Le consulter avant de coder dans le domaine concerné.**
Les pièges de **conception** hérités de Godot (câblage d'une arme, d'un ennemi, d'un affixe ; mixage ;
lisibilité) restent valables dans `docs/archive-godot/PITFALLS.md` — leurs chemins, non.
