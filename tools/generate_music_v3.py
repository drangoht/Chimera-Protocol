"""
generate_music_v3 — production de toute la musique de Chimera Protocol.

Implémente `docs/ART_BRIEF_AUDIO.md` : 5 biomes × 4 stems adaptatifs
(bed / pulse / lead / boss) + menu, intro, hub et stingers. Aucun sample
externe : tout est synthétisé (`synth_lib` + `synth_instruments`), donc les
pistes sont régénérables, libres de droits et modifiables sans logiciel tiers.

Usage :
  python tools/generate_music_v3.py                 # tout (~25 fichiers)
  python tools/generate_music_v3.py --only menu hub # une sélection
  python tools/generate_music_v3.py --only sanctuaire --preview
  python tools/generate_music_v3.py --list

`--preview` écrit dans `build/music_preview/` au lieu de `unity/Assets/Resources/Audio/music/`
et n'écrase donc rien.

Points de conception :
- **Bouclage** : chaque piste est rendue plus longue que sa boucle, puis
  `loopify()` réinjecte la queue (reverbs, delays) au début. Résultat : pas de
  blanc ni de coupure au point de bouclage.
- **Stems séparables** : le `bed` ne joue que la quinte nue (cf. brief §2), donc
  aucune couche ne sonne « fausse » ou « incomplète » quand les autres sont
  muettes — c'est la condition pour que le mixage adaptatif en jeu tienne.
- **Déterminisme** : toutes les sources aléatoires sont ensemencées à partir de
  l'identifiant de la piste ; deux exécutions produisent des fichiers identiques.
"""

from __future__ import annotations

import argparse
import os
import sys
import time
from dataclasses import dataclass, field

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import synth_instruments as I  # noqa: E402
import synth_lib as S  # noqa: E402
import unity_paths  # noqa: E402

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MUSIC_DIR = str(unity_paths.audio_dir("music"))
PREVIEW_DIR = os.path.join(PROJECT_ROOT, "build", "music_preview")

# Marge rendue au-delà de la boucle, réinjectée au début par `loopify`.
TAIL = 6.0

# Export des stems : même loudness pour tous (les rapports entre couches sont
# décidés en jeu) et 6 dB de garde au pic, sans quoi la somme des 4 écrête.
STEM_LUFS = -20.0
STEM_TRUE_PEAK = -6.0

# Niveaux relatifs des couches (brief §4.2) — la source de vérité est le C#
# (`MusicDirector`) ; repris ici pour la démo d'écoute et l'analyse.
STEM_MIX_DB = {"bed": -6.0, "pulse": -4.0, "lead": -3.0, "boss": 0.0}


# ---------------------------------------------------------------------------
# Grille rythmique
#
# Les patterns s'écrivent en doubles-croches, 16 pas par mesure :
#   'x' = frappe forte, 'o' = frappe faible, '.' = silence
# C'est lisible d'un coup d'œil et ça se relit comme une partition de boîte à
# rythmes, ce qui rend le tuning des biomes rapide.
# ---------------------------------------------------------------------------

def hits(pattern: str, bpm: float, bars: int, offset: float = 0.0) -> list[tuple[float, float]]:
    """
    Développe un pattern sur `bars` mesures.
    Renvoie [(instant en secondes, vélocité)].
    """
    step = 60.0 / bpm / 4.0  # une double-croche
    per_bar = len(pattern)
    out: list[tuple[float, float]] = []
    for bar in range(bars):
        for i, ch in enumerate(pattern):
            if ch == ".":
                continue
            vel = 1.0 if ch == "x" else 0.55
            out.append((offset + (bar * per_bar + i) * step, vel))
    return out


def kick_times(pattern: str, bpm: float, bars: int) -> list[float]:
    """Instants de grosse caisse — sert au sidechain du `bed`."""
    return [t for t, _ in hits(pattern, bpm, bars)]


# ---------------------------------------------------------------------------
# Profils de biome (cf. brief §3.5 à §3.9)
# ---------------------------------------------------------------------------

@dataclass
class Biome:
    id: str
    bpm: float
    bars: int
    progression: list[str]      # 4 accords, 2 mesures chacun
    drone_note: str             # fondamentale du drone (quinte nue)
    mode: str                   # mode de la gamme d'arpège
    mode_root: str              # tonique du mode
    rt60: float
    damping: float              # 0 = brillant, 1 = très amorti (glace)
    vowel: str                  # voyelle du chœur
    corruption: float           # dégradation du chœur (Rouille)
    texture: str                # texture climatique du bed
    kick: str
    snare: str
    hat: str
    bass: str
    arp: str
    arp_octave: int = 5
    bass_octave: int = 1
    swing: float = 0.0
    seed: int = 0
    lead_delay: float = 0.0     # délai de l'arpège (0 = croche pointée auto)
    boss_perc: str = "x.......x......."
    notes: str = ""

    @property
    def loop(self) -> float:
        return self.bars * 4 * 60.0 / self.bpm

    @property
    def beat(self) -> float:
        return 60.0 / self.bpm

    def chord_at(self, index: int) -> str:
        """Accord du bloc de 2 mesures n° `index` (cyclique)."""
        return self.progression[index % len(self.progression)]

    @property
    def blocks(self) -> int:
        """Nombre de blocs de 2 mesures dans la boucle."""
        return self.bars // 2


