# CREDITS AUDIO — Chimera Protocol

## Statut global

**SFX (24 fichiers) : ASSETS KENNEY CC0 — integres le 2026-06-22**
Convertis depuis les packs Kenney.nl (OGG -> WAV 44100 Hz 16-bit mono via ffmpeg 8.1.1).
Licence : CC0 / Domaine public — utilisation commerciale libre, aucune attribution obligatoire.

**Musique stingers (2 fichiers) : ASSETS KENNEY CC0 — integres le 2026-06-22**
Concatenation de jingles Kenney Music Jingles (OGG -> WAV 44100 Hz 16-bit stereo via ffmpeg).

**Musiques longues (5 fichiers) : ASSETS CC0 DEFINITIFS — integres le 2026-06-22**
Source : "Retro Game Music Pack" par Juhani Junkala / SubspaceAudio.
Licence : CC0 / Domaine public. https://opengameart.org/content/5-chiptunes-action

---

## Packs Kenney utilises

Tous ces packs sont distribues sous licence **CC0 1.0 Universal (Domaine public)**.
Source : https://kenney.nl — Kenney Vleugels.

| Pack | URL | Fichiers ZIP | Date telechargement |
|---|---|---|---|
| Sci-Fi Sounds | https://kenney.nl/assets/sci-fi-sounds | kenney_sci-fi-sounds.zip | 2026-06-22 |
| Impact Sounds | https://kenney.nl/assets/impact-sounds | kenney_impact-sounds.zip | 2026-06-22 |
| UI Audio | https://kenney.nl/assets/ui-audio | kenney_ui-audio.zip | 2026-06-22 |
| RPG Audio | https://kenney.nl/assets/rpg-audio | kenney_rpg-audio.zip | 2026-06-22 |
| Music Jingles | https://kenney.nl/assets/music-jingles | kenney_music-jingles.zip | 2026-06-22 |

Les ZIPs originaux sont conserves dans `tools/kenney_downloads/` (non versionnes — regenerables
via les URLs ci-dessus).

---

## SFX (`assets/audio/sfx/`)

### Armes

| Fichier WAV | Source originale (OGG) | Pack | Description |
|---|---|---|---|
| `sfx_weapon_impulse_shoot.wav` | `laserSmall_000.ogg` | Sci-Fi Sounds | Tir laser compact |
| `sfx_weapon_plasma_swing.wav` | `laserLarge_000.ogg` | Sci-Fi Sounds | Energie lame large |
| `sfx_weapon_rail_shoot.wav` | `laserLarge_002.ogg` | Sci-Fi Sounds | Tir puissant variante |
| `sfx_weapon_overload_pulse.wav` | `forceField_000.ogg` | Sci-Fi Sounds | Pulse EMP / champ de force |
| `sfx_weapon_drone_loop.wav` | `engineCircular_000.ogg` | Sci-Fi Sounds | Moteur circulaire (boucle 5s) |
| `sfx_weapon_fusion_activate.wav` | `forceField_004.ogg` | Sci-Fi Sounds | Activation champ de force |
| `sfx_weapon_fusion_loop.wav` | `engineCircular_002.ogg` | Sci-Fi Sounds | Moteur circulaire var. (boucle 5s) |
| `sfx_weapon_sentinel_shoot.wav` | `laserRetro_000.ogg` | Sci-Fi Sounds | Laser retro ennemi |

### Joueur

| Fichier WAV | Source originale (OGG) | Pack | Description |
|---|---|---|---|
| `sfx_player_hit.wav` | `impactMetal_medium_001.ogg` | Impact Sounds | Impact metal moyen |
| `sfx_player_die.wav` | `lowFrequency_explosion_000.ogg` | Sci-Fi Sounds | Explosion basse frequence (2s) |

### Ennemis

| Fichier WAV | Source originale (OGG) | Pack | Description |
|---|---|---|---|
| `sfx_enemy_swarm_die.wav` | `impactGeneric_light_000.ogg` | Impact Sounds | Impact leger generique |
| `sfx_enemy_drone_die.wav` | `explosionCrunch_001.ogg` | Sci-Fi Sounds | Explosion compacte (1.3s) |
| `sfx_enemy_sentinel_die.wav` | `impactMetal_heavy_002.ogg` | Impact Sounds | Impact metal lourd |
| `sfx_enemy_sentinel_projectile.wav` | `laserRetro_002.ogg` | Sci-Fi Sounds | Laser retro variante |
| `sfx_enemy_colossus_die.wav` | `lowFrequency_explosion_001.ogg` | Sci-Fi Sounds | Explosion grave grave (1s) |

