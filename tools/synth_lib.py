"""
synth_lib — moteur de synthèse audio pour la musique de Chimera Protocol.

Primitives DSP bas niveau uniquement (oscillateurs, filtres, enveloppes, effets,
utilitaires musicaux, export). Les *instruments* (nappe CS-80, chœur formantique,
basse analogique…) vivent dans `synth_instruments.py` ; les *partitions* dans
`generate_music_v3.py`.

Parti pris : ambiance Blade Runner / Vangelis — synthèse soustractive analogique
chaude, pas de chiptune. Tout est vectorisé numpy ; les filtres IIR passent par
scipy.signal (les boucles échantillon par échantillon en Python pur sont trop
lentes pour des pistes de 90 s).

Convention audio interne : float64, stéréo `(n, 2)`, plage nominale [-1, 1],
44 100 Hz. Toutes les fonctions rendent un nouveau tableau (pas de mutation).

Dépendances : numpy, scipy, ffmpeg (dans le PATH) pour l'export OGG.
"""

from __future__ import annotations

import math
import os
import subprocess
import wave
from dataclasses import dataclass

import numpy as np
from scipy import ndimage, signal

SR = 44100
TWO_PI = 2.0 * math.pi


# ---------------------------------------------------------------------------
# Utilitaires généraux
# ---------------------------------------------------------------------------

def n_samples(duration: float, sr: int = SR) -> int:
    """Nombre d'échantillons pour une durée en secondes."""
    return int(round(duration * sr))


def db2lin(db: float) -> float:
    """Décibels → gain linéaire."""
    return 10.0 ** (db / 20.0)


def lin2db(lin: float) -> float:
    """Gain linéaire → décibels (plancher à -120 dB)."""
    return 20.0 * math.log10(max(lin, 1e-6))


def as_array(value, n: int) -> np.ndarray:
    """Accepte un scalaire ou un tableau et renvoie toujours un tableau de taille n."""
    if np.isscalar(value):
        return np.full(n, float(value))
    arr = np.asarray(value, dtype=np.float64)
    if arr.size == n:
        return arr
    # Ré-échantillonne linéairement (utile pour piloter un paramètre par une courbe grossière)
    return np.interp(np.linspace(0.0, 1.0, n), np.linspace(0.0, 1.0, arr.size), arr)


def stereo(mono: np.ndarray, pan: float = 0.0) -> np.ndarray:
    """
    Mono → stéréo avec panoramique à puissance constante.
    `pan` ∈ [-1 (gauche), +1 (droite)].
    """
    pan = float(np.clip(pan, -1.0, 1.0))
    angle = (pan + 1.0) * 0.25 * math.pi  # 0 → π/2
    return np.stack([mono * math.cos(angle), mono * math.sin(angle)], axis=-1)


def to_mono(x: np.ndarray) -> np.ndarray:
    """Stéréo → mono (moyenne des canaux). Passe-plat si déjà mono."""
    return x if x.ndim == 1 else x.mean(axis=1)


def ensure_stereo(x: np.ndarray) -> np.ndarray:
    """Garantit un tableau (n, 2)."""
    return stereo(x, 0.0) if x.ndim == 1 else x


def pad_to(x: np.ndarray, n: int) -> np.ndarray:
    """Tronque ou complète de zéros pour atteindre exactement n échantillons."""
    if len(x) == n:
        return x
    if len(x) > n:
        return x[:n]
    pad = [(0, n - len(x))] + [(0, 0)] * (x.ndim - 1)
    return np.pad(x, pad)


# ---------------------------------------------------------------------------
# Théorie musicale — noms de notes, gammes, accords
# ---------------------------------------------------------------------------

_PITCH_CLASS = {
    "C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "Fb": 4,
    "F": 5, "F#": 6, "Gb": 6, "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10,
    "Bb": 10, "B": 11, "Cb": 11,
}


def midi(name: str) -> int:
    """
    Nom de note → numéro MIDI. Convention scientifique : C4 = 60, A4 = 69 = 440 Hz.
    Exemples : `midi("C2")` = 36, `midi("Eb3")` = 51.
    """
    name = name.strip()
    i = 1
    if len(name) > 1 and name[1] in "#b":
        i = 2
    pc = _PITCH_CLASS[name[:i]]
    octave = int(name[i:])
    return (octave + 1) * 12 + pc


def hz(note) -> float:
    """Numéro MIDI (ou nom de note) → fréquence en Hz."""
    n = midi(note) if isinstance(note, str) else note
    return 440.0 * (2.0 ** ((n - 69) / 12.0))


def cents(ratio_cents: float) -> float:
    """Écart en cents → multiplicateur de fréquence."""
    return 2.0 ** (ratio_cents / 1200.0)


# Intervalles (demi-tons) par qualité d'accord — ce dont on a besoin pour du
# synthwave modal : triades, septièmes, sus, add9.
_CHORD_INTERVALS = {
    "":      [0, 4, 7],           # majeur
    "m":     [0, 3, 7],           # mineur
    "dim":   [0, 3, 6],
    "aug":   [0, 4, 8],
    "5":     [0, 7],              # power chord (sans tierce : ambigu, très Vangelis)
    "sus2":  [0, 2, 7],
    "sus4":  [0, 5, 7],
    "7":     [0, 4, 7, 10],
    "m7":    [0, 3, 7, 10],
    "maj7":  [0, 4, 7, 11],
    "m9":    [0, 3, 7, 10, 14],
    "maj9":  [0, 4, 7, 11, 14],
    "add9":  [0, 4, 7, 14],
    "madd9": [0, 3, 7, 14],
    "m6":    [0, 3, 7, 9],
    "m11":   [0, 3, 7, 10, 14, 17],
}


