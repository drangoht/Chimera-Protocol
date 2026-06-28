# AUDIO GUIDE — Chimera Protocol

> Document de référence pour la direction sonore du MVP.
> Auteur : agent `musicien` — 2026-06-20.
> Statut de tous les fichiers audio : **PLACEHOLDERS MANQUANTS** — aucun fichier audio
> n'est encore présent dans le dépôt. AudioSystem gère leur absence sans crash (null-guard).
> Chaque fichier doit être remplacé par un asset definitif ou un asset CC0 documenté
> avant de considérer la Phase 3 comme terminée.

---

## 1. Direction musicale

### 1.1 Identité sonore

L'univers de Chimera Protocol repose sur une opposition centrale (cf. GDD §3 et §12) :
- **Matiere morte** : ruines, rouille, corruption mechanique — sons graves, metalliques,
  textures de synthese analogiques degradees, distorsion.
- **Energie vivante (Aether)** : magie ancienne reactivee — sons crystallins, harmoniques
  hautes, reverb longue, nappes electroniques lumineuses.

La bande-son doit incarner cette tension en permanence : le substrat sonore est rugueux et
metallique, mais traverse de saillies cristallines Aether qui grandissent a mesure que le
joueur monte en puissance.

### 1.2 References musicales

Ces references definissent le territoire sonore cible. Aucune n'est a copier litteralement ;
elles servent de boussole pour briefer un compositeur ou selectionner des assets CC0.

| Reference | Ce qu'on en prend |
|---|---|
| Vampire Survivors OST (Phas3r) | Energie repetitive hypnotique, montee graduelle de l'intensite pendant la run, boucles courtes qui s'epaississent |
| Hades OST (Darren Korb — "In the Blood") | Fusion rock electrique + ethnique + electronique ; permet l'ecriture d'une identite forte sur un budget reduit |
| Dead Cells OST (Yoann Laulan) | Pixel art + synthese modulaire ; sons de synthese qui sonnent "organiquement corrompus" |
| Transistor OST (Darren Korb) | Voix comme instrument, textures electroniques poetiques ; inspira pour les transitions et l'ecran de fin |

BPM cible par contexte :
- Menu principal : 70-80 BPM (atmopherique, suspension)
- Run (debut) : 110-120 BPM (tension montante)
- Run (fin, > 10 min) : identique en BPM, densite rythmique augmentee par couches additionnelles
- Hub : 85-95 BPM (calme strategique, pas de pression)
- Ecran de fin — victoire : stinger court puis retour Hub
- Ecran de fin — mort : stinger court grave puis silence ou ambient tres bas

### 1.3 Palette instrumentale

| Instrument / Son | Role |
|---|---|
| Basse de synthese analogique (sub + harmoniques) | Fondation rythmique, sentiment de poids et de danger |
| Synthese metallique (FM ou echantillons de metal rouille) | Texture "monde corrompu", en contrepoint des basses |
| Pad cristallin / glass harmoniques | Energie Aether, soulignement des montees de niveau |
| Percussion electronique (kick, clap numerique) | Rythme de run, acceleration percue |
| Arpeges haut registre (synthese granulaire ou piano electrique) | Melodie portante, memorabilite |
| Nappe de cordes numeriques | Tension emotionnelle, ecran de fin |
| Drone basse frequence (pedal point) | Fond continu de menace en run, s'intensifie |

### 1.4 Evolution dynamique pendant la run (tension croissante)

AudioSystem expose `SetRunIntensity(float t)` (0.0 a 1.0, lie au timer de run).
La realisation de la musique dynamique au MVP est simplifiee : une seule piste en boucle.
La montee de tension est obtenue par **changement de piste** a des seuils de temps :

| Seuil timer | Piste jouee | Description |
|---|---|---|
| 0:00 - 5:00 | `run_intro` | Rythmique legere, melodie claire, peu de distorsion |
| 5:00 - 10:00 | `run_mid` | Basse plus presente, percussion plus dense, harmoniques saturees |
| 10:00 - 15:00 | `run_intense` | Tout a la fois — distorsion, drone grave, arpeges acceleres |

Transition entre pistes : fondu enchaîne de 2 secondes via `PlayMusic(..., fadeInSec: 2f)`.
Post-MVP : integration Godot AudioBusLayout avec envoyes paralleles et automation de volume
par bus pour une musique vraiment adaptive (ajout de couches sans rupture).

---

## 2. Themes musicaux requis

### 2.1 Theme de menu principal — `music_menu`

