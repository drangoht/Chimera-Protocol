# État du projet — Chimera Protocol

> Détail chargé **à la demande** (pointé depuis `CLAUDE.md`). Liste évolutive de ce qui est
> implémenté — la mettre à jour à chaque ajout/refonte majeur. Le résumé de phase reste dans
> `CLAUDE.md` ; le design complet dans `docs/GDD.md` ; la carte du code dans `/carte-projet`.

- Pile technique : **Godot 4.7 .NET (C# / .NET 8 / GodotSharp)**
- **Bande-son metal industriel & musique adaptative (2026-07-27, NON PUBLIÉE).** Le jeu tournait
  sur des placeholders chiptune CC0 (Juhani Junkala) enchaînés par bascules de piste à 5 et 10 min
  de run. Une première refonte l'a remplacé par 26 pistes synthétisées par le dépôt (ambiance
  Blade Runner / Vangelis, 4 stems synchronisés par biome) — **écoutée puis écartée le même jour :
  trop lente et trop contemplative** pour un jeu où l'écran se remplit de monstres.
  **Bande-son en vigueur : 14 pistes générées sur Suno**, direction metal industriel / synth-metal
  (guitares down-tuned + batterie live au premier plan, synthés et chœurs sans paroles au service
  du riff — Mick Gordon, Carpenter Brut). Tempos 112 à 176 BPM. Prompts par piste :
  `docs/AUDIO_AI_PROMPTS.md`. Les 3 stingers (mort/victoire/level-up) restent synthétisés par le
  dépôt. **Licence Suno à confirmer avant publication** (plan d'abonnement) —
  `assets/audio/CREDITS.md`.
  **Pipeline d'intégration** : `tools/import_ai_music.py` prend les MP3 déposés dans `music_ai/`
  et fait ce qu'aucun générateur ne fait — détection du meilleur point de boucle par corrélation
  FFT (en écartant les baisses d'énergie, pour ne jamais boucler sur une outro), fondu de raccord,
  calage EBU R128, encodage OGG et nommage attendu par le moteur.
  **Musique adaptative** : chaque biome fournit deux versions du même morceau — `calm` (couplet,
  riff en retenue) et `combat` (refrain, tout ouvert) — plus `music_run_boss.ogg` commun aux cinq
  biomes. Les pistes étant des générations indépendantes (donc non synchronisées), `MusicDirector`
  (autoload) n'en rend **qu'une audible à la fois** et bascule par **fondu croisé à puissance
  constante** (3 s ; 2 s pour le boss). L'intensité (0-1) est calculée par la logique pure
  `MusicIntensity` depuis les ennemis à l'écran (poids 0,5, en racine : les premiers ennemis
  comptent le plus), le temps écoulé (0,3) et les PV du joueur (0,2), lissée 3× plus lentement en
  descente qu'en montée. Le choix de piste est protégé par une **hystérésis** (entrée 0,42 /
  sortie 0,26) et une durée de maintien de 10 s — sans quoi une intensité qui oscille ferait
  battre les pistes ; le boss court-circuite ce délai et démarre à son premier temps.
  Tonalités/tempos par biome : Sanctuaire Do min 140 BPM · Aether Ré phrygien dominant 152 ·
  Givre La dorien 130 (groove half-time) · Fournaise Sol phrygien 176 · Néon Mi mixolydien 160 ·
  boss Do min chromatique 150. Contrôle : `tools/check_music_assets.gd` (headless). Pièges
  (superposition interdite, fondu à puissance constante, hystérésis) : `docs/PITFALLS.md`.
  28 tests unitaires dédiés (167 au total). La bande-son synthétisée reste **régénérable** par
  `tools/generate_music_v3.py` et sert de filet de sécurité sans contrainte de licence
  (`docs/ART_BRIEF_AUDIO.md`).