def chord(symbol: str, octave: int = 3, inversion: int = 0) -> list[int]:
    """
    Symbole d'accord → liste de numéros MIDI.
    Exemples : `chord("Cm", 3)` → [48, 51, 55] ; `chord("Abmaj7", 2)`.
    `inversion` monte les n notes graves d'une octave.
    """
    symbol = symbol.strip()
    i = 2 if len(symbol) > 1 and symbol[1] in "#b" else 1
    root, quality = symbol[:i], symbol[i:]
    if quality not in _CHORD_INTERVALS:
        raise ValueError(f"Qualité d'accord inconnue : '{quality}' (dans '{symbol}')")
    base = (octave + 1) * 12 + _PITCH_CLASS[root]
    notes = [base + iv for iv in _CHORD_INTERVALS[quality]]
    for k in range(inversion):
        notes[k % len(notes)] += 12
    return sorted(notes)


# Modes utiles pour l'univers du jeu : mineur naturel (mélancolie), phrygien
# (tension orientale/menace), dorien (froid mais mobile), lydien (étrangeté).
_MODES = {
    "minor":    [0, 2, 3, 5, 7, 8, 10],
    "phrygian": [0, 1, 3, 5, 7, 8, 10],
    "dorian":   [0, 2, 3, 5, 7, 9, 10],
    "aeolian":  [0, 2, 3, 5, 7, 8, 10],
    "lydian":   [0, 2, 4, 6, 7, 9, 11],
    "major":    [0, 2, 4, 5, 7, 9, 11],
    "locrian":  [0, 1, 3, 5, 6, 8, 10],
    "harmonic_minor": [0, 2, 3, 5, 7, 8, 11],
}


def scale(root: str, mode: str = "minor", octaves: int = 3, start_octave: int = 2) -> list[int]:
    """Génère les notes MIDI d'une gamme sur plusieurs octaves (pour les arpèges)."""
    steps = _MODES[mode]
    base = (start_octave + 1) * 12 + _PITCH_CLASS[root]
    return [base + 12 * o + s for o in range(octaves) for s in steps]


def bar_seconds(bpm: float, beats_per_bar: int = 4) -> float:
    """Durée d'une mesure en secondes."""
    return beats_per_bar * 60.0 / bpm


# ---------------------------------------------------------------------------
# Oscillateurs anti-aliasés (PolyBLEP)
# ---------------------------------------------------------------------------

def phase_of(freq, n: int, sr: int = SR, phase0: float = 0.0) -> np.ndarray:
    """
    Phase normalisée [0,1) accumulée à partir d'une fréquence (scalaire ou tableau).
    Accepter un tableau permet vibrato, glissando et modulation de hauteur.
    """
    f = as_array(freq, n)
    return (phase0 + np.cumsum(f) / sr) % 1.0


def _polyblep(t: np.ndarray, dt: np.ndarray) -> np.ndarray:
    """
    Correction PolyBLEP : adoucit les discontinuités des formes d'onde à angles
    vifs (dent de scie, carré) pour supprimer l'essentiel du repliement.
    """
    out = np.zeros_like(t)
    dt = np.maximum(dt, 1e-9)

    m = t < dt
    if np.any(m):
        x = t[m] / dt[m]
        out[m] = x + x - x * x - 1.0

    m = t > 1.0 - dt
    if np.any(m):
        x = (t[m] - 1.0) / dt[m]
        out[m] = x * x + x + x + 1.0

    return out


def osc_sine(freq, n: int, sr: int = SR, phase0: float = 0.0) -> np.ndarray:
    return np.sin(TWO_PI * phase_of(freq, n, sr, phase0))


def osc_saw(freq, n: int, sr: int = SR, phase0: float = 0.0) -> np.ndarray:
    """Dent de scie anti-aliasée — le timbre de base des nappes et basses analogiques."""
    ph = phase_of(freq, n, sr, phase0)
    dt = as_array(freq, n) / sr
    return (2.0 * ph - 1.0) - _polyblep(ph, dt)


def osc_square(freq, n: int, sr: int = SR, phase0: float = 0.0, pulse_width=0.5) -> np.ndarray:
    """Carré / impulsion anti-aliasé. `pulse_width` modulable (PWM) pour épaissir."""
    ph = phase_of(freq, n, sr, phase0)
    dt = as_array(freq, n) / sr
    pw = as_array(pulse_width, n)

    out = np.where(ph < pw, 1.0, -1.0)
    out -= _polyblep(ph, dt)
    out += _polyblep((ph - pw) % 1.0, dt)
    return out


def osc_triangle(freq, n: int, sr: int = SR, phase0: float = 0.0) -> np.ndarray:
    """
    Triangle — doux, pour les sous-basses et les timbres type flûte.
    Pas de correction anti-aliasing : ses harmoniques décroissent en 1/n², le
    repliement reste sous le plancher de bruit dans les registres où on l'emploie.
    """
    ph = phase_of(freq, n, sr, phase0)
    return 2.0 * np.abs(2.0 * ph - 1.0) - 1.0