- Fichier : `assets/audio/music/music_menu.ogg`
- Duree cible : 2:00-3:00, loop seamless (point de bouclage sur mesure 1 ou 2)
- Caracteristiques : atmospherique, lent (70-80 BPM), large reverb, pad Aether dominant,
  motif melodique simple de 8 mesures qui resonne apres l'ecoute.
- Emotion : mystere et appel de l'aventure. Le joueur doit avoir envie de plonger.
- Point de bouclage : fin de phrase musicale complete, silence de 0.5 s pour la couture.

### 2.2 Theme de run — debut — `music_run_intro`

- Fichier : `assets/audio/music/music_run_intro.ogg`
- Duree cible : 2:00-2:30, loop seamless
- Caracteristiques : rythmique presente (110-120 BPM), basse solide, melodie portante,
  espace sonore encore aere (le joueur prend ses marques)
- Declenchement : au chargement de `Game.tscn`, fade in 0.5 s

### 2.3 Theme de run — milieu — `music_run_mid`

- Fichier : `assets/audio/music/music_run_mid.ogg`
- Duree cible : 2:00-2:30, loop seamless
- Caracteristiques : meme structure rythmique que `run_intro`, couche de basse additionnelle,
  percussion plus saturee, pad Aether plus present en arriere-plan
- Declenchement : a 5:00 restant (timer a 10:00 sur 15), fondu enchaîne 2 s

### 2.4 Theme de run — intense — `music_run_intense`

- Fichier : `assets/audio/music/music_run_intense.ogg`
- Duree cible : 2:00-2:30, loop seamless
- Caracteristiques : densite maximale, drone grave permanent, arpeges acceleres,
  distorsion sur la basse, sentiment d'urgence et de chaos maîtrise
- Declenchement : a 10:00 restant (timer a 5:00 sur 15), fondu enchaîne 2 s

### 2.5 Theme de Hub — `music_hub`

- Fichier : `assets/audio/music/music_hub.ogg`
- Duree cible : 2:00, loop seamless
- Caracteristiques : calme mais actif (85-95 BPM), sentiment de securite relative,
  harmonies plus consonantes que la run, quelques accents cristallins Aether
- Declenchement : au chargement de `HubScreen.tscn`, fade in 0.5 s

### 2.6 Stinger victoire — `music_stinger_victory`

- Fichier : `assets/audio/music/music_stinger_victory.ogg`
- Duree cible : 3-5 s, pas de loop
- Caracteristiques : accord majeur lumineux (harmoniques Aether), montee rapide,
  resolution positive. Joue a l'apparition de "EXTRACTION REUSSIE".

### 2.7 Stinger mort — `music_stinger_death`

- Fichier : `assets/audio/music/music_stinger_death.ogg`
- Duree cible : 2-3 s, pas de loop
- Caracteristiques : accord mineur descendant, son metallique de chute, decay grave.
  Joue a l'apparition de "MORT EN SERVICE".

---

## 3. Liste SFX complete

Tous les SFX sont des **placeholders** : aucun fichier audio n'existe encore.
AudioSystem logge un warning (pas de crash) pour chaque SFX manquant.

Convention de nommage : `sfx_[categorie]_[action].wav` (WAV prefere pour les SFX courts
a cause de la latence minimale ; OGG acceptable pour les boucles longues).

### 3.1 Armes — tirs et effets

| ID SFX | Fichier | Description precise |
|---|---|---|
| `sfx_weapon_impulse_shoot` | `sfx/sfx_weapon_impulse_shoot.wav` | Tir Canon a Impulsions : claquement electrique sec, composante haute frequence (energie), suivi d'un bref sifflement de projectile. ~0.15 s. |
| `sfx_weapon_plasma_swing` | `sfx/sfx_weapon_plasma_swing.wav` | Arc Lame Plasma : swoosh grave + buzz electrique, sentiment d'arc d'energie tranchant. ~0.3 s. |
| `sfx_weapon_overload_pulse` | `sfx/sfx_weapon_overload_pulse.wav` | Pulse Champ de Surcharge : onde de choc sourde + basse frequence "thump", reverb courte qui suggere une zone d'effet. ~0.4 s. |
| `sfx_weapon_drone_loop` | `sfx/sfx_weapon_drone_loop.wav` | Drone en orbite (boucle continue) : bourdonnement mecanique grave + harmonique sifflante, doit etre discret pour ne pas couvrir les autres sons. Loop seamless. ~1.0 s. |
| `sfx_weapon_sentinel_shoot` | `sfx/sfx_weapon_sentinel_shoot.wav` | Tir Sentinelle (ennemi) : son plus lourd et menacant que le tir joueur, composante grave. ~0.2 s. |
| `sfx_weapon_rail_shoot` | `sfx/sfx_weapon_rail_shoot.wav` | Rafale Rail Surchaege (3 projectiles rapides) : trio de claquements a 0.12 s d'intervalle, son plus puissant que le canon de base. Jouer 3 fois en sequence ou designer comme 1 SFX de 0.5 s couvrant la rafale. |
| `sfx_weapon_fusion_activate` | `sfx/sfx_weapon_fusion_activate.wav` | Activation Lame a Fusion (anneau continu) : son de demarrage — montee en frequence + resonance metallique, puis stabilisation en bourdonnement Aether. ~0.8 s. |
| `sfx_weapon_fusion_loop` | `sfx/sfx_weapon_fusion_loop.wav` | Boucle Lame a Fusion active : sifflement d'energie doux, discret. Loop seamless. ~1.0 s. |