- **Refonte des cadres d'UI — « plaque blindée » (PUBLIÉE 1.16.0, 2026-07-26).** Les cadres de
  boutons, cartes et popups recopiaient tous la même recette `StyleBoxFlat` (bordure uniforme +
  `corner_radius` arrondi, rayons 3/4/6/8/10 sans règle) sur ~300 sites : aucune identité, et
  l'arrondi anti-aliasé jurait avec le rendu `Nearest` des sprites. Désormais : coins chanfreinés
  (jamais arrondis), bevel reprenant la direction de lumière des sprites (`LIGHT_DIR` haut-gauche
  via `pseudo3d_lib.shade()`), rivets d'angle, bord « soudé » asymétrique, et un focus signalé par
  trois signaux cumulés — débordement de forme, liseré allumé, pulsation — au lieu d'un passage de
  bordure de 2 à 3 px. Socle : `src/UI/UiPalette.cs` (palette unique — la charte n'existait que
  dans la doc, le cyan était réécrit dans 8 blocs et ~20 littéraux, avec deux « fonds officiels »
  concurrents) et `src/UI/UiStyle.cs` (fabrique unique). 19 textures 9-slice dans
  `assets/sprites/ui/frames/`, régénérables par `tools/generate_ui_frames.py`. 21 `StyleBoxFlat`
  inline purgées de 4 scènes (`tools/strip_tscn_styleboxes.py`) — les laisser aurait rendu le
  nouveau style invisible. Étendue ensuite aux modales, à l'écran de level-up et aux écrans de
  sélection, puis aux **sliders/toggles/dropdowns d'Options** (rail creusé + poignée en plaque pour
  les curseurs, lecture par position du pavé pour les interrupteurs, `PopupMenu` câblé via
  `ApplyDropdownFrames` pour les listes déroulantes) — `tools/generate_ui_widgets.py`,
  `UiStyle.ApplySliderStyles`/`ApplyToggleStyles`/`ApplyPopupMenuStyles`. Parti pris et specs
  chiffrées : `docs/ART_BRIEF_UI_FRAMES.md`. Hors périmètre assumé : le HUD in-game, les puces de
  buff (couleur dynamique par buff), les drapeaux de langue (44×30 px, trop petits pour la plaque).
- **Fix audio (1.16.0)** : `AudioSystem` héritait du `ProcessMode` Pausable de la racine — musique et
  pool SFX se coupaient dès qu'une modale mettait le jeu en pause (level-up, pause, Assimilation, fin
  de run). `ProcessMode = Always` sur l'autoload corrige le problème et fait sonner les SFX d'UI dans
  les menus pausés (`src/Systems/AudioSystem.cs`).
- **Phase actuelle : libre** — dernière livraison majeure : **Assimilation en ligne (1.12.0,
  2026-07-07)** — 3e axe de progression publié pour la première fois (Phase A + Phase B volet 1 +
  écran Codex Chimère, tout ce qui suit était encore « non publié » à la sortie de 1.11.4) : 5
  greffes (Nuée Symbiotique, Servos Erratiques, Œil de Visée, Carapace Greffée, Onde du Rôdeur),
  2 fusions (**Charge Blindée** = Carapace+Servos → dash devient charge blindée ; **Ruche de
  Tourelles** = Œil+Nuée → 4 tourelles 360° + lifesteal), nouvel écran **`ChimeraCodexScreen`**
  (menu principal) expliquant greffes/fusions, HUD des greffes agrandi + liseré magenta (fin du
  recouvrement par la BuffBar). Détail chiffré : `docs/DESIGN_ASSIMILATION.md`. Version publiée
  itch : **1.12.0**.