def osc_supersaw(freq, n: int, sr: int = SR, voices: int = 7,
                 detune_cents: float = 14.0, spread: float = 0.85,
                 rng: np.random.Generator | None = None) -> np.ndarray:
    """
    Empilement de dents de scie légèrement désaccordées, réparties en stéréo.
    C'est la texture centrale des nappes « analogiques chaudes » : le battement
    entre voix crée le mouvement organique que ne donne aucun oscillateur seul.
    Renvoie du stéréo (n, 2).
    """
    rng = rng or np.random.default_rng(0)
    out = np.zeros((n, 2))
    f = as_array(freq, n)

    for v in range(voices):
        # Répartition symétrique du désaccord autour de la fondamentale
        offset = (v - (voices - 1) / 2.0) / max(1.0, (voices - 1) / 2.0)
        detuned = f * cents(offset * detune_cents)
        raw = osc_saw(detuned, n, sr, phase0=rng.random())
        pan = offset * spread
        out += stereo(raw, pan)

    return out / voices


def noise(n: int, color: str = "white", rng: np.random.Generator | None = None) -> np.ndarray:
    """
    Bruit blanc, rose (-3 dB/oct) ou brun (-6 dB/oct).
    Le rose sert aux souffles de nappe, le brun aux rumbles de boss.
    """
    rng = rng or np.random.default_rng(0)
    white = rng.standard_normal(n)
    if color == "white":
        return white
    if color == "pink":
        # Filtre de Voss approximé par un IIR d'ordre 3 (courbe -3 dB/oct classique)
        b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
        a = [1.0, -2.494956002, 2.017265875, -0.522189400]
        out = signal.lfilter(b, a, white)
    elif color == "brown":
        out = signal.lfilter([1.0], [1.0, -0.98], white)
    else:
        raise ValueError(f"Couleur de bruit inconnue : {color}")
    peak = np.max(np.abs(out))
    return out / peak if peak > 1e-9 else out


# ---------------------------------------------------------------------------
# Enveloppes
# ---------------------------------------------------------------------------

def adsr(n: int, attack: float, decay: float, sustain: float, release: float,
         sr: int = SR, curve: float = 2.0) -> np.ndarray:
    """
    Enveloppe ADSR. `release` est *inclus* dans les n échantillons (le note-off
    tombe à n - release), ce qui simplifie l'écriture des partitions : une note
    de 2 s occupe 2 s, queue comprise.
    `curve` > 1 rend le decay/release exponentiels (plus naturel qu'une rampe).
    """
    env = np.zeros(n)
    a = min(n_samples(attack, sr), n)
    d = min(n_samples(decay, sr), max(0, n - a))
    r = min(n_samples(release, sr), max(0, n - a - d))
    s = max(0, n - a - d - r)

    idx = 0
    if a > 0:
        env[idx:idx + a] = np.linspace(0.0, 1.0, a) ** (1.0 / curve)
        idx += a
    if d > 0:
        env[idx:idx + d] = 1.0 + (sustain - 1.0) * np.linspace(0.0, 1.0, d) ** curve
        idx += d
    if s > 0:
        env[idx:idx + s] = sustain
        idx += s
    if r > 0:
        start = env[idx - 1] if idx > 0 else sustain
        env[idx:idx + r] = start * (1.0 - np.linspace(0.0, 1.0, r)) ** curve

    return env


def env_perc(n: int, attack: float = 0.002, decay: float = 0.4,
             sr: int = SR, curve: float = 3.0) -> np.ndarray:
    """Enveloppe percussive : attaque très courte, décroissance exponentielle."""
    env = np.zeros(n)
    a = min(n_samples(attack, sr), n)
    if a > 0:
        env[:a] = np.linspace(0.0, 1.0, a)
    rest = n - a
    if rest > 0:
        tail = np.exp(-np.linspace(0.0, curve * 3.0, rest) * (0.4 / max(decay, 1e-3)))
        env[a:] = tail / tail[0]
    return env


def env_swell(n: int, peak_at: float = 0.4) -> np.ndarray:
    """
    Enveloppe en cloche asymétrique (montée jusqu'à `peak_at`, puis descente).
    C'est le geste de base d'une nappe qui « respire ».
    """
    peak = int(np.clip(peak_at, 0.05, 0.95) * n)
    env = np.empty(n)
    env[:peak] = np.sin(np.linspace(0.0, math.pi / 2, peak)) ** 1.6
    env[peak:] = np.cos(np.linspace(0.0, math.pi / 2, n - peak)) ** 1.2
    return env


def lfo(rate: float, n: int, sr: int = SR, depth: float = 1.0,
        phase0: float = 0.0, shape: str = "sine", offset: float = 0.0) -> np.ndarray:
    """LFO unipolaire ou bipolaire selon `offset` — vibrato, tremolo, balayages."""
    ph = (phase0 + np.arange(n) * rate / sr) % 1.0
    if shape == "sine":
        w = np.sin(TWO_PI * ph)
    elif shape == "triangle":
        w = 2.0 * np.abs(2.0 * ph - 1.0) - 1.0
    elif shape == "saw":
        w = 2.0 * ph - 1.0
    elif shape == "square":
        w = np.where(ph < 0.5, 1.0, -1.0)
    else:
        raise ValueError(f"Forme de LFO inconnue : {shape}")
    return offset + depth * w


# ---------------------------------------------------------------------------
# Filtres
#
# Les cutoffs modulés sont traités par blocs de 128 échantillons à coefficients
# constants (résolution ~2,9 ms, inaudible) : scipy reste vectorisé et le
# balayage de filtre — signature du son analogique — devient abordable.
# ---------------------------------------------------------------------------

