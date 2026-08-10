"""
Integre les assets audio Kenney CC0 dans Chimera Protocol.
Genere les stingers victoire/mort par concatenation de jingles Kenney.
Convertit tous les SFX (deja fait via bash) et documente les sources.
"""

import subprocess
import os
import sys

FFMPEG = r"C:\Users\drang\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.1-full_build\bin\ffmpeg.exe"
FFPROBE = r"C:\Users\drang\AppData\Local\Microsoft\WinGet\Packages\Gyan.FFmpeg_Microsoft.Winget.Source_8wekyb3d8bbwe\ffmpeg-8.1.1-full_build\bin\ffprobe.exe"

BASE = r"C:\CODE\JEUX\chimera-protocol"
SRC = r"C:\CODE\JEUX\chimera-protocol\tools\kenney_downloads\extracted"
DST_SFX = r"C:\CODE\JEUX\chimera-protocol\assets\audio\sfx"
DST_MUS = r"C:\CODE\JEUX\chimera-protocol\assets\audio\music"
JINGLES = r"C:\CODE\JEUX\chimera-protocol\tools\kenney_downloads\extracted\musicjingles\Audio"

errors = []
converted = []


def run_ffmpeg(args, label):
    cmd = [FFMPEG, "-y"] + args
    result = subprocess.run(cmd, capture_output=True, text=True)
    if result.returncode != 0:
        print(f"  ERROR [{label}]: {result.stderr[-200:]}")
        errors.append(label)
    else:
        print(f"  OK: {label}")
        converted.append(label)


def conv_sfx(src, dst_name):
    """Convertit OGG -> WAV mono 44100 Hz 16-bit."""
    dst = os.path.join(DST_SFX, dst_name)
    run_ffmpeg(["-i", src, "-ar", "44100", "-ac", "1", "-sample_fmt", "s16", dst, "-loglevel", "error"], dst_name)


def conv_sfx_filtered(src, dst_name, afilter):
    """Comme conv_sfx, mais en appliquant une chaine de filtres ffmpeg.

    Sert aux sons qu'on veut deriver d'un asset Kenney sans le remplacer : le meme laser peut
    servir de tir sec a une arme et de salve sourde a une autre. Le traitement vit ICI et non
    dans un editeur audio, sinon l'asset n'est plus regenerable et la recette se perd.
    """
    dst = os.path.join(DST_SFX, dst_name)
    run_ffmpeg(["-i", src, "-af", afilter, "-ar", "44100", "-ac", "1",
                "-sample_fmt", "s16", dst, "-loglevel", "error"], dst_name)


def conv_music(src, dst_name):
    """Convertit OGG -> WAV stereo 44100 Hz 16-bit."""
    dst = os.path.join(DST_MUS, dst_name)
    run_ffmpeg(["-i", src, "-ar", "44100", "-ac", "2", "-sample_fmt", "s16", dst, "-loglevel", "error"], dst_name)


def concat_music(sources, dst_name):
    """Concatene plusieurs OGG en WAV stereo."""
    dst = os.path.join(DST_MUS, dst_name)
    # Cree un fichier de concat temporaire
    list_file = os.path.join(BASE, "tools", "_concat_list.txt")
    with open(list_file, "w", encoding="utf-8") as f:
        for s in sources:
            # ffmpeg concat demuxer: les chemins avec espaces doivent etre entre apostrophes
            f.write(f"file '{s.replace(chr(92), '/')}'\n")

    run_ffmpeg(["-f", "concat", "-safe", "0", "-i", list_file,
                "-ar", "44100", "-ac", "2", "-sample_fmt", "s16",
                dst, "-loglevel", "error"], dst_name)

    os.unlink(list_file)