- Dernières livraisons précédentes : **Discord Rich Presence** (`DiscordPresence`,
  statut « joue à Chimera Protocol » + tampon de version `v<ver>-<sha>` bas-droite `VersionStamp`, 2026-07-05),
  **nouveau perso Vecteur** (cyborg de précision, arme de base Lance Vectorielle dirigée, 2026-07-05),
  **remap clavier + ZQSD par défaut**
  (`src/Systems/InputRemap.cs`, section Contrôles des Options, 2026-07-05), visée souris/stick + réticule
  Lance Vectorielle (1.8.0), fusions Rayon Vecteur & Voile de Givre (brume de froid + ennemis gelés,
  1.8.1), affixes d'élite. **Correctif carte de level-up** (texte ancré sous l'icône, fini le chevauchement
  sur descriptions longues type fusions, 1.11.1). **Polish VFX biome Givre** (rendu ennemi gelé refait par
  shader `enemy_frost.gdshader` — lerp vers bleu glacial franc au lieu d'un multiply qui ternit ; brume du
  Voile de Givre densifiée en 6 puffs volumétriques, `src/Weapons/FrostVeil.cs`, 1.11.2). **Poussée d'ennemis**
  (le joueur écarte les ennemis qui chevauchent son corps au lieu de les traverser, sans perte de vitesse ni
  perte des dégâts de contact, `Player.PushEnemiesAside()`, poussée dans le sens du déplacement si ennemi centré,
  1.11.3) et **fix occultation obstacles** (correction de z-index : les obstacles infranchissables dessinent
  désormais au-dessus du joueur, ombre re-ancrée au sol, dans les 5 biomes, 1.11.3). **Rééquilibrage boss de fin**
  (Le Noyau Rouille jugé impossible à tuer : PV de base 18000→12000 dans `data/enemies.json` — PV effectifs à
  13 min ~21360 en Normal au lieu de ~32040 — + fix `EnemySpawner.SpawnOvertimeBoss` qui bypassait
  `maxSimultaneous:1` et laissait plusieurs boss s'empiler en overtime, cause principale du ressenti
  « impossible » ; TTK mesuré ~36-40 s sur build de référence, cible ~43-61 s build moyen, 1.11.4).

### Défis / Succès — **Lot 1 socle** (✅ 2026-07-08, non publié)

4e levier de rétention (après arsenal / Hub / Assimilation). Objectifs explicites évalués à la fin de
run, récompensés en Échos (immédiat) ou en perks/cosmétiques débloqués (équipables aux lots 3-4). Cf.
`docs/DESIGN_CHALLENGES.md`.
- **`ChallengeTable`** (`src/Core/Rules/`, logique pure — **+16 tests**, suite à **140**) : parse
  `data/challenges.json`, `ChallengeContext` (instantané plat fin de run), `IsMet`/`NewlyCompleted`.
- **`ChallengeSystem`** (autoload) : agrège le contexte (RunStatsTracker + AssimilationSystem +
  GameSettings + compteurs cumulés), octroie les récompenses, émet `ChallengeUnlocked`. **Ne charge
  jamais sa propre SaveData** — mute `MetaProgressionSystem.Meta` puis `PersistMeta()` (piège save.json).
- **Persistance** : `MetaSaveData` étendu (`UnlockedChallenges`/`UnlockedPerks`/`UnlockedCosmetics`/
  `LifetimeKills`/`LifetimeRuns`). Hook dans `RunStatsTracker.EndRun`. `RunEndScreen` affiche une ligne
  dorée « ★ Défi accompli ». **13 défis** (combat/survie/assimilation/maîtrise), loc `CHAL_*` EN/FR/ES.
- **Lot 2 écran Défis** (✅ 2026-07-08) : `ChallengesScreen` (sous-classe `CodexScreenBase`) — liste
  tous les défis (objectif + récompense + statut accompli/à faire), progression `X/N`, icône par
  récompense ; bouton « Défis » au MainMenu. Vérifié visuellement (capture). Loc EN/FR/ES.
- **Lot 3 perks de départ** (✅ 2026-07-08) : `MetaSaveData.EquippedPerk` (1 seul équipé) ; registre
  `StartingPerks` ; section « Perk de départ » au Hub (chips sélectionnables, masquée si aucun débloqué) ;
  application au run start (`GameManager.ApplyStartingPerkHook`) — greffe offerte (`GrantStartingGraft`),
  glaive en 2e arme, ou +1 slot (`AddBonusSlots`). Vérifié en boot Game.tscn. Loc EN/FR/ES.
- **Lot 4 cosmétiques/titres** (✅ 2026-07-08) : `MetaSaveData.EquippedCosmetic` ; registre `Titles`
  (La Chimère / Prédateur Alpha / Exterminateur) ; section « Titre » au Hub (infra chips générique
  partagée avec les perks) ; flair affiché sous le logo du MainMenu (`ApplyTitleFlair`). Aucun effet
  gameplay. Vérifié visuellement. **Boucle de rétention complète de bout en bout** (défi → récompense →
  équipement → effet).