### 3.2 Ennemis — morts et projectiles

| ID SFX | Fichier | Description precise |
|---|---|---|
| `sfx_enemy_swarm_die` | `sfx/sfx_enemy_swarm_die.wav` | Mort Essaim de Rouille : craquement metallique bref + bruit de debris qui tombent. Son leger (ennemi fragile). ~0.2 s. |
| `sfx_enemy_drone_die` | `sfx/sfx_enemy_drone_die.wav` | Mort Drone Corrompu : explosion electrique courte + bip d'arret de systeme. ~0.25 s. |
| `sfx_enemy_sentinel_die` | `sfx/sfx_enemy_sentinel_die.wav` | Mort Sentinelle Corrompue : son plus ample (ennemi plus tanky), explosion mecanique avec reverb. ~0.4 s. |
| `sfx_enemy_colossus_die` | `sfx/sfx_enemy_colossus_die.wav` | Mort Colosse Greffe : impact tres lourd au sol + resonance grave long decay, doit signaler un evenement important. ~0.8 s. |
| `sfx_enemy_sentinel_projectile` | `sfx/sfx_enemy_sentinel_projectile.wav` | Projectile Sentinelle en vol (optionnel, joue au tir) : sifflement grave de projectile. ~0.15 s. |

### 3.3 Gameplay — progression et collecte