# Raccourcis pour les chemins sources
SCIFI = os.path.join(SRC, "scifi", "Audio")
IMPACT = os.path.join(SRC, "impact", "Audio")
UI = os.path.join(SRC, "ui", "Audio")
RPG = os.path.join(SRC, "rpg", "Audio")
NES = os.path.join(JINGLES, "8-Bit jingles")
HIT = os.path.join(JINGLES, "Hit jingles")
STEEL = os.path.join(JINGLES, "Steel jingles")
SAX = os.path.join(JINGLES, "Sax jingles")

print("=" * 60)
print("INTEGRATION AUDIO KENNEY CC0 — Chimera Protocol")
print("=" * 60)

# ================================================================
# SFX WEAPONS
# ================================================================
print("\n--- SFX Armes ---")
conv_sfx(os.path.join(SCIFI, "laserSmall_000.ogg"), "sfx_weapon_impulse_shoot.wav")
conv_sfx(os.path.join(SCIFI, "laserLarge_000.ogg"), "sfx_weapon_plasma_swing.wav")
conv_sfx(os.path.join(SCIFI, "laserLarge_002.ogg"), "sfx_weapon_rail_shoot.wav")
conv_sfx(os.path.join(SCIFI, "forceField_000.ogg"), "sfx_weapon_overload_pulse.wav")
conv_sfx(os.path.join(SCIFI, "engineCircular_000.ogg"), "sfx_weapon_drone_loop.wav")
conv_sfx(os.path.join(SCIFI, "forceField_004.ogg"), "sfx_weapon_fusion_activate.wav")
conv_sfx(os.path.join(SCIFI, "engineCircular_002.ogg"), "sfx_weapon_fusion_loop.wav")
conv_sfx(os.path.join(SCIFI, "laserRetro_000.ogg"), "sfx_weapon_sentinel_shoot.wav")

# Volee Multiple — salve volontairement DISCRETE (retour de jeu : « trop present, limite sature »).
# Elle partageait le claquement du Canon a Impulsions et tire souvent en meme temps que lui.
# Recette : passe-bas 1800 Hz (ote le mordant), coupe a 0,18 s avec fondu (une salve enchainee
# plusieurs fois par seconde ne doit pas trainer), puis crete ramenee a -1 dBFS comme le reste de
# la banque. Le NIVEAU final ne se regle pas ici mais dans AudioSystem.MixGainDb (-11 dB) : Unity
# renormalise les clips a l'import, une attenuation gravee dans le WAV y serait effacee.
conv_sfx_filtered(os.path.join(SCIFI, "laserSmall_003.ogg"), "sfx_weapon_scatter_shoot.wav",
                  "lowpass=f=1800:poles=2,atrim=0:0.18,afade=t=out:st=0.12:d=0.06,volume=9.6dB")

# ================================================================
# SFX JOUEUR & ENNEMIS
# ================================================================
print("\n--- SFX Joueur ---")
conv_sfx(os.path.join(IMPACT, "impactMetal_medium_001.ogg"), "sfx_player_hit.wav")
conv_sfx(os.path.join(SCIFI, "lowFrequency_explosion_000.ogg"), "sfx_player_die.wav")

print("\n--- SFX Ennemis ---")
conv_sfx(os.path.join(IMPACT, "impactGeneric_light_000.ogg"), "sfx_enemy_swarm_die.wav")
# Mort des ennemis erratiques — RACCOURCIE. Le fichier brut ne decroit pas : il tient ~-11 dB RMS
# pendant 1,1 s puis coupe net, ce qui n'est pas une explosion mais un grondement continu. Joue par
# six especes de faune qui arrivent d'un bloc a la 3e minute avec 11-17 PV (mortes au premier coup),
# il produisait un mur de bruit permanent — signale deux fois en jouant, la seconde fois comme « du
# reverb ». C'est bien cela : une trainee dense qui se recouvre. Coupe a 0,30 s avec decroissance
# exponentielle, il redevient une percussion (-10 -21 -40 -60 dB par tranches de 50 ms) tout en
# restant distinct du « clic » de la nuee (0,16 s).
conv_sfx_filtered(os.path.join(SCIFI, "explosionCrunch_001.ogg"), "sfx_enemy_drone_die.wav",
                  "atrim=0:0.30,afade=t=out:st=0.04:d=0.26:curve=exp")