_BLOCK = 128


def _biquad_lp(cutoff: float, q: float, sr: int) -> np.ndarray:
    """Biquad passe-bas résonant (RBJ cookbook) — la résonance fait le caractère."""
    w0 = TWO_PI * float(np.clip(cutoff, 20.0, sr * 0.48)) / sr
    alpha = math.sin(w0) / (2.0 * max(q, 0.05))
    cos_w0 = math.cos(w0)
    b = np.array([(1 - cos_w0) / 2, 1 - cos_w0, (1 - cos_w0) / 2])
    a = np.array([1 + alpha, -2 * cos_w0, 1 - alpha])
    return signal.tf2sos(b / a[0], a / a[0])


def _biquad_hp(cutoff: float, q: float, sr: int) -> np.ndarray:
    w0 = TWO_PI * float(np.clip(cutoff, 10.0, sr * 0.48)) / sr
    alpha = math.sin(w0) / (2.0 * max(q, 0.05))
    cos_w0 = math.cos(w0)
    b = np.array([(1 + cos_w0) / 2, -(1 + cos_w0), (1 + cos_w0) / 2])
    a = np.array([1 + alpha, -2 * cos_w0, 1 - alpha])
    return signal.tf2sos(b / a[0], a / a[0])


def _biquad_bp(center: float, q: float, sr: int, gain: float = 1.0) -> np.ndarray:
    """Passe-bande à gain crête constant — brique de base des formants vocaux."""
    w0 = TWO_PI * float(np.clip(center, 20.0, sr * 0.48)) / sr
    alpha = math.sin(w0) / (2.0 * max(q, 0.05))
    cos_w0 = math.cos(w0)
    b = np.array([alpha * gain, 0.0, -alpha * gain])
    a = np.array([1 + alpha, -2 * cos_w0, 1 - alpha])
    return signal.tf2sos(b / a[0], a / a[0])


def _apply_sos_modulated(x: np.ndarray, cutoff: np.ndarray, q: float,
                         sr: int, maker) -> np.ndarray:
    """Filtrage à cutoff variable, par blocs, avec conservation de l'état (zi)."""
    n = len(x)
    out = np.zeros(n)
    sos = maker(float(cutoff[0]), q, sr)
    zi = signal.sosfilt_zi(sos) * 0.0

    for start in range(0, n, _BLOCK):
        end = min(start + _BLOCK, n)
        sos = maker(float(np.mean(cutoff[start:end])), q, sr)
        out[start:end], zi = signal.sosfilt(sos, x[start:end], zi=zi)

    return out


def lowpass(x: np.ndarray, cutoff, q: float = 0.707, sr: int = SR) -> np.ndarray:
    """Passe-bas résonant. `cutoff` scalaire (rapide) ou tableau (balayage)."""
    if x.ndim == 2:
        return np.stack([lowpass(x[:, c], cutoff, q, sr) for c in range(2)], axis=-1)
    if np.isscalar(cutoff):
        return signal.sosfilt(_biquad_lp(float(cutoff), q, sr), x)
    return _apply_sos_modulated(x, as_array(cutoff, len(x)), q, sr, _biquad_lp)


def highpass(x: np.ndarray, cutoff, q: float = 0.707, sr: int = SR) -> np.ndarray:
    """Passe-haut — indispensable pour dégager le bas du spectre entre stems."""
    if x.ndim == 2:
        return np.stack([highpass(x[:, c], cutoff, q, sr) for c in range(2)], axis=-1)
    if np.isscalar(cutoff):
        return signal.sosfilt(_biquad_hp(float(cutoff), q, sr), x)
    return _apply_sos_modulated(x, as_array(cutoff, len(x)), q, sr, _biquad_hp)


def bandpass(x: np.ndarray, center, q: float = 4.0, sr: int = SR, gain: float = 1.0) -> np.ndarray:
    if x.ndim == 2:
        return np.stack([bandpass(x[:, c], center, q, sr, gain) for c in range(2)], axis=-1)
    if np.isscalar(center):
        return signal.sosfilt(_biquad_bp(float(center), q, sr, gain), x)
    return _apply_sos_modulated(x, as_array(center, len(x)), q, sr,
                                lambda c, qq, s: _biquad_bp(c, qq, s, gain))


def peaking_eq(x: np.ndarray, freq: float, gain_db: float, q: float = 1.0, sr: int = SR) -> np.ndarray:
    """Cloche d'égalisation (RBJ) — sculpte les stems pour qu'ils ne se battent pas."""
    if x.ndim == 2:
        return np.stack([peaking_eq(x[:, c], freq, gain_db, q, sr) for c in range(2)], axis=-1)
    A = 10.0 ** (gain_db / 40.0)
    w0 = TWO_PI * float(np.clip(freq, 20.0, sr * 0.48)) / sr
    alpha = math.sin(w0) / (2.0 * max(q, 0.05))
    cos_w0 = math.cos(w0)
    b = np.array([1 + alpha * A, -2 * cos_w0, 1 - alpha * A])
    a = np.array([1 + alpha / A, -2 * cos_w0, 1 - alpha / A])
    return signal.sosfilt(signal.tf2sos(b / a[0], a / a[0]), x)


