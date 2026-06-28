"""
Génère des fichiers WAV silencieux comme placeholders pour tous les assets audio
du projet Chimera Protocol. Aucune dépendance externe (stdlib Python uniquement).

Usage : python tools/generate_audio_placeholders.py
"""

import wave
import os
import struct

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MUSIC_DIR    = os.path.join(PROJECT_ROOT, "assets", "audio", "music")
SFX_DIR      = os.path.join(PROJECT_ROOT, "assets", "audio", "sfx")

SAMPLE_RATE = 44100
CHANNELS    = 1   # mono
SAMPWIDTH   = 2   # 16-bit

def make_wav(path: str, duration: float) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)
    if os.path.exists(path):
        return  # ne pas écraser un vrai fichier existant
    n_frames = int(SAMPLE_RATE * duration)
    with wave.open(path, "w") as f:
        f.setnchannels(CHANNELS)
        f.setsampwidth(SAMPWIDTH)
        f.setframerate(SAMPLE_RATE)
        f.writeframes(b"\x00\x00" * n_frames)
    print(f"  [créé] {os.path.relpath(path, PROJECT_ROOT)}")

# ---------------------------------------------------------------------------
# Musiques — 1 s de silence (WAV, AudioSystem tente .ogg puis .wav)
# ---------------------------------------------------------------------------
MUSIC_PLACEHOLDERS = [
    "music_menu",
    "music_run_intro",
    "music_run_mid",
    "music_run_intense",
    "music_hub",
    "music_stinger_victory",
    "music_stinger_death",
]

# ---------------------------------------------------------------------------
# SFX — durées représentatives (WAV)
# ---------------------------------------------------------------------------
SFX_PLACEHOLDERS = {
    # Armes
    "sfx_weapon_impulse_shoot":   0.15,
    "sfx_weapon_plasma_swing":    0.30,
    "sfx_weapon_overload_pulse":  0.40,
    "sfx_weapon_drone_loop":      1.00,
    "sfx_weapon_sentinel_shoot":  0.20,
    "sfx_weapon_rail_shoot":      0.50,
    "sfx_weapon_fusion_activate": 0.80,
    "sfx_weapon_fusion_loop":     1.00,
    # Ennemis
    "sfx_enemy_swarm_die":              0.20,
    "sfx_enemy_drone_die":              0.25,
    "sfx_enemy_sentinel_die":           0.40,
    "sfx_enemy_colossus_die":           0.80,
    "sfx_enemy_sentinel_projectile":    0.15,
    # Gameplay
    "sfx_xp_collect":   0.10,
    "sfx_core_collect": 0.30,
    "sfx_levelup":      0.50,
    "sfx_card_select":  0.15,
    "sfx_fusion_evolve":1.50,
    # Joueur
    "sfx_player_hit": 0.20,
    "sfx_player_die": 0.80,
    # UI
    "sfx_ui_button":   0.10,
    "sfx_ui_purchase": 0.30,
    "sfx_ui_victory":  0.50,
    "sfx_ui_death":    0.50,
}

def main() -> None:
    print("=== Génération des placeholders audio ===\n")

    print("Musiques (WAV silent) :")
    for name in MUSIC_PLACEHOLDERS:
        make_wav(os.path.join(MUSIC_DIR, f"{name}.wav"), duration=1.0)

    print("\nSFX (WAV silent) :")
    for name, dur in SFX_PLACEHOLDERS.items():
        make_wav(os.path.join(SFX_DIR, f"{name}.wav"), duration=dur)

    print("\nTerminé. Remplacer ces fichiers par de vrais assets audio (cf. docs/AUDIO_GUIDE.md §5).")

if __name__ == "__main__":
    main()