conv_sfx(os.path.join(IMPACT, "impactMetal_heavy_002.ogg"), "sfx_enemy_sentinel_die.wav")
conv_sfx(os.path.join(SCIFI, "laserRetro_002.ogg"), "sfx_enemy_sentinel_projectile.wav")
conv_sfx(os.path.join(SCIFI, "lowFrequency_explosion_001.ogg"), "sfx_enemy_colossus_die.wav")

# ================================================================
# SFX GAMEPLAY & UI
# ================================================================
print("\n--- SFX Gameplay ---")
conv_sfx(os.path.join(UI, "switch24.ogg"), "sfx_levelup.wav")
conv_sfx(os.path.join(UI, "rollover2.ogg"), "sfx_card_select.wav")
conv_sfx(os.path.join(RPG, "handleCoins.ogg"), "sfx_core_collect.wav")
conv_sfx(os.path.join(UI, "click1.ogg"), "sfx_xp_collect.wav")
conv_sfx(os.path.join(SCIFI, "forceField_003.ogg"), "sfx_fusion_evolve.wav")

print("\n--- SFX Interface ---")
conv_sfx(os.path.join(UI, "mouseclick1.ogg"), "sfx_ui_button.wav")
conv_sfx(os.path.join(UI, "switch33.ogg"), "sfx_ui_purchase.wav")
conv_sfx(os.path.join(UI, "switch26.ogg"), "sfx_ui_victory.wav")
conv_sfx(os.path.join(IMPACT, "impactBell_heavy_000.ogg"), "sfx_ui_death.wav")

# ================================================================
# MUSIQUE — STINGERS (Kenney jingles concatenes)
# ================================================================
print("\n--- Musique Stingers (Kenney jingles) ---")

# music_stinger_victory : ~4.5s
# NES00 (1.757s) + NES13 (1.047s) + NES05 (0.905s) + NES11 (0.834s) = 4.54s
victory_files = [
    os.path.join(NES, "jingles_NES00.ogg"),  # intro long majeur
    os.path.join(NES, "jingles_NES13.ogg"),  # crescendo
    os.path.join(NES, "jingles_NES05.ogg"),  # montee
    os.path.join(NES, "jingles_NES11.ogg"),  # final
]
concat_music(victory_files, "music_stinger_victory.wav")

# music_stinger_death : ~2.9s
# HIT09 (0.745s) + HIT11 (0.958s) + HIT15 (1.163s) = 2.86s
death_files = [
    os.path.join(HIT, "jingles_HIT09.ogg"),
    os.path.join(HIT, "jingles_HIT11.ogg"),
    os.path.join(HIT, "jingles_HIT15.ogg"),
]
concat_music(death_files, "music_stinger_death.wav")

# ================================================================
# RAPPORT FINAL
# ================================================================
print("\n" + "=" * 60)
print(f"TERMINE : {len(converted)} OK, {len(errors)} erreurs")
if errors:
    print("ERREURS :")
    for e in errors:
        print(f"  - {e}")
print("=" * 60)

# Verifier les durees des stingers produits
print("\nDurees des stingers generes :")
for fname in ["music_stinger_victory.wav", "music_stinger_death.wav"]:
    fpath = os.path.join(DST_MUS, fname)
    if os.path.exists(fpath):
        result = subprocess.run([FFPROBE, "-i", fpath, "-show_entries", "format=duration",
                                  "-v", "quiet", "-of", "csv=p=0"],
                                 capture_output=True, text=True)
        dur = result.stdout.strip()
        size = os.path.getsize(fpath)
        print(f"  {fname}: {dur}s ({size//1024} KB)")