def tilt_air(x: np.ndarray, gain_db: float = 3.0, freq: float = 8000.0, sr: int = SR) -> np.ndarray:
    """Shelf aigu — ouvre le haut du spectre sans siffler (le « verre » de l'Aether)."""
    if x.ndim == 2:
        return np.stack([tilt_air(x[:, c], gain_db, freq, sr) for c in range(2)], axis=-1)
    A = 10.0 ** (gain_db / 40.0)
    w0 = TWO_PI * min(freq, sr * 0.45) / sr
    cos_w0, sin_w0 = math.cos(w0), math.sin(w0)
    alpha = sin_w0 / 2.0 * math.sqrt((A + 1 / A) * (1 / 0.9 - 1) + 2)
    two_sqrt_a_alpha = 2 * math.sqrt(A) * alpha
    b = np.array([
        A * ((A + 1) + (A - 1) * cos_w0 + two_sqrt_a_alpha),
        -2 * A * ((A - 1) + (A + 1) * cos_w0),
        A * ((A + 1) + (A - 1) * cos_w0 - two_sqrt_a_alpha),
    ])
    a = np.array([
        (A + 1) - (A - 1) * cos_w0 + two_sqrt_a_alpha,
        2 * ((A - 1) - (A + 1) * cos_w0),
        (A + 1) - (A - 1) * cos_w0 - two_sqrt_a_alpha,
    ])
    return signal.sosfilt(signal.tf2sos(b / a[0], a / a[0]), x)


# ---------------------------------------------------------------------------
# Formants — le chœur
#
# Un chœur de synthèse convaincant ne s'obtient pas en filtrant une nappe : il
# faut une *source glottale* (train d'impulsions riche) mise en forme par une
# banque de résonateurs fixes (les formants de la voyelle), plus les
# imperfections humaines (jitter de hauteur, shimmer d'amplitude, vibrato
# désynchronisé entre voix). C'est ce que fait `formant_filter`.
# ---------------------------------------------------------------------------

# (fréquence Hz, gain dB, largeur de bande Hz) × 4 formants, par voyelle et registre.
# Valeurs dérivées des tables classiques de synthèse par formants (CSound/Klatt).
VOWELS = {
    "a_bass":  [(600, 0, 60), (1040, -7, 70), (2250, -9, 110), (2450, -9, 120)],
    "a_alto":  [(800, 0, 80), (1150, -4, 90), (2800, -20, 120), (3500, -36, 130)],
    "a_sop":   [(800, 0, 80), (1150, -6, 90), (2900, -32, 120), (3900, -20, 130)],
    "o_bass":  [(400, 0, 40), (750, -11, 80), (2400, -21, 100), (2600, -20, 120)],
    "o_alto":  [(450, 0, 70), (800, -9, 80), (2830, -16, 100), (3500, -28, 130)],
    "u_bass":  [(350, 0, 40), (600, -20, 80), (2400, -32, 100), (2675, -28, 120)],
    "u_alto":  [(325, 0, 50), (700, -12, 60), (2530, -30, 170), (3500, -40, 180)],
    "e_alto":  [(400, 0, 60), (1600, -24, 80), (2700, -30, 120), (3300, -35, 150)],
    "i_alto":  [(350, 0, 50), (1700, -20, 100), (2700, -30, 120), (3700, -40, 150)],
}


def formant_filter(x: np.ndarray, vowel: str = "a_alto", sr: int = SR,
                   shift: float = 1.0) -> np.ndarray:
    """
    Applique une banque de formants à un signal source.
    `shift` déplace tous les formants (>1 = voix plus « petite »/féminine).
    """
    out = np.zeros_like(x)
    for freq, gain_db, bw in VOWELS[vowel]:
        f = freq * shift
        if f >= sr * 0.47:
            continue
        q = max(f / max(bw, 20.0), 0.5)
        out += bandpass(x, f, q=q, sr=sr) * db2lin(gain_db)
    return out


def glottal_source(freq, n: int, sr: int = SR, phase0: float = 0.0,
                   tension: float = 0.62) -> np.ndarray:
    """
    Source glottale : impulsion asymétrique riche en harmoniques (approximation
    du modèle de Rosenberg). Plus musclée qu'une dent de scie pour la voix car
    son spectre décroît en -12 dB/oct comme les vraies cordes vocales.
    """
    ph = phase_of(freq, n, sr, phase0)
    tp = float(np.clip(tension, 0.3, 0.85))  # instant de fermeture glottale
    out = np.zeros(n)

    rising = ph < tp
    out[rising] = 3.0 * (ph[rising] / tp) ** 2 - 2.0 * (ph[rising] / tp) ** 3

    falling = (ph >= tp) & (ph < 1.0)
    u = (ph[falling] - tp) / max(1.0 - tp, 1e-6)
    out[falling] = 1.0 - u * u

    out -= np.mean(out)
    peak = np.max(np.abs(out))
    return out / peak if peak > 1e-9 else out


# ---------------------------------------------------------------------------
# Effets
# ---------------------------------------------------------------------------

