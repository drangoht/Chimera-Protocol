# ASSET_STATUS.md — Chimera Protocol Phase 3

> Produit par l'agent `graphiste` le 2026-06-20.
> Mis a jour apres chaque livraison. Source de verite pour l'integration.

---

## Methode de production

Sprites generes **proceduralement en Python + Pillow** via `tools/generate_sprites.py`.
Palette conforme a `docs/STYLE_GUIDE.md` (hex codes exacts).
Resolution : 32x32 px (Colosse : 48x48 px). Format : PNG RGBA transparent.

Pour regenerer tous les sprites :
```
# Python 3.13 installe dans AppData/Local/Programs/Python/Python313/
C:/Users/<user>/AppData/Local/Programs/Python/Python313/python.exe tools/generate_sprites.py
```

---

## Iteration 1 — Jouabilite visible (LIVREE)

| Asset | Fichiers | Resolution | Frames | Ancre | Status |
|---|---|---|---|---|---|
| Joueur idle | `player_idle_01-04.png` | 32x32 | 4 | centre | LIVREE |
| Joueur run_right | `player_run_right_01-06.png` | 32x32 | 6 | centre | LIVREE |
| Joueur run_down | `player_run_down_01-06.png` | 32x32 | 6 | centre | LIVREE |
| Joueur death | `player_death_01-08.png` | 32x32 | 8 | centre | LIVREE |
| SpriteFrames joueur | `player_frames.tres` | — | — | — | LIVREE |
| Essaim idle | `enemy_rustswarm_idle_01-03.png` | 32x32 | 3 | centre | LIVREE |
| Essaim move | `enemy_rustswarm_move_01-04.png` | 32x32 | 4 | centre | LIVREE |
| Essaim death | `enemy_rustswarm_death_01-05.png` | 32x32 | 5 | centre | LIVREE |
| SpriteFrames Essaim | `rustswarm_frames.tres` | — | — | — | LIVREE |
| Projectile Canon | `weapon_bullet_impulse.png` | 8x4 | 1 (statique) | centre | LIVREE |
| Tile sol base | `tile_floor_01.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile sol variation | `tile_floor_02.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile sol fissure | `tile_floor_crack.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile sol rouille | `tile_floor_rust.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile sol debris | `tile_floor_debris.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile mur base | `tile_wall_01.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile mur rouille | `tile_wall_rust.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile mur fissure Aether | `tile_wall_crack_aether.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile debris pierre | `tile_debris_01.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile debris metal | `tile_debris_metal.png` | 32x32 | 1 | coin haut-gauche | LIVREE |
| Tile colonne | `tile_column.png` | 32x64 | 1 | coin haut-gauche | LIVREE |

**Integration scenes :**
- `scenes/entities/Player.tscn` : Polygon2D remplace par AnimatedSprite2D + player_frames.tres
- `scenes/entities/RustSwarm.tscn` : Polygon2D remplace par AnimatedSprite2D + rustswarm_frames.tres
- `scenes/weapons/Bullet.tscn` : Polygon2D remplace par Sprite2D + weapon_bullet_impulse.png
- `src/Entities/Player/Player.cs` : logique animation idle/run_right/run_down/death + clignotement HP critique
- `src/Entities/Enemies/RustSwarm.cs` : logique animation move/death + flip_h selon direction

---

## Iteration 2 — Contenu complet (LIVREE)

| Asset | Fichiers | Resolution | Frames | Ancre | Status |
|---|---|---|---|---|---|
| Drone idle | `enemy_drone_idle_01-03.png` | 32x32 | 3 | centre | LIVREE |
| Drone move | `enemy_drone_move_01-04.png` | 32x32 | 4 | centre | LIVREE |
| Drone death | `enemy_drone_death_01-04.png` | 32x32 | 4 | centre | LIVREE |
| Sentinelle idle | `enemy_sentinel_idle_01-04.png` | 32x32 | 4 | centre | LIVREE |
| Sentinelle move | `enemy_sentinel_move_01-06.png` | 32x32 | 6 | centre | LIVREE |
| Sentinelle attack | `enemy_sentinel_attack_01-04.png` | 32x32 | 4 | centre | LIVREE |
| Sentinelle death | `enemy_sentinel_death_01-06.png` | 32x32 | 6 | centre | LIVREE |
| Colosse idle | `enemy_colossus_idle_01-04.png` | 48x48 | 4 | centre | LIVREE |
| Colosse move | `enemy_colossus_move_01-06.png` | 48x48 | 6 | centre | LIVREE |
| Colosse attack | `enemy_colossus_attack_01-05.png` | 48x48 | 5 | centre | LIVREE |
| Colosse death | `enemy_colossus_death_01-10.png` | 48x48 | 10 | centre | LIVREE |
| Projectile Rail | `weapon_bullet_rail.png` | 12x4 | 1 (statique) | centre | LIVREE |
| Projectile Sentinelle | `enemy_bullet_sentinel_01-02.png` | 6x6 | 2 | centre | LIVREE |
| Pickup XP orbe | `pickup_xporb_idle_01-03.png` | 16x16 | 3 | centre | LIVREE |
| Pickup Noyau Aether | `pickup_noyau_idle_01-03.png` | 16x16 | 3 | centre | LIVREE |
| Tile geyser Aether | `tile_aether_geyser_01-03.png` | 32x32 | 3 | centre | LIVREE |
| Tile pilier tech | `tile_tech_pillar.png` | 32x64 | 1 | coin haut-gauche | LIVREE |
| Tile flaque Rouille | `tile_rust_pool_01-02.png` | 32x32 | 2 | coin haut-gauche | LIVREE |

**Integration a faire par `developpeur` :**
- `scenes/entities/CorruptedDrone.tscn` : ajouter AnimatedSprite2D + SpriteFrames drone a creer
- `scenes/entities/CorruptedSentinel.tscn` : ajouter AnimatedSprite2D + SpriteFrames sentinel a creer
- `scenes/entities/GraftedColossus.tscn` : ajouter AnimatedSprite2D + SpriteFrames colossus a creer
  (attention : 48x48 px, pas 32x32 — redimensionner le CollisionShape2D si necessaire)
- `scenes/entities/XpOrb.tscn` : remplacer le placeholder par Sprite2D + pickup_xporb_idle_01.png
- `scenes/entities/AetherCore.tscn` : remplacer le placeholder par Sprite2D + pickup_noyau_idle_01.png

---

## Iteration 3 — Polish (LIVREE)

| Asset | Fichiers | Resolution | Frames | Status |
|---|---|---|---|---|
| VFX particule Essaim | `vfx_particle_rustswarm.png` | 2x2 | 1 | LIVREE |
| VFX particule Drone | `vfx_particle_drone.png` | 2x2 | 1 | LIVREE |
| VFX particule Sentinelle | `vfx_particle_sentinel.png` | 2x2 | 1 | LIVREE |
| VFX particule Colosse | `vfx_particle_colossus.png` | 2x2 | 1 | LIVREE |
| VFX particule XP | `vfx_particle_xp.png` | 2x2 | 1 | LIVREE |
| VFX particule Noyau | `vfx_particle_noyau.png` | 3x3 | 1 | LIVREE |
| VFX aura Lame Fusion | `vfx_aura_fusionblade.png` | 3x3 | 1 | LIVREE |
| VFX aura Rail | `vfx_aura_rail.png` | 3x3 | 1 | LIVREE |
| VFX swing Plasma Blade | `weapon_plasmablade_swing_01-04.png` | 32x32 | 4 | LIVREE |
| VFX drone orbital | `weapon_drone_idle_01-03.png` | 8x8 | 3 | LIVREE |
| Texture anneau Lame Fusion | `weapon_fusionblade_ring_texture.png` | 64x4 | 1 | LIVREE |
| Metamorphose Lame Fusion | `weapon_fusionblade_metamorphose_01-08.png` | 32x32 | 8 | LIVREE |
| Metamorphose Rail Surchage | `weapon_rail_metamorphose_01-10.png` | 32x32 | 10 | LIVREE |
| Icone HP | `ui_icon_hp.png` | 8x8 | 1 | LIVREE |
| Icone Noyau | `ui_icon_noyau.png` | 8x8 | 1 | LIVREE |
| Icone Canon a Impulsions | `ui_icon_impulse_cannon.png` | 32x32 | 1 | LIVREE |
| Icone Lame Plasma | `ui_icon_plasmablade.png` | 32x32 | 1 | LIVREE |
| Icone Essaim de Drones | `ui_icon_droneswarm.png` | 32x32 | 1 | LIVREE |
| Icone Champ de Surcharge | `ui_icon_overloadfield.png` | 32x32 | 1 | LIVREE |
| Icone Noyau Thermique | `ui_icon_thermal_core.png` | 32x32 | 1 | LIVREE |
| Icone Plaque Renforcee | `ui_icon_reinforced_plate.png` | 32x32 | 1 | LIVREE |
| Icone Servo-Moteurs | `ui_icon_servomotors.png` | 32x32 | 1 | LIVREE |
| Icone Capaciteur | `ui_icon_capacitor.png` | 32x32 | 1 | LIVREE |
| Icone Lame a Fusion | `ui_icon_fusionblade.png` | 32x32 | 1 | LIVREE |
| Icone Rail Surchage | `ui_icon_rail.png` | 32x32 | 1 | LIVREE |

---

## Reste a faire (hors scope graphiste — code ou art final)

| Item | Responsable | Priorite |
|---|---|---|
| SpriteFrames .tres pour Drone, Sentinelle, Colosse | `graphiste` ou `developpeur` | HAUTE |
| Integration AnimatedSprite2D dans CorruptedDrone.tscn, CorruptedSentinel.tscn, GraftedColossus.tscn | `developpeur` | HAUTE |
| Logique animation dans CorruptedDrone.cs, CorruptedSentinel.cs, GraftedColossus.cs | `developpeur` | HAUTE |
| Hit flash shader (CanvasItemMaterial blanc 0,05 s) | `developpeur` | MOYENNE |
| Bloom WorldEnvironment.glow (threshold < 0,6 luminosite) | `developpeur` | MOYENNE |
| Flash desaturation fusion (CanvasItemShader) | `developpeur` | MOYENNE |
| GPUParticles2D mort ennemis (utiliser les vfx_particle_*.png) | `developpeur` | MOYENNE |
| GPUParticles2D collecte XP (vfx_particle_xp.png) | `developpeur` | BASSE |
| GPUParticles2D collecte Noyau (vfx_particle_noyau.png) | `developpeur` | BASSE |
| Splash screen 1280x720 key art | art final ou generateur AI | BASSE MVP |
| Police m5x7.ttf (telecharger sur managore.itch.io) | `developpeur` | BASSE |
| Tilemap arene finale "Sanctuaire en Ruines" | `developpeur` | HAUTE |

---

## Notes de lisibilite pour `game-designer`

**A verifier en jeu avant validation :**

1. **Essaim de Rouille vs fond de sol** : la masse brun-fer (`#4A2A15`) sur fond gris-ardoise
   (`#1A1A22`) doit etre lisible en moins de 150 ms (regle STYLE_GUIDE §0). Si le contraste
   est insuffisant en jeu avec 50+ ennemis, proposer d'augmenter la rouille claire
   (`#7A4A2A`) sur une plus grande zone du sprite. A signaler a `directeur-artistique`.

2. **Colosse Greffe 48x48** : au milieu d'un essaim, le Colosse depasse en taille mais sa
   palette tres sombre (chair necrosee `#3A2A20`) peut se confondre avec le fond. Les implants
   violets `#AA44FF` sont le seul marqueur lumineux visible — verifier que le bloom les
   distingue suffisamment a distance (> 200 px du joueur).

3. **Projectile Sentinelle vs projectile joueur** : rouge `#FF2200` vs orange `#FF8800` —
   couleurs proches en peripherie de vision. Le format circulaire (sentinelle) vs fusee
   (joueur) est le differenciateur principal. A confirmer en test avec 5+ sentinelles actives.

4. **Noyau Aether vs orbe XP** : violet `#AA44FF` vs jaune-vert `#AAFF44` — contraste
   chromatique fort, pas de risque de confusion. Valide.
