"""
Chimera Protocol — Audio v2 (synthese amelioree, CC0/domaine public)

Remplace generate_music_synth.py avec une qualite sonore nettement superieure :
- Filtres IIR (passe-bas resonant, passe-haut) en pur Python
- Reverb algorithmique (reseau de delais de Schroeder)
- Chorus / detune stereo plus riche
- Conception musicale : progressions Am-Dm-Em-F (ruines) et rythmes plus expressifs
- SFX : formes d'onde variees (carre, scie, pulsar), enveloppes precises

Licence : CC0 / Domaine public (synthese 100% originale, aucun sample tiers)

Usage :
  C:/Users/drang/AppData/Local/Programs/Python/Python313/python.exe tools/generate_audio_v2.py
"""

import wave
import math
import struct
import random
import os

SAMPLE_RATE = 44100
TAU = 2.0 * math.pi
PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MUSIC_DIR = os.path.join(PROJECT_ROOT, "assets", "audio", "music")
SFX_DIR   = os.path.join(PROJECT_ROOT, "assets", "audio", "sfx")

random.seed(7)  # reproductibilite

# ---------------------------------------------------------------------------
# Utilitaires bas niveau
# ---------------------------------------------------------------------------

def clamp16(v):
    return max(-32768, min(32767, int(v * 32767)))

def write_wav(path, samples_l, samples_r=None, rate=SAMPLE_RATE):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    stereo = samples_r is not None
    channels = 2 if stereo else 1
    n = len(samples_l)
    with wave.open(path, "w") as f:
        f.setnchannels(channels)
        f.setsampwidth(2)
        f.setframerate(rate)
        frames = bytearray()
        for i in range(n):
            frames += struct.pack("<h", clamp16(samples_l[i]))
            if stereo:
                frames += struct.pack("<h", clamp16(samples_r[i]))
        f.writeframes(bytes(frames))
    rel = os.path.relpath(path, PROJECT_ROOT)
    dur = n / rate
    print(f"  [OK] {rel}  ({dur:.2f}s {'stereo' if stereo else 'mono'})")


# ---------------------------------------------------------------------------
# Formes d'onde de base
# ---------------------------------------------------------------------------

def sin_wave(t, freq, phase=0.0):
    return math.sin(TAU * freq * t + phase)

def saw_wave(t, freq):
    """Scie montante [-1,1]."""
    p = (t * freq) % 1.0
    return 2.0 * p - 1.0

def square_wave(t, freq, duty=0.5):
    """Carre avec rapport cyclique."""
    p = (t * freq) % 1.0
    return 1.0 if p < duty else -1.0

def tri_wave(t, freq):
    """Triangle [-1,1]."""
    p = (t * freq) % 1.0
    return 4.0 * p - 1.0 if p < 0.5 else 3.0 - 4.0 * p

def noise_sample():
    return random.uniform(-1.0, 1.0)

def soft_clip(v, threshold=0.75):
    if abs(v) <= threshold:
        return v
    sign = 1.0 if v > 0 else -1.0
    excess = abs(v) - threshold
    return sign * (threshold + excess / (1.0 + excess / (1.0 - threshold + 0.001)))

def tanh_clip(v, drive=1.0):
    """Saturation tanh douce."""
    x = v * drive
    return math.tanh(x) / max(0.001, math.tanh(drive))


# ---------------------------------------------------------------------------
# Filtres IIR simples (traitement sequentiel buffer entier)
# ---------------------------------------------------------------------------

def lpf_1pole(samples, cutoff_hz, rate=SAMPLE_RATE):
    """Filtre passe-bas 1 pole (6 dB/oct) — tres leger, pour adoucir les sifflets."""
    alpha = 1.0 - math.exp(-TAU * cutoff_hz / rate)
    out = [0.0] * len(samples)
    y = 0.0
    for i, x in enumerate(samples):
        y += alpha * (x - y)
        out[i] = y
    return out

def lpf_resonant(samples, cutoff_hz, resonance=0.5, rate=SAMPLE_RATE):
    """
    Butterworth-like 2 poles resonant (12 dB/oct).
    resonance in [0, ~0.99] — au-dela instable.
    """
    omega = TAU * cutoff_hz / rate
    cos_o = math.cos(omega)
    sin_o = math.sin(omega)
    alpha = sin_o / (2.0 * max(0.01, (1.0 - resonance) * 2.0))
    b0 = (1.0 - cos_o) / 2.0
    b1 =  1.0 - cos_o
    b2 = b0
    a0 =  1.0 + alpha
    a1 = -2.0 * cos_o
    a2 =  1.0 - alpha
    b0 /= a0; b1 /= a0; b2 /= a0; a1 /= a0; a2 /= a0
    out = [0.0] * len(samples)
    x1 = x2 = y1 = y2 = 0.0
    for i, x0 in enumerate(samples):
        y0 = b0*x0 + b1*x1 + b2*x2 - a1*y1 - a2*y2
        out[i] = y0
        x2, x1 = x1, x0
        y2, y1 = y1, y0
    return out

def hpf_1pole(samples, cutoff_hz, rate=SAMPLE_RATE):
    """Filtre passe-haut 1 pole — retire les basses indésirables."""
    alpha = math.exp(-TAU * cutoff_hz / rate)
    out = [0.0] * len(samples)
    y = prev_x = 0.0
    for i, x in enumerate(samples):
        y = alpha * (y + x - prev_x)
        out[i] = y
        prev_x = x
    return out


# ---------------------------------------------------------------------------
# Reverb algorithmique (Schroeder simplifie)
# ---------------------------------------------------------------------------

class SchroederReverb:
    """
    4 combs en parallele + 2 allpass en serie.
    Tres peu CPU en generation offline.
    """
    COMB_DELAYS_MS = [29.7, 37.1, 41.1, 43.7]
    COMB_GAINS     = [0.805, 0.827, 0.783, 0.764]
    AP_DELAYS_MS   = [5.0, 1.7]
    AP_GAIN        = 0.7

    def __init__(self, rate=SAMPLE_RATE):
        self._rate = rate
        self._combs  = [self._make_buf(d) for d in self.COMB_DELAYS_MS]
        self._aps    = [self._make_buf(d) for d in self.AP_DELAYS_MS]
        self._comb_pos = [0] * 4
        self._ap_pos   = [0] * 2

    def _make_buf(self, delay_ms):
        size = int(delay_ms * 0.001 * self._rate)
        return [0.0] * max(1, size)

    def process(self, samples, wet=0.25, dry=0.75):
        """Retourne buffer avec reverb mixee."""
        out = [0.0] * len(samples)
        for i, x in enumerate(samples):
            # Combs en parallele
            comb_sum = 0.0
            for c in range(4):
                buf = self._combs[c]
                pos = self._comb_pos[c]
                delayed = buf[pos]
                buf[pos] = x + delayed * self.COMB_GAINS[c]
                self._comb_pos[c] = (pos + 1) % len(buf)
                comb_sum += delayed
            sig = comb_sum * 0.25  # normalise 4 combs
            # Allpass en serie
            for a in range(2):
                buf = self._aps[a]
                pos = self._ap_pos[a]
                delayed = buf[pos]
                buf[pos] = sig + delayed * self.AP_GAIN
                self._ap_pos[a] = (pos + 1) % len(buf)
                sig = -sig + delayed
            out[i] = dry * x + wet * sig
        return out