BIOMES: dict[str, Biome] = {
    # §3.5 — référence neutre, ruines à échelle humaine
    "sanctuaire": Biome(
        id="sanctuaire", bpm=100, bars=16,
        progression=["Cm", "Ab", "Eb", "Bb"],
        drone_note="C1", mode="minor", mode_root="C",
        rt60=1.8, damping=0.5, vowel="o_alto", corruption=0.12,
        texture="rust",
        kick="x.......x.....o.",
        snare="....x.......x...",
        hat="..o...o...o...o.",
        bass="x.x.x.x.x.x.x.x.",
        arp="x.x.x.x.x.x.x.x.",
        arp_octave=5, bass_octave=1, seed=101,
        boss_perc="x.......x...x...",
        notes="Terrain neutre : groove sobre, métal à échelle humaine.",
    ),
    # §3.6 — friche saturée d'Aether, Phrygien dominant (tierce majeure sur tonique)
    "aether": Biome(
        id="aether", bpm=112, bars=16,
        progression=["D", "Eb", "Gm", "A7"],
        drone_note="D1", mode="phrygian_dominant", mode_root="D",
        rt60=3.2, damping=0.35, vowel="a_alto", corruption=0.55,
        texture="aether",
        kick="x.....x...x.....",
        snare="....x.......x...",
        hat="..o.o...o.o.o...",
        bass="x.xx..x.x.xx..x.",
        arp="xxx.xxx.xxx.xxx.",
        arp_octave=5, bass_octave=1, seed=202,
        boss_perc="x...x...x...x.x.",
        notes="Cathédrale corrompue : le frottement D->Eb ne se résout jamais.",
    ),
    # §3.7 — suspendu, tempo le plus lent, aigus absorbés par la glace
    "givre": Biome(
        id="givre", bpm=72, bars=16,
        progression=["Am", "D", "G", "Em"],
        drone_note="A0", mode="dorian", mode_root="A",
        rt60=4.0, damping=0.85, vowel="u_alto", corruption=0.08,
        texture="frost",
        kick="x...............",
        snare="........o.......",
        hat="....o.......o...",
        bass="x...............",
        arp="x.......x...x...",
        arp_octave=6, bass_octave=1, seed=303,
        boss_perc="x.......x.......",
        notes="Rien ne se presse : la percussion s'egrene, elle ne groove pas.",
    ),
    # §3.8 — le plus rapide et le plus syncopé
    "fournaise": Biome(
        id="fournaise", bpm=136, bars=16,
        progression=["Gm", "Ab", "Cm", "Ab"],
        drone_note="G0", mode="phrygian", mode_root="G",
        rt60=0.9, damping=0.4, vowel="a_bass", corruption=0.42,
        texture="ember",
        kick="x..x..x.x..x..x.",
        snare="....x.......x..o",
        hat="xoxoxoxoxoxoxoxo",
        bass="xx.xxx.xx.xxx.x.",
        arp="x.xx.x.xx.xx.x.x",
        arp_octave=5, bass_octave=1, seed=404,
        boss_perc="x...x.x.x...x.x.",
        notes="La couche pulse la plus dense du jeu ; la reverb reste courte.",
    ),
    # §3.9 — 4-à-la-noire strict, timbres numériques
    "neon": Biome(
        id="neon", bpm=128, bars=16,
        progression=["E", "D", "A", "E"],
        drone_note="E1", mode="mixolydian", mode_root="E",
        rt60=1.2, damping=0.3, vowel="e_alto", corruption=0.3,
        texture="data",
        kick="x...x...x...x...",
        snare="....x.......x...",
        hat="xoxoxoxoxoxoxoxo",
        bass="x.xxx.xxx.xxx.xx",
        arp="xxxxxxxxxxxxxxxx",
        arp_octave=5, bass_octave=1, seed=505,
        boss_perc="x...x...x...x...",
        notes="L'overclock ne s'arrete jamais : le kick 4-a-la-noire traverse meme le boss.",
    ),
}

# Le Phrygien dominant n'est pas dans la table de modes de base de synth_lib
S._MODES.setdefault("phrygian_dominant", [0, 1, 4, 5, 7, 8, 10])


# ---------------------------------------------------------------------------
# Textures climatiques (stem `bed`)
# ---------------------------------------------------------------------------

