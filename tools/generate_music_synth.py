"""
Génère des fichiers WAV audibles (synthèse procédurale) pour tous les assets audio
du projet Chimera Protocol — musiques ET SFX.

Source des SFX : packs Kenney CC0 (téléchargés dans /tmp/kenney_*) — convertis en
  WAV via re-encodage Python pur (sans ffmpeg). Kenney publie en OGG, Python stdlib
  ne décode pas OGG nativement, donc TOUS les sons sont ici synthétisés.
  Les packs Kenney ont servi de référence descriptive pour le design sonore.

Statut de chaque fichier produit : PLACEHOLDER SYNTHÉTISÉ (CC0, domaine public).
  Remplacer par des assets professionnels avant publication commerciale.

Usage :
  C:/Users/drang/AppData/Local/Programs/Python/Python313/python.exe tools/generate_music_synth.py

Aucune dépendance externe (stdlib Python uniquement : wave, math, struct, random, os).
"""

import wave
import math
import struct
import random
import os

PYTHON = r"C:/Users/drang/AppData/Local/Programs/Python/Python313/python.exe"
SAMPLE_RATE = 44100
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MUSIC_DIR = os.path.join(PROJECT_ROOT, "assets", "audio", "music")
SFX_DIR   = os.path.join(PROJECT_ROOT, "assets", "audio", "sfx")

# ---------------------------------------------------------------------------
# Utilitaires de bas niveau
# ---------------------------------------------------------------------------

def clamp16(v: float) -> int:
    """Ramène un float [-1, 1] vers int16."""
    return max(-32768, min(32767, int(v * 32767)))


def soft_clip(v: float, threshold: float = 0.7) -> float:
    """Saturation douce (type tanh) pour simuler distorsion analogique."""
    if abs(v) <= threshold:
        return v
    sign = 1.0 if v > 0 else -1.0
    excess = abs(v) - threshold
    return sign * (threshold + excess / (1.0 + excess / (1.0 - threshold + 0.001)))