def make_ir(duration: float = 3.5, sr: int = SR, predelay: float = 0.02,
            damping: float = 0.55, width: float = 1.0,
            seed: int = 7) -> np.ndarray:
    """
    Réponse impulsionnelle synthétique pour la réverbération par convolution.

    Trois bandes avec des temps de décroissance différents (les aigus meurent
    plus vite que les graves, comme dans une vraie salle), plus quelques
    réflexions précoces. Les canaux L/R sont décorrélés → image très large,
    exactement le halo « cathédrale » recherché.
    """
    rng = np.random.default_rng(seed)
    n = n_samples(duration, sr)
    t = np.arange(n) / sr

    ir = np.zeros((n, 2))
    for c in range(2):
        w = rng.standard_normal(n)
        low = lowpass(w, 400.0, 0.7, sr) * np.exp(-t / (duration * 0.85))
        mid = bandpass(w, 1200.0, 0.6, sr) * np.exp(-t / (duration * 0.5 * (1.2 - damping)))
        high = highpass(w, 3500.0, 0.7, sr) * np.exp(-t / (duration * 0.22 * (1.2 - damping)))
        ir[:, c] = low * 1.0 + mid * 0.65 + high * 0.32

    # Réflexions précoces : quelques diracs décorrélés, ce qui « place » la salle
    for _ in range(14):
        pos = int(rng.uniform(0.004, 0.09) * sr)
        if pos < n:
            ir[pos, 0] += rng.uniform(-0.5, 0.5)
            ir[pos, 1] += rng.uniform(-0.5, 0.5)

    # Pré-délai : le direct doit rester lisible avant que la salle réponde
    pre = n_samples(predelay, sr)
    if pre > 0:
        ir = np.vstack([np.zeros((pre, 2)), ir])[:n]

    # Fondu d'entrée court pour éviter un clic, fondu de sortie pour la queue
    fade_in = min(n_samples(0.005, sr), n)
    ir[:fade_in] *= np.linspace(0, 1, fade_in)[:, None]
    fade_out = min(n_samples(0.3, sr), n)
    ir[-fade_out:] *= np.linspace(1, 0, fade_out)[:, None]

    # Élargissement stéréo (M/S)
    mid_s = (ir[:, 0] + ir[:, 1]) * 0.5
    side = (ir[:, 0] - ir[:, 1]) * 0.5 * width
    ir = np.stack([mid_s + side, mid_s - side], axis=-1)

    # Normalisation en ÉNERGIE (et non en crête) : une IR de bruit de plusieurs
    # secondes normalisée en crête multiplie le signal par ~30 à la convolution.
    # Diviser par la norme L2 rend le gain du wet unitaire, donc `mix` signifie
    # vraiment « proportion de réverbération ».
    return ir / max(math.sqrt(float(np.sum(ir ** 2)) / 2.0), 1e-9)


_IR_CACHE: dict[tuple, np.ndarray] = {}


def reverb(x: np.ndarray, time: float = 3.5, mix: float = 0.35, sr: int = SR,
           predelay: float = 0.02, damping: float = 0.55, width: float = 1.0,
           seed: int = 7, tail: bool = True) -> np.ndarray:
    """
    Réverbération par convolution avec une IR synthétique.
    `tail=False` tronque la queue à la longueur d'entrée (obligatoire sur une
    boucle : la queue doit être réinjectée au début, cf. `loopify`).
    """
    x = ensure_stereo(x)
    key = (round(time, 3), round(predelay, 4), round(damping, 3), round(width, 3), seed, sr)
    if key not in _IR_CACHE:
        _IR_CACHE[key] = make_ir(time, sr, predelay, damping, width, seed)
    ir = _IR_CACHE[key]

    wet = np.stack([signal.oaconvolve(x[:, c], ir[:, c], mode="full") for c in range(2)], axis=-1)
    if tail:
        dry = pad_to(x, len(wet))
    else:
        wet = wet[:len(x)]
        dry = x

    # Calage du wet sur le RMS du dry : un signal soutenu accumule l'énergie de
    # l'IR (~+12 dB), ce qui rendrait `mix` ininterprétable d'une IR à l'autre.
    # Après ce calage, mix=0.4 veut dire la même chose pour une salle de 1 s ou 6 s.
    rms_dry = float(np.sqrt(np.mean(x ** 2)))
    rms_wet = float(np.sqrt(np.mean(wet ** 2)))
    if rms_wet > 1e-9 and rms_dry > 1e-9:
        wet = wet * (rms_dry / rms_wet)

    return dry * (1.0 - mix) + wet * mix


def delay(x: np.ndarray, time: float = 0.375, feedback: float = 0.42,
          mix: float = 0.3, sr: int = SR, ping_pong: bool = True,
          damp: float = 6000.0) -> np.ndarray:
    """
    Délai (ping-pong par défaut) avec amortissement des aigus dans la boucle.
    Caler `time` sur la croche pointée du tempo est *le* cliché synthwave —
    il crée un contretemps qui remplit le mix sans ajouter de notes.
    """
    x = ensure_stereo(x)
    d = max(1, n_samples(time, sr))
    n = len(x)
    wet = np.zeros((n, 2))

    buf = x.copy()
    gain = 1.0
    for rep in range(1, 9):
        gain *= feedback
        if gain < 0.004:
            break
        buf = lowpass(buf, damp, 0.707, sr)  # chaque répétition perd des aigus
        start = d * rep
        if start >= n:
            break
        seg = buf[:n - start]
        if ping_pong and rep % 2 == 1:
            seg = seg[:, ::-1]
        wet[start:start + len(seg)] += seg * gain

    return x * (1.0 - mix) + wet * mix