def climate_texture(kind: str, duration: float, seed: int) -> np.ndarray:
    """Couleur atmosphérique propre au biome, sans hauteur définie."""
    n = S.n_samples(duration)
    rng = np.random.default_rng(seed)

    if kind == "rust":
        return I.rust_texture(duration, density=0.8, base=220.0, seed=seed) * 0.55

    if kind == "aether":
        # Filtre très résonant qui monte/descend sur 8 mesures : pulsation magique
        src = S.noise(n, "pink", rng)
        sweep = 700.0 * (1.0 + 3.0 * (0.5 + 0.5 * S.lfo(1.0 / (duration / 2), n, phase0=0.1)))
        out = S.bandpass(S.stereo(src, 0.0), sweep, q=5.0) * 0.8
        shimmer = np.zeros((n, 2))
        for i in range(7):
            at = rng.uniform(0, duration * 0.9)
            bell = I.bell_glass(S.midi("A5") + rng.integers(-5, 8), 2.6,
                                ratio=rng.uniform(2.7, 4.3), index=4.0,
                                decay=2.0, pan=rng.uniform(-0.8, 0.8), seed=seed + i)
            st = S.n_samples(at)
            end = min(n, st + len(bell))
            shimmer[st:end] += bell[:end - st] * 0.12
        return out + shimmer

    if kind == "frost":
        # Vent glacé : bruit passe-haut, respiration lente, aigus « vitreux »
        src = S.noise(n, "white", rng)
        wind = S.highpass(S.stereo(src, 0.0), 2200.0, 0.7)
        breath = 0.35 + 0.65 * (0.5 + 0.5 * S.lfo(1.0 / (duration / 2), n, phase0=0.4))
        wind *= breath[:, None]
        ice = np.zeros((n, 2))
        for i in range(10):
            at = rng.uniform(0, duration * 0.95)
            tick = I.perc_metal(rng.uniform(0.6, 1.4), base=rng.uniform(1800, 4200),
                                decay=rng.uniform(0.3, 0.9), partials=3,
                                pan=rng.uniform(-0.9, 0.9), rust=0.05, seed=seed + 20 + i)
            st = S.n_samples(at)
            end = min(n, st + len(tick))
            ice[st:end] += tick[:end - st] * 0.10
        return wind * 0.45 + ice

    if kind == "ember":
        # Crépitement de braises : craquements haute fréquence irréguliers
        out = S.stereo(S.lowpass(S.noise(n, "brown", rng), 700.0, 1.0), 0.0) * 0.3
        crackle = np.zeros(n)
        n_pops = int(duration * 22)
        for _ in range(n_pops):
            pos = int(rng.uniform(0, n - 400))
            length = int(rng.uniform(60, 380))
            crackle[pos:pos + length] += rng.standard_normal(length) * \
                np.exp(-np.linspace(0, 6, length)) * rng.uniform(0.15, 0.7)
        out += S.stereo(S.highpass(crackle, 2600.0, 0.8), 0.0) * 0.35
        return out

    if kind == "data":
        # Grain numérique : impulsions courtes quantifiées sur la grille
        out = np.zeros((n, 2))
        for i in range(int(duration * 9)):
            at = rng.uniform(0, duration)
            blip = S.osc_square(rng.uniform(2000, 7000), S.n_samples(0.03),
                                pulse_width=0.2)
            blip = blip * S.env_perc(len(blip), 0.0002, 0.012)
            st = S.n_samples(at)
            end = min(n, st + len(blip))
            out[st:end] += S.stereo(blip[:end - st], rng.uniform(-0.9, 0.9)) * 0.10
        hiss = S.highpass(S.stereo(S.noise(n, "pink", rng), 0.0), 5000.0, 0.7) * 0.12
        return out + hiss

    raise ValueError(f"Texture climatique inconnue : {kind}")


# ---------------------------------------------------------------------------
# Rendu des stems
# ---------------------------------------------------------------------------

def render_bed(b: Biome) -> np.ndarray:
    """
    Couche toujours audible : drone d'Aether + quinte nue suivant la progression
    + texture climatique. Sans tierce (brief §2), donc modalement neutre.
    """
    dur = b.loop
    mix = S.Mixer(dur + TAIL)

    # Drone continu — rendu sur toute la longueur, sans transitoire d'attaque au
    # premier temps (contrainte de bouclage §5)
    drone = I.drone_aether(b.drone_note, dur + TAIL, cutoff=430.0, resonance=6.0,
                           noise_mix=0.22, seed=b.seed)
    mix.add(drone, 0.0, 0.85)

    # Quinte nue de chaque accord, 2 mesures par bloc
    block_sec = 2 * 4 * b.beat
    for blk in range(b.blocks):
        root = S.chord(b.chord_at(blk), octave=2)[0]
        fifth = root + 7
        pad = I.pad_cs80([root, fifth, root + 12], block_sec + 2.2,
                         cutoff=1150.0, resonance=1.9, attack=1.0, release=1.8,
                         detune=14.0, brightness_sweep=2.1,
                         seed=b.seed + blk * 7)
        mix.add(pad, blk * block_sec, 0.5, wrap=True)

    mix.add(climate_texture(b.texture, dur + TAIL, b.seed + 900), 0.0, 0.5)

    # Shimmer 8–12 kHz : la seconde bande attribuée au `bed` (brief §4.1). Sans
    # elle le bed n'est qu'un bourdon sourd et la musique paraît « voilée » tant
    # que l'intensité reste basse.
    shim_n = S.n_samples(dur + TAIL)
    shim = S.highpass(S.stereo(S.noise(shim_n, "pink",
                                       np.random.default_rng(b.seed + 950)), 0.0),
                      8000.0, 0.7)
    breathe = 0.45 + 0.55 * (0.5 + 0.5 * S.lfo(1.0 / (dur / 2), shim_n, phase0=0.2))
    mix.add(shim * breathe[:, None], 0.0, 0.16)

    out = mix.out
    # Scoop 400 Hz–2 kHz : la place est réservée à pulse et lead (brief §4.1)
    out = S.peaking_eq(out, 900.0, -4.5, 0.9)
    out = S.tilt_air(out, 3.0, 9000.0)
    out = S.reverb(out, b.rt60 * 1.15, 0.42, predelay=0.03, damping=b.damping,
                   width=1.2, seed=b.seed, tail=True)

    # Duck discret du kick (-2 dB / 80 ms, brief §4.3) : imperceptible quand
    # `pulse` est muet, mais donne l'avancée dès qu'il entre
    out = S.sidechain(out, kick_times(b.kick, b.bpm, b.bars), amount=0.20,
                      attack=0.005, release=0.08)

    return S.loopify(out, dur, crossfade=0.4)