# ---------------------------------------------------------------------------
# Delay stéréo simple (pour élargissement)
# ---------------------------------------------------------------------------

def stereo_delay(samples, delay_ms, feedback=0.3, rate=SAMPLE_RATE):
    """Retourne (left, right) avec delay sur R."""
    delay_samples = int(delay_ms * 0.001 * rate)
    buf = [0.0] * max(1, delay_samples)
    pos = 0
    left  = list(samples)
    right = [0.0] * len(samples)
    for i, x in enumerate(samples):
        delayed = buf[pos]
        buf[pos] = x + delayed * feedback
        pos = (pos + 1) % len(buf)
        right[i] = delayed
    return left, right


# ---------------------------------------------------------------------------
# Enveloppes et utilitaires musicaux
# ---------------------------------------------------------------------------

def adsr(t, dur, a, d, s_level, r_frac):
    """Enveloppe ADSR, r_frac = fraction de dur pour la release."""
    r = dur * r_frac
    sustain_dur = max(0, dur - a - d - r)
    if t < a:
        return t / a if a > 0 else 1.0
    t -= a
    if t < d:
        return 1.0 - (1.0 - s_level) * (t / d) if d > 0 else s_level
    t -= d
    if t < sustain_dur:
        return s_level
    t -= sustain_dur
    if t < r:
        return s_level * (1.0 - t / r) if r > 0 else 0.0
    return 0.0

def fade_ends(samples, fade_ms=30.0, rate=SAMPLE_RATE):
    """Fade in+out sur fade_ms millisecondes pour eviter les clics."""
    n_fade = int(fade_ms * 0.001 * rate)
    n = len(samples)
    out = list(samples)
    for i in range(min(n_fade, n)):
        f = i / n_fade
        out[i] *= f
        out[n - 1 - i] *= f
    return out