### Gameplay

| Fichier WAV | Source originale (OGG) | Pack | Description |
|---|---|---|---|
| `sfx_levelup.wav` | `switch24.ogg` | UI Audio | Switch positif montant |
| `sfx_card_select.wav` | `rollover2.ogg` | UI Audio | Survol / rollover propre |
| `sfx_core_collect.wav` | `handleCoins.ogg` | RPG Audio | Manipulation pieces / loot |
| `sfx_xp_collect.wav` | `click1.ogg` | UI Audio | Click court (0.09s) |
| `sfx_fusion_evolve.wav` | `forceField_003.ogg` | Sci-Fi Sounds | Champ de force charge-liberation |

### Interface

| Fichier WAV | Source originale (OGG) | Pack | Description |
|---|---|---|---|
| `sfx_ui_button.wav` | `mouseclick1.ogg` | UI Audio | Clic souris net (0.05s) |
| `sfx_ui_purchase.wav` | `switch33.ogg` | UI Audio | Switch de confirmation |
| `sfx_ui_victory.wav` | `switch26.ogg` | UI Audio | Switch positif bref |
| `sfx_ui_death.wav` | `impactBell_heavy_000.ogg` | Impact Sounds | Cloche grave (1.5s) |

---

## Musiques (`assets/audio/music/`)

### Stingers — ASSETS KENNEY CC0

| Fichier WAV | Sources concatenees | Pack | Duree | Description |
|---|---|---|---|---|
| `music_stinger_victory.wav` | `jingles_NES00` + `jingles_NES13` + `jingles_NES05` + `jingles_NES11` | Music Jingles | 4.5s | 4 jingles 8-bit concatenes |
| `music_stinger_death.wav` | `jingles_HIT09` + `jingles_HIT11` + `jingles_HIT15` | Music Jingles | 2.9s | 3 jingles "hit" concatenes |

### Musiques longues — PLACEHOLDERS SYNTHETISES v2 (a remplacer)

**Source : "Retro Game Music Pack" par Juhani Junkala (SubspaceAudio)**
https://opengameart.org/content/5-chiptunes-action
Licence : CC0 / Domaine public. Aucune attribution obligatoire.
Conversion : WAV source → ffmpeg -ar 44100 -ac 2 -sample_fmt s16 (stereo 44100 Hz 16-bit).
music_menu : bouclee 3x via -stream_loop 2 -t 35 pour atteindre ≥30s.

| Fichier | Duree | Source originale (ZIP Juhani Junkala) | Statut |
|---|---|---|---|
| `music_menu.wav` | 33.9 s stereo | "Title Screen.wav" × 3 boucles | CC0 definitif |
| `music_hub.wav` | 44.6 s stereo | "Ending.wav" | CC0 definitif |
| `music_run_intro.wav` | 1:14 stereo | "Level 1.wav" | CC0 definitif |
| `music_run_mid.wav` | 1:12 stereo | "Level 2.wav" | CC0 definitif |
| `music_run_intense.wav` | 1:21 stereo | "Level 3.wav" | CC0 definitif |

---

## Conversion technique

```
SFX mono  : ffmpeg -y -i input.ogg -ar 44100 -ac 1 -sample_fmt s16 output.wav
SFX stereo: ffmpeg -y -i input.ogg -ar 44100 -ac 2 -sample_fmt s16 output.wav
Concat    : ffmpeg -y -f concat -safe 0 -i list.txt -ar 44100 -ac 2 -sample_fmt s16 output.wav
```

Script d'integration reproductible :
```
C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe tools/integrate_kenney_audio.py
```

Necessite les packs Kenney dans `tools/kenney_downloads/extracted/` (re-telechargeables depuis
les URLs Kenney documentees ci-dessus).

---

## Roadmap audio

**Statut actuel : 31/31 fichiers CC0 definitifs** (24 SFX Kenney + 2 stingers Kenney + 5 musiques Juhani Junkala).

### Pour une bande-son sur mesure (post-MVP)
Commission compositeur — budget estime 500-2000 EUR.
References stylistiques :
- Darren Korb (Hades, Transistor) — fusion electronique/acoustique
- Yoann Laulan (Dead Cells) — metal/chiptune phases intenses
Plateformes : Soundbetter.com, AirGigs.com, Fiverr Pro

---

*Document maintenu par l'agent `musicien`*
*Integration Kenney CC0 : 2026-06-22 — 24 SFX + 2 stingers*
*Integration Juhani Junkala CC0 : 2026-06-22 — 5 musiques longues definitives*