def write_wav(path: str, samples_l: list, samples_r: list = None, rate: int = SAMPLE_RATE):
    """Écrit un WAV stéréo (ou mono si samples_r est None)."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    stereo = samples_r is not None
    channels = 2 if stereo else 1
    n = len(samples_l)
    with wave.open(path, "w") as f:
        f.setnchannels(channels)
        f.setsampwidth(2)  # 16-bit
        f.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            l_val = clamp16(samples_l[i])
            frames += struct.pack("<h", l_val)
            if stereo:
                r_val = clamp16(samples_r[i])
                frames += struct.pack("<h", r_val)
        f.writeframes(bytes(frames))
    rel = os.path.relpath(path, PROJECT_ROOT)
    print(f"  [OK] {rel} ({n / rate:.2f}s, {'stereo' if stereo else 'mono'})")


def sine(t: float, freq: float, phase: float = 0.0) -> float:
    return math.sin(2 * math.pi * freq * t + phase)


def noise() -> float:
    return random.uniform(-1.0, 1.0)


def envelope_adsr(t: float, dur: float,
                  attack: float, decay: float, sustain_level: float, release: float) -> float:
    """Enveloppe ADSR simple, retourne un scalaire [0, 1]."""
    if t < attack:
        return t / attack if attack > 0 else 1.0
    t -= attack
    if t < decay:
        return 1.0 - (1.0 - sustain_level) * (t / decay) if decay > 0 else sustain_level
    t -= decay
    sustain_dur = dur - attack - decay - release
    if t < sustain_dur:
        return sustain_level
    t -= sustain_dur
    if t < release:
        return sustain_level * (1.0 - t / release) if release > 0 else 0.0
    return 0.0


def fade_in_out(samples: list, fade_samples: int) -> list:
    """Fade in + fade out linéaires sur un buffer mono."""
    n = len(samples)
    out = samples[:]
    for i in range(min(fade_samples, n)):
        factor = i / fade_samples
        out[i] *= factor
        out[n - 1 - i] *= factor
    return out


def crossfade_loop(samples: list, xfade_samples: int) -> list:
    """Rend une boucle seamless par crossfade de xfade_samples frames."""
    n = len(samples)
    out = samples[:]
    for i in range(xfade_samples):
        a = i / xfade_samples
        # début reçoit la fin fondue
        out[i] = samples[i] * a + samples[n - xfade_samples + i] * (1 - a)
    return out


# ---------------------------------------------------------------------------
# Oscillateurs et couches sonores
# ---------------------------------------------------------------------------

def osc_pad(t: float, freqs: list, detune_hz: float = 2.0) -> float:
    """Pad synthé : plusieurs oscillateurs légèrement désaccordés (effet chorus)."""
    total = 0.0
    n = len(freqs)
    for i, f in enumerate(freqs):
        phase_offset = i * 0.3
        detune = detune_hz * (i - n / 2) / max(1, n - 1)
        total += sine(t, f + detune, phase_offset)
    return total / n


def osc_bass(t: float, freq: float, drive: float = 1.0) -> float:
    """Basse analogique : fondamentale + 2e harmonique + saturation."""
    v = 0.6 * sine(t, freq) + 0.3 * sine(t, freq * 2) + 0.1 * sine(t, freq * 3)
    return soft_clip(v * drive)


def osc_arp(t: float, notes: list, bpm: float, note_div: float = 0.25) -> float:
    """Arpège : cycles sur une liste de fréquences à un tempo donné."""
    beat_dur = 60.0 / bpm
    note_dur = beat_dur * note_div
    idx = int(t / note_dur) % len(notes)
    t_in_note = t % note_dur
    env = envelope_adsr(t_in_note, note_dur, 0.005, 0.05, 0.4, note_dur * 0.4)
    return sine(t, notes[idx]) * env


def osc_kick(t_in_note: float, dur: float = 0.2) -> float:
    """Kick électronique : sine descendant avec punch."""
    if t_in_note > dur:
        return 0.0
    freq = 120 * math.exp(-t_in_note * 20)
    env = math.exp(-t_in_note * 15)
    return sine(t_in_note, freq) * env


def osc_hihat(t_in_note: float, dur: float = 0.05) -> float:
    """Hi-hat : bruit blanc court avec decay."""
    if t_in_note > dur:
        return 0.0
    env = math.exp(-t_in_note * 80)
    return noise() * env * 0.3


def osc_snare(t_in_note: float, dur: float = 0.12) -> float:
    """Caisse claire : bruit bref + tonalité courte."""
    if t_in_note > dur:
        return 0.0
    env = math.exp(-t_in_note * 30)
    return (noise() * 0.7 + sine(t_in_note, 200) * 0.3) * env


def pulse_lfo(t: float, freq_lfo: float = 0.5) -> float:
    """LFO sinusoïdal pour vibrato/tremolo."""
    return math.sin(2 * math.pi * freq_lfo * t)


# ---------------------------------------------------------------------------
# MUSIQUES
# ---------------------------------------------------------------------------

def gen_music_menu(duration: float = 30.0) -> tuple:
    """
    Ambiance lente (70 BPM), accord mineur La m (A-C-E).
    Pad cristallin Aether, mélodie grave, vibrato lent.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    xfade = int(0.5 * rate)
    L, R = [], []

    # Accord La mineur : A2(110), C3(130.8), E3(164.8), A3(220)
    pad_freqs = [110.0, 130.81, 164.81, 220.0]
    melody_notes = [110.0, 123.47, 130.81, 146.83, 164.81]  # Am pentatonic
    mel_bpm = 70.0

    for i in range(n):
        t = i / rate

        # LFO lent pour vibrato
        lfo_slow = pulse_lfo(t, 0.3)
        lfo_med  = pulse_lfo(t, 0.8)

        # Pad Aether : frequencies + léger vibrato
        pad = osc_pad(t, [f * (1 + 0.003 * lfo_slow) for f in pad_freqs], detune_hz=1.5)
        pad_vol = 0.35

        # Mélodie grave (cycle 4 mesures)
        beat = 60.0 / mel_bpm
        cycle = beat * 16  # 4 mesures
        t_cycle = t % cycle
        mel_idx = int(t_cycle / (beat * 4)) % len(melody_notes)
        t_in_mel = t_cycle % (beat * 4)
        mel_env = envelope_adsr(t_in_mel, beat * 4, 0.1, 0.2, 0.6, beat * 1.5)
        melody = sine(t, melody_notes[mel_idx] * (1 + 0.004 * lfo_slow)) * mel_env * 0.25

        # Drone basse
        drone = osc_bass(t, 55.0, drive=0.8) * 0.15

        # Harmoniques cristallines hautes (Aether)
        crystal = sine(t, 880.0 * (1 + 0.005 * lfo_med)) * 0.08 * (0.5 + 0.5 * lfo_slow)
        crystal += sine(t, 1320.0) * 0.04 * max(0, lfo_med)

        sample = pad * pad_vol + melody + drone + crystal
        sample = soft_clip(sample, 0.85)

        # Légère différence L/R pour largeur stéréo
        L.append(sample + crystal * 0.1)
        R.append(sample - crystal * 0.1)

    L = crossfade_loop(L, xfade)
    R = crossfade_loop(R, xfade)
    return L, R


