"""
synth_instruments — banque de timbres de Chimera Protocol.

Chaque fonction rend un extrait stéréo `(n, 2)` prêt à être placé dans un
`synth_lib.Mixer`. Les primitives DSP viennent de `synth_lib` ; les partitions
vivent dans `generate_music_v3.py`.

Le cahier des charges sonore (cf. `docs/ART_BRIEF_AUDIO.md`) : synthèse
analogique chaude type CS-80, chœurs, reverbs immenses, percussions métalliques.
Aucun timbre chiptune. Chaque instrument porte une intention narrative — la
Rouille Vivante ronge le son autant qu'elle ronge le monde, d'où les paramètres
`corruption` / `rust` présents sur plusieurs patches.

Convention : `notes` accepte des numéros MIDI (int) ou des noms ("C3"), un seul
ou une liste. `duration` est en secondes et *inclut* la retombée de l'enveloppe.
"""

from __future__ import annotations

import math

import numpy as np

import synth_lib as S
from synth_lib import SR


def _freqs(notes) -> list[float]:
    """
    Normalise l'entrée en liste de fréquences.
    Accepte un nom ("C3"), un numéro MIDI, un scalaire numpy (les tirages
    aléatoires en produisent) ou une séquence de ces types.
    """
    if isinstance(notes, str) or np.isscalar(notes):
        notes = [notes]
    return [S.hz(n if isinstance(n, str) else float(n)) for n in notes]


def _rng(seed: int) -> np.random.Generator:
    return np.random.default_rng(seed)


# ---------------------------------------------------------------------------
# Nappes
# ---------------------------------------------------------------------------