def crossfade_seamless(samples, xfade_ms=500.0, rate=SAMPLE_RATE):
    """Crossfade debut/fin pour boucle sans clic."""
    n_xf = int(xfade_ms * 0.001 * rate)
    n = len(samples)
    out = list(samples)
    for i in range(min(n_xf, n // 2)):
        a = i / n_xf
        out[i] = samples[i] * a + samples[n - n_xf + i] * (1.0 - a)
    return out

def lfo(t, freq_hz, offset=0.0):
    return math.sin(TAU * freq_hz * t + offset)

def freq_midi(note):
    """MIDI note -> Hz (A4=69=440Hz)."""
    return 440.0 * (2.0 ** ((note - 69) / 12.0))

# Gamme Am naturelle : A B C D E F G
AM_SCALE = [57, 59, 60, 62, 64, 65, 67, 69]  # notes MIDI

def note_hz(degree, octave_shift=0):
    """Degre dans gamme Am (0=A) avec decalage d'octave."""
    midi = AM_SCALE[degree % len(AM_SCALE)] + octave_shift * 12
    return freq_midi(midi)


# ---------------------------------------------------------------------------
# Modules de synthese musicale
# ---------------------------------------------------------------------------

def synth_pad(t, base_freqs, detune=2.0, lfo_depth=0.003, lfo_rate=0.3, vol=0.3):
    """Pad chaud : 2 oscillateurs scie legèrement desaccordes + LFO vibrato."""
    lfo_val = math.sin(TAU * lfo_rate * t)
    total = 0.0
    n = len(base_freqs)
    for i, f in enumerate(base_freqs):
        dt = detune * ((i - (n - 1) / 2.0) / max(1, n - 1))
        fr = f * (1 + lfo_depth * lfo_val) + dt
        # Melange scie + sinus (plus chaleureux)
        total += 0.6 * saw_wave(t, fr) + 0.4 * sin_wave(t, fr)
    return (total / n) * vol

def synth_bass(t, freq, drive=1.2, sub_level=0.4):
    """Basse analog : scie grave + sub sinus + saturation."""
    v  = 0.6 * saw_wave(t, freq)
    v += 0.3 * sin_wave(t, freq)
    v += sub_level * sin_wave(t, freq * 0.5)  # sub-octave
    return tanh_clip(v, drive)

def synth_lead(t, freq, env_val, detune=1.5):
    """Lead melodique : carre + sinus desaccorde."""
    v  = 0.5 * square_wave(t, freq, 0.45)
    v += 0.3 * sin_wave(t, freq + detune)
    v += 0.2 * sin_wave(t, freq * 2.0)
    return v * env_val

def synth_aether(t, freq, env_val, lfo_rate=0.7):
    """Timbre Aether crystallin : triangle haut + harmoniques impaires."""
    lfo_v = 0.5 + 0.5 * math.sin(TAU * lfo_rate * t)
    v  = 0.5 * sin_wave(t, freq)
    v += 0.3 * sin_wave(t, freq * 2.0)
    v += 0.15 * sin_wave(t, freq * 3.0) * lfo_v
    v += 0.05 * sin_wave(t, freq * 5.0) * (1 - lfo_v)
    return v * env_val

def drum_kick(dt, dur=0.18, punch=1.0):
    """Kick : sine descendant avec punch transient."""
    if dt > dur:
        return 0.0
    freq = 150.0 * math.exp(-dt * 22.0) + 30.0
    env  = math.exp(-dt * 10.0) * punch
    v    = sin_wave(dt, freq) * 0.7 + noise_sample() * 0.1 * math.exp(-dt * 60.0)
    return v * env

def drum_snare(dt, dur=0.12):
    """Caisse claire : bruit + tonalite."""
    if dt > dur:
        return 0.0
    env = math.exp(-dt * 22.0)
    v   = noise_sample() * 0.6 + sin_wave(dt, 220.0) * 0.3 + sin_wave(dt, 180.0) * 0.1
    return v * env

def drum_hihat(dt, dur=0.04, open_hat=False):
    """Hi-hat : bruit blanc filtre."""
    real_dur = dur * 5.0 if open_hat else dur
    if dt > real_dur:
        return 0.0
    env = math.exp(-dt * (8.0 if open_hat else 60.0))
    return noise_sample() * 0.25 * env

def drum_clap(dt, dur=0.08):
    """Clap : 3 bruits rapides."""
    v = 0.0
    for offset in [0.0, 0.008, 0.016]:
        d = dt - offset
        if 0 <= d < dur:
            v += noise_sample() * math.exp(-d * 35.0) * 0.4
    return v


# ---------------------------------------------------------------------------
# Arpégiateur
# ---------------------------------------------------------------------------

def arp_step(t, note_freqs, bpm, subdivisions=4):
    """Arpège : change de note toutes les (beat/subdivisions)."""
    beat_dur = 60.0 / bpm
    step_dur = beat_dur / subdivisions
    idx  = int(t / step_dur) % len(note_freqs)
    t_s  = t % step_dur
    env  = adsr(t_s, step_dur, 0.005, 0.05, 0.5, 0.4)
    return sin_wave(t, note_freqs[idx]) * env


# ---------------------------------------------------------------------------
# MUSIQUES
# ---------------------------------------------------------------------------

# --- menu ---

def gen_music_menu(duration=30.0):
    """
    Menu : ambiance ruines Aether.
    Harmonie Am - F - C - G (progression cinematique).
    BPM 72, pad large, melodie cristalline, basse discrete.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []

    bpm = 72.0
    beat = 60.0 / bpm  # 0.833 s
    measure = beat * 4

    # Accord par mesure : Am F C G (boucle 4 mesures)
    chord_freqs = [
        [note_hz(0, -1), note_hz(2, -1), note_hz(4, -1), note_hz(0)],   # Am
        [note_hz(5, -1), note_hz(0), note_hz(2), note_hz(5)],            # F
        [freq_midi(60-12), freq_midi(64-12), freq_midi(67-12), freq_midi(60)],  # C
        [freq_midi(67-12), freq_midi(71-12), freq_midi(74-12), freq_midi(67)],  # G
    ]
    # Melodie Am lente (1 note par beat)
    mel_notes_hz = [note_hz(0), note_hz(4), note_hz(2), note_hz(5),
                    note_hz(4), note_hz(2), note_hz(0,1), note_hz(5)]

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        chord_idx = int((t % (measure * 4)) / measure)
        freqs = chord_freqs[chord_idx]

        # Pad large
        pad = synth_pad(t, freqs, detune=1.8, lfo_depth=0.004, lfo_rate=0.25, vol=0.22)

        # Melodie Aether lente
        beat_in_cycle = int(t / beat) % len(mel_notes_hz)
        t_in_beat = t % beat
        mel_env = adsr(t_in_beat, beat, 0.08, 0.2, 0.6, 0.5)
        melody = synth_aether(t, mel_notes_hz[beat_in_cycle], mel_env * 0.18)

        # Basse discrete (uniquement sur les 1er et 3e beats de chaque mesure)
        beat_in_measure = int((t % measure) / beat)
        bass_freq = freqs[0]  # fondamentale de l'accord
        bass_env = adsr(t % beat, beat, 0.01, 0.15, 0.3, 0.5)
        bass_vol = 0.14 if beat_in_measure in (0, 2) else 0.0
        bass = synth_bass(t, bass_freq * 0.5, drive=0.9, sub_level=0.5) * bass_env * bass_vol

        # Pulse Aether haut (atmosphere)
        aether_hi = sin_wave(t, 1320.0) * 0.04 * max(0, lfo(t, 0.17))
        aether_hi += sin_wave(t, 880.0)  * 0.03 * max(0, lfo(t, 0.23, 1.1))

        raw = pad + melody + bass + aether_hi
        raw = soft_clip(raw, 0.82)
        buf.append(raw)

    # Reverb
    buf = rev.process(buf, wet=0.22, dry=0.78)

    # Stereo : pad L/R legerement decales
    L, R = stereo_delay(buf, delay_ms=18.0, feedback=0.15)
    for i in range(len(L)):
        L[i] = soft_clip(L[i], 0.92)
        R[i] = soft_clip(R[i], 0.92)

    L = crossfade_seamless(L, xfade_ms=600)
    R = crossfade_seamless(R, xfade_ms=600)
    return L, R


# --- run intro ---

def gen_music_run_intro(duration=30.0):
    """
    Run 0-3 min : tension qui monte, 110 BPM.
    Am - Dm en alternance, batterie solide, basse pulsee, lead discret.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    bpm = 110.0
    beat = 60.0 / bpm
    measure = beat * 4

    # Am et Dm (4 mesures Am, 4 mesures Dm en boucle 8 mesures)
    chord_blocks = [
        [note_hz(0,-1), note_hz(2,-1), note_hz(4,-1)],   # Am
        [note_hz(3,-1), note_hz(5,-1), note_hz(0)],       # Dm
    ]
    lead_seq = [note_hz(0), note_hz(4), note_hz(2), note_hz(5),
                note_hz(4), note_hz(7), note_hz(5), note_hz(2)]

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        block_idx = int((t % (measure * 8)) / (measure * 4))
        freqs = chord_blocks[block_idx]

        beat_in_measure = int((t % measure) / beat)
        t_beat = t % beat

        # Batterie
        kick  = drum_kick(t_beat)  * (0.55 if beat_in_measure in (0, 2) else 0.0)
        snare = drum_snare(t_beat) * (0.45 if beat_in_measure in (1, 3) else 0.0)
        hihat = drum_hihat(t % (beat * 0.5), dur=0.04) * 0.28

        # Basse pulsee (suit le kick)
        bass_freq = freqs[0]
        bass_env = math.exp(-t_beat * 6.0) if beat_in_measure in (0, 2) else math.exp(-(t_beat - beat * 0.5 if t_beat > beat*0.5 else 99) * 6.0)
        bass_env_simple = adsr(t_beat, beat, 0.005, 0.1, 0.4, 0.4)
        bass = synth_bass(t, bass_freq * 0.5, drive=1.3, sub_level=0.4) * bass_env_simple * 0.28

        # Pad (leger)
        pad = synth_pad(t, freqs, detune=1.5, lfo_rate=0.3, vol=0.12)

        # Lead periodique (toutes les 2 mesures)
        lead_beat = int(t / beat) % len(lead_seq)
        t_in_note = t % beat
        lead_env = adsr(t_in_note, beat, 0.01, 0.08, 0.65, 0.4)
        lead = synth_lead(t, lead_seq[lead_beat], lead_env * 0.14)

        raw = kick + snare + hihat + bass + pad + lead
        raw = soft_clip(raw, 0.85)
        buf.append(raw)

    buf = rev.process(buf, wet=0.15, dry=0.85)
    L, R = stereo_delay(buf, delay_ms=12.0, feedback=0.18)
    for i in range(len(L)):
        L[i] = soft_clip(L[i], 0.9)
        R[i] = soft_clip(R[i], 0.9)

    L = crossfade_seamless(L, xfade_ms=400)
    R = crossfade_seamless(R, xfade_ms=400)
    return L, R


# --- run mid ---

def gen_music_run_mid(duration=30.0):
    """
    Run 3-8 min : meme progression Am-Dm mais plus dense.
    Arpege ajoute, basse saturee, double hi-hat.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    bpm = 115.0
    beat = 60.0 / bpm
    measure = beat * 4

    chord_blocks = [
        [note_hz(0,-1), note_hz(2,-1), note_hz(4,-1)],   # Am
        [note_hz(3,-1), note_hz(5,-1), note_hz(0)],       # Dm
        [note_hz(4,-1), note_hz(6,-1), note_hz(1)],       # Em
        [note_hz(5,-1), note_hz(0), note_hz(2)],          # F
    ]

    # Arpege : Am7 / Dm7
    arp_seqs = [
        [note_hz(0), note_hz(2), note_hz(4), note_hz(7)],  # Am7
        [note_hz(3), note_hz(5), note_hz(0,1), note_hz(4)], # Dm7
        [note_hz(4), note_hz(6), note_hz(1,1), note_hz(4)], # Em
        [note_hz(5), note_hz(0,1), note_hz(2,1), note_hz(5)], # Fmaj
    ]

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        chord_idx = int((t % (measure * 4)) / (measure))
        freqs = chord_blocks[chord_idx]
        arp_notes = arp_seqs[chord_idx]

        beat_in_measure = int((t % measure) / beat)
        t_beat = t % beat

        # Batterie plus dense
        kick  = drum_kick(t_beat, dur=0.16, punch=1.1) * (0.62 if beat_in_measure in (0, 2) else 0.0)
        # Sous-kick entre les beats 2 et 4
        sub_kick_t = t % (beat * 2)
        sub_kick = drum_kick(sub_kick_t - beat * 1.75, dur=0.12, punch=0.7) * 0.35 if sub_kick_t > beat * 1.75 else 0.0
        snare = drum_snare(t_beat, dur=0.1) * (0.52 if beat_in_measure in (1, 3) else 0.0)
        # Double hi-hat (croches)
        hihat = drum_hihat(t % (beat * 0.5), dur=0.035) * 0.32
        # Clap sur beats 2 et 4
        clap = drum_clap(t_beat) * (0.3 if beat_in_measure in (1, 3) else 0.0)

        # Basse plus saturee
        bass_env = adsr(t_beat, beat, 0.005, 0.08, 0.5, 0.35)
        bass = synth_bass(t, freqs[0] * 0.5, drive=1.6, sub_level=0.5) * bass_env * 0.32
        bass = soft_clip(bass, 0.7)

        # Pad plus present
        pad = synth_pad(t, freqs, detune=2.0, lfo_rate=0.35, vol=0.16)

        # Arpege Aether
        arp = arp_step(t, arp_notes, bpm * 4, subdivisions=4) * 0.15

        raw = kick + sub_kick + snare + hihat + clap + bass + pad + arp
        raw = soft_clip(raw, 0.80)
        buf.append(raw)

    buf = rev.process(buf, wet=0.13, dry=0.87)
    L, R = stereo_delay(buf, delay_ms=10.0, feedback=0.2)
    for i in range(len(L)):
        L[i] = soft_clip(L[i], 0.88)
        R[i] = soft_clip(R[i], 0.88)

    L = crossfade_seamless(L, xfade_ms=400)
    R = crossfade_seamless(R, xfade_ms=400)
    return L, R


# --- run intense ---

def gen_music_run_intense(duration=30.0):
    """
    Run 8+ min : chaos contr?le.
    BPM 125, batterie double croche, basse saturee tanh, lead urgent.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    bpm = 125.0
    beat = 60.0 / bpm
    measure = beat * 4

    # Progression Am - Em - F - G (urgence/montee)
    chord_prog = [
        [note_hz(0,-1), note_hz(2,-1), note_hz(4,-1)],    # Am
        [note_hz(4,-1), note_hz(6,-1), note_hz(1)],        # Em
        [note_hz(5,-1), note_hz(0), note_hz(2)],           # F
        [freq_midi(67-12), freq_midi(71-12), freq_midi(74-12)],  # G
    ]
    lead_urgent = [note_hz(0,1), note_hz(7), note_hz(4,1), note_hz(2,1),
                   note_hz(5,1), note_hz(4,1), note_hz(2,1), note_hz(0,1)]

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        chord_idx = int((t % (measure * 4)) / measure)
        freqs = chord_prog[chord_idx]
        beat_in_measure = int((t % measure) / beat)
        t_beat = t % beat

        # Batterie double : kick lourd + double croche
        kick = drum_kick(t_beat, dur=0.14, punch=1.2) * (0.7 if beat_in_measure in (0, 2) else 0.0)
        # Double kick sur le "e" du beat 1
        t_half = t % (beat * 0.5)
        dbl_kick = drum_kick(t_half - beat * 0.25, dur=0.10) * 0.45 if t_half > beat * 0.25 else 0.0
        snare = drum_snare(t_beat, dur=0.09) * (0.58 if beat_in_measure in (1, 3) else 0.0)
        # Hi-hat double croche
        hihat = drum_hihat(t % (beat * 0.25), dur=0.025) * 0.30
        clap  = drum_clap(t_beat) * (0.32 if beat_in_measure in (1, 3) else 0.0)

        # Basse ultra saturee
        bass_env = adsr(t_beat, beat, 0.003, 0.06, 0.55, 0.3)
        bass = synth_bass(t, freqs[0] * 0.5, drive=2.2, sub_level=0.6) * bass_env * 0.35
        bass = tanh_clip(bass, 1.5)

        # Lead urgent (chaque beat)
        lead_idx = int(t / beat) % len(lead_urgent)
        lead_env = adsr(t_beat, beat, 0.005, 0.06, 0.7, 0.3)
        lead = synth_lead(t, lead_urgent[lead_idx], lead_env * 0.18, detune=2.0)

        # Pad (recule en arriere-plan)
        pad = synth_pad(t, freqs, detune=2.5, lfo_rate=0.5, vol=0.10)

        # LFO sur saturation pour effet "alarm"
        alarm_lfo = 0.5 + 0.5 * math.sin(TAU * 2.0 * t)
        noise_texture = noise_sample() * 0.03 * alarm_lfo

        raw = kick + dbl_kick + snare + hihat + clap + bass + lead + pad + noise_texture
        raw = soft_clip(raw, 0.75)
        buf.append(raw)

    buf = rev.process(buf, wet=0.10, dry=0.90)
    L, R = stereo_delay(buf, delay_ms=8.0, feedback=0.22)
    for i in range(len(L)):
        L[i] = tanh_clip(L[i], 1.1)
        R[i] = tanh_clip(R[i], 1.1)

    L = crossfade_seamless(L, xfade_ms=300)
    R = crossfade_seamless(R, xfade_ms=300)
    return L, R


# --- hub ---

def gen_music_hub(duration=30.0):
    """
    Hub meta : securite, reflet pensif.
    BPM 80, C majeur avec couleur Am, pad chaud, melodie douce, basse rare.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    bpm = 80.0
    beat = 60.0 / bpm
    measure = beat * 4

    # C - Am - F - G (majeur lumineux mais avec Am pour ancrer)
    chord_prog = [
        [freq_midi(60-12), freq_midi(64-12), freq_midi(67-12), freq_midi(72-12)],  # C
        [note_hz(0,-1), note_hz(2,-1), note_hz(4,-1), note_hz(0)],                 # Am
        [freq_midi(65-12), freq_midi(69-12), freq_midi(72-12), freq_midi(65)],     # F
        [freq_midi(67-12), freq_midi(71-12), freq_midi(74-12), freq_midi(67)],     # G
    ]
    # Melodie calme (descend, s'eleve doucement)
    mel_seq = [
        freq_midi(72), freq_midi(71), freq_midi(69), freq_midi(67),
        freq_midi(64), freq_midi(65), freq_midi(67), freq_midi(69),
    ]

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        chord_idx = int((t % (measure * 4)) / measure)
        freqs = chord_prog[chord_idx]
        beat_in_measure = int((t % measure) / beat)
        t_beat = t % beat

        # Percussion tres legere (rim shot discret)
        rim  = noise_sample() * 0.12 * math.exp(-t_beat * 35) * (1 if beat_in_measure == 2 else 0)
        hihat_vol = 0.12 if beat_in_measure % 2 == 1 else 0.0
        hihat = drum_hihat(t_beat, dur=0.03, open_hat=(beat_in_measure == 3)) * hihat_vol

        # Basse douce
        bass_active = beat_in_measure == 0
        bass_env = adsr(t_beat, beat, 0.02, 0.2, 0.4, 0.5) if bass_active else 0.0
        bass = synth_bass(t, freqs[0] * 0.5, drive=0.8, sub_level=0.3) * bass_env * 0.15

        # Pad tres chaud
        pad = synth_pad(t, freqs, detune=1.2, lfo_depth=0.002, lfo_rate=0.18, vol=0.28)

        # Melodie douce
        mel_idx = int(t / beat) % len(mel_seq)
        mel_env = adsr(t_beat, beat, 0.1, 0.25, 0.65, 0.5)
        melody = synth_aether(t, mel_seq[mel_idx], mel_env * 0.20, lfo_rate=0.5)

        # Cristaux Aether apaisants (tres doux)
        crystal = sin_wave(t, 1760.0) * 0.03 * max(0, lfo(t, 0.11))
        crystal += sin_wave(t, 2093.0) * 0.02 * max(0, lfo(t, 0.19, 0.7))

        raw = rim + hihat + bass + pad + melody + crystal
        raw = soft_clip(raw, 0.86)
        buf.append(raw)

    buf = rev.process(buf, wet=0.28, dry=0.72)
    L, R = stereo_delay(buf, delay_ms=22.0, feedback=0.12)
    for i in range(len(L)):
        L[i] = soft_clip(L[i], 0.92)
        R[i] = soft_clip(R[i], 0.92)

    L = crossfade_seamless(L, xfade_ms=700)
    R = crossfade_seamless(R, xfade_ms=700)
    return L, R


# --- stinger victoire ---

def gen_music_stinger_victory(duration=4.0):
    """
    Victoire : arpege C majeur ascendant, shimmer Aether, lumineux.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []

    # C4 E4 G4 C5 E5 -> declenchement triomphant
    victory_notes = [
        freq_midi(60), freq_midi(64), freq_midi(67),
        freq_midi(72), freq_midi(76),
    ]
    note_dur = duration / (len(victory_notes) + 0.5)

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        note_idx = min(int(t / note_dur), len(victory_notes) - 1)
        t_in_note = t - note_idx * note_dur
        freq = victory_notes[note_idx]

        env = adsr(t_in_note, note_dur, 0.01, 0.1, 0.75, 0.35)

        # Arpege note principale + harmoniques
        v  = sin_wave(t, freq)          * 0.40 * env
        v += sin_wave(t, freq * 2.0)    * 0.18 * env
        v += sin_wave(t, freq * 3.0)    * 0.09 * env
        v += sin_wave(t, freq * 0.5)    * 0.10 * env

        # Shimmer cristallin Aether (sur la derniere note)
        shimmer_vol = min(1.0, (t - duration * 0.6) / 0.3) if t > duration * 0.6 else 0.0
        shimmer = sin_wave(t, freq * 4.0) * 0.07 * env * shimmer_vol
        shimmer += sin_wave(t, freq * 5.0) * 0.04 * env * shimmer_vol

        # Fade out final propre
        fade_out = max(0.0, 1.0 - max(0.0, (t - duration * 0.75) / (duration * 0.25)))
        v = (v + shimmer) * fade_out

        buf.append(soft_clip(v, 0.88))

    buf = rev.process(buf, wet=0.30, dry=0.70)
    L, R = stereo_delay(buf, delay_ms=15.0, feedback=0.10)
    return L, R


# --- stinger mort ---

def gen_music_stinger_death(duration=3.0):
    """
    Mort : chute grave, Am descendant, distorsion finale.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []

    rev = SchroederReverb(rate)

    for i in range(n):
        t = i / rate
        p = t / duration

        # Glissando descendant : A3 -> A1 (440 -> 110 -> 55)
        freq = 440.0 * math.exp(-p * math.log(8.0))

        # Accord Am avec septieme (couleur sombre)
        chord = [freq, freq * 1.189, freq * 1.498, freq * 1.782]
        v = sum(sin_wave(t, f) * 0.22 for f in chord)

        # Impact metallique initial
        impact = (noise_sample() * 0.35 + sin_wave(t, 180)) * math.exp(-t * 18) if t < 0.15 else 0.0

        # Distorsion qui augmente (systeme qui cede)
        distort_drive = 0.8 + p * 1.2
        v = tanh_clip(v + impact, distort_drive)

        # Enveloppe globale
        env = adsr(t, duration, 0.03, 0.1, 0.5, 0.55)
        v *= env

        # Bruit de fin (static, systeme eteint)
        static = noise_sample() * 0.08 * (p ** 2) * math.exp(-(1.0 - p) * 3.0)
        v += static

        buf.append(soft_clip(v, 0.88))

    buf = rev.process(buf, wet=0.20, dry=0.80)
    L, R = stereo_delay(buf, delay_ms=11.0, feedback=0.08)
    return L, R


# ---------------------------------------------------------------------------
# SFX — nouvelle generation
# ---------------------------------------------------------------------------

def sfx_impulse_shoot(duration=0.18):
    """
    Canon a Impulsions : claquement electrique sec.
    Sweep 1200->200 Hz + punch transient + distorsion legere.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 1200.0 * math.exp(-p * math.log(6.0))
        # Transient (bruit + punch)
        transient = (noise_sample() * 0.4 + sin_wave(t, 2000)) * math.exp(-t * 80)
        # Corps du son
        body = sin_wave(t, freq) * 0.5 + sin_wave(t, freq * 2) * 0.15
        env = math.exp(-t * 18)
        v = (transient + body * env) * 0.85
        buf.append(soft_clip(v, 0.85))
    buf = lpf_resonant(buf, 3500.0, resonance=0.3)
    return fade_ends(buf, fade_ms=5)


def sfx_plasma_swing(duration=0.35):
    """
    Lame Plasma : swoosh grave + buzz electrique + harmoniques.
    Scie descendante 350->60 Hz.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Swoosh fréquence descendante
        freq = 350.0 * math.exp(-p * math.log(350.0 / 60.0))
        sweep_env = math.sin(math.pi * p ** 0.7)  # bosse asymetrique (plus fort en debut)
        sweep = (0.5 * saw_wave(t, freq) + 0.3 * sin_wave(t, freq)) * sweep_env
        # Buzz electrique plasma
        buzz = square_wave(t, 120 + 60 * p, 0.3) * 0.20 * math.exp(-t * 4)
        # Harmoniques hautes (etincelles)
        spark = noise_sample() * 0.12 * math.exp(-t * 15) * max(0, 1 - p * 2)
        v = sweep + buzz + spark
        buf.append(soft_clip(v, 0.88))
    buf = lpf_resonant(buf, 2800.0, resonance=0.4)
    return fade_ends(buf, fade_ms=8)


def sfx_overload_pulse(duration=0.45):
    """
    Champ de Surcharge : onde de choc basse frequence + reverberation.
    Thump 60 Hz + blast noise + sub resonance.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    for i in range(n):
        t = i / rate
        p = t / duration
        # Thump basse fréquence descendant
        freq = 80.0 * math.exp(-t * 5.0)
        thump = sin_wave(t, freq) * 0.65 * math.exp(-t * 7)
        # Explosion de bruit initiale
        blast = noise_sample() * 0.45 * math.exp(-t * 22)
        # Resonance prolongee
        res_freq = 55.0 + 10.0 * math.sin(TAU * 0.8 * t)
        resonance = sin_wave(t, res_freq) * 0.20 * math.exp(-t * 3.5)
        v = thump + blast + resonance
        buf.append(soft_clip(v, 0.86))
    buf = rev.process(buf, wet=0.25, dry=0.75)
    return fade_ends(buf, fade_ms=10)


def sfx_drone_loop(duration=1.2):
    """
    Drone en orbite : moteur grave 75 Hz + sifflement organique.
    Boucle seamless.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        lfo_val = 0.5 + 0.5 * math.sin(TAU * 4.3 * t)
        lfo_slow = 0.5 + 0.5 * math.sin(TAU * 1.1 * t)
        # Moteur
        motor = saw_wave(t, 75 + 3 * lfo_slow) * 0.30
        motor += sin_wave(t, 150 + 2 * lfo_val) * 0.14
        # Sifflement caracteristique
        whistle = sin_wave(t, 330 + 15 * lfo_val) * 0.10
        # Petites fluctuations mecaniques
        mech = noise_sample() * 0.04 * lfo_val
        v = motor + whistle + mech
        buf.append(soft_clip(v, 0.84))
    xfade_n = int(0.08 * rate)
    n = len(buf)
    for i in range(xfade_n):
        a = i / xfade_n
        buf[i] = buf[i] * a + buf[n - xfade_n + i] * (1.0 - a)
    return buf


def sfx_sentinel_shoot(duration=0.22):
    """
    Tir Sentinelle : lourd et mecanique, sweep 500->120 Hz.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 500.0 * math.exp(-p * math.log(500.0 / 120.0))
        env = math.exp(-t * 10)
        v  = saw_wave(t, freq) * 0.45
        v += sin_wave(t, freq * 0.5) * 0.25  # sub
        v += noise_sample() * 0.18 * math.exp(-t * 25)
        buf.append(soft_clip(v * env, 0.84))
    buf = lpf_resonant(buf, 2200.0, resonance=0.5)
    return fade_ends(buf, fade_ms=5)


def sfx_rail_shoot(duration=0.55):
    """
    Rail Surcharg? : 3 claquements a 0.14 s, son percant puissant.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    shot_times = [0.0, 0.14, 0.28]
    for i in range(n):
        t = i / rate
        v = 0.0
        for st in shot_times:
            dt = t - st
            if 0 <= dt < 0.20:
                freq = 1500.0 * math.exp(-dt * 20)
                env  = math.exp(-dt * 22)
                crack = sin_wave(dt, freq) * 0.5 + noise_sample() * 0.35 * math.exp(-dt * 50)
                sub   = sin_wave(dt, freq * 0.25) * 0.20 * math.exp(-dt * 12)
                v += (crack + sub) * env
        buf.append(soft_clip(v, 0.85))
    return fade_ends(buf, fade_ms=5)


def sfx_fusion_activate(duration=0.90):
    """
    Activation fusion : montee en frequence + resonance metallique + burst Aether.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    for i in range(n):
        t = i / rate
        p = t / duration
        # Sweep montant rapide 80 -> 600 Hz
        freq = 80.0 * math.exp(p * math.log(600.0 / 80.0))
        ramp_env = min(1.0, p * 2.5)
        decay_env = math.exp(-(p - 0.4) * 3) if p > 0.4 else 1.0
        sweep = (sin_wave(t, freq) * 0.4 + saw_wave(t, freq) * 0.25) * ramp_env * decay_env
        # Resonance metallique inharmonique
        metal = sin_wave(t, freq * 2.73) * 0.15 * math.exp(-t * 2.5)
        # Burst Aether en fin
        burst_vol = max(0, (p - 0.55) * 3.0) if p > 0.55 else 0.0
        aether_burst = synth_aether(t, 440.0, burst_vol * 0.25 * math.exp(-(p - 0.55) * 4), lfo_rate=8.0)
        v = sweep + metal + aether_burst
        buf.append(soft_clip(v, 0.88))
    buf = rev.process(buf, wet=0.20, dry=0.80)
    return fade_ends(buf, fade_ms=10)


def sfx_fusion_loop(duration=1.2):
    """
    Lame a Fusion active : sifflement d'energie stable, boucle seamless.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        lfo_v = 0.5 + 0.5 * math.sin(TAU * 2.3 * t)
        v  = sin_wave(t, 440 + 6 * lfo_v) * 0.17
        v += sin_wave(t, 660 + 4 * lfo_v) * 0.08
        v += sin_wave(t, 880)              * 0.04
        v += tri_wave(t, 220 + 2 * lfo_v) * 0.05
        buf.append(v)
    xfade_n = int(0.06 * rate)
    n_buf = len(buf)
    for i in range(xfade_n):
        a = i / xfade_n
        buf[i] = buf[i] * a + buf[n_buf - xfade_n + i] * (1.0 - a)
    return buf


def sfx_swarm_die(duration=0.22):
    """
    Mort Essaim de Rouille : craquement sec metallique + debris en cascade.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        # Craquement initial sec
        crack = noise_sample() * 0.6 * math.exp(-t * 35)
        # Debris metalliques : 3 tonalites breves descendantes
        d1 = sin_wave(t, 900 * math.exp(-t * 12)) * 0.22 * math.exp(-t * 18)
        d2 = sin_wave(t, 1350 * math.exp(-t * 14)) * 0.16 * math.exp(-t * 22)
        d3 = noise_sample() * 0.10 * math.exp(-t * 45)
        v = crack + d1 + d2 + d3
        buf.append(soft_clip(v, 0.84))
    return fade_ends(buf, fade_ms=5)


def sfx_drone_die(duration=0.28):
    """
    Mort Drone Corrompu : explosion electrique + arc + bip d'extinction.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        # Arc electrique
        arc = (noise_sample() * 0.5 + square_wave(t, 250, 0.3) * 0.25) * math.exp(-t * 18)
        # Tonalite electronique descendante
        tone = sin_wave(t, 1400 * math.exp(-t * 7)) * 0.3 * math.exp(-t * 10)
        # Bip de fin (systeme qui s'eteint)
        bip_freq = 1800 if t < 0.12 else 0
        bip = sin_wave(t, bip_freq) * 0.25 * math.exp(-t * 14) if t < 0.14 else 0.0
        v = arc + tone + bip
        buf.append(soft_clip(v, 0.84))
    return fade_ends(buf, fade_ms=5)


def sfx_sentinel_die(duration=0.45):
    """
    Mort Sentinelle : explosion mecanique ample avec queue reverberee.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    for i in range(n):
        t = i / rate
        p = t / duration
        # Explosion principale
        explosion = (noise_sample() * 0.55 + sin_wave(t, 180) * 0.22) * math.exp(-t * 7)
        # Resonance metallique
        metal = sin_wave(t, 650 * math.exp(-t * 5)) * 0.25 * math.exp(-t * 6)
        # Queue electronique
        elec_tail = sin_wave(t, 440 * math.exp(-t * 3)) * 0.12 * max(0.0, (p - 0.2) / 0.8) if p > 0.2 else 0.0
        v = explosion + metal + elec_tail
        buf.append(soft_clip(v, 0.87))
    buf = rev.process(buf, wet=0.18, dry=0.82)
    return fade_ends(buf, fade_ms=8)


def sfx_colossus_die(duration=1.0):
    """
    Mort Colosse Greffe : impact massif, resonance longue, flash Aether violet.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    for i in range(n):
        t = i / rate
        p = t / duration
        # Impact massif : scie grave + bruit puissant
        impact_freq = 60.0 * math.exp(-t * 4.0)
        impact = (saw_wave(t, impact_freq) * 0.55 + sin_wave(t, impact_freq) * 0.30) * math.exp(-t * 4.5)
        crash  = noise_sample() * 0.50 * math.exp(-t * 10)
        # Resonance grave prolongee
        resonance = sin_wave(t, 40.0) * 0.28 * math.exp(-t * 1.2)
        resonance += sin_wave(t, 55.0) * 0.18 * math.exp(-t * 1.8)
        # Flash Aether violet (premiere 80 ms)
        aether_flash = synth_aether(t, 880.0, math.exp(-t * 50) * 0.35) if t < 0.08 else 0.0
        # Debris metalliques
        debris = noise_sample() * 0.12 * math.exp(-t * 5) * (p ** 0.5)
        v = impact + crash + resonance + aether_flash + debris
        buf.append(soft_clip(v, 0.84))
    buf = rev.process(buf, wet=0.22, dry=0.78)
    return fade_ends(buf, fade_ms=10)


def sfx_sentinel_projectile(duration=0.16):
    """
    Projectile Sentinelle en vol : sifflement mecanique grave-medium.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 600.0 - 250.0 * p  # effet Doppler
        env = math.sin(math.pi * p)  # bosse
        v = sin_wave(t, freq) * 0.35 * env
        v += sin_wave(t, freq * 0.5) * 0.18 * env
        v += noise_sample() * 0.06 * env
        buf.append(v)
    return fade_ends(buf, fade_ms=4)


def sfx_xp_collect(duration=0.12):
    """
    Collecte orbe XP : "ting" cristallin bref, montee courte.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 1400.0 + 600.0 * p
        env = math.exp(-t * 28) * math.sin(math.pi * p * 0.7)
        v  = sin_wave(t, freq) * 0.42 * env
        v += sin_wave(t, freq * 2.0) * 0.16 * env
        v += sin_wave(t, freq * 3.0) * 0.06 * env
        buf.append(v)
    return fade_ends(buf, fade_ms=3)


def sfx_core_collect(duration=0.35):
    """
    Collecte Noyau d'Aether : accord mystique Am7, shimmer violet.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    # Am7 : A C E G (couleur "magie ancienne")
    freqs = [440.0, 523.25, 659.25, 783.99]
    for i in range(n):
        t = i / rate
        env = adsr(t, duration, 0.01, 0.08, 0.55, 0.4)
        v = sum(sin_wave(t, f) * 0.20 for f in freqs) * env
        # Shimmer aether haute frequence
        v += sin_wave(t, 1760.0) * 0.10 * math.exp(-t * 8)
        v += sin_wave(t, 2093.0) * 0.06 * math.exp(-t * 12)
        # Vibrato Aether
        v += synth_aether(t, 440.0, env * 0.12, lfo_rate=6.0)
        buf.append(v)
    buf = rev.process(buf, wet=0.22, dry=0.78)
    return fade_ends(buf, fade_ms=8)


def sfx_levelup(duration=0.55):
    """
    Level-up : arpege Am ascendant rapide + shimmer Aether marquant.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    # A4 C5 E5 A5 (Am en octaves)
    notes = [440.0, 523.25, 659.25, 880.0]
    note_dur = duration / (len(notes) + 0.3)
    for i in range(n):
        t = i / rate
        note_idx = min(int(t / note_dur), len(notes) - 1)
        freq = notes[note_idx]
        t_in = t - note_idx * note_dur
        env = adsr(t_in, note_dur, 0.005, 0.08, 0.7, 0.3)
        v  = sin_wave(t, freq) * 0.42 * env
        v += sin_wave(t, freq * 2.0) * 0.18 * env
        v += sin_wave(t, freq * 3.0) * 0.08 * env
        shimmer = sin_wave(t, freq * 4.0) * 0.10 * env
        buf.append(soft_clip(v + shimmer, 0.87))
    buf = rev.process(buf, wet=0.18, dry=0.82)
    return fade_ends(buf, fade_ms=8)


def sfx_card_select(duration=0.17):
    """
    Selection carte : click mecanique net + accent cristallin bref.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        # Click sec
        click = noise_sample() * math.exp(-t * 90) * 0.52
        # Accent crystal descendant
        freq = 1600.0 - 500.0 * (t / duration)
        tone = sin_wave(t, freq) * math.exp(-t * 28) * 0.38
        # Harmonique
        tone += sin_wave(t, freq * 1.5) * math.exp(-t * 40) * 0.12
        buf.append(click + tone)
    return fade_ends(buf, fade_ms=4)


def sfx_fusion_evolve(duration=2.0):
    """
    Fusion/evolution d'arme : impact sourd, silence flash blanc, explosion Aether, resonance longue.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    for i in range(n):
        t = i / rate
        p = t / duration
        # Phase 1 (0-0.25s) : impact grave
        impact = 0.0
        if t < 0.25:
            impact_freq = 70.0 * math.exp(-t * 15)
            impact = (saw_wave(t, impact_freq) * 0.6 + noise_sample() * 0.4) * math.exp(-t * 18)

        # Phase 2 (0.25-0.4s) : quasi-silence (flash visuel)
        # (silence naturel)

        # Phase 3 (0.4s+) : explosion Aether
        aether = 0.0
        if t >= 0.40:
            ta = t - 0.40
            burst_env = math.exp(-ta * 2.5) * (1.0 + 0.4 * math.sin(TAU * 1.8 * ta))
            # Harmoniques Aether
            aether_freqs = [220.0, 330.0, 440.0, 660.0, 880.0, 1100.0]
            aether = sum(sin_wave(ta, f) * 0.12 for f in aether_freqs) * burst_env
            # Bruit initial
            aether += noise_sample() * math.exp(-ta * 12) * 0.35
            # Resonance grave longue
            aether += sin_wave(ta, 110.0) * 0.28 * math.exp(-ta * 1.0)
            # Sweep montant (energie liberee)
            sweep_f = 220.0 * math.exp(min(ta, 0.3) * math.log(4.0) / 0.3)
            aether += sin_wave(ta, sweep_f) * 0.20 * math.exp(-ta * 2.0)

        v = impact + aether
        buf.append(soft_clip(v, 0.84))

    buf = rev.process(buf, wet=0.25, dry=0.75)
    return fade_ends(buf, fade_ms=12)


def sfx_player_hit(duration=0.22):
    """
    Joueur touche : impact grave net, signal de danger immediat.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        # Impact grave + bruit
        freq = 220.0 * math.exp(-t * 10.0)
        env  = math.exp(-t * 13.0)
        impact = sin_wave(t, freq) * 0.50 + noise_sample() * 0.28 * math.exp(-t * 28)
        # Sub
        sub = sin_wave(t, 80.0) * 0.20 * math.exp(-t * 8)
        v = (impact + sub) * env
        buf.append(soft_clip(v, 0.85))
    buf = lpf_resonant(buf, 1800.0, resonance=0.35)
    return fade_ends(buf, fade_ms=5)


def sfx_player_die(duration=1.0):
    """
    Mort joueur : systeme cyborg qui s'eteint.
    Glissando descendant + accord mineur + static final.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    rev = SchroederReverb(rate)
    for i in range(n):
        t = i / rate
        p = t / duration
        # Glissando descendant Am (A3 -> A1)
        freq_main = 440.0 * math.exp(-p * math.log(8.0))
        chord = [freq_main, freq_main * 1.189, freq_main * 1.498]
        chord_vol = sum(sin_wave(t, f) * 0.22 for f in chord)
        # Alerte courte initiale (systeme en detresse)
        alert = sin_wave(t, 1200.0) * math.exp(-t * 20) * 0.22 if t < 0.12 else 0.0
        # Distorsion progressive
        drive = 0.8 + p * 1.5
        signal = tanh_clip(chord_vol + alert, drive)
        # Enveloppe globale
        env = adsr(t, duration, 0.01, 0.12, 0.5, 0.55)
        signal *= env
        # Static final (systeme eteint)
        static = noise_sample() * 0.10 * (p ** 2.5)
        v = signal + static
        buf.append(soft_clip(v, 0.88))
    buf = rev.process(buf, wet=0.20, dry=0.80)
    return fade_ends(buf, fade_ms=10)


def sfx_ui_button(duration=0.12):
    """
    Clic bouton UI : net, technologique, discret.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        # Click bref + accent crystal court
        click = noise_sample() * 0.42 * math.exp(-t * 70)
        tone  = sin_wave(t, 1100.0) * 0.36 * math.exp(-t * 45)
        tone += sin_wave(t, 1650.0) * 0.12 * math.exp(-t * 60)
        buf.append(click + tone)
    return fade_ends(buf, fade_ms=3)


def sfx_ui_purchase(duration=0.32):
    """
    Achat Hub : validation positive deux notes, accent Aether final.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    # Do-Mi (C5-E5) — sentiment de recompense
    note1, note2 = freq_midi(72), freq_midi(76)
    split = 0.42
    for i in range(n):
        t = i / rate
        p = t / duration
        if p < split:
            freq = note1
            ta = t
            env = adsr(ta, duration * split, 0.01, 0.06, 0.65, 0.3)
        else:
            freq = note2
            ta = t - duration * split
            env = adsr(ta, duration * (1 - split), 0.01, 0.06, 0.65, 0.35)
        v  = sin_wave(t, freq) * 0.38 * env
        v += sin_wave(t, freq * 2.0) * 0.14 * env
        # Shimmer Aether sur la 2e note
        if p > split:
            v += sin_wave(t, freq * 4.0) * 0.08 * env
        buf.append(v)
    return fade_ends(buf, fade_ms=6)


def sfx_ui_victory(duration=0.52):
    """
    Victoire UI : accord majeur C5-E5-G5 court, lumineux.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    freqs = [freq_midi(72), freq_midi(76), freq_midi(79)]  # C5 E5 G5
    for i in range(n):
        t = i / rate
        env = adsr(t, duration, 0.01, 0.10, 0.62, 0.42)
        v = sum(sin_wave(t, f) * 0.24 for f in freqs) * env
        v += sin_wave(t, freqs[-1] * 2.0) * 0.10 * math.exp(-t * 6)
        buf.append(v)
    return fade_ends(buf, fade_ms=8)


def sfx_ui_death(duration=0.52):
    """
    Mort UI : accord mineur court descendant Am.
    """
    rate = SAMPLE_RATE
    n = int(duration * rate)
    buf = []
    for i in range(n):
        t = i / rate
        p = t / duration
        freq = 220.0 * math.exp(-p * math.log(220.0 / 110.0))
        env  = adsr(t, duration, 0.01, 0.10, 0.52, 0.46)
        chord = [freq, freq * 1.189, freq * 1.498]
        v = sum(sin_wave(t, f) * 0.22 for f in chord) * env
        v += noise_sample() * 0.07 * math.exp(-t * 9)
        buf.append(soft_clip(v, 0.85))
    return fade_ends(buf, fade_ms=6)


# ---------------------------------------------------------------------------
# Execution
# ---------------------------------------------------------------------------

def main():
    print("=" * 62)
    print("Chimera Protocol — Audio v2 (synthese amelioree, CC0)")
    print("=" * 62)

    # --- Musiques (stereo) ---
    print("\n[MUSIQUES — stereo, filtres IIR + reverb Schroeder]")
    music_tasks = [
        ("music_menu",            gen_music_menu,            30.0),
        ("music_run_intro",       gen_music_run_intro,       30.0),
        ("music_run_mid",         gen_music_run_mid,         30.0),
        ("music_run_intense",     gen_music_run_intense,     30.0),
        ("music_hub",             gen_music_hub,             30.0),
        ("music_stinger_victory", gen_music_stinger_victory,  4.5),
        ("music_stinger_death",   gen_music_stinger_death,    3.5),
    ]
    for name, fn, dur in music_tasks:
        path = os.path.join(MUSIC_DIR, f"{name}.wav")
        L, R = fn(dur)
        write_wav(path, L, R)

    # --- SFX (mono) ---
    print("\n[SFX — mono, formes d'ondes variees + filtres]")
    sfx_tasks = [
        # Armes
        ("sfx_weapon_impulse_shoot",   sfx_impulse_shoot,     0.18),
        ("sfx_weapon_plasma_swing",    sfx_plasma_swing,      0.35),
        ("sfx_weapon_overload_pulse",  sfx_overload_pulse,    0.45),
        ("sfx_weapon_drone_loop",      sfx_drone_loop,        1.20),
        ("sfx_weapon_sentinel_shoot",  sfx_sentinel_shoot,    0.22),
        ("sfx_weapon_rail_shoot",      sfx_rail_shoot,        0.55),
        ("sfx_weapon_fusion_activate", sfx_fusion_activate,   0.90),
        ("sfx_weapon_fusion_loop",     sfx_fusion_loop,       1.20),
        # Ennemis
        ("sfx_enemy_swarm_die",           sfx_swarm_die,           0.22),
        ("sfx_enemy_drone_die",           sfx_drone_die,           0.28),
        ("sfx_enemy_sentinel_die",        sfx_sentinel_die,        0.45),
        ("sfx_enemy_colossus_die",        sfx_colossus_die,        1.00),
        ("sfx_enemy_sentinel_projectile", sfx_sentinel_projectile, 0.16),
        # Gameplay
        ("sfx_xp_collect",    sfx_xp_collect,    0.12),
        ("sfx_core_collect",  sfx_core_collect,  0.35),
        ("sfx_levelup",       sfx_levelup,       0.55),
        ("sfx_card_select",   sfx_card_select,   0.17),
        ("sfx_fusion_evolve", sfx_fusion_evolve, 2.00),
        # Joueur
        ("sfx_player_hit", sfx_player_hit, 0.22),
        ("sfx_player_die", sfx_player_die, 1.00),
        # UI
        ("sfx_ui_button",   sfx_ui_button,   0.12),
        ("sfx_ui_purchase", sfx_ui_purchase, 0.32),
        ("sfx_ui_victory",  sfx_ui_victory,  0.52),
        ("sfx_ui_death",    sfx_ui_death,    0.52),
    ]
    for name, fn, dur in sfx_tasks:
        path = os.path.join(SFX_DIR, f"{name}.wav")
        samples = fn(dur)
        write_wav(path, samples)

    print("\n" + "=" * 62)
    print("Generation terminee.")
    print()
    print("STATUT : PLACEHOLDERS SYNTHETISES AMELIORES (v2)")
    print("  - Synthese 100% originale Python stdlib — CC0 / domaine public")
    print("  - Filtres IIR resonants, reverb Schroeder, saturation tanh")
    print("  - Progressions harmoniques Am/Dm/Em/F (ruines) + C/G (hub)")
    print("  - Aucun sample tiers — aucun droit tiers")
    print()
    print("Ces fichiers sont des PLACEHOLDERS de qualite amelioree.")
    print("Pour une bande-son definitive :")
    print("  - Installer ffmpeg et convertir les packs Kenney CC0 en WAV")
    print("  - Ou commissionner un compositeur")
    print("=" * 62)


if __name__ == "__main__":
    main()