def gen_music_run_intro(duration: float = 20.0) -> tuple:
    """
    Run début : 110 BPM, rythme solide, basse 60 Hz, mélodie 220 Hz, espace aéré.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    xfade = int(0.5 * rate)
    L, R = [], []

    bpm = 110.0
    beat = 60.0 / bpm  # ~0.545 s
    # Mélodie Am (A3, C4, E4, G4)
    melody_seq = [220.0, 261.63, 329.63, 392.0, 329.63, 261.63, 220.0, 246.94]

    for i in range(n):
        t = i / rate
        t_beat = t % beat
        beat_idx = int(t / beat)

        # Kick à chaque mesure (4 beats) en beat 0 et 2
        kick_beat = beat_idx % 4
        t_in_beat = t_beat
        kick = 0.0
        if kick_beat in (0, 2):
            kick = osc_kick(t_in_beat, dur=0.18) * 0.6
        # Snare sur beats 1 et 3
        snare = 0.0
        if kick_beat in (1, 3):
            snare = osc_snare(t_in_beat, dur=0.10) * 0.4
        # Hi-hat croches (tous les 0.5 beat)
        t_hh = (t % (beat * 0.5))
        hihat = osc_hihat(t_hh, dur=0.04) * 0.25

        # Basse
        bass = osc_bass(t, 60.0, drive=1.1) * 0.3
        bass += osc_bass(t, 120.0, drive=0.8) * 0.1

        # Mélodie
        mel_note_dur = beat * 2
        mel_idx = int(t / mel_note_dur) % len(melody_seq)
        t_in_mel = t % mel_note_dur
        mel_env = envelope_adsr(t_in_mel, mel_note_dur, 0.02, 0.1, 0.7, mel_note_dur * 0.3)
        melody = sine(t, melody_seq[mel_idx]) * mel_env * 0.2

        # Pad léger
        pad = osc_pad(t, [110.0, 164.81, 220.0], detune_hz=1.0) * 0.12

        sample = kick + snare + hihat + bass + melody + pad
        sample = soft_clip(sample, 0.88)

        L.append(sample + hihat * 0.05)
        R.append(sample - hihat * 0.05)

    L = crossfade_loop(L, xfade)
    R = crossfade_loop(R, xfade)
    return L, R


def gen_music_run_mid(duration: float = 20.0) -> tuple:
    """
    Run milieu : même base que intro + couche basse 55 Hz plus présente, percussion saturée.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    xfade = int(0.5 * rate)
    L, R = [], []

    bpm = 110.0
    beat = 60.0 / bpm
    melody_seq = [220.0, 261.63, 329.63, 392.0, 329.63, 261.63, 220.0, 246.94]
    arp_notes  = [440.0, 523.25, 659.25, 784.0]

    for i in range(n):
        t = i / rate
        t_beat = t % beat
        beat_idx = int(t / beat)
        kick_beat = beat_idx % 4

        kick = osc_kick(t_beat, dur=0.18) * 0.65 if kick_beat in (0, 2) else 0.0
        snare = osc_snare(t_beat, dur=0.10) * 0.5 if kick_beat in (1, 3) else 0.0
        hihat = osc_hihat(t % (beat * 0.5), dur=0.04) * 0.3

        # Basse plus présente + sub 55 Hz
        bass = osc_bass(t, 55.0, drive=1.4) * 0.35
        bass += osc_bass(t, 110.0, drive=1.0) * 0.15

        # Mélodie
        mel_note_dur = beat * 2
        mel_idx = int(t / mel_note_dur) % len(melody_seq)
        t_in_mel = t % mel_note_dur
        mel_env = envelope_adsr(t_in_mel, mel_note_dur, 0.02, 0.1, 0.7, mel_note_dur * 0.3)
        melody = sine(t, melody_seq[mel_idx]) * mel_env * 0.18

        # Arpège ajouté en couche
        arp = osc_arp(t, arp_notes, bpm * 2, note_div=0.25) * 0.12

        # Pad Aether plus présent
        pad = osc_pad(t, [110.0, 164.81, 220.0, 330.0], detune_hz=2.0) * 0.15

        lfo = pulse_lfo(t, 0.4)
        drone = osc_bass(t, 55.0) * 0.08 * (0.5 + 0.5 * lfo)

        sample = kick + snare + hihat + bass + melody + arp + pad + drone
        sample = soft_clip(sample, 0.82)

        L.append(sample + arp * 0.08)
        R.append(sample - arp * 0.08)

    L = crossfade_loop(L, xfade)
    R = crossfade_loop(R, xfade)
    return L, R