def pad_cs80(notes, duration: float, cutoff: float = 1400.0, resonance: float = 2.2,
             attack: float = 1.4, release: float = 2.5, detune: float = 16.0,
             voices: int = 7, brightness_sweep: float = 2.6, drive: float = 1.3,
             width: float = 1.3, seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Nappe polyphonique « CS-80 » — le socle harmonique de tout le jeu.

    Un empilement de dents de scie désaccordées passé dans un passe-bas résonant
    qui s'ouvre lentement pendant la note (`brightness_sweep`). C'est ce
    mouvement de timbre, et non les notes, qui crée la sensation de respiration.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    out = np.zeros((n, 2))

    for i, f in enumerate(_freqs(notes)):
        # Léger vibrato désynchronisé par note : évite l'effet « orgue figé »
        vib = 1.0 + 0.0016 * S.lfo(0.28 + 0.07 * i, n, sr, phase0=rng.random())
        raw = S.osc_supersaw(f * vib, n, sr, voices=voices,
                             detune_cents=detune, spread=0.9, rng=_rng(seed + i * 17))
        # Une impulsion douce une octave en dessous épaissit le bas sans boue
        raw += S.stereo(S.osc_square(f * 0.5 * vib, n, sr, pulse_width=0.35 + 0.12 *
                                     S.lfo(0.13, n, sr, phase0=rng.random())), 0.0) * 0.18

        sweep = np.geomspace(cutoff * 0.45, cutoff * brightness_sweep, n)
        sweep *= 1.0 + 0.25 * S.lfo(0.09, n, sr, phase0=rng.random())
        voice = S.lowpass(raw, np.clip(sweep, 60.0, sr * 0.45), resonance, sr)

        env = S.adsr(n, attack, duration * 0.3, 0.72, release, sr)
        out += voice * env[:, None]

    out = S.saturate(out, drive)
    out = S.chorus(out, rate=0.22, depth_ms=7.5, mix=0.45, sr=sr, voices=3)
    out = S.stereo_width(out, width, 120.0, sr)
    return out / max(len(_freqs(notes)), 1)


def strings_synth(notes, duration: float, attack: float = 0.9, release: float = 1.8,
                  cutoff: float = 2600.0, seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Cordes synthétiques (type Solina/mellotron) : plus dures et plus « bandes
    magnétiques » que la nappe CS-80. Réservées aux moments de tension.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    out = np.zeros((n, 2))

    for i, f in enumerate(_freqs(notes)):
        acc = np.zeros((n, 2))
        for v in range(4):
            # Le pleurage de bande (wow & flutter) est ce qui « date » le timbre
            wow = 1.0 + 0.0035 * S.lfo(0.6 + 0.9 * v, n, sr, phase0=rng.random()) \
                      + 0.0012 * S.lfo(6.1 + v, n, sr, phase0=rng.random())
            raw = S.osc_saw(f * wow * S.cents(rng.uniform(-9, 9)), n, sr, phase0=rng.random())
            acc += S.stereo(raw, (v - 1.5) / 1.5 * 0.75)
        acc = S.lowpass(acc, cutoff, 1.1, sr)
        acc = S.highpass(acc, 180.0, 0.7, sr)
        out += acc * S.adsr(n, attack, duration * 0.4, 0.62, release, sr)[:, None]

    return out / max(len(_freqs(notes)) * 3.0, 1.0)


def drone_aether(note, duration: float, cutoff: float = 520.0, resonance: float = 7.0,
                 noise_mix: float = 0.28, movement: float = 0.06,
                 seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Drone d'Aether — la présence sourde et continue de l'énergie sous le
    Sanctuaire. Deux oscillateurs presque unissons (battements très lents) plus
    du bruit passé dans un filtre très résonant qui dérive.

    C'est le seul élément présent dans TOUS les stems `bed` : il garantit que la
    musique ne « disparaît » jamais quand les autres couches sont muettes.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    f = _freqs(note)[0]

    a = S.osc_saw(f, n, sr, phase0=rng.random())
    b = S.osc_saw(f * S.cents(7.0), n, sr, phase0=rng.random())
    c = S.osc_triangle(f * 0.5, n, sr, phase0=rng.random())
    core = S.stereo(a, -0.35) + S.stereo(b, 0.35) + S.stereo(c, 0.0) * 0.6

    breath = S.stereo(S.noise(n, "pink", rng), 0.0)
    breath = S.bandpass(breath, f * 4.0, q=1.6, sr=sr)
    core = core * (1.0 - noise_mix) + breath * noise_mix * 2.0

    drift = cutoff * (1.0 + movement * 4.0 * S.lfo(0.035, n, sr, phase0=rng.random()))
    out = S.lowpass(core, np.clip(drift, 40.0, sr * 0.4), resonance, sr)
    out *= S.adsr(n, 2.5, 1.0, 0.9, 3.0, sr)[:, None]

    return S.stereo_width(out, 1.25, 90.0, sr) * 0.5


# ---------------------------------------------------------------------------
# Chœur — la signature du jeu
# ---------------------------------------------------------------------------

def choir(notes, duration: float, vowel: str = "a_alto", singers: int = 4,
          attack: float = 1.1, release: float = 2.2, vibrato: float = 0.008,
          breath: float = 0.10, corruption: float = 0.0, octave_shift: float = 1.0,
          width: float = 1.35, seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Chœur de synthèse par formants — le timbre demandé pour l'identité du jeu.

    Une nappe filtrée ne sonne jamais « voix ». Il faut trois choses :
      1. une source glottale (spectre en -12 dB/oct, pas une dent de scie) ;
      2. une banque de formants fixes qui définit la voyelle (`synth_lib.VOWELS`) ;
      3. des imperfections humaines — chaque chanteur a son propre désaccord, son
         vibrato déphasé, son entrée décalée de quelques dizaines de ms et un
         souffle qui lui est propre. C'est ce désordre qui fait le chœur.

    `corruption` ∈ [0,1] : la Rouille Vivante avale la voix — modulation en
    anneau métallique et formants tirés vers l'aigu. À 0, chœur humain ; à 1,
    quelque chose qui *imite* une voix humaine sans en être une.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    out = np.zeros((n, 2))
    freqs = _freqs(notes)

    for i, base_f in enumerate(freqs):
        f0 = base_f * octave_shift
        for s in range(singers):
            # Humanisation : désaccord, vibrato déphasé, entrée décalée
            detune = S.cents(rng.uniform(-11.0, 11.0))
            vib_rate = rng.uniform(4.4, 6.1)
            vib = 1.0 + vibrato * S.lfo(vib_rate, n, sr, phase0=rng.random())
            # Montée progressive du vibrato : un chanteur n'en met pas dès l'attaque
            vib_ramp = np.clip(np.linspace(-0.35, 1.0, n), 0.0, 1.0)
            vib = 1.0 + (vib - 1.0) * vib_ramp

            src = S.glottal_source(f0 * detune * vib, n, sr,
                                   phase0=rng.random(),
                                   tension=rng.uniform(0.55, 0.70))

            shift = rng.uniform(0.97, 1.05) * (1.0 + 0.35 * corruption)
            voiced = S.formant_filter(src, vowel, sr, shift=shift)

            if breath > 0:
                air = S.bandpass(S.noise(n, "pink", _rng(seed + 100 + s)),
                                 2600.0, q=0.9, sr=sr)
                voiced += air * breath * 0.6

            if corruption > 0:
                # Modulation en anneau à un intervalle inharmonique : la voix
                # garde son enveloppe mais son spectre devient métallique
                ring = S.osc_sine(f0 * 2.41, n, sr, phase0=rng.random())
                voiced = voiced * (1.0 - corruption * 0.55) + \
                    voiced * ring * corruption * 0.55
                voiced = S.saturate(voiced, 1.0 + corruption * 2.5)

            delay_s = rng.uniform(0.0, 0.09)
            env = S.adsr(n, attack + delay_s, duration * 0.35,
                         rng.uniform(0.62, 0.78), release, sr)

            pan = ((s + 0.5) / singers - 0.5) * 2.0 * 0.8 + rng.uniform(-0.1, 0.1)
            out += S.stereo(voiced * env, float(np.clip(pan, -1, 1)))

    peak = np.max(np.abs(out))
    if peak > 1e-9:
        out = out / peak * 0.85

    out = S.chorus(out, rate=0.19, depth_ms=5.0, mix=0.3, sr=sr, voices=2)
    out = S.tilt_air(out, 2.5, 7000.0, sr)
    return S.stereo_width(out, width, 150.0, sr)


# ---------------------------------------------------------------------------
# Basses
# ---------------------------------------------------------------------------

def bass_analog(note, duration: float, cutoff: float = 320.0, resonance: float = 3.4,
                decay: float = 0.42, sub: float = 0.55, drive: float = 2.2,
                accent: float = 1.0, seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Basse analogique mono (saw + carré, filtre résonant enveloppé, saturation).
    Le balayage de filtre par note est ce qui fait le « twang » d'une basse de
    synthé ; sans lui, on n'entend qu'un bourdon.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    f = _freqs(note)[0]

    raw = S.osc_saw(f, n, sr, phase0=rng.random()) * 0.75
    raw += S.osc_square(f, n, sr, phase0=rng.random(), pulse_width=0.42) * 0.25

    env = S.adsr(n, 0.004, decay, 0.28, min(0.25, duration * 0.4), sr)
    fenv = S.env_perc(n, 0.003, decay * 0.8, sr, curve=2.2)
    sweep = cutoff * (1.0 + 5.5 * fenv * accent)
    voice = S.lowpass(raw, np.clip(sweep, 40.0, sr * 0.42), resonance, sr) * env

    if sub > 0:
        voice += S.osc_sine(f * 0.5, n, sr) * S.adsr(n, 0.006, decay * 1.3, 0.4,
                                                     min(0.3, duration * 0.5), sr) * sub

    voice = S.saturate(voice, drive)
    return S.stereo(voice, 0.0) * 0.8


def sub_bass(note, duration: float, attack: float = 0.02, release: float = 0.5,
             sr: int = SR) -> np.ndarray:
    """Sinus pur sous-grave — pose le fondement sans encombrer le spectre."""
    n = S.n_samples(duration, sr)
    f = _freqs(note)[0]
    voice = S.osc_sine(f, n, sr) * S.adsr(n, attack, duration * 0.3, 0.85, release, sr)
    return S.stereo(S.saturate(voice, 1.15), 0.0)


# ---------------------------------------------------------------------------
# Motifs mélodiques
# ---------------------------------------------------------------------------

def pluck(note, duration: float, cutoff: float = 2600.0, resonance: float = 3.0,
          decay: float = 0.35, bright: float = 4.0, pan: float = 0.0,
          seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Note d'arpège courte et brillante. Le moteur rythmique des stems `lead` :
    répétée en croches, elle donne l'élan sans occuper la place de la basse.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    f = _freqs(note)[0]

    raw = S.osc_saw(f, n, sr, phase0=rng.random()) * 0.6
    raw += S.osc_square(f * S.cents(6), n, sr, phase0=rng.random(), pulse_width=0.3) * 0.4

    fenv = S.env_perc(n, 0.002, decay * 0.6, sr, curve=2.6)
    voice = S.lowpass(raw, np.clip(cutoff * (1.0 + bright * fenv), 80.0, sr * 0.45),
                      resonance, sr)
    voice *= S.env_perc(n, 0.003, decay, sr, curve=2.0)
    return S.stereo(voice, pan) * 0.7


def lead_saw(note, duration: float, cutoff: float = 2200.0, resonance: float = 3.6,
             attack: float = 0.08, release: float = 0.4, vibrato: float = 0.006,
             vibrato_delay: float = 0.35, glide_from=None, drive: float = 1.8,
             seed: int = 0, sr: int = SR) -> np.ndarray:
    """
    Lead expressif (le thème principal). Vibrato retardé et portamento optionnel
    (`glide_from`) : ce sont les deux gestes qui rendent une ligne « jouée »
    plutôt que séquencée.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    f = _freqs(note)[0]

    if glide_from is not None:
        f_from = _freqs(glide_from)[0]
        glide_n = min(S.n_samples(0.09, sr), n)
        track = np.full(n, f)
        track[:glide_n] = np.geomspace(f_from, f, glide_n)
    else:
        track = np.full(n, f)

    ramp = np.clip((np.arange(n) / sr - vibrato_delay) / 0.5, 0.0, 1.0)
    track = track * (1.0 + vibrato * ramp * S.lfo(5.2, n, sr, phase0=rng.random()))

    raw = S.osc_saw(track, n, sr, phase0=rng.random()) * 0.65
    raw += S.osc_saw(track * S.cents(9), n, sr, phase0=rng.random()) * 0.35

    env = S.adsr(n, attack, duration * 0.3, 0.75, release, sr)
    voice = S.lowpass(raw, np.clip(cutoff * (0.6 + 0.9 * env), 80.0, sr * 0.45),
                      resonance, sr) * env
    return S.stereo(S.saturate(voice, drive), 0.0) * 0.75


def bell_glass(note, duration: float, ratio: float = 3.51, index: float = 5.0,
               decay: float = 1.6, pan: float = 0.0, seed: int = 0,
               sr: int = SR) -> np.ndarray:
    """
    Cloche cristalline par modulation de fréquence — le son de l'Aether pur
    (Noyaux, level-up, cristaux). Le ratio inharmonique 3.51 donne le timbre
    « verre » ; un ratio entier sonnerait comme un orgue.
    """
    n = S.n_samples(duration, sr)
    f = _freqs(note)[0]
    rng = _rng(seed)

    mod_env = S.env_perc(n, 0.001, decay * 0.35, sr, curve=3.0)
    modulator = S.osc_sine(f * ratio, n, sr, phase0=rng.random()) * index * mod_env
    phase = S.phase_of(f, n, sr, rng.random())
    voice = np.sin(S.TWO_PI * phase + modulator)
    voice *= S.env_perc(n, 0.002, decay, sr, curve=2.2)

    return S.stereo(voice, pan) * 0.55


# ---------------------------------------------------------------------------
# Percussions
# ---------------------------------------------------------------------------

def kick(duration: float = 0.6, f_start: float = 130.0, f_end: float = 44.0,
         decay: float = 0.32, click: float = 0.25, drive: float = 2.4,
         sr: int = SR) -> np.ndarray:
    """Grosse caisse électronique : descente de hauteur + clic d'attaque."""
    n = S.n_samples(duration, sr)
    pitch_env = np.geomspace(f_start, f_end, n)
    body = S.osc_sine(pitch_env, n, sr) * S.env_perc(n, 0.001, decay, sr, curve=2.4)

    tick = S.noise(S.n_samples(0.02, sr), "white", _rng(3))
    tick = S.highpass(tick, 1800.0, 0.7, sr) * S.env_perc(len(tick), 0.0005, 0.008, sr)
    body[:len(tick)] += tick * click

    return S.stereo(S.saturate(body, drive), 0.0) * 0.9


def snare(duration: float = 0.4, tone: float = 190.0, decay: float = 0.16,
          noise_mix: float = 0.75, seed: int = 5, sr: int = SR) -> np.ndarray:
    """Caisse claire électronique — corps sinus + bruit filtré."""
    n = S.n_samples(duration, sr)
    body = S.osc_sine(np.geomspace(tone * 1.6, tone, n), n, sr) * \
        S.env_perc(n, 0.001, decay * 0.6, sr, curve=2.5)
    hiss = S.bandpass(S.noise(n, "white", _rng(seed)), 2400.0, q=0.7, sr=sr) * \
        S.env_perc(n, 0.001, decay, sr, curve=2.0)
    mono = body * (1.0 - noise_mix) + hiss * noise_mix
    return S.stereo(S.saturate(mono, 1.6), 0.0) * 0.8


def hat(duration: float = 0.12, decay: float = 0.05, hp: float = 7000.0,
        seed: int = 9, sr: int = SR) -> np.ndarray:
    """Charleston : bruit passe-haut très court. Marque la subdivision."""
    n = S.n_samples(duration, sr)
    h = S.highpass(S.noise(n, "white", _rng(seed)), hp, 0.8, sr)
    return S.stereo(h * S.env_perc(n, 0.0004, decay, sr, curve=3.0), 0.0) * 0.42


def perc_metal(duration: float = 0.9, base: float = 320.0, decay: float = 0.5,
               partials: int = 6, pan: float = 0.0, rust: float = 0.4,
               seed: int = 11, sr: int = SR) -> np.ndarray:
    """
    Percussion métallique industrielle — le timbre des Sanctuaires : une plaque
    d'acier frappée. Empilement de partiels inharmoniques (comme une vraie
    cloche/plaque) + bruit résonant. `rust` ajoute le grain de corrosion.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    mono = np.zeros(n)

    for p in range(partials):
        # Ratios irrationnels : aucune fondamentale perceptible, juste du métal
        ratio = 1.0 + p * rng.uniform(1.31, 2.17)
        amp = 1.0 / (1.0 + p * 1.4)
        mono += S.osc_sine(base * ratio, n, sr, phase0=rng.random()) * amp * \
            S.env_perc(n, 0.0008, decay / (1.0 + p * 0.45), sr, curve=2.4)

    if rust > 0:
        grit = S.bandpass(S.noise(n, "white", _rng(seed + 1)), base * 3.2, q=2.2, sr=sr)
        mono += grit * S.env_perc(n, 0.001, decay * 0.7, sr, curve=2.8) * rust

    mono = S.saturate(mono, 1.4)
    peak = np.max(np.abs(mono))
    if peak > 1e-9:
        mono /= peak
    return S.stereo(mono, pan) * 0.55


def taiko(duration: float = 1.2, f_start: float = 92.0, f_end: float = 58.0,
          decay: float = 0.55, seed: int = 13, sr: int = SR) -> np.ndarray:
    """
    Tambour lourd — réservé aux boss. Corps grave + peau bruitée, sans le clic
    électronique du kick : il doit sonner *organique* face au métal des ennemis.
    """
    n = S.n_samples(duration, sr)
    body = S.osc_sine(np.geomspace(f_start, f_end, n), n, sr) * \
        S.env_perc(n, 0.004, decay, sr, curve=2.0)
    body += S.osc_triangle(np.geomspace(f_start * 1.5, f_end * 1.4, n), n, sr) * 0.3 * \
        S.env_perc(n, 0.003, decay * 0.5, sr, curve=2.4)
    skin = S.lowpass(S.noise(n, "brown", _rng(seed)), 900.0, 1.2, sr) * \
        S.env_perc(n, 0.001, 0.07, sr, curve=3.0)
    return S.stereo(S.saturate(body + skin * 0.5, 1.8), 0.0) * 0.85


def rev_cymbal(duration: float = 2.0, sr: int = SR, seed: int = 17) -> np.ndarray:
    """Cymbale inversée — annonce une transition (arrivée de vague, de boss)."""
    n = S.n_samples(duration, sr)
    h = S.highpass(S.noise(n, "white", _rng(seed)), 3000.0, 0.7, sr)
    env = np.linspace(0.0, 1.0, n) ** 2.6
    return S.stereo(h * env, 0.0) * 0.5


# ---------------------------------------------------------------------------
# Textures & transitions
# ---------------------------------------------------------------------------

def rust_texture(duration: float, density: float = 1.0, base: float = 200.0,
                 seed: int = 23, sr: int = SR) -> np.ndarray:
    """
    Texture de Rouille Vivante — le bruit de fond du monde : métal qui travaille,
    craquements irréguliers, résonances qui dérivent. Sans hauteur définie, donc
    compatible avec n'importe quelle tonalité ; on peut la superposer partout.
    """
    n = S.n_samples(duration, sr)
    rng = _rng(seed)

    bed = S.noise(n, "brown", rng)
    sweep = base * (1.0 + 2.5 * (0.5 + 0.5 * S.lfo(0.021, n, sr, phase0=rng.random())))
    bed = S.bandpass(bed, np.clip(sweep, 40.0, sr * 0.4), q=3.5, sr=sr)
    out = S.stereo(bed, 0.0) * 0.6

    # Craquements : de courtes résonances métalliques dispersées
    n_cracks = int(duration * 1.6 * density)
    for i in range(n_cracks):
        at = rng.uniform(0.0, duration)
        crack = perc_metal(rng.uniform(0.25, 0.8), base=rng.uniform(400, 1600),
                           decay=rng.uniform(0.12, 0.4), partials=4,
                           pan=rng.uniform(-0.9, 0.9), rust=0.8,
                           seed=seed + 40 + i, sr=sr)
        start = S.n_samples(at, sr)
        end = min(n, start + len(crack))
        if end > start:
            out[start:end] += crack[:end - start] * rng.uniform(0.08, 0.24)

    return out


def noise_sweep(duration: float = 2.0, up: bool = True, f_lo: float = 200.0,
                f_hi: float = 9000.0, seed: int = 29, sr: int = SR) -> np.ndarray:
    """Balayage de bruit filtré — colle deux sections sans coupure."""
    n = S.n_samples(duration, sr)
    src = S.noise(n, "white", _rng(seed))
    track = np.geomspace(f_lo, f_hi, n) if up else np.geomspace(f_hi, f_lo, n)
    swept = S.bandpass(src, track, q=1.4, sr=sr)
    env = np.linspace(0.0, 1.0, n) ** 1.8 if up else np.linspace(1.0, 0.0, n) ** 0.8
    return S.stereo(swept * env, 0.0) * 0.6


def impact(duration: float = 3.0, seed: int = 31, sr: int = SR) -> np.ndarray:
    """Impact cinématique grave (boss, mort, fin d'intro)."""
    n = S.n_samples(duration, sr)
    rng = _rng(seed)
    body = S.osc_sine(np.geomspace(180.0, 32.0, n), n, sr) * \
        S.env_perc(n, 0.002, 1.1, sr, curve=1.6)
    boom = S.lowpass(S.noise(n, "brown", rng), 260.0, 1.6, sr) * \
        S.env_perc(n, 0.001, 0.9, sr, curve=2.0)
    metal = perc_metal(duration, base=140.0, decay=1.4, partials=8, rust=0.6,
                       seed=seed + 1, sr=sr)
    mono = S.saturate(body + boom * 0.7, 2.0)
    return S.stereo(mono, 0.0) * 0.9 + metal * 0.5