- **Retouches playtest** (✅ 2026-07-08, retours de l'utilisateur après 1re session réelle — 9/13 défis
  débloqués en jeu) : (1) le voile de recharge du HUD gérait `dash`/`charge` mais pas l'effet `novaDash`
  → la **Frappe Nova** affiche enfin son cooldown (`HUD.RefreshGraftSlots`) ; (2) **sélecteur de langue**
  déplacé en haut à droite avec **drapeaux** FR/EN/ES (`tools/gen_lang_flags.py` → `assets/sprites/ui/
  flag_*.png`) ; (3) **menu principal désencombré** : Bestiaire/Arsenal/Chimère/Défis regroupés sous un
  **sous-menu Codex** (`CodexMenuScreen`) → MainMenu à 5 entrées ; (4) nouvel écran **Perks**
  (`PerksScreen`) décrivant les perks de départ, ajouté au sous-menu Codex. `CodexScreenBase.BackScenePath`
  → retour au sous-menu Codex.
- **Lot 5 — PUBLIÉ 1.15.0 le 2026-07-08** : seuil `exterminator` 10000→30000 (jalon prestige) ; README +
  DEVLOG (« Challenges & rewards » EN/FR) ; export .NET (189 fichiers) + `butler push` OK (patch 633 KiB,
  99,76% éco, build processing) ; `version.json`→1.15.0 (bandeau MAJ web) ; tampon `464a7e4` ; tout poussé
  sur `origin/main`. **Chantier Défis & Récompenses COMPLET et EN LIGNE.** Reste côté utilisateur : coller
  le devlog v1.15.0 sur itch (l'agent ne pilote pas le navigateur).

### Système d'Assimilation / Greffes — **Phase A + Phase B volet 1** (✅ publié 1.12.0, 2026-07-07)

Troisième axe de progression (« deviens la chimère »), cf. `docs/DESIGN_ASSIMILATION.md` Partie II.
Livré en Phase A :
- **`GraftTable`** (`src/Core/Rules/`, logique pure testée — **+25 tests xUnit**, suite à 112) : parse
  `data/grafts.json`, routage kill→jauge (`RouteKill` : basique/élite/mini-boss/boss → jauge d'archétype
  et/ou `stalker`), seuils effectifs (bonus méta `graft_metabolism`) et de refus (×1,5), `SlotCount`.
- **`AssimilationSystem`** (autoload) : jauges de points par archétype (`Dictionary<string,float>`),
  slots équipés + remplacement, pause de jauge d'une greffe possédée + reprise depuis valeur mémorisée,
  émet `GaugeFilled`. `Reset()` par run lit `graft_slots`/`graft_metabolism`.
- **`AssimilationScreen`** (`src/UI/`, scène `scenes/ui/AssimilationScreen.tscn`) : écran modal magenta,
  slot libre → ASSIMILER/REJETER, slots pleins → remplacer/CONSERVER. Partage **`ModalQueue`** avec le
  LevelUpScreen (un seul `Paused`, level-up prioritaire, jamais simultanés).
- **5 greffes** (`GraftManager`, enfant du Player) : Nuée Symbiotique (3 mini-essaims orbitants +
  lifesteal), Servos Erratiques (dash invulnérable, action d'entrée `dash` = Maj gauche/RB), Œil de Visée
  (tourelle auto réutilisant `Bullet`), Carapace Greffée (+DR/+PV/thorns, malus −18% via
  `Player.GraftSpeedMultiplier`), Onde du Rôdeur (onde de choc périodique + knockback, réutilise
  `ShockwaveRing`). Retrait propre au remplacement (deltas de stat réversibles, hardcaps respectés).
- **Rendu Phase A minimal** : rangée d'emplacements de greffe au HUD (sous la barre XP), teinte additive
  cumulée sur `SelfModulate` du joueur, `FusionFlash` à l'assimilation.
  - **Icônes de greffe au HUD livrées (2026-07-06)** : `HUD.RefreshGraftSlots()` affiche la texture
    `def.HudIcon` (`assets/sprites/grafts/<id>_icon.png`) via un `TextureRect` (Nearest,
    `KeepAspectCentered`, ~16-18 px dans le slot de 20 px), même pattern de chargement que
    `AssimilationScreen.LoadGraftIcon` (`Godot.FileAccess.FileExists` + `GD.Load<Texture2D>`).
    **Fallback carré teinté conservé** si l'icône est absente ; slot vide toujours grisé.
- **Méta Hub** : `graft_slots` (500/950, +1 slot, max 5) et `graft_metabolism` (180/320/520, −30% seuil max)
  dans `meta_upgrades.json` (arbre → 19 items). Codex : découvertes persistées (`GameSettings.DiscoverGraft`).
- **Phase B volet 1 — Fusions de greffes (2026-07-06)** : 2 greffes prérequises se lient en 1 fusion
  (occupation 2→1, un slot libéré). **Charge Blindée** (Carapace+Servos : le dash devient une charge
  240 px / 45 dmg + knockback, tank conservé, malus vitesse allégé) et **Ruche de Tourelles**
  (Œil+Nuée : 4 essaims → 4 tourelles en suivi lerp, ~48 DPS 360° + lifesteal). Jauge de fusion dédiée
  (`fusion_<id>`) qui n'accumule que si les 2 prérequis sont équipés (routage `AssimilationSystem.
  RouteFusionKill` + garde) ; carte de fusion sur `AssimilationScreen` (2 boutons, jamais de
  remplacement) ; `FusionFlash` à l'acceptation. Data-driven (`data/grafts.json` → section `fusions`),
  logique pure `GraftTable.FusionDef` (+7 tests xUnit → **119**). Comportements côté nœuds : charge
  (`Player` — couloir de dégâts, contourne `MaxSpeed`, i-frames en max), tourelles (`GraftManager`,
  réutilise `Bullet`). Détail chiffré : `docs/DESIGN_ASSIMILATION.md` §15. Clés loc `GRAFT_FUSION_*`/
  `ASSIM_FUSE`/`ASSIM_FUSION_*` posées (placeholder à finaliser `story-teller`) ; icônes
  `fusion_*_icon.png` à produire (`graphiste`, fallback carré teinté).
- **Écran Codex « Chimère » (2026-07-07)** : `ChimeraCodexScreen` (`src/UI/`, scène
  `scenes/ui/ChimeraCodexScreen.tscn`), accessible depuis le menu principal au même rang que
  Bestiaire/Arsenal — liste les 5 greffes + 2 fusions (icône, effet, prérequis), même socle
  `CodexScreenBase` (scroll clavier/manette).
- **Lisibilité HUD des greffes (2026-07-07)** : la rangée de la `BuffBar` (power-ups temporaires)
  recouvrait la rangée d'emplacements de greffe — emplacements agrandis + liseré magenta, fin du
  chevauchement (`f1c7431`, `21f18c4`).
- **Silhouette-chimère — Phase B volet 2 (2026-07-07)** : le corps du joueur **change visuellement**
  selon les greffes/fusions équipées (fini la simple teinte). **Props attachés** procéduraux ombrés
  pseudo-3D, indépendants du personnage (4 corps jouables) : Carapace (plastron+pauldrons), Servos
  (tuyères+vents qui s'embrasent au dash), Œil (orbe flottant, pupille qui vise), Onde (couronne-
  résonateur qui enfle avant l'onde), Charge Blindée (proue orientée au facing), Ruche (cœur de ruche).
  La Nuée/Ruche utilisaient déjà leurs essaims/tourelles comme silhouette. Impl. `GraftManager`
  (`GraftProp`/`BuildPropFor`/`UpdateProps`, ombrage `Shade`/`BaseColorFromTint`), miroir via
  `Player.FacingLeft`. Flag debug `--force-graft=<id|all>`, outil `tools/capture_graft_silhouette.py`.
  Détail : `docs/DESIGN_ASSIMILATION.md` §19. Validé visuellement, 119 tests verts. **Non publié.**
- **3e fusion — Frappe Nova (2026-07-07, `fusion_nova_rodeur`)** : Onde du Rôdeur + Servos Erratiques.
  Le dash devient une **téléportation offensive** : blink 190 px + i-frames, puis **nova** au point
  d'arrivée (onde de choc 175 px / 80 dmg / knockback 90, gatée par la recharge du dash). L'onde passive
  devient un burst positionnel visé. **Partage `erratic_servos` avec Charge Blindée → mutuellement
  exclusives** (choix de build ram blindé vs blink-nova ; l'infra fusion existante absorbe le partage).
  Data-driven (effet `novaDash`, 0 changement `GraftTable`), helper partagé `EmitShockwave`, prop cœur
  d'étoile pulsant. Détail : `docs/DESIGN_ASSIMILATION.md` §15.8. **Non publié.**
- **Variantes de greffe par biome — affinités (2026-07-07)** : **où** tu assimiles compte. Une greffe/
  fusion capture le biome courant et gagne son **affinité** (5 leviers) : Sanctuaire +12% dégâts,
  Aether +20% portée, Fournaise **brûlure** on-hit, Givre **ralentissement** on-hit, Néon −18% cooldown.
  damage/radius/cooldown sur toutes les greffes ; burn/slow sur dégâts directs (Nuée/thorns/onde/nova)
  + balles (Œil/Ruche, `Bullet.BurnDps/SlowMult`). Data-driven (`biomeAffinities` de `grafts.json`),
  logique pure `GraftTable.BiomeAffinity`/`GetAffinity` (+5 tests → **124**). Carte d'assimilation
  affiche l'affinité gagnée ici ; accent biome baké dans le prop de silhouette. Rejouabilité : une Nuée
  brûle en Fournaise, gèle en Givre. Détail : `docs/DESIGN_ASSIMILATION.md` §21. **Non publié.**
- **Phase B TERMINÉE.** Reste optionnel : textes/lore/loc à peaufiner par `story-teller` ; icônes de
  greffe/fusion à produire par `graphiste` (fallback carré teinté en attendant).

## Ce qui est implémenté

- Direction artistique **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) appliquée à TOUS les sprites via `tools/pseudo3d_lib.py` (lumière fixe haut-gauche 45°, dérivation shadow/highlight HSV, ombre portée elliptique) : 3 persos joueurs, 8 ennemis/mini-boss/boss existants, 20 nouveaux ennemis, obstacles, tuiles de biome, icônes d'armes/UI (640 PNG régénérés, `.import` à jour). Validé game-tester PASS 2026-07-03 (cohérence lumière, lisibilité joueur en nuée).
- 4 personnages jouables (Chimera/Canon à Impulsions, Titan/Essaim de Drones, Vagabond/Lame Plasma, **Vecteur/Lance Vectorielle** — cyborg de précision, arme dirigée, ajouté 2026-07-05), 5 biomes (Sanctuaire, Aether, Fournaise, Givre, Néon)
- 12 armes actives + 9 fusions + 4 passifs ; power-ups temporaires (4 types)
  (`vector_lance` = 1re arme **dirigée** : tire vers `Player.AimDirection`, pas l'ennemi le plus proche — cf. GDD §23 ;
  sa fusion `vector_beam` + servo_motors = **rayon perforant continu dirigé** ;
  `frost_veil` = cryo_lance + reinforced_plating → **aura de givre continue** (dégâts + slow radial, contrôle défensif))
  (fusions : fusion_blade, rail_overcharged, orbital_swarm, overload_aegis, ionic_storm,
  solar_column, hornet_swarm — chaque évolution = arme de base niv.5 + passif requis, remplace l'arme)
- Fin de niveau complète : survie sans fin, overtime, boss en boucle, déblocage progressif, high scores (temps+difficulté), arsenal à découverte
- Hub méta rééquilibré (2026-07-02) : 17 upgrades (7 rééquilibrés + 10 nouveaux ; `starting_weapon_alt` retiré 2026-07-04 car aucun sélecteur d'arme de départ n'est câblé), formule d'Échos plafonnée standard/overtime (`EchoFormula.Calculate`, caps + `overtimeDampening`/`overtimeBonusCap`), 5e composante "Bonus de Surcharge" sur `RunEndScreen`, `UpgradesList` scrollable
- Cinématique d'intro (2026-07-03, **plan Assimilation ajouté 2026-07-06**) : `src/UI/IntroScreen.cs` (scène de boot) — cut-scene 2D scriptée en **6 plans narratifs + reveal du titre** (noyau d'Aether, corruption d'un drone, nuée + colosse, sanctuaire, descente de l'Arpenteur, **assimilation**), sprites animés réutilisant les `SpriteFrames` existants + particules `CpuParticles2D` + zoom caméra via `Tween`, synchronisée sur la narration `INTRO_BEAT_1..6` (EN/FR/ES) et la musique dédiée `music_intro` (CC0, "Transmission"/SRG774, cf. `assets/audio/CREDITS.md`). Skippable. Reveal via clés `INTRO_TITLE`/`INTRO_TAGLINE` (tagline alignée sur le pitch : « Ne tue pas les monstres. Deviens-les. »). **Plan 6 `ShotAssimilation`** (`INTRO_BEAT_6`, 4,0 s, cf. `docs/DESIGN_ASSIMILATION.md` §20) : mise à mort d'un Rust Swarm → arrachement d'un fragment (particules rouille→cyan vers le joueur) → mutation (aura `FusionFlash`/`FusionAura` + teinte subtile du joueur), n'utilise que des assets déjà chargés. Outil de capture : `tools/capture_intro.py`
- Localisation EN/FR/ES (`localization/ui.csv` → clé `Loc.T("CLÉ")`) ; support manette complet
- HUD thématisé par biome, atmosphère (brume/rais/parallaxe), scanlines CRT
- Arènes : obstacles thématisés par biome (`BiomeObstacles.cs`), features de sol (`FloorFeatures.cs` — lave/rivières/chemin pavé/conduits), gabarits structurés, décor rouillé réservé au Sanctuaire ; flag `--biome=<id>` pour forcer un biome (tests/captures)
- Faune par biome (2026-07-03) : **28 ennemis basiques au total** (8 d'origine + 20 nouveaux, 4/biome), câblés via sprite data-driven (`EnemyBase.SetSpriteFrames` + `EnemySpawnData.FramesPath`/`AiType`) — aucune nouvelle scène/sous-classe, réutilise les 4 scènes archétype existantes (cf. `docs/GDD.md` §21). Sprites générés (`tools/generate_new_enemies.py`). Densité par biome doublée (spawnWeight dilué mais compensé par les ids globaux toujours actifs) — validé game-tester PASS. Limite connue : les 5 variantes d'un même archétype partagent une silhouette recolorée (pas de nouvelle forme par ennemi) — à arbitrer si plus de variété visuelle est souhaitée
- **Affixes d'élite (2026-07-04)** : une fraction des ennemis *basiques* (jamais mini-boss/boss) est promue élite avec 1 affixe parmi 5 (Blindé/Régénérant/Explosif/Frénétique/Vampirique) — cf. `docs/GDD.md` §22. Logique pure testée `src/Core/Rules/EliteAffixTable.cs` (fréquence `clamp(0.03+0.02×t, 0, 0.28)`), appliquée par `EnemyBase.ApplyElite` (stats après `ApplyScaling` + comportement + rendu teinté/agrandi + halo `EliteAura`), tirée dans `EnemySpawner.SpawnEnemy`. Répond à la limite « silhouettes recolorées » (variété = comportement). Flag debug `--force-elites`. Répond au brainstorm « inspirations d'autres jeux » (élites façon Risk of Rain 2 / Diablo)
- **Correctifs 2026-07-04** : purge des VFX/projectiles résiduels par-dessus le menu/Hub à la sortie de run (`SceneCleanup.ClearWorldVfx`, cf. `docs/PITFALLS.md`) ; retrait de l'upgrade Hub sans effet `starting_weapon_alt`
- **Retours testeur 2026-07-04** (cf. `docs/GDD.md` §23) : (1) `Player.ZIndex=5` — le joueur reste visible au-dessus des flammes/VFX d'armes ; (2) **Lance Vectorielle** (`vector_lance`, Rare) — arme dirigée vers `Player.AimDirection` (skill de visée : **souris** en clavier/souris, **stick droit** en manette, + réticule autour du joueur — MAJ 2026-07-04), réutilise `Bullet`, éventail aux niv. 4-5 ; (3) **courbe de difficulté non-linéaire** `EnemyScaling.CurvedFactor`/`ScaledCurved` (early grace −15% à t=0 puis accélération quadratique après 4 min) branchée dans `EnemySpawner` — le late rattrape le power-creep du build. 4 tests ajoutés (87 au total)

Voir aussi `docs/EXPANSION_PLAN.md` et `docs/LEVEL_PROGRESSION_PLAN.md` pour le détail des plans.