def gen_music_run_intense(duration: float = 20.0) -> tuple:
    """
    Run intense : densité maximale, basse 55 Hz saturée, arpège rapide 880-1100-1320 Hz, drone grave.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    xfade = int(0.5 * rate)
    L, R = [], []

    bpm = 110.0
    beat = 60.0 / bpm
    melody_seq = [220.0, 246.94, 261.63, 293.66, 329.63, 293.66, 261.63, 246.94]
    arp_notes  = [880.0, 1046.5, 1174.66, 1318.51, 1174.66, 1046.5]
    lfo_drive  = 0.0

    for i in range(n):
        t = i / rate
        t_beat = t % beat
        beat_idx = int(t / beat)
        kick_beat = beat_idx % 4

        # Double kick pour densité
        kick = 0.0
        if kick_beat == 0:
            kick = osc_kick(t_beat, dur=0.16) * 0.7
        elif kick_beat == 2:
            kick = osc_kick(t_beat, dur=0.16) * 0.65
        elif kick_beat == 1 and t_beat < beat * 0.5:
            kick = osc_kick(t % (beat * 0.5), dur=0.12) * 0.4

        snare = osc_snare(t_beat, dur=0.12) * 0.6 if kick_beat in (1, 3) else 0.0
        hihat = osc_hihat(t % (beat * 0.25), dur=0.03) * 0.35  # doubles croches

        # Basse saturée
        bass = osc_bass(t, 55.0, drive=1.8) * 0.4
        bass += osc_bass(t, 110.0, drive=1.4) * 0.18
        bass = soft_clip(bass, 0.65)

        # Mélodie (tension)
        mel_note_dur = beat * 1
        mel_idx = int(t / mel_note_dur) % len(melody_seq)
        t_in_mel = t % mel_note_dur
        mel_env = envelope_adsr(t_in_mel, mel_note_dur, 0.01, 0.08, 0.6, mel_note_dur * 0.25)
        melody = sine(t, melody_seq[mel_idx]) * mel_env * 0.15

        # Arpège rapide hautes fréquences
        arp = osc_arp(t, arp_notes, bpm * 4, note_div=0.125) * 0.14

        # Drone grave permanent + LFO
        lfo = pulse_lfo(t, 0.2)
        drone = osc_bass(t, 27.5, drive=2.0) * 0.12 * (0.6 + 0.4 * abs(lfo))

        # Pad Aether saturé
        pad = osc_pad(t, [220.0, 293.66, 440.0, 659.25], detune_hz=3.0) * 0.1

        sample = kick + snare + hihat + bass + melody + arp + drone + pad
        sample = soft_clip(sample, 0.75)

        L.append(sample + arp * 0.1)
        R.append(sample - arp * 0.1)

    L = crossfade_loop(L, xfade)
    R = crossfade_loop(R, xfade)
    return L, R


def gen_music_hub(duration: float = 25.0) -> tuple:
    """
    Hub : calme 85 BPM, accord majeur Do (C-E-G), sentiment de sécurité.
    264 Hz (C4) + 330 Hz (E4) + 396 Hz (G4).
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    xfade = int(0.5 * rate)
    L, R = [], []

    bpm = 85.0
    beat = 60.0 / bpm
    # C major : C4=261.63, E4=329.63, G4=392.0, C5=523.25
    pad_freqs = [261.63, 329.63, 392.0, 523.25]
    # Mélodie douce
    mel_seq = [261.63, 329.63, 392.0, 329.63, 261.63, 293.66, 261.63, 246.94]

    for i in range(n):
        t = i / rate
        lfo_slow = pulse_lfo(t, 0.2)
        lfo_med  = pulse_lfo(t, 0.7)

        # Pad majeur doux
        pad = osc_pad(t, [f * (1 + 0.002 * lfo_slow) for f in pad_freqs], detune_hz=1.0) * 0.3

        # Mélodie calme
        mel_note_dur = beat * 2
        mel_idx = int(t / mel_note_dur) % len(mel_seq)
        t_in_mel = t % mel_note_dur
        mel_env = envelope_adsr(t_in_mel, mel_note_dur, 0.05, 0.15, 0.65, mel_note_dur * 0.4)
        melody = sine(t, mel_seq[mel_idx] * (1 + 0.003 * lfo_slow)) * mel_env * 0.22

        # Basse douce
        t_beat = t % beat
        beat_idx = int(t / beat)
        bass_active = (beat_idx % 4) in (0,)
        bass = osc_bass(t, 130.81, drive=0.7) * 0.12 if bass_active else 0.0

        # Accents cristallins Aether légers
        crystal = sine(t, 1046.5) * 0.06 * max(0, lfo_med) * (0.3 + 0.7 * pulse_lfo(t, 0.15))

        # Percussion très légère (bossa nova feel)
        t_hh = t % (beat * 0.5)
        hihat = osc_hihat(t_hh, dur=0.03) * 0.12

        sample = pad + melody + bass + crystal + hihat
        sample = soft_clip(sample, 0.85)

        L.append(sample + crystal * 0.08)
        R.append(sample - crystal * 0.08 + pad * 0.02)

    L = crossfade_loop(L, xfade)
    R = crossfade_loop(R, xfade)
    return L, R


def gen_music_stinger_victory(duration: float = 4.0) -> tuple:
    """
    Stinger victoire : accord majeur ascendant C-E-G-C, 4 secondes, lumineux.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    L, R = [], []

    # Arpège montant C4-E4-G4-C5 (une note par seconde, puis résonance)
    notes = [261.63, 329.63, 392.0, 523.25]
    note_dur = duration / (len(notes) + 1)

    for i in range(n):
        t = i / rate
        note_idx = min(int(t / note_dur), len(notes) - 1)
        t_in_note = t % note_dur
        freq = notes[note_idx]
        env = envelope_adsr(t_in_note, note_dur, 0.01, 0.1, 0.7, note_dur * 0.5)

        # Note principale
        v = sine(t, freq) * 0.4 * env
        # Harmoniques Aether (octave + quinte)
        v += sine(t, freq * 2) * 0.15 * env
        v += sine(t, freq * 1.5) * 0.1 * env

        # Reverb légère simulée (cosinus décalé)
        reverb_decay = math.exp(-t_in_note * 2.0)
        v += sine(t + 0.03, freq) * 0.08 * reverb_decay

        # Shimmer cristallin
        shimmer = sine(t, freq * 4) * 0.05 * env

        # Fade out final
        fade_out = max(0.0, 1.0 - (t / duration) ** 3)
        v = (v + shimmer) * fade_out

        L.append(v + shimmer * 0.1)
        R.append(v - shimmer * 0.1)

    return L, R


def gen_music_stinger_death(duration: float = 3.0) -> tuple:
    """
    Stinger mort : accord mineur descendant, sine grave 110 Hz → 55 Hz, 3 secondes.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    L, R = [], []

    for i in range(n):
        t = i / rate
        progress = t / duration  # 0 → 1

        # Fréquence descendante : 110 Hz → 55 Hz (glissando)
        freq = 110.0 * math.exp(-math.log(2) * progress)

        # Am chord descendant : A2, C3, E3
        chord_freqs = [freq, freq * 1.189, freq * 1.498]
        v = sum(sine(t, f) * 0.25 for f in chord_freqs)

        # Harmoniques métalliques (distorsion)
        metal = sine(t, freq * 3.5) * 0.08 * (1 - progress)
        v += metal

        # Enveloppe globale : sustain puis long decay
        env = envelope_adsr(t, duration, 0.05, 0.2, 0.5, duration * 0.55)
        v *= env

        # Bruit grave de chute mécanique (bref, en début)
        crash_env = math.exp(-t * 8) if t < 0.5 else 0.0
        crash = noise() * 0.15 * crash_env

        v = soft_clip(v + crash, 0.88)

        L.append(v + metal * 0.05)
        R.append(v - metal * 0.05)

    return L, R


