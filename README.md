# Chimera Protocol

> Survivor roguelite vue du dessus — univers fantaisie-science-fiction, inspiré de Vampire Survivors et Everything is Crab.

Dans un monde ravagé par **la Rouille Vivante** (corruption mi-organique, mi-mécanique née de la fusion de la magie et de la technologie), tu incarnes un **Arpenteur** envoyé en mission d'extraction dans un Sanctuaire en ruines. Survive, évolue, et rapporte des **Noyaux d'Aether** avant d'être submergé.

---

## État du projet

**Victoire par boss final + badge de complétion par biome** *(2026-06-28)*

| Phase | Statut | Contenu |
|---|---|---|
| Phase 1 — Prototype | ✅ Terminé | Joueur, arène, 1 ennemi, 1 arme |
| Phase 2 — Gameplay core | ✅ Terminé | 4 ennemis, 4 armes, 4 passifs, 2 fusions, XP/level-up, Échos d'Aether, Hub, sauvegarde |
| Phase 3 — Contenu & polish | ✅ Terminé | Sprites pixel art, audio synthétique CC0, arène graphique, menu principal, UI complète, FusionFlash |
| Phase 4 — Arène + VFX | ✅ Terminé | Arène 1920×1216, obstacles A–D, lueur geysers, death burst, XpOrb trail/pulse, impact burst, ambiants Aether |
| Phase 5 — Navigation & audio | ✅ Terminé | Navigation clavier/manette complète, bande-son CC0 définitive (31 fichiers : 24 SFX Kenney + 5 musiques Junkala) |
| Équilibrage MVP | ✅ Validé | 0 crash sur 3 runs (~25 min), XP ennemis différenciés par tiers |
| Polish visuel — base | ✅ Livré | Fond arène assombri, PointLight2D joueur/projectiles, notifications armes équipées avec flash coloré |
| Polish visuel — next-level | ✅ Livré | 4 shaders GLSL, screen shake, vignette dynamique (suit le joueur), grille holographique sol, shockwave Colosse, hit stop, trail joueur, chromatic aberration fusion, VFX armes (PlasmaBlade arc flash, muzzle flash, trails balles, OverloadField, drones lumineux) |
| Mini-boss & orbes XP | ✅ Livré | 2 mini-boss (Rôdeur de Rouille + Sentinelle Maîtresse, 64×64 px, écran choix d'arme à mort), orbes XP 4 tiers (vert/cyan/violet/or), revamp sprites 4 ennemis |
| Drops HP + Progression VS | ✅ Livré | Orbes HP rouges (8%/25% mini-boss, +15% MaxHP), heal 25% MaxHP à chaque level-up, courbe XP inspirée Vampire Survivors (L1=5 XP, linéaire +10/niveau, mur L20) |
| HUD "juicy" sci-fi | ✅ Livré | Panel sombre + bordure cyan, barres plus épaisses (HP 18px / XP 12px), glow derrière chaque barre, drain HP animé, pulsation rouge <25% HP, flash XP overexposé au level-up |
| HUD — assets concept cyberpunk | ✅ Livré | Extraction/retouche depuis concept `idea/idee_hud_chimera_core.png` (masquage HSV numpy) : barre XP 20 segments, hexagone LV 44×26, icône Chimera Core violet, cadre panneau stats tech (generated from scratch), cadre timer avec crochets, titres de panneau "CHIMERA PROTOCOL" / "NOYAUX AETHER" / "RUNTIME ENCRYPTED" |
| Typographie pixel | ✅ Livré | Police **VT323** (pixel/terminal CRT, OFL) en rendu net (anti-aliasing désactivé) appliquée globalement via Theme — fin du texte "baveux" ; tailles HUD ré-accordées, glyphes spéciaux → ASCII |
| Juice & densité VS | ✅ Livré | VFX scalés par niveau d'arme (brillance balles, impact bursts, flash), explosions de mort calibrées par tier + onde de choc, aura joueur croissante, screen shake d'impact ; arène éclaircie ; spawn façon Vampire Survivors (cap 300, courbe raide, lots + vagues) ; i-frames joueur (0.45 s) |
| Boss & nouvelles armes | ✅ Livré | 2 armes 100% VFX (Bobine Tesla = éclair en chaîne, Nova d'Aether = détonation dilatante) ; mini-boss de mi-temps **Revenant d'Aether** (7 min, ruades) ; **boss de fin Le Noyau Rouillé** (13 min, HP base 1600 → ~4096 effectif après scaling temporel, salves radiales, 500 XP + 3 Noyaux + choix d'arme) |
| Sprites dédiés boss | ✅ Livré | Sprites pixel art 64×64 dédiés générés (`tools/generate_boss_sprites.py`) : Revenant (spectre cyborg violet, bras-lames, dissolution) + Noyau Rouillé (titan rouille-or, noyau en fusion, surcharge) — fin de la réutilisation teintée |
| Bestiaire & Arsenal | ✅ Livré | 2 rubriques au menu : **Bestiaire** (8 ennemis — sprite **animé** + tag + description) et **Arsenal** (8 armes + 4 passifs — icône + description). Icônes Tesla/Volée créées ; icônes sur les cartes de choix d'arme et dans les notifs HUD |
| Lisibilité UI | ✅ Livré | Police principale **Share Tech Mono** (mono techno lisible, anti-aliasée) en remplacement de VT323 — texte et HUD nettement moins pixelisés ; tailles ré-accordées |
| Personnages jouables | ✅ Livré | 3 personnages (registre `Characters.cs`) : **Chimera** (cyborg, impulse_cannon), **Titan-Gardien** (robot lourd, drone_swarm), **Vagabond** (humain, plasma_blade) — sprites pixel art dédiés, aura d'identité, sélecteur dans le Hub. Le perso garde toujours son arme de signature |
| Biomes d'arène | ✅ Livré | 4 biomes (Sanctuaire Rouillé, Friche d'Aether, Fournaise, Givre Cryogénique) — tuiles dédiées, obstacles colorés à l'accent, effets gameplay (XP +20%, vitesse ennemis ±18%), seed de layout randomisé |
| Sélection de niveau | ✅ Livré | « Jouer » mène à un écran de sélection de biome (4 cartes aperçu + Aléatoire) ; badge **« VAINCU »** sur les biomes déjà battus |
| Options & difficulté | ✅ Livré | Écran Options (sliders volume, plein écran, secousses) persisté dans `settings.cfg` ; sélecteur de difficulté Facile/Normal/Difficile (multiplicateurs dégâts/HP/spawn) ; équilibrage early-game assoupli |
| Intro narrative | ✅ Livré | Scène de boot jouant le lore en 5 temps, fondu enchaîné, **skippable** (toute touche → menu) |
| HUD thématisé par biome | ✅ Livré | HUD reconstruit 100% en code, look minimal Cyberpunk 2077 ; coloré par l'accent du biome ; **scanlines CRT**, bandeau de loadout, chip de biome, animations discrètes (liseré qui respire, XP lerp, pop des Noyaux) |
| **Victoire par boss final** | ✅ Livré | La run se gagne en **vainquant Le Noyau Rouillé** (plus d'auto-victoire au timer) ; **badge « VAINCU »** par biome/difficulté persisté dans `settings.cfg` — validé game-tester 5/5 |

---

## Gameplay

- **Boucle run** : survivre puis **vaincre le boss final (Le Noyau Rouillé, ~13 min) pour gagner** — le timer n'est plus une auto-victoire mais un décompte avant l'arrivée du boss. Nuées denses façon Vampire Survivors (jusqu'à 300 ennemis, spawn par lots + vagues), i-frames qui rendent les hordes jouables, ramassage d'XP
- **Choix du biome** : 4 arènes (Sanctuaire, Aether, Fournaise, Givre) avec effets gameplay propres ; un badge « VAINCU » marque les biomes déjà battus, par difficulté
- **3 personnages** : Chimera (cyborg), Titan-Gardien (robot), Vagabond (humain) — chacun avec stats, arme de signature et aura propres
- **Montée de niveau** : choix entre 3 cartes (armes, passifs, fusions) à chaque level-up + restauration de 25% des HP max
- **Drops HP** : les ennemis droppent aléatoirement un orbe rouge (losange) qui restaure 15% des HP max au contact
- **Fusions** : atteindre le niveau max d'une arme + posséder le passif prérequis débloque une forme évoluée qui transforme visuellement et mécaniquement l'arme
- **Meta progression** : les Échos d'Aether gagnés en run s'investissent en améliorations permanentes (Hub)

### Ennemis (4 + 3 mini-boss + 1 boss de fin)

| Ennemi | Rôle | HP | XP | Apparition |
|---|---|---|---|---|
| Essaim de Rouille | Fourrage — fonce en ligne droite | 20 | 3 🟢 | dès 0:00 |
| Drone Corrompu | Harceleur rapide — trajectoire erratique ±45° | 15 | 7 🔵 | dès 2:00 |
| Sentinelle Corrompue | Pression à distance — tire et recule | 45 | 20 🟣 | dès 5:00 |
| **Revenant d'Aether** *(mini-boss mi-temps)* | Poursuite rapide + ruades, aura violette — drop arme | 550 | 180 🟣 | dès 7:00 |
| Colosse Greffé | Bruiser lent, dégâts lourds + drop Noyau | 200 | 60 🟡 | dès 9:00 |
| **Rôdeur de Rouille** *(mini-boss)* | Araignée 64×64, très résistant — drop arme | 300 | 80 🟡 | dès 12:00 |
| **Le Noyau Rouillé** *(BOSS DE FIN)* | Salves radiales + ondes de choc — 3 Noyaux + drop arme | 1600¹ | 500 🟡 | dès 13:00 |
| **Sentinelle Maîtresse** *(mini-boss)* | Double tir ±12°, kiter — drop arme | 450 | 120 🟡 | dès 16:00 |

> ¹ **PV de base.** L'`EnemySpawner` applique un scaling temporel `PV = base × (1 + t_min × hpScaling) × difficulté`. Le Noyau Rouillé arrivant à 13 min, son PV effectif est **≈4096 en Normal** (≈3277 Facile / ≈5325 Difficile). Idem pour les autres ennemis selon leur heure d'apparition.

### Armes & passifs (10 cartes + fusions)

**Actives** : Canon à Impulsions · Lame Plasma · Essaim de Drones · Champ de Surcharge · **Bobine Tesla** (éclair en chaîne) · **Volée Multiple** (tir multi-cible, +1 projectile/niveau)

**Passifs** : Noyau Thermique · Plaque Renforcée · Servo-Moteurs · Capaciteur

**Fusions MVP** :
- Lame Plasma (niv. 5) + Noyau Thermique → **Lame à Fusion** (anneau continu 55 dps)
- Canon à Impulsions (niv. 5) + Capaciteur → **Rail Surchargé** (rafale 3 projectiles perforants)

---

## Pile technique

| Outil | Version |
|---|---|
| Moteur | **Godot 4.7 .NET** |
| Langage | **C# / .NET 8** |
| Cible | **Windows (.exe)** |
| Style graphique | Pixel art 32×32 px, `texture_filter = Nearest` |
| Typographie | **Share Tech Mono** (OFL) — mono techno lisible, Theme global (VT323/Press Start 2P en réserve) |

### Structure du projet

```
chimera-protocol/
├── src/
│   ├── Core/          GameManager, Constants
│   ├── Entities/      Player, EnemyBase + 4 ennemis, XpOrb, AetherGeyser
│   ├── Weapons/       4 armes actives + 2 fusions + Bullet
│   ├── Systems/       XpSystem, InventorySystem, LevelUpSystem, EnemySpawner, GroundRenderer
│   ├── UI/            HUD, LevelUpScreen, RunEndScreen, HubScreen, MainMenu
│   ├── VFX/           EnemyDeathBurst, ImpactBurst, FusionFlash, PlasmaArcFlash, MuzzleFlash, ShockwaveRing
│   └── Systems/       ScreenShake (AutoLoad)
├── scenes/            Scènes .tscn (entities, weapons, ui, vfx)
├── assets/
│   ├── sprites/       PNG pixel art — joueur, ennemis, tiles, VFX, UI
│   ├── shaders/       4 shaders GLSL (floor_grid, screen_vignette, shockwave_ring, chromatic_aberration)
│   ├── fonts/         Share Tech Mono (UI/HUD) + VT323 + Press Start 2P (OFL, réserve)
│   ├── themes/        ui_theme.tres (Theme global Share Tech Mono)
│   └── audio/         WAV/OGG CC0 (5 musiques Junkala + 24 SFX + 2 stingers Kenney)
├── data/              enemies.json, weapons.json, levelup_config.json, meta_upgrades.json
├── tools/             Scripts de génération d'assets (Python 3.13 + Pillow)
└── docs/              GDD.md, STYLE_GUIDE.md, AUDIO_GUIDE.md, ARENA_DA_BRIEF.md
```

---

## Lancer le projet

1. Installer **Godot 4.7 .NET** (variante `.NET` obligatoire — pas la version standard)
2. Installer **.NET 8 SDK**
3. Ouvrir `project.godot` dans Godot
4. *Project → Tools → C# → Create C# Solution* si la solution n'est pas détectée
5. `F5` pour lancer

**Build Windows :**
```
"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe" --headless --export-release "Windows Desktop" "./build/ChimeraProtocol.exe"
```
> Export templates requis — télécharger sur godotengine.org/download/archive/ → Godot 4.7 stable → "Export templates (.NET)"

---

## Backlog post-MVP

- [x] Personnages Humain et Robot jouables (Vagabond + Titan-Gardien) ✅
- [x] Mini-boss en run (Rôdeur de Rouille + Sentinelle Maîtresse + Revenant d'Aether) ✅
- [x] Boss de fin de run ("Le Noyau Rouillé") — désormais condition de victoire ✅
- [x] Biomes / arènes additionnelles (4 biomes + sélection de niveau) ✅
- [x] Fusions supplémentaires au-delà des 2 MVP (Essaim Orbital + Égide de Surcharge) ✅
- [ ] Support manette officiel (validation physique)
- [ ] Succès / intégration plateforme (itch.io, Steam)
- [ ] Publication sur itch.io

---

## Design document

Le GDD complet (pitch, univers, valeurs de tuning, direction artistique, décisions techniques) est dans [`docs/GDD.md`](docs/GDD.md).