def chorus(x: np.ndarray, rate: float = 0.35, depth_ms: float = 6.0,
           mix: float = 0.4, sr: int = SR, voices: int = 3) -> np.ndarray:
    """
    Chorus/ensemble par lignes de retard modulées. Épaissit une nappe et lui
    donne le léger désaccord flottant des polysynthés analogiques.
    """
    x = ensure_stereo(x)
    n = len(x)
    idx = np.arange(n, dtype=np.float64)
    wet = np.zeros((n, 2))

    for v in range(voices):
        phase = v / voices
        for c in range(2):
            mod = (depth_ms * 0.001 * sr) * (0.5 + 0.5 * np.sin(
                TWO_PI * (rate * idx / sr + phase + 0.25 * c)))
            base = 0.012 * sr
            read = np.clip(idx - base - mod, 0, n - 1)
            wet[:, c] += np.interp(read, idx, x[:, c])

    wet /= voices
    return x * (1.0 - mix) + wet * mix


def saturate(x: np.ndarray, drive: float = 1.6, mix: float = 1.0) -> np.ndarray:
    """
    Saturation douce (tanh). Ajoute des harmoniques paires/impaires et « colle »
    le signal : c'est ce qui distingue une basse analogique d'une sinusoïde morte.
    """
    wet = np.tanh(x * drive) / math.tanh(max(drive, 1e-3))
    return x * (1.0 - mix) + wet * mix


def compress(x: np.ndarray, threshold_db: float = -18.0, ratio: float = 3.0,
             attack: float = 0.01, release: float = 0.18, sr: int = SR,
             makeup_db: float | None = None) -> np.ndarray:
    """Compresseur à détection RMS lissée — resserre la dynamique d'un stem."""
    x = ensure_stereo(x)
    det = np.abs(to_mono(x))

    a_coef = math.exp(-1.0 / max(attack * sr, 1.0))
    r_coef = math.exp(-1.0 / max(release * sr, 1.0))
    # Lissage asymétrique approché : deux one-pole, on garde le plus réactif
    fast = signal.lfilter([1 - a_coef], [1, -a_coef], det)
    slow = signal.lfilter([1 - r_coef], [1, -r_coef], det)
    env = np.maximum(fast, slow)

    env_db = 20.0 * np.log10(np.maximum(env, 1e-6))
    over = np.maximum(env_db - threshold_db, 0.0)
    gain_db = -over * (1.0 - 1.0 / ratio)

    if makeup_db is None:
        makeup_db = -threshold_db * (1.0 - 1.0 / ratio) * 0.5

    return x * (10.0 ** ((gain_db + makeup_db) / 20.0))[:, None]


def sidechain(x: np.ndarray, hits: list[float], amount: float = 0.55,
              attack: float = 0.006, release: float = 0.22, sr: int = SR) -> np.ndarray:
    """
    Ducking rythmique déclenché par une liste d'instants (en secondes) — en
    général les temps de la grosse caisse. C'est ce qui donne la « respiration »
    pompée des nappes électroniques et libère de la place pour le kick.
    """
    x = ensure_stereo(x)
    n = len(x)
    env = np.ones(n)

    a = max(1, n_samples(attack, sr))
    r = max(1, n_samples(release, sr))
    shape = np.concatenate([
        np.linspace(1.0, 1.0 - amount, a),
        1.0 - amount * (1.0 - np.linspace(0.0, 1.0, r)) ** 2.2,
    ])

    for hit in hits:
        start = n_samples(hit, sr)
        if start >= n:
            continue
        seg = shape[:n - start]
        env[start:start + len(seg)] = np.minimum(env[start:start + len(seg)], seg)

    return x * env[:, None]


def stereo_width(x: np.ndarray, width: float = 1.4, bass_mono_hz: float = 140.0,
                 sr: int = SR) -> np.ndarray:
    """
    Élargit l'image stéréo en M/S tout en gardant les graves au centre
    (indispensable : des basses déphasées disparaissent en mono).
    """
    x = ensure_stereo(x)
    mid_s = (x[:, 0] + x[:, 1]) * 0.5
    side = (x[:, 0] - x[:, 1]) * 0.5 * width
    if bass_mono_hz > 0:
        side = highpass(side, bass_mono_hz, 0.707, sr)
    return np.stack([mid_s + side, mid_s - side], axis=-1)


# ---------------------------------------------------------------------------
# Mixage & bouclage
# ---------------------------------------------------------------------------

class Mixer:
    """
    Buffer stéréo d'accumulation. `add()` place un extrait à un instant donné
    (en secondes) sans se soucier des dépassements de fin de buffer.
    """

    def __init__(self, duration: float, sr: int = SR):
        self.sr = sr
        self.n = n_samples(duration, sr)
        self.buf = np.zeros((self.n, 2))

    def add(self, sound: np.ndarray, at: float = 0.0, gain: float = 1.0,
            pan: float | None = None, wrap: bool = False) -> "Mixer":
        """
        `wrap=True` réinjecte au début ce qui dépasse la fin — la façon propre de
        faire tenir une queue de reverb dans une boucle sans coupure.
        """
        snd = ensure_stereo(sound) if pan is None else stereo(to_mono(sound), pan)
        snd = snd * gain
        start = n_samples(at, self.sr)

        if start >= self.n:
            if not wrap:
                return self
            start %= self.n

        end = start + len(snd)
        if end <= self.n:
            self.buf[start:end] += snd
        else:
            self.buf[start:] += snd[:self.n - start]
            rest = snd[self.n - start:]
            if wrap:
                for off in range(0, len(rest), self.n):
                    chunk = rest[off:off + self.n]
                    self.buf[:len(chunk)] += chunk
        return self

    @property
    def out(self) -> np.ndarray:
        return self.buf