# ---------------------------------------------------------------------------
# SFX
# ---------------------------------------------------------------------------

def gen_sfx_laser_small(duration: float = 0.15) -> list:
    """Canon à Impulsions : claquement électrique sec, sweep descendant 800→300 Hz."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 800 * math.exp(-p * math.log(800 / 300))
        env = math.exp(-t * 20) * (1 - p * 0.3)
        # Composante électrique haute
        elec = sine(t, freq * 3) * 0.2 * math.exp(-t * 40)
        v = (sine(t, freq) * 0.6 + elec + noise() * 0.15 * math.exp(-t * 30)) * env
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_plasma_swing(duration: float = 0.30) -> list:
    """Lame Plasma : swoosh grave 300→80 Hz + buzz électrique."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Swoosh : fréquence descendante
        freq = 300 * math.exp(-p * math.log(300 / 80))
        sweep_env = math.exp(-p * 3) * math.sin(math.pi * p)  # bosse en milieu
        # Buzz électrique
        buzz_freq = 120 + 80 * p
        buzz = sine(t, buzz_freq) * 0.3 * math.exp(-t * 5)
        v = sine(t, freq) * 0.5 * sweep_env + buzz + noise() * 0.1 * sweep_env
        out.append(soft_clip(v, 0.9))
    return out


def gen_sfx_overload_pulse(duration: float = 0.40) -> list:
    """Champ de Surcharge : onde de choc sourde + basse fréquence thump."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Thump basse fréquence
        freq_low = 80 * math.exp(-t * 5)
        thump = sine(t, freq_low) * 0.7 * math.exp(-t * 8)
        # Onde de choc (bruit court initial)
        shock = noise() * math.exp(-t * 25) * 0.4
        # Résonance "zone d'effet"
        reverb = sine(t + 0.02, 160) * 0.15 * math.exp(-t * 4) * math.sin(math.pi * p * 0.7)
        v = thump + shock + reverb
        out.append(soft_clip(v, 0.88))
    return out


def gen_sfx_drone_loop(duration: float = 1.0) -> list:
    """Drone en orbite : bourdonnement mécanique grave 80 Hz + harmonique sifflante."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        lfo = 0.5 + 0.5 * math.sin(2 * math.pi * 3.7 * t)
        # Bourdonnement moteur
        motor = osc_bass(t, 80.0, drive=0.9) * 0.35
        # Sifflement harmonique
        whistle = sine(t, 320 + 20 * lfo) * 0.12
        # Vibration mécanique
        vib = sine(t, 160 + 5 * lfo) * 0.18
        v = motor + whistle + vib
        out.append(soft_clip(v, 0.85))
    # Crossfade pour loop seamless
    xfade = int(0.05 * rate)
    return crossfade_loop(out, xfade)


def gen_sfx_sentinel_shoot(duration: float = 0.20) -> list:
    """Tir Sentinelle ennemi : plus lourd/menaçant que le tir joueur, sweep 400→150 Hz."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 400 * math.exp(-p * math.log(400 / 150))
        env = math.exp(-t * 12)
        # Son plus grave/dense
        v  = sine(t, freq) * 0.55
        v += sine(t, freq * 0.5) * 0.25  # sub-octave
        v += noise() * 0.2 * math.exp(-t * 20)
        out.append(soft_clip(v * env, 0.85))
    return out


def gen_sfx_rail_shoot(duration: float = 0.50) -> list:
    """Rail Surchargé : trio de claquements à 0.12 s d'intervalle, son puissant."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    shot_times = [0.0, 0.12, 0.24]
    for i in range(n):
        t = i / rate
        v = 0.0
        for st in shot_times:
            dt = t - st
            if 0 <= dt < 0.18:
                freq = 1200 * math.exp(-dt * 15)
                env = math.exp(-dt * 25)
                v += (sine(dt, freq) * 0.5 + noise() * 0.3 * math.exp(-dt * 40)) * env
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_fusion_activate(duration: float = 0.80) -> list:
    """Activation Lame à Fusion : montée en fréquence + résonance métallique → bourdonnement Aether."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Sweep montant 100 Hz → 440 Hz
        freq = 100 * math.exp(p * math.log(440 / 100))
        env_ramp = min(1.0, p * 3)  # montée rapide
        env_decay = math.exp(-(p - 0.5) * 4) if p > 0.5 else 1.0  # sustain puis légère descente
        v  = sine(t, freq) * 0.4 * env_ramp * env_decay
        # Résonance métallique
        v += sine(t, freq * 2.76) * 0.15 * math.exp(-t * 2)  # note inharmonique (métal)
        # Stabilisation en bourdonnement à la fin
        stability = min(1.0, max(0.0, (p - 0.6) * 5))
        v += osc_pad(t, [440.0, 660.0], detune_hz=3.0) * 0.12 * stability
        out.append(soft_clip(v, 0.88))
    return out


def gen_sfx_fusion_loop(duration: float = 1.0) -> list:
    """Boucle Lame à Fusion active : sifflement d'énergie doux, 440 Hz + harmoniques."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        lfo = 0.5 + 0.5 * math.sin(2 * math.pi * 2.1 * t)
        v  = sine(t, 440 + 5 * lfo) * 0.18
        v += sine(t, 660 + 3 * lfo) * 0.08
        v += sine(t, 880) * 0.04
        out.append(v)
    return crossfade_loop(out, int(0.05 * rate))


