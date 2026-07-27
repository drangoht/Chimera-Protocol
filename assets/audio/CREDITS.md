# CREDITS AUDIO — Chimera Protocol

## Statut global

**MUSIQUE (14 pistes) : GENEREE SUR SUNO — 2026-07-27**
**Toutes les musiques du jeu ont ete generees avec Suno** (https://suno.com) a partir des prompts
de `docs/AUDIO_AI_PROMPTS.md`, puis traitees par `tools/import_ai_music.py` (bouclage, calage de
loudness EBU R128, encodage OGG). Direction : metal industriel / synth-metal.

> **LICENCE — A VERIFIER AVANT TOUTE PUBLICATION.** Les droits commerciaux sur les morceaux Suno
> dependent du **plan d'abonnement actif au moment de la generation** : le plan gratuit est
> reserve a un usage non commercial, les plans payants accordent des droits commerciaux.
> Chimera Protocol etant distribue sur itch.io, **relire les CGU Suno et consigner ici le plan
> utilise, avec la date**, avant la premiere release integrant ces pistes.
> Plan utilise : *a renseigner*. CGU relues le : *a renseigner*.

Les 3 stingers (`death`, `victory`, `levelup`) restent **synthetises par le depot**
(`tools/generate_music_v3.py`) — propriete du projet, aucune contrainte de licence.

Historique : ces pistes remplacent la bande-son synthetisee "Blade Runner / Vangelis" du
2026-07-27 (26 fichiers, 5 biomes × 4 stems), jugee trop lente pour le rythme du jeu. Cette
bande-son reste **regenerable a l'identique** par `python tools/generate_music_v3.py` et sert de
filet de securite sans contrainte de licence. Elle-meme avait remplace des placeholders chiptune
CC0 (Juhani Junkala) et une intro CC0 (SRG774).

**SFX (24 fichiers) : ASSETS KENNEY CC0 — integres le 2026-06-22** (inchanges)
Convertis depuis les packs Kenney.nl (OGG -> WAV 44100 Hz 16-bit mono via ffmpeg 8.1.1).
Licence : CC0 / Domaine public — utilisation commerciale libre, aucune attribution obligatoire.

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

## Musiques (`assets/audio/music/`) — GENEREES SUR SUNO

Prompts source (un par piste, style + description longue) : **`docs/AUDIO_AI_PROMPTS.md`**.
Direction : metal industriel / synth-metal — guitares down-tuned et batterie au premier plan,
synthes analogiques et chœurs sans paroles au service du riff.

### Pistes simples (non adaptatives)

| Fichier | Boucle | Tonalite / BPM | Role |
|---|---|---|---|
| `music_menu.ogg` | 51.5 s | La mineur, 112 BPM | Theme principal — riff palm-mute, batterie mid-tempo |
| `music_hub.ogg` | 105.0 s | Re mineur, 118 BPM | L'enclave — groove stoner, basse fuzz |
| `music_intro.ogg` | 94.0 s (non bouclee) | Re mineur | Cinematique (`IntroScreen`), calee sur la cut-scene |

### Stingers — synthetises par le depot (pas Suno)

> **Non cables** : aucun `music_stinger_*` n'est reference dans le code — ils ne jouent jamais.
> Les moments concernes utilisent des SFX Kenney (`sfx_levelup`, `sfx_ui_death`, `sfx_ui_victory`).
> A cabler ou a supprimer.

| Fichier | Duree | Tonalite | Role |
|---|---|---|---|
| `music_stinger_death.ogg` | 4.6 s | glissando descendant | Dissolution, sans resolution harmonique |
| `music_stinger_victory.ogg` | 6.5 s | Do majeur | Hors diegese — le moment lumineux |
| `music_stinger_levelup.ogg` | 2.0 s | Do majeur | 4 notes cloche, bande 2-6 kHz pour percer en combat |

### Pistes de run — 5 biomes × 2 versions + 1 theme de boss commun

`music_run_<biome>_{calm,combat}.ogg` + `music_run_boss.ogg`. Les deux versions d'un biome sont
**le meme morceau** (meme tonalite, meme tempo, meme riff) en couplet et en refrain. `MusicDirector`
n'en rend qu'une audible a la fois et bascule par fondu croise selon l'intensite de l'action
(cf. `src/Core/Rules/MusicIntensity.cs`).

| Biome | Tonalite / Mode | BPM | Boucle calm | Boucle combat |
|---|---|---|---|---|
| `sanctuaire` | Do mineur | 140 | 40.3 s | 129.3 s |
| `aether` | Re Phrygien dominant | 152 | 40.3 s | 54.6 s |
| `givre` | La Dorien (groove half-time) | 130 | 29.6 s | 45.1 s |
| `fournaise` | Sol Phrygien | 176 | 109.7 s | 27.4 s |
| `neon` | Mi Mixolydien | 160 | 42.3 s | 186.6 s |
| `boss` (commun) | Do mineur chromatique | 150 | 72.3 s | — |

Export : OGG Vorbis q6, 44100 Hz stereo, -1.5 dBTP. Loudness **-22 LUFS** pour les pistes de run
(-23 menu/hub, -21 intro) — volontairement bas pour de la musique : ce metal est tres compresse et
son RMS reste haut en permanence, alors que les SFX du jeu sont des transitoires courts (un
ramassage d'XP tourne autour de -30 dB RMS). A -16 LUFS, premier niveau essaye, la bande-son
couvrait purement et simplement les SFX.

Le calage se fait par **mesure puis gain constant** (`apply_loudness`), pas par le filtre
`loudnorm` de ffmpeg : en une passe celui-ci travaille en mode dynamique, donc il recompresse un
master deja fini et rate sa cible (-14.3 mesure pour -16 demande). Pour changer le niveau general
de la musique : `MUSIC_LUFS` dans `tools/import_ai_music.py`, puis relancer l'import.

### Reintegration depuis les sources Suno

Les MP3 deposes dans `music_ai/` ne sont pas versionnes ; garder une copie hors depot.

```
python tools/import_ai_music.py                    # tout ce qui est dans music_ai/
python tools/import_ai_music.py --list             # etat : depose / integre
python tools/import_ai_music.py --only neon_combat --keep-preview   # ecouter avant d'installer
python tools/import_ai_music.py --loop-tolerance 0.8                # boucles plus longues
"…/Godot…mono.exe" --headless --import             # generer les .import Godot
```

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

**Statut actuel : 14 musiques generees sur Suno + 3 stingers synthetises + 24 SFX Kenney CC0.**

### Points ouverts
- **Licence Suno (bloquant pour la publication)** : renseigner le plan d'abonnement et la date de
  relecture des CGU dans l'encadre en tete de ce document, avant la premiere release.
- **Longueur des boucles** : quatre pistes bouclent sous 45 s (`givre_calm` 29.6 s,
  `fournaise_combat` 27.4 s, `sanctuaire_calm` et `aether_calm` 40.3 s) parce que les morceaux
  generes evoluent trop pour offrir un raccord propre plus tard. A retravailler soit en relancant
  l'import avec `--loop-tolerance 0.8`, soit en regenerant ces pistes sur Suno avec un prompt plus
  repetitif.
- **SFX** : derniers assets tiers. Ils pourraient etre regeneres avec `synth_lib` pour une
  coherence timbrale totale avec la musique.
- **Composition humaine** : `docs/ART_BRIEF_AUDIO.md` (tonalites, BPM, progressions, contraintes
  de bouclage) reste directement exploitable par un compositeur.

---

*Document maintenu par l'agent `musicien`*
*Musique generee sur Suno : 2026-07-27 — 14 pistes (menu, hub, intro, 5 biomes × 2, boss)*
*Stingers synthetises par le depot : 2026-07-27 — 3 pistes*
*Integration Kenney CC0 : 2026-06-22 — 24 SFX*
