# CLAUDE.md — Mémoire de projet

Chargé automatiquement au démarrage de chaque session : **rester court et stable**. Le détail vit
dans des fichiers chargés **à la demande** (pointés ci-dessous) pour limiter le contexte par session.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction (humains,
cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab.

- **Design complet → `docs/GDD.md`** : le consulter avant toute tâche de design/implémentation, et le tenir à jour à chaque décision.
- **Localiser du code** (système, écran, arme, ennemi, données, outil) → invoquer le skill **`/carte-projet`** plutôt que Glob/Grep à froid : il indexe l'arborescence + les checklists de câblage. Le maintenir à jour dans le même commit qu'un changement structurel.
- **Avant de coder** dans un domaine (armes, ennemis, UI/focus, VFX, scènes, assets, tests headless) → lire **`docs/PITFALLS.md`** (pièges non-évidents Godot/C# + checklists de câblage). Y ajouter tout nouveau piège découvert.
- **État d'implémentation détaillé & version courante → `docs/PROJECT_STATE.md`** (évolutif). Résumé de phase ci-dessous.
- **Synthétiser du volume** (relever/résumer/inventorier à partir de plusieurs gros fichiers : `data/*.json`, docs longues, logs, rapports de test) → déléguer au **MCP local** `mcp__local-llm__local_digest` / `local_map` (outils différés : `ToolSearch` d'abord) plutôt que d'enchaîner les `Read` : le serveur lit les fichiers côté LM Studio, seule la synthèse entre en contexte. Ne pas l'utiliser pour du code que l'on s'apprête à éditer — là, le contenu réel est nécessaire.

**Phase actuelle : trois chantiers faits, non publiés — mid-boss à valider en jouant, puis 1.23.0.**

**(3) Survie en overtime — mesurée en jeu, cause réelle trouvée** (2026-07-29). La session jouée de
validation du chantier (1) l'a **réfuté** : le découplage tient sa métrique (dégâts entrants
**−33 %**) et n'achète que **14 secondes** (60 s → 74 s de survie, cible 5-10 min). Le relevé montre
que **la défense du joueur sature à la 11ᵉ minute**, deux minutes *avant* l'overtime — PV bloqués à
451, DR au cap depuis la 4ᵉ — pendant que le DPS fait **×700** sur la run. Deux causes, toutes deux
corrigées :
**(a)** effet de bord du §30 — `PassiveScaling` amortissait *aussi* les +25 PV/niveau de
`reinforced_plating` (500 → 251 PV à L20), alors qu'il visait `capacitor`/`thermal_core` ; des PV
plats et additifs croissent linéairement et n'ont jamais causé de power-creep. Les PV max sont
désormais la **seule stat exemptée** → **675 PV** mesurés en banc.
**(b)** les **niveaux vides** — le joueur passait du niveau **124 à 140 en 74 s** pour un gain
**nul** (armes L20, passifs saturés retirés du pool), `LevelUpSystem` complétant avec `XP_BONUS` :
de l'XP pour gagner des niveaux qui ne donnent rien. Livré : **`OverloadCards`**, trois cartes de
fin de partie **sans plafond** (Blindage +45 PV max et soigne d'autant · Auto-réparation +0,6 PV/s ·
Surtension +5 % de dégâts), proposées uniquement quand le pool est vide. Nouveau flag
**`--saturate-arsenal`** (le banc n'atteint jamais cet état seul).
**(c)** 2ᵉ session jouée : PV **700 → 2680** en overtime (la courbe n'est plus plate), survie 74 s →
**5 min 18 s**… mais **mort volontaire**, la run « aurait pu durer beaucoup plus longtemps ». Le
pendule était allé trop loin : la menace passait **sous** la défense à 5 min (×2,37 contre ×2,44),
les cartes rapportant ~306 PV/min. → `OvertimeEscalation.StatAcceleration` **1,5 → 3** (la valeur de
1,5 n'existait que parce que la survie était plafonnée — les cartes ont supprimé ce plafond). Le test
ne compare plus la menace à un **seuil absolu** mais à la **pente de la défense**.
**(d)** 3ᵉ session : à ×3, mort **subie** à **1 min 31 s** — trop dur. Valeur retenue **2,25**.
⚠ **La variance entre runs domine le réglage** : à l'entrée en overtime, *où `StatAcceleration` n'a
aucun effet*, deux sessions différaient d'un facteur **2,4** en survie (1060 PV / 28,9 dég/s contre
745 / 48,9) selon que l'arsenal sature vers la 11ᵉ ou la 13ᵉ min. **Une session par réglage mesure
surtout le bruit.**
**(e)** Deux angles morts d'ergonomie signalés par le testeur, corrigés : le **dash n'annonçait sa
touche nulle part** (ni HUD, ni description, ni écran d'assimilation — une run entière jouée sans
savoir qu'une touche existait) → ligne « Shift — esquive » au HUD + rappel à l'acquisition, libellé
lu de l'`InputMap` ; l'**Auto-réparation était crue *active*** (« son effet ne se voit pas ») →
indicateur `♥ +X/s` au HUD + description « automatiquement et en permanence, aucune touche à
presser ». Valeur **inchangée à dessein** : le retour porte sur la lisibilité.
**(f)** Deux correctifs de finition : les **modales étaient assombries par la vignette** (`PostFX` est
à `layer 90` ; level-up 10, fin de run 20 et assimilation 60 passaient dessous) → remontées à 97/98 ;
et les **tirs ennemis écrasaient le mixage** (`sfx_weapon_sentinel_shoot` à −7,5 dB RMS, le plus fort
de la banque, **+9,4 dB au-dessus du tir du joueur**) → nouvelle table `AudioSystem.MixGainDb`,
**−12 dB** après un premier essai à −9 encore jugé trop fort : mixer selon la **polyphonie réelle**
(N sentinelles contre 1 arme), pas seulement selon le niveau du fichier.
**(g)** **`2,25` mesuré — cible atteinte** : 5ᵉ session, mort **subie** à **8 min 36 s** d'overtime
(fenêtre visée 5-10 min). La menace y distance franchement la défense (dégâts subis **×9,1** contre
PV max **×2,11**) et met malgré tout ce temps à tuer. Le relevé **déplace le levier principal** : la
pente de la défense n'est pas une propriété du réglage mais un **choix du joueur** — 270 PV/min sur
les 3 premières minutes, puis **56**, avec un plateau *strictement plat* de près de 2 min pendant
lequel le DPS montait de 27 % (~46 prises de **Surtension** contre 25 de **Blindage**, ratio 1,84 ;
la session précédente donnait l'inverse, 0,80). Même joueur, deux runs, facteur **2,3** sur la pente
de la défense. Les cartes produisent donc bien l'**arbitrage** pour lequel elles ont été écrites, et
c'est lui — pas l'escalade — qui décide de l'heure de la mort. Le repère
`OverloadCards.MeasuredHpGainPerOvertimeMinute = 306` est requalifié : **profil de jeu, pas
constante**.
Design → `docs/GDD.md` §31.6, **§31.7**, **§31.8**, **§33** ; pièges → `docs/PITFALLS.md`
(§Amortissement des passifs, §Cartes de surcharge, §Capacités déclenchées par une touche,
§Calques d'écran, §Mixage des SFX) ; mesures → `docs/TEST_REPORT.md`. **244 tests.**
**Reste** : ressenti à valider en jouant — mixage des tirs ennemis (−12 dB), Auto-réparation
(non instrumentée : `PowerTelemetry` ne journalise pas la régénération), mid-boss jamais joués.

**(2) Mid-boss par biome** (2026-07-29) — dernier point non livré de `docs/EXPANSION_PLAN.md` (B.3).
La faune par biome était complète, mais **trois niveaux sur cinq n'avaient aucun champion de mi-run**
et `master_sentinel` (16 min, pour une run de 13) n'apparaissait **jamais** hors overtime. Livré :
**Colosse en Fusion** (Fournaise — charges télégraphiées laissant un sillage de magma), **Sentinelle
Cryo** (Givre — cône de gel dirigé + plaques dans l'axe), **Gardien Néon** (Néon — bouclier orbital
qui n'absorbe que 20 % des dégâts venus du secteur couvert), + tag de biome des existants et
`master_sentinel` ramenée à 11 min. Le boss de fin ayant déjà une signature par biome (§29), chaque
mid-boss demande le réflexe **inverse** de l'incarnation finale de son biome. Nouveau flag
**`--debug-enemy=<id>`** (spawn isolé) + `tools/capture_midboss.py`. Design → `docs/GDD.md` §32 ;
pièges → `docs/PITFALLS.md` §Mid-boss (dont : **ne jamais dessiner un effet dans le `_Draw` du
champion** — `HitFlash` sature les couleurs à blanc via `Modulate` → `ChampionOverlay`).

**(1) Survie en overtime.** Le point
laissé « à surveiller » en 1.22.0 est instruit : le testeur mourait **1 min après l'entrée en
overtime**, quand l'économie d'Échos est dimensionnée sur **5-10 min** d'overtime (GDD §9.2) — ce
levier de méta-progression était donc inatteignable. Cause : `EnemySpawner` dérivait ses deux temps
de référence l'un de l'autre (`tStat = tDensity + offset`), si bien que l'accélérateur d'overtime
**×4 destiné à la densité** se déversait *en entier* sur les PV et les dégâts, via un terme
**quadratique** — alors que **tous les leviers de densité sont saturés** à l'entrée en overtime (cap
de 300 dès la 8ᵉ min). En face, la survie du joueur est **triplement plafonnée** (`reinforced_plating`
L20, DR 0,40, vitesse 380) : aucune fenêtre possible, quel que soit le skill. Corrigé →
`OvertimeEscalation` (densité ×4 conservé, scaling **×1,5**, courbes découplées). Dégâts entrants à
10 min d'overtime : **×10,9 → ×4,5**. Design → `docs/GDD.md` §31 ; pièges → `docs/PITFALLS.md`
§Escalade d'overtime ; mesures → `docs/TEST_REPORT.md`. **237 tests.**
⚠ **Diagnostic réfuté depuis par la session jouée** : correct sur son propre objectif, mais ce
n'était pas la cause de la mort du joueur → voir le chantier **(3)** ci-dessus.
Avant ça : **courbe de puissance assainie**, publiée
**1.22.0** le 2026-07-28. Le point ouvert de la 1.21.0 est
corrigé : la puissance faisait **×6,42 en 12 min d'overtime** (mesure `PowerTelemetry`, nouveau flag
**`--power-curve`** + `tools/power_curve_session.ps1`). Cause : les 4 passifs ne définissent que
**3 niveaux** pour un plafond de **20**, et au-delà le delta était réappliqué **en additif non
borné** — `capacitor` atteignait **100 % de réduction de recharge dès L8**, mettant *toutes* les
armes au plancher de 0,15 s (une arme lourde tirait aussi vite qu'un canon léger). Corrigé →
`PassiveScaling` (rendements décroissants), `StatCaps.MaxCooldownReduction = 0,75`, passifs saturés
retirés du pool de cartes. Ratio ramené à **×2,73**, et **×1,33** sur la session jouée de validation
(Fournaise, niveau 124 : le Capaciteur s'y arrête de lui-même à L7). Le TTK joué a fixé
`rusted_core.maxHp` à **5000** — le 4000 calculé donnait 18,7 s, sous la fenêtre : *ce boss ne se
calibre que sur un TTK joué*. Deux bugs trouvés dans le journal de cette session, sans rapport avec
l'équilibrage : `(int)GD.Randi() % n` **négatif une fois sur deux** (la récompense de mini-boss était
perdue une fois sur deux, silencieusement) et les sprites de faune `slow_hunter` sans animation
`attack` (144 erreurs/session) → `EnemyBase.PlayAnim`. Design → `docs/GDD.md` §30 ; pièges →
`docs/PITFALLS.md` (§Aléatoire, §Animations d'ennemis, §Passifs) ; mesures → `docs/TEST_REPORT.md`.
**231 tests.**
Avant ça : **fusions d'armes réparées + boss recalibré**,
publiée **1.21.0** le 2026-07-28. Les 9 fusions **divisaient le DPS de fin de run par 3 à 6** (dégâts
en dur jamais multipliés, retour au niveau 1, absentes de tout pool de cartes) : la carte la plus
spectaculaire du jeu en était le pire choix. Corrigé → héritage du niveau, multiplicateurs, montée
par cartes (piège → `docs/PITFALLS.md` §Fusions ; règle → `docs/GDD.md` §8). Dans la foulée,
`rusted_core.maxHp` **12000 → 8000** sur la première mesure de TTK *jouée* (GDD §20.6). Outillage :
`BossTelemetry` (journal `user://boss_ttk.log`), `tools/boss_ttk_session.ps1`, flags **`--auto-play`**
+ **`--timescale`** pour jouer une run complète en banc.
Avant ça : **boss de fin — phases & incarnations**, publié **1.20.0**. Le
Noyau Rouillé reste la condition de victoire unique des 5 niveaux, mais combat désormais en **trois
phases** (100→66→33→0 % de PV, cadences resserrées, adds en phase III, 1 s de surcharge télégraphiée
à chaque bascule) et prend une **incarnation par biome** (éventail dirigé / translocation / nova de
givre / flaques de magma / faisceaux rotatifs), avec sprite et nom propres. Nouvelle **barre de boss**
au HUD (crans aux seuils, numéro de phase). Design → `docs/GDD.md` §29 ; logique pure →
`BossPhases` + `BossIncarnations` (222 tests) ; pièges + checklist « ajouter une incarnation » →
`docs/PITFALLS.md`. **Reste à faire** : mesure de TTK par un testeur humain (le bot de test kite mal).
Avant ça : **options enrichies + accès depuis la pause**, publiées **1.19.0**, et les **paliers de
menace** en **1.18.0**. Avant ça : **bande-son metal industriel & musique adaptative**, publiée **1.17.0** le 2026-07-27. Les 14 musiques sont **générées sur Suno** à partir des
prompts de `docs/AUDIO_AI_PROMPTS.md` (source de vérité de la direction sonore) — metal industriel
/ synth-metal : guitares down-tuned et batterie live au premier plan, synthés et chœurs sans
paroles au service du riff, 112 à 176 BPM. **Licence Suno : plan gratuit = usage non commercial**,
acté pour un jeu distribué gratuitement — monétiser imposerait de regénérer sous plan payant
(`assets/audio/CREDITS.md`). Intégration : déposer les fichiers dans `music_ai/` puis
`python tools/import_ai_music.py` (bouclage, loudness, encodage). Pendant une run,
`MusicDirector` alterne **deux versions du même morceau par biome** (`calm` couplet / `combat`
refrain) + un thème de **boss commun**, par fondu croisé selon l'intensité de l'action — jamais
en superposition, ces pistes ne sont pas synchronisées entre elles. La bande-son synthétisée par
le dépôt (`tools/generate_music_v3.py`, ambiance Vangelis, `docs/ART_BRIEF_AUDIO.md`) reste
régénérable et sert de filet de sécurité sans contrainte de licence. Avant ça : refonte des **cadres d'UI
« plaque blindée »** (chanfreins, bevel, rivets, focus pulsé — `docs/ART_BRIEF_UI_FRAMES.md`)
étendue aux modales / level-up / écrans de sélection puis aux curseurs, interrupteurs et menus
déroulants + correctif audio, publié **1.16.0** le 2026-07-26. Précédemment : **Défis & Récompenses** — 4e levier de rétention (après arsenal / Hub /
Assimilation), publié **1.15.0** le 2026-07-08. 13 défis évalués en fin de run → Échos, **perks de
départ** (greffe offerte / arme sup / +1 slot) ou **titres** cosmétiques. Nouvel écran **Défis**
(progression X/N), sélection perk/titre au Hub, flair du titre sur le menu. Menu principal réorganisé
(sous-menu **Codex** regroupant Bestiaire/Arsenal/Chimère/Défis/Perks) + sélecteur de langue à
**drapeaux**. Correctif : cooldown de la Frappe Nova visible au HUD. Détail :
`docs/DESIGN_CHALLENGES.md`. Avant ça : **Assimilation** (Phase A+B, 1.12.0→1.14.0,
`docs/DESIGN_ASSIMILATION.md`). Détail dans `docs/PROJECT_STATE.md`.

## Équipe d'agents

Agents dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`, `developpeur`,
`musicien`, `story-teller`, `marketing`, `game-tester`, `release-manager`. Déléguer proactivement à
l'agent compétent (ordre de lancement : `GUIDE-CLAUDE-CODE.md`).

- **`game-tester`** : lance Godot (`--rendering-driver d3d12`), joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.
- **`release-manager`** : publie la release binaire de bout en bout (bump semver, release notes, `tools/release_itch.ps1`, MAJ doc) puis **rédige le devlog** (titre + corps à coller) — l'utilisateur le publie lui-même sur itch (l'agent ne pilote pas le navigateur). Source des notes : `docs/DEVLOG.md`.

## Maintenance de la doc

- `README.md` (racine) — MAJ à chaque changement de phase / ajout majeur (tableau des phases, roadmap, captures).
- `docs/PROJECT_STATE.md` + `docs/GDD.md` + `/carte-projet` + `docs/PITFALLS.md` — MAJ dans le commit qui change ce qu'ils décrivent.

## Conventions

- Plateforme cible : Windows (.exe). Moteur : **Godot 4.7 .NET** (toujours la variante `.NET`, jamais la standard). Langage : C# (.NET 8), GodotSharp.
- Build Windows : `"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"`
- **CRITIQUE export .NET** : `ChimeraProtocol.sln` DOIT être présent à la racine (sinon le .exe crashe au lancement). Recréer : `dotnet new sln --name ChimeraProtocol --format sln && dotnet sln ChimeraProtocol.sln add ChimeraProtocol.csproj`. L'export produit `build/ChimeraProtocol.exe` + `build/data_ChimeraProtocol_windows_x86_64/` (runtime .NET 8, ignoré par git, régénéré).
- **Publication & MAJ auto (itch.io + Butler)** : incrémenter `config/version` dans `project.godot`, puis skill **`/publier-itch`** (ou `tools/release_itch.ps1 -Version X.Y.Z`), ou déléguer à l'agent **`release-manager`** (pipeline complet + devlog itch). Runbook : `docs/RELEASE.md`. Notes de version cumulées : `docs/DEVLOG.md`. Un push = auto-update pour les joueurs de l'app itch.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut.
- Architecture : `src/` (logique C#) / `scenes/` (.tscn) / `assets/` (raw) / `data/` (JSON tuning modifiable sans recompiler).
- **Logique pure testable** : `src/Core/Rules/` (classes statiques sans dépendance Godot — `XpCurve`, `EnemyScaling`, `EliteAffixTable`…). Les nœuds y délèguent (SRP).
- **Tests unitaires** : xUnit, `dotnet test tests/ChimeraProtocol.Tests.csproj`. **237 tests**.
- **Difficulté** : trois axes multiplicatifs — réglage du joueur (`DifficultyTuning`), **palier de menace du niveau joué** (`LevelThreat`, croît avec l'ordre de déblocage : PV/dégâts/densité/Échos, cf. `docs/GDD.md` §28) et escalade d'overtime.
- Singletons (AutoLoad) : `GameManager`, `XpSystem`, `InventorySystem`, `LevelUpSystem`, `SaveManager`, `MetaProgressionSystem`, `ChallengeSystem` (défis/succès, `docs/DESIGN_CHALLENGES.md`), `AudioSystem`, `MusicDirector` (musique adaptative : calm/combat/boss en fondu croisé), `FusionFlash`, `ScreenShake`, `GameSettings`, `DiscordPresence` (Rich Presence), `VersionStamp` (tampon `v<ver>-<sha>` bas-droite).
- Sauvegarde : `user://save.json` (méta/Échos) + `user://settings.cfg` (préférences, high scores, complétions, armes découvertes).
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48 — exception), `texture_filter = Nearest` global. Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py` — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()`, jamais des couleurs plates ad hoc.
- **Audio** : musique **générée sur Suno** (prompts : `docs/AUDIO_AI_PROMPTS.md`) — jamais éditer
  un `.ogg` de `assets/audio/music/` à la main. Pour en remplacer une : regénérer sur Suno, déposer
  dans `music_ai/`, puis `python tools/import_ai_music.py [--only <id>] [--keep-preview]` et
  `godot --headless --import`. Contrôle : `tools/check_music_assets.gd`. Bande-son de secours
  synthétisée (sans contrainte de licence) : `tools/generate_music_v3.py`, `docs/ART_BRIEF_AUDIO.md`.
  SFX = WAV Kenney CC0. Crédits et **licence Suno (plan gratuit, non commercial)** :
  `assets/audio/CREDITS.md`.
- Localisation EN/FR/ES : `localization/ui.csv` → `Loc.T("CLÉ")`.
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0.45 s (CRITIQUE pour les nuées).
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`. Police : Share Tech Mono (AA on, `ui_theme.tres`, size 16) ; VT323 en réserve (AA off).
- **Cadres & couleurs d'UI** : toujours via `src/UI/UiPalette.cs` (couleurs) et `src/UI/UiStyle.cs` (fabrique des cadres « plaque blindée » — chanfreins, bevel, rivets, focus pulsé). Jamais de `StyleBoxFlat` ad hoc ni de couleur en dur, ni dans le C# ni dans les `.tscn`. Parti pris → `docs/ART_BRIEF_UI_FRAMES.md` ; assets → `tools/generate_ui_frames.py`.
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH).

## Pièges critiques → `docs/PITFALLS.md`

Tous les pièges non-évidents (API Godot C# manquante, callbacks/threading, checklists de câblage
armes/ennemis, affixes d'élite, VFX parentés à la racine, navigation clavier/manette, StyleBox/focus,
cycle de vie des scènes, assets `.import`, tests headless) sont dans **`docs/PITFALLS.md`**.
**Le consulter avant de coder dans le domaine concerné.**