def gen_sfx_swarm_die(duration: float = 0.20) -> list:
    """Mort Essaim de Rouille : craquement métallique bref + debris."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Craquement : bruit blanc filtré + tonalité courte
        crack = noise() * math.exp(-t * 30) * 0.6
        # Débris : quelques tonalités aléatoires descendantes
        debris = sine(t, 800 * math.exp(-t * 8)) * 0.25 * math.exp(-t * 15)
        debris += sine(t, 1200 * math.exp(-t * 10)) * 0.15 * math.exp(-t * 20)
        v = crack + debris
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_drone_die(duration: float = 0.25) -> list:
    """Mort Drone Corrompu : explosion électrique + bip d'arrêt système."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Explosion électrique initiale
        elec = (noise() * 0.5 + sine(t, 400) * 0.3) * math.exp(-t * 20)
        # Bip d'arrêt système descendant
        beep_freq = 1200 * math.exp(-t * 5) if t < 0.15 else 0
        beep = sine(t, beep_freq) * 0.35 * math.exp(-t * 10)
        v = elec + beep
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_sentinel_die(duration: float = 0.40) -> list:
    """Mort Sentinelle : explosion mécanique plus ample avec reverb."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Explosion principale
        explosion = (noise() * 0.6 + sine(t, 200) * 0.25) * math.exp(-t * 8)
        # Réverbération simulée
        reverb_delay = 0.05
        t_rev = t - reverb_delay
        reverb = 0.0
        if t_rev > 0:
            reverb = (noise() * 0.2 + sine(t_rev, 150) * 0.15) * math.exp(-t_rev * 12) * 0.4
        # Métal vibrant
        metal = sine(t, 600 * math.exp(-t * 4)) * 0.2 * math.exp(-t * 6)
        v = explosion + reverb + metal
        out.append(soft_clip(v, 0.88))
    return out


def gen_sfx_colossus_die(duration: float = 0.80) -> list:
    """Mort Colosse Greffé : impact très lourd + résonance grave long decay."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Impact initial très lourd : sine grave + bruit
        impact_freq = 80 * math.exp(-t * 3)
        impact = sine(t, impact_freq) * 0.7 * math.exp(-t * 5)
        # Bruit de chute massive
        crash = noise() * math.exp(-t * 12) * 0.5
        # Résonance grave longue
        resonance = sine(t, 55) * 0.3 * math.exp(-t * 1.5) * (0.3 + 0.7 * math.sin(math.pi * p))
        # Flash Aether (harmonique violette - quelques ms)
        aether = sine(t, 880) * 0.15 * math.exp(-t * 40) if t < 0.05 else 0.0
        v = impact + crash + resonance + aether
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_sentinel_projectile(duration: float = 0.15) -> list:
    """Projectile Sentinelle en vol : sifflement grave."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 500 - 200 * p  # léger Doppler descendant
        env = math.sin(math.pi * p)  # bosse
        v = sine(t, freq) * 0.4 * env + noise() * 0.1 * env
        out.append(v)
    return out


def gen_sfx_xp_collect(duration: float = 0.10) -> list:
    """Collecte orbe XP : son cristallin court, "ting" Aether."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Tonalité cristalline ascendante brève
        freq = 1200 + 400 * p
        env = math.exp(-t * 30) * math.sin(math.pi * p * 0.8)
        v  = sine(t, freq) * 0.45 * env
        v += sine(t, freq * 2) * 0.15 * env
        out.append(v)
    return out


