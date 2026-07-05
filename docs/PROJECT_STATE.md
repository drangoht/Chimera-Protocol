# État du projet — Chimera Protocol

> Détail chargé **à la demande** (pointé depuis `CLAUDE.md`). Liste évolutive de ce qui est
> implémenté — la mettre à jour à chaque ajout/refonte majeur. Le résumé de phase reste dans
> `CLAUDE.md` ; le design complet dans `docs/GDD.md` ; la carte du code dans `/carte-projet`.

- Pile technique : **Godot 4.7 .NET (C# / .NET 8 / GodotSharp)**
- **Phase actuelle : libre** — dernières livraisons : **nouveau perso Vecteur** (cyborg de précision,
  arme de base Lance Vectorielle dirigée, 2026-07-05), **remap clavier + ZQSD par défaut**
  (`src/Systems/InputRemap.cs`, section Contrôles des Options, 2026-07-05), visée souris/stick + réticule
  Lance Vectorielle (1.8.0), fusions Rayon Vecteur & Voile de Givre (brume de froid + ennemis gelés,
  1.8.1), affixes d'élite. Version publiée itch : **1.9.0**.

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
- Cinématique d'intro (2026-07-03) : `src/UI/IntroScreen.cs` (scène de boot) — cut-scene 2D scriptée en 6 plans (noyau d'Aether, corruption d'un drone, nuée + colosse, sanctuaire, descente de l'Arpenteur, reveal du titre), sprites animés réutilisant les `SpriteFrames` existants + particules `CpuParticles2D` + zoom caméra via `Tween`, synchronisée sur la narration `INTRO_BEAT_1..5` (EN/FR/ES) et la musique dédiée `music_intro` (CC0, "Transmission"/SRG774, cf. `assets/audio/CREDITS.md`). Skippable. Reveal via clés `INTRO_TITLE`/`INTRO_TAGLINE`. Outil de capture : `tools/capture_intro.py`
- Localisation EN/FR/ES (`localization/ui.csv` → clé `Loc.T("CLÉ")`) ; support manette complet
- HUD thématisé par biome, atmosphère (brume/rais/parallaxe), scanlines CRT
- Arènes : obstacles thématisés par biome (`BiomeObstacles.cs`), features de sol (`FloorFeatures.cs` — lave/rivières/chemin pavé/conduits), gabarits structurés, décor rouillé réservé au Sanctuaire ; flag `--biome=<id>` pour forcer un biome (tests/captures)
- Faune par biome (2026-07-03) : **28 ennemis basiques au total** (8 d'origine + 20 nouveaux, 4/biome), câblés via sprite data-driven (`EnemyBase.SetSpriteFrames` + `EnemySpawnData.FramesPath`/`AiType`) — aucune nouvelle scène/sous-classe, réutilise les 4 scènes archétype existantes (cf. `docs/GDD.md` §21). Sprites générés (`tools/generate_new_enemies.py`). Densité par biome doublée (spawnWeight dilué mais compensé par les ids globaux toujours actifs) — validé game-tester PASS. Limite connue : les 5 variantes d'un même archétype partagent une silhouette recolorée (pas de nouvelle forme par ennemi) — à arbitrer si plus de variété visuelle est souhaitée
- **Affixes d'élite (2026-07-04)** : une fraction des ennemis *basiques* (jamais mini-boss/boss) est promue élite avec 1 affixe parmi 5 (Blindé/Régénérant/Explosif/Frénétique/Vampirique) — cf. `docs/GDD.md` §22. Logique pure testée `src/Core/Rules/EliteAffixTable.cs` (fréquence `clamp(0.03+0.02×t, 0, 0.28)`), appliquée par `EnemyBase.ApplyElite` (stats après `ApplyScaling` + comportement + rendu teinté/agrandi + halo `EliteAura`), tirée dans `EnemySpawner.SpawnEnemy`. Répond à la limite « silhouettes recolorées » (variété = comportement). Flag debug `--force-elites`. Répond au brainstorm « inspirations d'autres jeux » (élites façon Risk of Rain 2 / Diablo)
- **Correctifs 2026-07-04** : purge des VFX/projectiles résiduels par-dessus le menu/Hub à la sortie de run (`SceneCleanup.ClearWorldVfx`, cf. `docs/PITFALLS.md`) ; retrait de l'upgrade Hub sans effet `starting_weapon_alt`
- **Retours testeur 2026-07-04** (cf. `docs/GDD.md` §23) : (1) `Player.ZIndex=5` — le joueur reste visible au-dessus des flammes/VFX d'armes ; (2) **Lance Vectorielle** (`vector_lance`, Rare) — arme dirigée vers `Player.AimDirection` (skill de visée : **souris** en clavier/souris, **stick droit** en manette, + réticule autour du joueur — MAJ 2026-07-04), réutilise `Bullet`, éventail aux niv. 4-5 ; (3) **courbe de difficulté non-linéaire** `EnemyScaling.CurvedFactor`/`ScaledCurved` (early grace −15% à t=0 puis accélération quadratique après 4 min) branchée dans `EnemySpawner` — le late rattrape le power-creep du build. 4 tests ajoutés (87 au total)

Voir aussi `docs/EXPANSION_PLAN.md` et `docs/LEVEL_PROGRESSION_PLAN.md` pour le détail des plans.
