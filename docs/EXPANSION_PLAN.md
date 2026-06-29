# Plan d'expansion — Armes, Power-ups, Ennemis/Boss par biome, Refonte visuelle des arènes

> Plan préparé le 2026-06-29. Ancré sur les conventions réelles du projet (checklist de câblage
> `[[add-weapons-bosses]]`, système d'équilibrage `src/Core/Rules/`, biomes, couches VFX).
>
> **Décisions verrouillées (2026-06-29) :**
> - **Séquencement : dépendances d'abord** (socle biome + Néon → visuel → armes → power-ups → ennemis/boss).
> - **Power-ups : buffs temporaires ramassables** (pas de passifs permanents pour cette vague).
> - **Boss : un mid-boss par biome + `rusted_core` reste le final universel.**

## Garde-fous transversaux (équilibrage & perf)

- **Budget DPS par rareté** : toute arme single-target reste calibrée sur `impulse_cannon` (référence) ;
  les armes de contrôle/AoE échangent du DPS brut contre de l'utilité (slow, pull, DoT). Rareté via le
  poids existant (common 60 / rare 30 / epic 10) — les epics restent rares.
- **Plafonds durs** (nouvelle classe `Rules/CrowdControlCaps` testée) : slow cumulé ennemis **−40 % max**,
  rayon de gravité plafonné, DoT non stackable au-delà d'un cap. Réutilise `StatCaps` / `WeaponLeveling`.
- **Extrapolation** : niveaux 6→20 via `WeaponLeveling.ExtrapolatedDamage` (mécaniques plafonnées, seuls
  les dégâts montent +10 %/niv) — cohérent avec l'existant.
- **Validation obligatoire à chaque lot** : `dotnet test` (garder ≥ 51 verts, +tests pour toute formule
  pure neuve), outil `--debug-boss` + `tools/boss_ttk_test.py` (le TTK boss doit rester **~35-45 s**),
  passe `game-tester` de non-régression. Perf : cap 300 ennemis intact, particules/shaders légers.

---

## PARTIE A — 5 nouvelles armes (+ VFX) et power-ups

### A.1 — Les 5 armes (archétypes distincts, aucun doublon)

L'arsenal actuel couvre : projectile mono-cible, arc de mêlée, drones orbitaux, pulse AoE, chaîne
d'éclairs, volée multi-cible. Les nouvelles comblent des niches manquantes :

| # | Id | Nom | Mécanique (niche) | Rareté | VFX phare |
|---|----|-----|-------------------|--------|-----------|
| 1 | `glaive` | Lame Boomerang | Projectile qui part en arc et **revient** (touche 2×) | common | glaive en rotation + traînée + gerbe d'étincelles |
| 2 | `seeker_swarm` | Essaim Traqueur | Missiles **à tête chercheuse** (auto-lock plus proche) | rare | traînées violettes sinueuses + bloom d'impact |
| 3 | `cryo_lance` | Lance Cryo | **Rayon perçant** en ligne qui **ralentit** les touchés | rare | faisceau glacé + éclats de givre + aura de ralenti |
| 4 | `pyre_stream` | Jet de Pyre | **Cône** courte portée + **brûlure (DoT)** | rare | flammes GPU + braises + tint ardent sur l'ennemi |
| 5 | `singularity` | Singularité | Déploie un **puits gravitationnel** qui aspire + tick | epic | vortex (shader de distorsion) + anneau d'horizon |

Chacune respecte la **checklist 8 points** : classe `src/Weapons/`, scène `.tscn`, `weapons.json`
(5 niveaux), `levelup_config.json` rarityByCard, `InventorySystem` (paths + display + ApplySpecializedStats),
**`LevelUpSystem.AllWeaponIds`** (sinon jamais proposée), `Codex.Weapons` + `Codex.IconById`, icône 32×32
(`tools/generate_weapon_icons.py`).

**Équilibrage par arme :**
- `glaive` : 2 hits → dégâts/hit = ~60 % d'un tir impulse (somme équivalente).
- `seeker_swarm` : homing = forte précision → DPS un cran sous scatter, cadence plus lente.
- `cryo_lance` : slow plafonné (−15 %/arme, soumis au cap global −40 %), dégâts modestes (utilité).
- `pyre_stream` : DoT non stackable (refresh, pas d'addition), portée courte = risque/récompense.
- `singularity` (epic) : pull doux + tick faible ; rayon plafonné ; cooldown long. Anti-trivialisation.

### A.2 — Fusions (extension du système, optionnel/stretch)

4 fusions existent (1 par passif). Chaque nouvelle arme **peut** recevoir une fusion quand de nouveaux
passifs arrivent (cf. A.3). Non bloquant pour la 1re livraison.

### A.3 — Power-ups : 2 volets

**Volet 1 — Power-ups temporaires ramassables (NOUVEAU système, recommandé).** Des bonus **à durée
limitée** (8-12 s) qui apparaissent en jeu comme l'aimant (`PowerUpSpawner`, modèle `MagnetSpawner`,
fenêtres programmées, max N/run) — donne du frisson moment-à-moment **sans power creep permanent** :
- **Surcadence** (Overclock) : −50 % cooldown temporaire (halo cyan).
- **Furie** (Berserk) : +60 % dégâts temporaire (aura rouge).
- **Égide** (Shield) : absorbe X coups / brève invuln (bulle dorée).
- **Célérité** : +40 % vitesse + traînée.
- **Implémentation** : `PlayerStats` reçoit des **buffs timés** (liste `(stat, mult, tempsRestant)`),
  appliqués/retirés proprement ; VFX plein écran bref + icône HUD de buff actif. Pure logic (durées,
  cumul) → testable.

**Volet 2 — passifs permanents** : *écarté pour cette vague* (décision verrouillée = buffs temporaires
uniquement). À reconsidérer plus tard si on veut débloquer des fusions pour les nouvelles armes.

> ✅ **Verrouillé** : volet 1 (buffs temporaires ramassables) uniquement.

---

## PARTIE B — Ennemis & boss variant selon le biome

### B.1 — Rendre le spawn « biome-aware » (fondation)

Aujourd'hui `EnemySpawner` filtre par `spawnStartMinute` + poids, **sans tenir compte du biome**.
Ajout minimal et propre :
- Champ **`biomes`** dans `enemies.json` (tableau d'ids de biome ; absent = tous biomes).
- `EnemySpawner` filtre `_enemyPool` par **`GameManager.CurrentBiomeId`** (déjà posé par `GroundRenderer`)
  en plus du temps. Aucun nouveau couplage.
- Rétro-compatible : les ennemis actuels n'ont pas de `biomes` → spawnent partout (inchangé).

### B.2 — Nouveaux ennemis thématisés (2-3, taggés 1-2 biomes)

| Ennemi | Biome(s) | Gimmick |
|--------|----------|---------|
| Spectre de Phase | Aether | se **téléporte** par sauts courts |
| Brute de Magma | Fournaise | laisse une **flaque brûlante** (zone) |
| Rampant de Givre | Givre | **ralentit le joueur** au contact |
| Sentinelle Néon | Néon (nouv.) | tire des **lasers téléguidés** lents |

Scaling via le système existant (hpScalingPerMinute/damageScaling), fenêtres de spawn dédiées.

### B.3 — Boss par biome

Actuel : `aether_revenant` (mi-temps 7 min) + `rusted_core` (final 13 min, = victoire). Plan :
- **Un mid-boss par biome** (~8-10 min), taggé via `biomes`, `maxSimultaneous=1`, sprite dédié
  (`generate_boss_sprites.py`) :
  - Aether → *Revenant d'Aether* (existant, à tagger Aether).
  - Fournaise → **Colosse en Fusion** (projette des coulées).
  - Givre → **Sentinelle Cryo** (cône de gel ralentissant).
  - Néon → **Gardien Néon** (drones-tourelles + bouclier rotatif).
- **Boss final** : `rusted_core` **reste le final universel** (préserve la condition de victoire unique
  et le badge). *Stretch optionnel* : variantes finales par biome (skin + 1 attaque signature).
- Câblage via la **checklist boss** (5 points + `ShowWeaponDrop`/onDeath). Équilibrage calé sur les
  deux boss existants ; aucun ne descend sous le TTK cible.

> ✅ **Verrouillé** : mid-boss par biome + `rusted_core` final partagé.

---

## PARTIE C — Refonte visuelle des arènes

### C.1 — Parallaxe multi-couches (le gros morceau visuel)

- `ParallaxBackground` (nouveau, sous l'arène) à **3 couches** défilant à des vitesses différentes
  selon la caméra : **fond lointain** (étoiles/grille/horizon), **silhouettes mid** (structures), **brume
  proche**. Textures + teintes **par biome**.
- Intégré via `Game.tscn` (ou posé en code par `GroundRenderer` selon le biome courant).

### C.2 — Brume / volumes

- Couche de **brume dérivante** : `GpuParticles2D` lents (ou shader de fog scrollé) à faible alpha,
  teintée à l'accent du biome, **au-dessus du sol / sous les entités** (z géré). Léger (cap particules).

### C.3 — Effets de lumière

- **Puits de lumière / god-rays** factices (gradients lumineux animés), **halos néon**, shafts par biome.
- S'appuie sur le `WorldEnvironment` glow existant + nouveaux `PointLight2D` d'ambiance animés.

### C.4 — Nouveau biome futuriste **« Secteur Néon »** (5ᵉ biome)

- Sol grille sombre + **lignes néon** magenta/cyan vives, panneaux holographiques (en parallaxe),
  scanlines appuyées, accent bleu électrique. Tuiles via `tools/generate_biome_tiles.py`.
- `BiomeDef` (gameplay) + `BiomeCatalog` (UI) + effet de jeu (ex. **+10 % cadence de tir** ou densité
  accrue) + ajout au `LevelSelectScreen`. Lie le boss **Gardien Néon** (B.3).

### C.5 — Perf

- Toutes les couches respectent le budget (cap 300 ennemis) : shaders simples, particules plafonnées,
  parallaxe = sprites/tilesets légers. Mesure FPS avant/après (capture `tools/screenshot_swarm.py`).

---

## Séquencement proposé (phases livrables & testées)

L'ordre **dépendances-first** (le hook biome + le biome Néon servent de socle aux boss variés) :

1. **Phase 1 — Socle biome** : spawn biome-aware (B.1) + biome **Néon** (C.4, tuiles + def + LevelSelect).
2. **Phase 2 — Refonte visuelle** : parallaxe (C.1) + brume (C.2) + lumières (C.3), appliquées à tous
   les biomes (Néon inclus).
3. **Phase 3 — Armes** : les 5 armes (A.1) avec VFX, une par une, chacune build+test+TTK.
4. **Phase 4 — Power-ups** : système de buffs temporaires (A.3 v1) [+ passifs A.3 v2 si retenu].
5. **Phase 5 — Ennemis & boss** : nouveaux ennemis (B.2) + mid-boss par biome (B.3).
6. **Phase 6 — Équilibrage & validation** : passe `game-tester`, TTK, ajustements, doc (README/CLAUDE),
   build + ZIP itch.

> ✅ **Verrouillé** : ordre dépendances-first ci-dessus (Phase 1 = socle biome + biome Néon).

## Estimation grossière d'effort (sessions)

| Phase | Effort relatif |
|-------|----------------|
| 1 — Socle biome + Néon | moyen |
| 2 — Parallaxe/brume/lumières | moyen-élevé (le plus « visuel ») |
| 3 — 5 armes + VFX | élevé (le plus de câblage) |
| 4 — Power-ups | moyen |
| 5 — Ennemis + 4 boss | élevé |
| 6 — Équilibrage/doc/build | moyen |