def gen_sfx_core_collect(duration: float = 0.30) -> list:
    """Collecte Noyau d'Aether : plus marqué, tonalité mystique violette."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Accord mystique : quinte + septième
        freqs = [440.0, 660.0, 770.0]  # A4, E5, Bb5 (couleur violette/mystique)
        env = envelope_adsr(t, duration, 0.01, 0.08, 0.5, duration * 0.4)
        v = 0.0
        for f in freqs:
            v += sine(t, f) * 0.25
        # Shimmer Aether
        v += sine(t, 1760) * 0.08 * math.exp(-t * 10)
        out.append(v * env)
    return out


def gen_sfx_levelup(duration: float = 0.50) -> list:
    """Level-up : son ascendant marquant, harmoniques Aether."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Fréquence montante rapide
        freq = 440 + 440 * p
        env  = envelope_adsr(t, duration, 0.01, 0.1, 0.7, duration * 0.35)
        v    = sine(t, freq) * 0.45 * env
        v   += sine(t, freq * 2) * 0.2 * env  # octave
        v   += sine(t, freq * 1.5) * 0.15 * env  # quinte
        # Shimmer cristallin Aether
        shimmer = sine(t, freq * 4) * 0.1 * env
        out.append(soft_clip(v + shimmer, 0.88))
    return out


def gen_sfx_card_select(duration: float = 0.15) -> list:
    """Sélection carte level-up : clic net + bref accent cristallin."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        # Clic sec court
        click = noise() * math.exp(-t * 80) * 0.5
        # Accent cristallin
        freq = 1400 - 400 * (t / duration)
        tone = sine(t, freq) * math.exp(-t * 25) * 0.35
        out.append(click + tone)
    return out


def gen_sfx_fusion_evolve(duration: float = 1.80) -> list:
    """Fusion/évolution : impact sourd + flash silence + explosion Aether + résonance longue."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration

        # Phase 1 (0–0.2s) : impact sourd
        impact = 0.0
        if t < 0.2:
            impact = (sine(t, 80) * 0.8 + noise() * 0.5) * math.exp(-t * 20)

        # Phase 2 (0.2–0.35s) : quasi-silence (flash blanc visuel)
        silence_fade = 0.0

        # Phase 3 (0.35s+) : explosion d'énergie Aether
        aether_burst = 0.0
        if t >= 0.35:
            ta = t - 0.35
            # Harmoniques Aether montantes
            aether_freqs = [440.0, 660.0, 880.0, 1100.0, 1320.0]
            burst_env = math.exp(-ta * 3) * (1 + 0.5 * math.sin(math.pi * ta * 2))
            aether_burst = sum(sine(ta, f) * 0.15 for f in aether_freqs) * burst_env
            # Bruit blanc bref initial
            aether_burst += noise() * math.exp(-ta * 15) * 0.4
            # Résonance longue grave
            aether_burst += sine(ta, 110) * 0.25 * math.exp(-ta * 1.2)

        v = impact + aether_burst
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_player_hit(duration: float = 0.20) -> list:
    """Joueur touché : impact court grave, signal de danger net."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        # Impact grave
        freq = 200 * math.exp(-t * 8)
        env  = math.exp(-t * 15)
        v    = sine(t, freq) * 0.5 * env
        v   += noise() * 0.25 * math.exp(-t * 25)  # texture
        out.append(soft_clip(v, 0.85))
    return out


def gen_sfx_player_die(duration: float = 0.90) -> list:
    """Mort joueur : système qui s'éteint, accord mineur descendant."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Glissando descendant (système qui s'éteint)
        freq = 330 * math.exp(-p * math.log(330 / 55))
        env = envelope_adsr(t, duration, 0.02, 0.15, 0.5, duration * 0.5)
        # Accord mineur
        v  = sine(t, freq) * 0.35 * env
        v += sine(t, freq * 1.189) * 0.2 * env  # tierce mineure
        # Distorsion de fin (bruit de système)
        glitch = noise() * 0.15 * math.exp(-t * 3) * (p ** 2)
        # Signal d'alerte court
        alert = sine(t, 800) * math.exp(-t * 15) * 0.2 if t < 0.1 else 0.0
        out.append(soft_clip(v + glitch + alert, 0.88))
    return out


def gen_sfx_ui_button(duration: float = 0.10) -> list:
    """Clic bouton générique : sec, net, neutre."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        v = (noise() * 0.4 + sine(t, 1000) * 0.4) * math.exp(-t * 60)
        out.append(v)
    return out


def gen_sfx_ui_purchase(duration: float = 0.30) -> list:
    """Achat Hub : validation positive, léger accent Aether."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Deux notes ascendantes rapides (do-mi)
        freq1, freq2 = 523.25, 659.25
        if p < 0.4:
            freq = freq1
            env = envelope_adsr(t, duration * 0.4, 0.01, 0.05, 0.6, duration * 0.15)
        else:
            freq = freq2
            ta = t - duration * 0.4
            env = envelope_adsr(ta, duration * 0.6, 0.01, 0.05, 0.6, duration * 0.3)
        v  = sine(t, freq) * 0.35 * env
        v += sine(t, freq * 2) * 0.12 * env  # shimmer Aether
        out.append(v)
    return out