| ID SFX | Fichier | Description precise |
|---|---|---|
| `sfx_xp_collect` | `sfx/sfx_xp_collect.wav` | Collecte orbe XP : son cristallin court, harmonique haute (Aether), leger "ting". ~0.1 s. Sera joue tres frequemment — doit etre tres discret et non-fatiguant. |
| `sfx_core_collect` | `sfx/sfx_core_collect.wav` | Collecte Noyau d'Aether : son plus marque que l'orbe XP, tonalite violette/mystique (violet #AA44FF), sentiment de recompense. ~0.3 s. |
| `sfx_levelup` | `sfx/sfx_levelup.wav` | Montee de niveau : son ascendant marquant, harmoniques Aether, doit clairement signaler l'evenement positif. ~0.5 s. |
| `sfx_card_select` | `sfx/sfx_card_select.wav` | Selection carte level-up : clic net + bref accent selon rarete (commun : sec, rare : cristallin, epique : resonant). Version unique pour le MVP. ~0.15 s. |
| `sfx_fusion_evolve` | `sfx/sfx_fusion_evolve.wav` | Fusion/evolution : son le plus marque du jeu (evenement rare et important). Sequence : impact sourd + flash de silence + explosion d'energie Aether + resonance longue. ~1.5-2.0 s. Accompagne le flash de desaturation visuelle (GDD §12). |

### 3.4 Joueur

| ID SFX | Fichier | Description precise |
|---|---|---|
| `sfx_player_hit` | `sfx/sfx_player_hit.wav` | Joueur touche : son court et net, signal clair de danger sans etre genant (joue potentiellement tres souvent). Composante grave (douleur/impact), pas trop long. ~0.2 s. |
| `sfx_player_die` | `sfx/sfx_player_die.wav` | Mort du joueur : son de defaite lisible, plus long que le hit, accord mineur descendant ou son de systeme qui s'eteint. ~0.8-1.0 s. |

### 3.5 Interface utilisateur

| ID SFX | Fichier | Description precise |
|---|---|---|
| `sfx_ui_button` | `sfx/sfx_ui_button.wav` | Clic bouton generique (menu, Hub) : son sec et net, neutre, pas intrusif. ~0.1 s. |
| `sfx_ui_purchase` | `sfx/sfx_ui_purchase.wav` | Achat Hub (amelioration permanente) : son de validation positive, leger accent Aether. ~0.3 s. |
| `sfx_ui_victory` | `sfx/sfx_ui_victory.wav` | Identique a `music_stinger_victory` ou version SFX seule sans la partie musicale. Peut etre le meme fichier. |
| `sfx_ui_death` | `sfx/sfx_ui_death.wav` | Identique a `music_stinger_death` ou version SFX seule. Peut etre le meme fichier. |

---

## 4. Format des fichiers

### 4.1 Musiques

- Format : **OGG Vorbis** (compresse, loop seamless support natif Godot)
- Qualite : 128 kbps minimum, 192 kbps recommande
- Canaux : stereo
- Loop : definir les points de loop dans les metadonnees OGG ou via l'import Godot
  (propriete `loop` dans le panneau Import, cocher "Enable Loop" + ajuster "Loop Offset")
- Nom de fichier : `music_[id].ogg` — sans espace, sans majuscule
- Dossier : `res://assets/audio/music/`

### 4.2 SFX

- Format prefere : **WAV** (latence minimale, pas de decompression)
- Format alternatif : **OGG** acceptable pour les boucles longues (drone) ou les SFX > 1 s
- Qualite WAV : 44 100 Hz, 16 bits minimum
- Canaux : mono (les SFX sont positionnels ou globaux — le stereo n'apporte rien sauf pour
  les stingers de victoire/mort)
- Nom de fichier : `sfx_[categorie]_[action].wav` — sans espace, sans majuscule
- Dossier : `res://assets/audio/sfx/`

### 4.3 Volumes de reference (dB)

Ces valeurs sont des cibles de mixage avant normalisation par le slider utilisateur.
AudioSystem applique un volume lineaire (0.0-1.0) ; le mixage fin se fera en Phase 3.

| Type | Volume cible (relatif) | Notes |
|---|---|---|
| Musique run | -12 dB | Ne doit pas couvrir les SFX de hit |
| SFX tir joueur | -6 dB | Son frequent, ne pas fatiguer |
| SFX mort ennemi basique | -8 dB | Tres frequent, doit rester discret |
| SFX mort Colosse | -3 dB | Evenement important, son marque |
| SFX level-up | -3 dB | Evenement positif, bien audible |
| SFX fusion | 0 dB | Evenement rare et dramatique |
| SFX XP collecte | -14 dB | Tres frequent, quasi-subliminaire |
| Drone loop | -16 dB | Ambiance, tres discrett |

---

## 5. Sources CC0 recommandees (assets libres de droits)

Ces sources permettent d'integrer rapidement des placeholders de qualite avant la
commande eventuelle d'une bande-son originale. Tous les assets listes ci-dessous sont
sous licence CC0 ou equivalente (domaine public).

**Important** : verifier systematiquement la licence de chaque asset individuel sur ces
plateformes avant integration. Les termes CC0 peuvent varier par pack.

### 5.1 Musiques

| Source | URL | Ce qu'on y trouve |
|---|---|---|
| Free Music Archive (FMA) | freemusicarchive.org | Nombreux tracks electroniques/ambient CC0 ou CC-BY |
| OpenGameArt.org (musiques) | opengameart.org/content/browse/music | Section "music", filtre licence CC0 ou CC-BY ; plusieurs tracks chiptune/synthwave |
| Incompetech (Kevin MacLeod) | incompetech.com | Excellent catalogue libre (CC-BY), nombreux styles dont electronique/tense |
| ccMixter | ccmixter.org | Samples et tracks CC |

Recherches cibles sur OpenGameArt.org :
- "dark ambient loop" — theme menu
- "intense electronic loop" — theme run
- "calm ambient loop" — Hub

### 5.2 SFX

| Source | URL | Ce qu'on y trouve |
|---|---|---|
| Freesound.org (filtre CC0) | freesound.org | Enorme base, filtrer par CC0 ; rechercher "laser", "explosion", "metal hit", "crystal" |
| OpenGameArt.org (SFX) | opengameart.org/content/browse/sfx | Packs SFX retro et electroniques CC0 |
| Kenney.nl | kenney.nl/assets?q=audio | Packs SFX de grande qualite, tous CC0. "Impact Sounds", "Sci-fi Sounds", "UI Audio" |
| SoundSnap (limites gratuites) | soundsnap.com | Qualite elevee, verifier les termes |

Packs Kenney.nl particulierement pertinents :
- **"Sci-Fi Sounds"** (kenney.nl/assets/sci-fi-sounds) : tirs laser, impacts — parfait pour
  les SFX d'armes
- **"Impact Sounds"** : explosions, crashes — morts d'ennemis
- **"UI Audio"** : clics, boutons — interface
- **"RPG Audio"** : collectes, level-up

### 5.3 Recommandation de workflow

1. Telecharger les packs Kenney "Sci-Fi Sounds", "Impact Sounds" et "UI Audio" (CC0, aucune
   attribution requise) pour couvrir la majorite des SFX en une heure.
2. Pour la musique : tester 2-3 tracks CC0 de Kevin MacLeod (incompetech.com) pour le menu
   et le Hub ; chercher sur OpenGameArt.org pour la run.
3. Placer les fichiers dans `assets/audio/music/` et `assets/audio/sfx/` avec les noms
   documentes dans la section 3 de ce guide.
4. Verifier que AudioSystem charge et joue chaque asset (les warnings de fichier manquant
   disparaissent au fur et a mesure).
5. Documenter les attributions dans un fichier `assets/audio/CREDITS.md` si les licences
   le requierent (CC-BY notamment).

---

## 6. Structure de dossiers

```
assets/audio/
├── music/
│   ├── music_menu.ogg               [PLACEHOLDER MANQUANT]
│   ├── music_run_intro.ogg          [PLACEHOLDER MANQUANT]
│   ├── music_run_mid.ogg            [PLACEHOLDER MANQUANT]
│   ├── music_run_intense.ogg        [PLACEHOLDER MANQUANT]
│   ├── music_hub.ogg                [PLACEHOLDER MANQUANT]
│   ├── music_stinger_victory.ogg    [PLACEHOLDER MANQUANT]
│   └── music_stinger_death.ogg      [PLACEHOLDER MANQUANT]
└── sfx/
    ├── sfx_weapon_impulse_shoot.wav     [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_plasma_swing.wav      [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_overload_pulse.wav    [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_drone_loop.wav        [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_sentinel_shoot.wav    [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_rail_shoot.wav        [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_fusion_activate.wav   [PLACEHOLDER MANQUANT]
    ├── sfx_weapon_fusion_loop.wav       [PLACEHOLDER MANQUANT]
    ├── sfx_enemy_swarm_die.wav          [PLACEHOLDER MANQUANT]
    ├── sfx_enemy_drone_die.wav          [PLACEHOLDER MANQUANT]
    ├── sfx_enemy_sentinel_die.wav       [PLACEHOLDER MANQUANT]
    ├── sfx_enemy_colossus_die.wav       [PLACEHOLDER MANQUANT]
    ├── sfx_enemy_sentinel_projectile.wav [PLACEHOLDER MANQUANT]
    ├── sfx_xp_collect.wav               [PLACEHOLDER MANQUANT]
    ├── sfx_core_collect.wav             [PLACEHOLDER MANQUANT]
    ├── sfx_levelup.wav                  [PLACEHOLDER MANQUANT]
    ├── sfx_card_select.wav              [PLACEHOLDER MANQUANT]
    ├── sfx_fusion_evolve.wav            [PLACEHOLDER MANQUANT]
    ├── sfx_player_hit.wav               [PLACEHOLDER MANQUANT]
    ├── sfx_player_die.wav               [PLACEHOLDER MANQUANT]
    ├── sfx_ui_button.wav                [PLACEHOLDER MANQUANT]
    ├── sfx_ui_purchase.wav              [PLACEHOLDER MANQUANT]
    ├── sfx_ui_victory.wav               [PLACEHOLDER MANQUANT]
    └── sfx_ui_death.wav                 [PLACEHOLDER MANQUANT]
```

---

## 7. Integration Godot — points cles

- AudioSystem est un AutoLoad (singleton) enregistre apres MetaProgressionSystem dans
  `project.godot`.
- Les volumes musique et SFX sont controlables independamment via les proprietes
  `MusicVolume` et `SfxVolume` (0.0 a 1.0, convertis en dB lineairement).
- Le pool SFX contient 8 canaux `AudioStreamPlayer` ; si tous sont occupes, le son le plus
  ancien est interrompu pour ceder la place (comportement voulu pour eviter l'accumulation
  lors des essaims denses).
- La boucle du drone (`sfx_weapon_drone_loop`) est geree separement : PlaySfxLoop /
  StopSfxLoop a implémenter en Phase 3 si necessaire ; pour le MVP, `PlaySfx` avec un
  AudioStreamPlayer dedie dans DroneSwarm.cs est acceptable.
- Les options audio (volumes musique/SFX) sont a sauvegarder dans `user://settings.json`
  post-MVP ; pour le MVP, les valeurs par defaut (1.0 / 1.0) sont acceptables.
- Chaque appel `AudioSystem.Instance?.PlaySfx(...)` est null-safe : si AudioSystem n'est
  pas encore charge (ex. tests unitaires hors Godot), l'appel est silencieusement ignore.