def render_pulse(b: Biome) -> np.ndarray:
    """Basse séquencée + percussions. Monte en volume, jamais en tonalité."""
    dur = b.loop
    mix = S.Mixer(dur + TAIL)
    block_sec = 2 * 4 * b.beat
    metal_biome = b.id in ("sanctuaire", "aether", "fournaise", "givre")

    # --- Basse : suit la fondamentale de l'accord courant
    step = b.beat / 4.0
    for at, vel in hits(b.bass, b.bpm, b.bars):
        blk = int(at // block_sec) % b.blocks
        root = S.chord(b.chord_at(blk), octave=b.bass_octave)[0]
        note = I.bass_analog(root, min(step * 2.4, 0.5),
                             cutoff=280.0 + 80.0 * vel, resonance=3.2,
                             decay=step * 1.6, sub=0.5, drive=2.1, accent=vel,
                             seed=b.seed + int(at * 97) % 997)
        mix.add(note, at, 0.55 * vel, wrap=True)

    # --- Percussions
    for at, vel in hits(b.kick, b.bpm, b.bars):
        k = I.kick(0.55, f_start=125.0, f_end=45.0, decay=0.28,
                   click=0.18 if metal_biome else 0.3)
        mix.add(k, at, 0.7 * vel, wrap=True)

    for at, vel in hits(b.snare, b.bpm, b.bars):
        if metal_biome:
            snd = I.perc_metal(0.7, base=430.0, decay=0.24, partials=5,
                               pan=0.12, rust=0.45, seed=b.seed + int(at * 31) % 991)
        else:
            snd = I.snare(0.35, tone=200.0, decay=0.14, seed=b.seed + int(at * 13) % 977)
        mix.add(snd, at, 0.5 * vel, wrap=True)

    for at, vel in hits(b.hat, b.bpm, b.bars):
        h = I.hat(0.11, decay=0.035 + 0.03 * vel, hp=7800.0,
                  seed=b.seed + int(at * 7) % 983)
        mix.add(h, at, 0.42 * vel, wrap=True)

    out = mix.out
    out = S.peaking_eq(out, 700.0, -3.5, 1.0)   # scoop 300 Hz–1,5 kHz (§4.1)
    out = S.highpass(out, 42.0, 0.7)
    out = S.reverb(out, b.rt60 * 0.5, 0.16, predelay=0.012, damping=b.damping + 0.1,
                   width=0.9, seed=b.seed + 1, tail=True)
    out = S.compress(out, threshold_db=-16.0, ratio=2.6, attack=0.008, release=0.14)

    return S.loopify(out, dur)


def render_lead(b: Biome) -> np.ndarray:
    """Arpège identifiant le biome + chœur. Porte la couleur modale (la tierce)."""
    dur = b.loop
    mix = S.Mixer(dur + TAIL)
    block_sec = 2 * 4 * b.beat
    rng = np.random.default_rng(b.seed + 77)

    # --- Arpège : monte sur les notes de l'accord, redescend au 2e cycle
    step = b.beat / 4.0
    for at, vel in hits(b.arp, b.bpm, b.bars):
        blk = int(at // block_sec) % b.blocks
        cycle = 0 if at < dur / 2 else 1
        tones = S.chord(b.chord_at(blk), octave=b.arp_octave - 2)
        idx = int(round(at / step))
        # Variation obligatoire entre les deux cycles (§5) : sens de parcours
        pos = idx % len(tones) if cycle == 0 else (len(tones) - 1 - idx % len(tones))
        note = tones[pos] + (12 if (idx // len(tones)) % 3 == 2 else 0)
        p = I.pluck(note, min(step * 3.5, 0.75), cutoff=2400.0, resonance=3.0,
                    decay=step * 2.2, bright=4.5,
                    pan=float(np.clip((pos / max(len(tones) - 1, 1) - 0.5) * 1.1, -1, 1)),
                    seed=b.seed + idx)
        mix.add(p, at, 0.42 * vel, wrap=True)

    # --- Chœur : soutien harmonique, jamais une mélodie
    for blk in range(b.blocks):
        voicing = S.chord(b.chord_at(blk), octave=3)
        if blk % 2 == 1:
            voicing = [voicing[0] + 12] + voicing[1:]  # variation de voicing
        ch = I.choir(voicing, block_sec + 2.4, vowel=b.vowel, singers=4,
                     attack=block_sec * 0.28, release=2.0,
                     vibrato=0.007, breath=0.09, corruption=b.corruption,
                     seed=b.seed + 300 + blk * 11)
        mix.add(ch, blk * block_sec, 0.5, wrap=True)

    out = mix.out
    out = S.highpass(out, 190.0, 0.7)   # aucun sub sur ce stem (§4.1)

    # Delai à la croche pointée : remplit le mix sans ajouter de notes
    dotted = b.lead_delay or (b.beat * 0.75)
    out = S.delay(out, dotted, feedback=0.34, mix=0.22, ping_pong=True, damp=5000.0)
    out = S.reverb(out, b.rt60, 0.34, predelay=0.025, damping=b.damping,
                   width=1.35, seed=b.seed + 2, tail=True)

    return S.loopify(out, dur)


def render_boss(b: Biome) -> np.ndarray:
    """
    Couche de tension exclusive au combat de boss : cluster de cordes graves,
    chœur grave dissonant, percussion lourde. S'empile sur les trois autres.
    """
    dur = b.loop
    mix = S.Mixer(dur + TAIL)
    block_sec = 2 * 4 * b.beat

    for blk in range(b.blocks):
        root = S.chord(b.chord_at(blk), octave=2)[0]
        # Cluster : fondamentale + seconde mineure + quinte — le frottement de
        # la seconde mineure est la couleur « Rouille à son paroxysme » (§2)
        cluster = [root, root + 1, root + 7, root + 13]
        st = I.strings_synth(cluster, block_sec + 2.0, attack=block_sec * 0.22,
                             release=1.8, cutoff=1900.0, seed=b.seed + 600 + blk)
        mix.add(st, blk * block_sec, 0.55, wrap=True)

        grave = I.choir([root - 12, root - 5], block_sec + 2.6, vowel="a_bass",
                        singers=3, attack=block_sec * 0.3, release=2.2,
                        vibrato=0.01, breath=0.06,
                        corruption=min(1.0, b.corruption + 0.35),
                        seed=b.seed + 700 + blk * 13)
        mix.add(grave, blk * block_sec, 0.45, wrap=True)

    # Percussion lourde — sèche, sans reverb (§4.5)
    for at, vel in hits(b.boss_perc, b.bpm, b.bars):
        mix.add(I.taiko(1.1, f_start=95.0, f_end=56.0, decay=0.5,
                        seed=b.seed + int(at * 17) % 971), at, 0.62 * vel, wrap=True)
        mix.add(I.perc_metal(0.9, base=180.0, decay=0.4, partials=7, rust=0.7,
                             seed=b.seed + int(at * 23) % 967), at, 0.3 * vel, wrap=True)

    out = mix.out
    # Traîne longue réservée au cluster/chœur : on la pose avant de sommer les
    # percussions serait plus rigoureux, mais un pré-délai suffit à garder les
    # transitoires nets tout en enveloppant les tenues.
    out = S.reverb(out, b.rt60 * 1.4, 0.30, predelay=0.05, damping=b.damping,
                   width=1.2, seed=b.seed + 3, tail=True)
    out = S.saturate(out, 1.5, mix=0.5)
    out = S.compress(out, threshold_db=-18.0, ratio=3.2, attack=0.012, release=0.2)

    return S.loopify(out, dur)


# ---------------------------------------------------------------------------
# Pistes non adaptatives
# ---------------------------------------------------------------------------

def render_menu() -> np.ndarray:
    """
    §3.2 — thème principal, La Aeolien, 70 BPM, 16 mesures (54,9 s).
    Aucune percussion : la respiration entre deux missions.
    """
    bpm, bars = 70.0, 16
    beat = 60.0 / bpm
    block = 2 * 4 * beat
    dur = bars * 4 * beat
    prog = ["Am", "F", "C", "G"]
    mix = S.Mixer(dur + TAIL)
    seed = 1001

    for blk in range(bars // 2):
        ch = prog[blk % 4]
        notes = S.chord(ch, octave=3)

        # Nappe dès la mesure 1
        mix.add(I.pad_cs80(notes + [notes[0] - 12], block + 3.0, cutoff=1250.0,
                           resonance=1.8, attack=2.0, release=2.6,
                           brightness_sweep=2.3, seed=seed + blk * 5),
                blk * block, 0.52, wrap=True)

        # Arpège épars à partir de la mesure 3 — noires uniquement, jamais occupé
        if blk >= 1:
            for i, n_ in enumerate(notes + [notes[0] + 12]):
                at = blk * block + i * beat * 2.0
                if at < dur:
                    mix.add(I.pluck(n_ + 12, 1.5, cutoff=2100.0, decay=1.0,
                                    bright=3.0, pan=(i % 3 - 1) * 0.5,
                                    seed=seed + 60 + blk * 4 + i),
                            at, 0.26, wrap=True)

        # Chœur lointain à partir de la mesure 5, gonfle en fin de phrase
        if blk >= 2:
            swell = 0.30 + 0.16 * (blk / max(bars // 2 - 1, 1))
            mix.add(I.choir([n_ + 12 for n_ in notes], block + 3.0, vowel="u_alto",
                            singers=4, attack=block * 0.45, release=2.6,
                            vibrato=0.006, breath=0.12, corruption=0.05,
                            seed=seed + 200 + blk * 9),
                    blk * block, swell, wrap=True)

    out = S.reverb(mix.out, 2.2, 0.40, predelay=0.03, damping=0.5, width=1.3,
                   seed=seed, tail=True)
    return S.loopify(out, dur, crossfade=0.3)


def render_hub() -> np.ndarray:
    """§3.4 — l'enclave : Fa majeur / Ré mineur, 84 BPM, 16 mesures (45,7 s)."""
    bpm, bars = 84.0, 16
    beat = 60.0 / bpm
    block = 2 * 4 * beat
    dur = bars * 4 * beat
    prog = ["Dm", "Bb", "F", "C"]
    mix = S.Mixer(dur + TAIL)
    seed = 2002

    for blk in range(bars // 2):
        notes = S.chord(prog[blk % 4], octave=3)

        # Accords « joués » (attaque plus franche qu'un pad tenu) : des mains qui réparent
        for i, n_ in enumerate(notes):
            mix.add(I.pad_cs80([n_], block + 2.0, cutoff=1350.0, resonance=2.0,
                               attack=0.35 + i * 0.06, release=1.9,
                               brightness_sweep=2.0, seed=seed + blk * 7 + i),
                    blk * block + i * 0.055, 0.30, wrap=True)

        mix.add(I.bass_analog(notes[0] - 24, beat * 2.6, cutoff=300.0,
                              decay=beat * 1.8, sub=0.6, seed=seed + 40 + blk),
                blk * block, 0.42, wrap=True)

        # Chœur chaud et proche (moins de reverb que le menu — lieu physique)
        mix.add(I.choir(notes, block + 2.2, vowel="a_alto", singers=3,
                        attack=block * 0.3, release=1.8, vibrato=0.008,
                        breath=0.10, corruption=0.0, seed=seed + 300 + blk * 11),
                blk * block, 0.34, wrap=True)

        # Tintement d'outil lointain — jamais un beat
        if blk % 2 == 0:
            mix.add(I.perc_metal(1.1, base=760.0, decay=0.45, partials=4,
                                 pan=0.55, rust=0.3, seed=seed + 500 + blk),
                    blk * block + beat * 5.5, 0.16, wrap=True)

    out = S.reverb(mix.out, 2.5, 0.34, predelay=0.028, damping=0.45, width=1.2,
                   seed=seed, tail=True)
    return S.loopify(out, dur, crossfade=0.3)


def render_intro() -> np.ndarray:
    """
    §3.3 — cinématique ~94 s, non bouclée. Quatre sections : lignes profondes,
    montée de la Convergence, climax (l'humain absorbé), installation de la
    Rouille, point d'orgue non résolu sur La.
    """
    dur = 94.0
    mix = S.Mixer(dur + 4.0)
    seed = 3003

    # 0:00–0:20 — le monde d'avant : drone seul, aucune tonalité claire
    mix.add(I.drone_aether("D1", 30.0, cutoff=340.0, resonance=6.5,
                           noise_mix=0.35, seed=seed), 0.0, 0.75)
    mix.add(climate_texture("rust", 26.0, seed + 10), 2.0, 0.35)

    # 0:20–0:45 — la Convergence approche : cordes + chœur, dissonance croissante
    mix.add(I.strings_synth(S.chord("Dm", 3), 26.0, attack=7.0, release=6.0,
                            cutoff=2100.0, seed=seed + 20), 20.0, 0.34, wrap=False)
    mix.add(I.choir(S.chord("Dm", 4), 24.0, vowel="a_alto", singers=5,
                    attack=9.0, release=6.0, breath=0.12, corruption=0.05,
                    seed=seed + 30), 22.0, 0.32)
    # Frottement qui monte : une seconde mineure qui s'installe sous l'accord
    mix.add(I.strings_synth([S.midi("Eb3"), S.midi("Eb2")], 18.0, attack=9.0,
                            release=5.0, seed=seed + 40), 30.0, 0.22)

    # 0:45–1:00 — climax : impact unique, le chœur se brise
    mix.add(I.impact(5.0, seed=seed + 50), 45.0, 0.95)
    mix.add(I.rev_cymbal(3.0, seed=seed + 55), 42.0, 0.4)
    broken = I.choir([S.midi("A4"), S.midi("Bb4"), S.midi("D4")], 9.0,
                     vowel="a_sop", singers=6, attack=0.6, release=2.0,
                     breath=0.2, corruption=0.15, seed=seed + 60)
    # Coupure brutale en plein souffle, puis reprise corrompue : « l'humain absorbé »
    cut = S.n_samples(3.4)
    fade = S.n_samples(0.05)
    broken[cut:cut + fade] *= np.linspace(1, 0, fade)[:, None]
    broken[cut + fade:] = 0.0
    mix.add(broken, 45.2, 0.5)
    mix.add(I.choir([S.midi("A3"), S.midi("Bb3")], 12.0, vowel="a_alto", singers=4,
                    attack=1.2, release=4.0, breath=0.05, corruption=0.9,
                    seed=seed + 70), 49.0, 0.34)

    # 1:00–1:30 — la Rouille s'installe : le drone se stabilise en Ré mineur
    mix.add(I.drone_aether("D1", 34.0, cutoff=420.0, resonance=7.0,
                           noise_mix=0.3, seed=seed + 80), 58.0, 0.7)
    mix.add(I.pad_cs80(S.chord("Dm", 3), 30.0, cutoff=1000.0, attack=6.0,
                       release=8.0, brightness_sweep=1.8, seed=seed + 90),
            60.0, 0.38)
    mix.add(climate_texture("rust", 30.0, seed + 100), 60.0, 0.4)

    # 1:30–1:34 — point d'orgue sur La, non résolu (amène le La mineur du menu)
    mix.add(I.pad_cs80([S.midi("A2"), S.midi("A3"), S.midi("E4")], 14.0,
                       cutoff=1100.0, attack=3.0, release=8.0, seed=seed + 110),
            84.0, 0.42)
    mix.add(I.choir([S.midi("A4"), S.midi("E5")], 12.0, vowel="u_alto", singers=4,
                    attack=3.5, release=6.0, breath=0.1, corruption=0.25,
                    seed=seed + 120), 85.0, 0.3)

    out = S.reverb(mix.out, 4.2, 0.42, predelay=0.04, damping=0.45, width=1.4,
                   seed=seed, tail=False)
    return S.pad_to(out, S.n_samples(dur))


def render_stinger_death() -> np.ndarray:
    """§3.10 — dissolution : glissando descendant, aucune résolution."""
    dur = 4.6
    mix = S.Mixer(dur + 2.0)
    n = S.n_samples(3.6)

    # Chœur en glissando chromatique descendant
    for i, start in enumerate([S.midi("A4"), S.midi("E4"), S.midi("C4")]):
        track = np.geomspace(S.hz(start), S.hz(start - 14), n)
        src = S.glottal_source(track, n, tension=0.6)
        voiced = S.formant_filter(src, "a_alto", shift=1.0)
        voiced *= S.adsr(n, 0.05, 1.0, 0.5, 2.2)
        mix.add(S.stereo(voiced, (i - 1) * 0.6), 0.0, 0.5)

    sub = S.osc_sine(np.geomspace(90.0, 26.0, n), n) * S.adsr(n, 0.02, 1.2, 0.4, 2.0)
    mix.add(S.stereo(S.saturate(sub, 1.8), 0.0), 0.0, 0.55)
    mix.add(I.noise_sweep(3.2, up=False, f_lo=180.0, f_hi=6000.0, seed=91), 0.4, 0.3)

    out = S.reverb(mix.out, 3.4, 0.45, damping=0.55, seed=91, tail=False)
    return S.pad_to(out, S.n_samples(dur))


def render_stinger_victory() -> np.ndarray:
    """§3.10 — Do majeur, hors diégèse : le seul moment franchement lumineux."""
    dur = 6.5
    mix = S.Mixer(dur + 2.0)
    notes = [S.midi("C4"), S.midi("E4"), S.midi("G4"), S.midi("C5"), S.midi("E5")]

    for i, n_ in enumerate(notes):
        mix.add(I.pluck(n_, 2.4, cutoff=2800.0, decay=1.4, bright=4.0,
                        pan=(i / (len(notes) - 1) - 0.5) * 1.2, seed=70 + i),
                i * 0.13, 0.42)

    mix.add(I.choir([S.midi("C4"), S.midi("E4"), S.midi("G4"), S.midi("C5")], 5.5,
                    vowel="o_alto", singers=5, attack=1.1, release=2.6,
                    breath=0.1, corruption=0.0, seed=72), 0.35, 0.48)
    mix.add(I.bell_glass(S.midi("C6"), 4.0, ratio=3.51, index=4.5, decay=2.6, seed=73),
            0.6, 0.4)
    mix.add(I.pad_cs80([S.midi("C2"), S.midi("G2"), S.midi("C3")], 6.0,
                       cutoff=1200.0, attack=0.9, release=2.6, seed=74), 0.0, 0.35)

    out = S.reverb(mix.out, 3.0, 0.38, damping=0.4, seed=75, tail=False)
    return S.pad_to(out, S.n_samples(dur))


def render_stinger_levelup() -> np.ndarray:
    """§3.10 — 4 notes montantes, bande 2–6 kHz pour percer un combat dense."""
    dur = 2.0
    mix = S.Mixer(dur + 1.5)
    for i, n_ in enumerate([S.midi("C5"), S.midi("E5"), S.midi("G5"), S.midi("C6")]):
        mix.add(I.bell_glass(n_, 1.4, ratio=3.0, index=3.4, decay=0.85,
                             pan=(i - 1.5) * 0.35, seed=80 + i), i * 0.085, 0.55)
    mix.add(I.choir([S.midi("C5"), S.midi("G5")], 1.6, vowel="a_sop", singers=3,
                    attack=0.12, release=0.9, breath=0.08, seed=85), 0.1, 0.28)

    out = S.reverb(mix.out, 1.6, 0.3, damping=0.35, seed=86, tail=False)
    out = S.highpass(out, 400.0, 0.7)   # laisse le grave au combat
    return S.pad_to(out, S.n_samples(dur))


# ---------------------------------------------------------------------------
# Catalogue & pilotage
# ---------------------------------------------------------------------------

STEM_RENDERERS = {
    "bed": render_bed,
    "pulse": render_pulse,
    "lead": render_lead,
    "boss": render_boss,
}

SINGLE_TRACKS = {
    "music_menu": (render_menu, -18.0),
    "music_hub": (render_hub, -18.0),
    "music_intro": (render_intro, -17.0),
    "music_stinger_death": (render_stinger_death, -16.0),
    "music_stinger_victory": (render_stinger_victory, -16.0),
    "music_stinger_levelup": (render_stinger_levelup, -15.0),
}


def targets() -> list[str]:
    return ["menu", "hub", "intro", "stingers"] + list(BIOMES)


def produce(names: list[str], out_dir: str, quality: int, keep_wav: bool) -> None:
    os.makedirs(out_dir, exist_ok=True)
    todo = set(names)
    made: list[tuple[str, float, int]] = []

    def emit(track_id: str, audio: np.ndarray, lufs: float, tp: float = -1.5) -> None:
        t0 = time.time()
        path = S.render(os.path.join(out_dir, track_id), audio,
                        loudnorm_lufs=lufs, quality=quality, keep_wav=keep_wav,
                        true_peak=tp)
        made.append((track_id, time.time() - t0, os.path.getsize(path)))
        print(f"  -> {track_id}.ogg  ({os.path.getsize(path) // 1024} Ko, "
              f"{len(audio) / S.SR:.1f}s)")

    for track_id, (fn, lufs) in SINGLE_TRACKS.items():
        short = track_id.replace("music_", "")
        if short in todo or (short.startswith("stinger") and "stingers" in todo):
            print(f"[{track_id}]")
            emit(track_id, fn(), lufs)

    for biome_id, b in BIOMES.items():
        if biome_id not in todo:
            continue
        print(f"[run_{biome_id}] {b.bpm:g} BPM, {b.bars} mes., "
              f"boucle {b.loop:.1f}s — {' '.join(b.progression)}")
        for stem, fn in STEM_RENDERERS.items():
            # Tous les stems au MÊME loudness, avec 6 dB de garde au pic : les
            # rapports de niveau entre couches sont ensuite décidés en jeu par
            # `MusicDirector` (brief §4.2), donc réglables sans régénérer l'audio.
            emit(f"music_run_{biome_id}_{stem}", fn(b), STEM_LUFS, STEM_TRUE_PEAK)

    total = sum(m[1] for m in made)
    print(f"\n{len(made)} fichier(s) en {total:.1f}s -> {out_dir}")


def main() -> int:
    ap = argparse.ArgumentParser(description=__doc__,
                                 formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("--only", nargs="*", metavar="ID",
                    help=f"sous-ensemble parmi : {', '.join(targets())}")
    ap.add_argument("--preview", action="store_true",
                    help="écrit dans build/music_preview/ au lieu des assets")
    ap.add_argument("--quality", type=int, default=6, help="qualité Vorbis (0-10)")
    ap.add_argument("--keep-wav", action="store_true", help="conserve les WAV intermédiaires")
    ap.add_argument("--list", action="store_true", help="liste les cibles et sort")
    args = ap.parse_args()

    if args.list:
        for t in targets():
            b = BIOMES.get(t)
            print(f"  {t:12s} " + (f"{b.bpm:g} BPM  {b.loop:5.1f}s  "
                                   f"{' '.join(b.progression):18s} {b.notes}" if b else ""))
        return 0

    names = args.only or targets()
    unknown = [n for n in names if n not in targets()]
    if unknown:
        print(f"Cible(s) inconnue(s) : {', '.join(unknown)}", file=sys.stderr)
        return 2

    out_dir = PREVIEW_DIR if args.preview else MUSIC_DIR
    t0 = time.time()
    produce(names, out_dir, args.quality, args.keep_wav)
    print(f"Total : {time.time() - t0:.1f}s")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