def gen_sfx_ui_victory(duration: float = 0.50) -> list:
    """Victoire UI : accord majeur court (version SFX du stinger)."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    freqs = [523.25, 659.25, 783.99]  # C5, E5, G5
    for i in range(n):
        t = i / rate
        env = envelope_adsr(t, duration, 0.01, 0.1, 0.6, duration * 0.4)
        v = sum(sine(t, f) * 0.25 for f in freqs) * env
        v += sine(t, 1046.5) * 0.1 * math.exp(-t * 8)  # shimmer
        out.append(v)
    return out


def gen_sfx_ui_death(duration: float = 0.50) -> list:
    """Mort UI : accord mineur court descendant."""
    rate = SAMPLE_RATE
    n = int(duration * rate)
    out = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 220 * math.exp(-p * math.log(220 / 110))
        env  = envelope_adsr(t, duration, 0.01, 0.1, 0.5, duration * 0.45)
        v    = sine(t, freq) * 0.4 * env
        v   += sine(t, freq * 1.189) * 0.2 * env
        v   += noise() * 0.08 * math.exp(-t * 10)
        out.append(soft_clip(v, 0.85))
    return out


# ---------------------------------------------------------------------------
# Moteur d'exécution
# ---------------------------------------------------------------------------

def main():
    random.seed(42)  # reproductibilité

    print("=" * 60)
    print("Chimera Protocol — Génération audio synthétisée (CC0)")
    print("=" * 60)

    # --- MUSIQUES (stéréo) ---
    print("\n[MUSIQUES — synthèse procédurale stéréo]")

    music_tasks = [
        ("music_menu",             gen_music_menu,            30.0),
        ("music_run_intro",        gen_music_run_intro,       20.0),
        ("music_run_mid",          gen_music_run_mid,         20.0),
        ("music_run_intense",      gen_music_run_intense,     20.0),
        ("music_hub",              gen_music_hub,             25.0),
        ("music_stinger_victory",  gen_music_stinger_victory,  4.0),
        ("music_stinger_death",    gen_music_stinger_death,    3.0),
    ]

    for name, gen_func, dur in music_tasks:
        path = os.path.join(MUSIC_DIR, f"{name}.wav")
        L, R = gen_func(dur)
        write_wav(path, L, R)

    # --- SFX (mono) ---
    print("\n[SFX — synthèse procédurale mono]")

    sfx_tasks = [
        # Armes
        ("sfx_weapon_impulse_shoot",   gen_sfx_laser_small,          0.15),
        ("sfx_weapon_plasma_swing",    gen_sfx_plasma_swing,         0.30),
        ("sfx_weapon_overload_pulse",  gen_sfx_overload_pulse,       0.40),
        ("sfx_weapon_drone_loop",      gen_sfx_drone_loop,           1.00),
        ("sfx_weapon_sentinel_shoot",  gen_sfx_sentinel_shoot,       0.20),
        ("sfx_weapon_rail_shoot",      gen_sfx_rail_shoot,           0.50),
        ("sfx_weapon_fusion_activate", gen_sfx_fusion_activate,      0.80),
        ("sfx_weapon_fusion_loop",     gen_sfx_fusion_loop,          1.00),
        # Ennemis
        ("sfx_enemy_swarm_die",             gen_sfx_swarm_die,           0.20),
        ("sfx_enemy_drone_die",             gen_sfx_drone_die,           0.25),
        ("sfx_enemy_sentinel_die",          gen_sfx_sentinel_die,        0.40),
        ("sfx_enemy_colossus_die",          gen_sfx_colossus_die,        0.80),
        ("sfx_enemy_sentinel_projectile",   gen_sfx_sentinel_projectile, 0.15),
        # Gameplay
        ("sfx_xp_collect",    gen_sfx_xp_collect,   0.10),
        ("sfx_core_collect",  gen_sfx_core_collect,  0.30),
        ("sfx_levelup",       gen_sfx_levelup,       0.50),
        ("sfx_card_select",   gen_sfx_card_select,   0.15),
        ("sfx_fusion_evolve", gen_sfx_fusion_evolve, 1.80),
        # Joueur
        ("sfx_player_hit", gen_sfx_player_hit, 0.20),
        ("sfx_player_die", gen_sfx_player_die, 0.90),
        # UI
        ("sfx_ui_button",   gen_sfx_ui_button,   0.10),
        ("sfx_ui_purchase", gen_sfx_ui_purchase, 0.30),
        ("sfx_ui_victory",  gen_sfx_ui_victory,  0.50),
        ("sfx_ui_death",    gen_sfx_ui_death,    0.50),
    ]

    for name, gen_func, dur in sfx_tasks:
        path = os.path.join(SFX_DIR, f"{name}.wav")
        samples = gen_func(dur)
        write_wav(path, samples)

    print("\n" + "=" * 60)
    print("Génération terminée.")
    print()
    print("STATUT : tous les fichiers sont des PLACEHOLDERS SYNTHÉTISÉS.")
    print("  - Musiques : synthèse procédurale Python (CC0, domaine public)")
    print("  - SFX      : synthèse procédurale Python (CC0, domaine public)")
    print("  - Référence : packs Kenney CC0 consultés pour le design sonore,")
    print("    non inclus directement (format OGG incompatible avec les .import WAV).")
    print()
    print("Pour remplacer par des assets définitifs :")
    print("  1. Convertir les OGG Kenney en WAV 44100Hz 16-bit (ffmpeg requis)")
    print("  2. Ou commissionner un compositeur pour la bande-son finale")
    print("  3. Voir docs/AUDIO_GUIDE.md §5 pour les sources CC0")
    print("=" * 60)


if __name__ == "__main__":
    main()