def loopify(x: np.ndarray, loop_duration: float, sr: int = SR,
            crossfade: float = 0.0) -> np.ndarray:
    """
    Transforme un rendu plus long que la boucle en boucle parfaite : la queue
    (reverbs, delays) qui dépasse `loop_duration` est réinjectée au début.
    Résultat : aucun silence ni coupure au point de bouclage.
    """
    n = n_samples(loop_duration, sr)
    x = ensure_stereo(x)
    out = np.zeros((n, 2))
    out[:min(n, len(x))] = x[:n]

    tail = x[n:]
    for off in range(0, len(tail), n):
        chunk = tail[off:off + n]
        out[:len(chunk)] += chunk

    if crossfade > 0:
        cf = min(n_samples(crossfade, sr), n // 4)
        ramp = np.linspace(0.0, 1.0, cf)[:, None]
        out[:cf] = out[:cf] * ramp + out[-cf:] * (1.0 - ramp)

    return out


def normalize(x: np.ndarray, peak: float = 0.89) -> np.ndarray:
    """Normalisation crête (le vrai calage de loudness est fait par ffmpeg)."""
    m = np.max(np.abs(x))
    return x * (peak / m) if m > 1e-9 else x


def limiter(x: np.ndarray, ceiling: float = 0.95, lookahead: float = 0.004,
            sr: int = SR) -> np.ndarray:
    """Limiteur à anticipation — évite l'écrêtage dur sur les pics de percussion."""
    x = ensure_stereo(x)
    la = max(3, n_samples(lookahead, sr))
    peak = np.maximum(np.abs(x[:, 0]), np.abs(x[:, 1]))

    # Maximum glissant centré : la réduction de gain arrive AVANT le pic
    env = ndimage.maximum_filter1d(peak, size=2 * la + 1, mode="nearest")
    # Lissage du gain pour éviter la distorsion de modulation
    env = ndimage.uniform_filter1d(env, size=la, mode="nearest")

    gain = np.minimum(1.0, ceiling / np.maximum(env, 1e-6))
    return x * gain[:, None]


# ---------------------------------------------------------------------------
# Export
# ---------------------------------------------------------------------------

def write_wav(path: str, x: np.ndarray, sr: int = SR, bits: int = 16) -> str:
    """Écrit un WAV PCM (16 ou 24 bits) avec dithering TPDF en 16 bits."""
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    x = ensure_stereo(np.clip(x, -1.0, 1.0))

    if bits == 16:
        rng = np.random.default_rng(1)
        dither = (rng.random(x.shape) - rng.random(x.shape)) / 32768.0
        data = np.clip(x + dither, -1.0, 1.0)
        pcm = (data * 32767.0).astype("<i2")
        sampwidth = 2
    elif bits == 24:
        ints = (np.clip(x, -1.0, 1.0) * 8388607.0).astype("<i4")
        pcm = ints.astype("<u4").view(np.uint8).reshape(-1, 4)[:, :3].tobytes()
        sampwidth = 3
    else:
        raise ValueError("bits doit valoir 16 ou 24")

    with wave.open(path, "wb") as f:
        f.setnchannels(2)
        f.setsampwidth(sampwidth)
        f.setframerate(sr)
        f.writeframes(pcm if bits == 24 else pcm.tobytes())

    return path


def to_ogg(wav_path: str, ogg_path: str, quality: int = 6,
           loudnorm_lufs: float | None = -16.0, keep_wav: bool = False,
           true_peak: float = -1.5, sr: int = SR) -> str:
    """
    Encode en OGG Vorbis via ffmpeg, avec calage de loudness EBU R128.

    -16 LUFS est le compromis habituel pour de la musique de jeu : assez fort
    pour ne pas disparaître, assez de marge pour que les SFX passent devant.
    `true_peak` doit être abaissé (-6 dBTP) pour les *stems* destinés à être
    sommés en jeu, sinon la somme des couches écrête.
    """
    os.makedirs(os.path.dirname(os.path.abspath(ogg_path)), exist_ok=True)
    cmd = ["ffmpeg", "-y", "-loglevel", "error", "-i", wav_path]
    if loudnorm_lufs is not None:
        cmd += ["-af", f"loudnorm=I={loudnorm_lufs}:TP={true_peak}:LRA=11"]
    # CRITIQUE : le filtre `loudnorm` travaille en interne à 192 kHz et sort à
    # ce taux si on ne le force pas — les fichiers seraient 4,35× trop gros et
    # Godot devrait les rééchantillonner à chaque lecture.
    cmd += ["-ar", str(sr), "-c:a", "libvorbis", "-q:a", str(quality), ogg_path]

    subprocess.run(cmd, check=True)
    if not keep_wav and os.path.exists(wav_path):
        os.remove(wav_path)

    return ogg_path


def render(path_no_ext: str, x: np.ndarray, sr: int = SR,
           loudnorm_lufs: float | None = -16.0, quality: int = 6,
           keep_wav: bool = False, true_peak: float = -1.5) -> str:
    """Normalise, limite, écrit le WAV puis encode l'OGG. Renvoie le chemin OGG."""
    x = limiter(normalize(x, 0.92), ceiling=0.96, sr=sr)
    wav = write_wav(path_no_ext + ".wav", x, sr, bits=24)
    return to_ogg(wav, path_no_ext + ".ogg", quality, loudnorm_lufs, keep_wav,
                  true_peak, sr)
