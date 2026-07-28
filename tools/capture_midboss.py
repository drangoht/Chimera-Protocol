"""Valide visuellement les mid-boss de biome (docs/GDD.md section 32) en conditions de jeu.

Lance Game.tscn avec `--debug-enemy=<id>` (champion isole, spawn ambiant coupe) dans son biome,
puis prend une RAFALE de captures : le bouclier du Gardien et le cone de la Sentinelle sont dessines
EN CODE (_Draw), ils n'apparaissent donc sur aucune planche de sprites -- seule une capture en jeu
les montre.

La rafale est volontairement rapprochee : le hook de debug equipe un loadout de test (~600 DPS) et
le champion ne survit que quelques secondes. Mieux vaut 10 vignettes dont 3 utiles qu'une capture
unique prise apres sa mort.

Usage :
    python tools/capture_midboss.py                    # les 3 mid-boss
    python tools/capture_midboss.py neon_warden        # un seul
Env :
    OUTDIR   dossier de sortie (defaut : scratchpad de session)
    SHOTS    nombre de captures par champion (defaut 10)
    EVERY    intervalle entre captures, secondes (defaut 0.7)
    WARMUP   attente avant la rafale, secondes (defaut 12)

WARMUP n'est pas du confort : un champion apparait HORS CHAMP (a ~800 px) et rejoint le joueur a sa
propre vitesse -- 58 px/s pour la Sentinelle Cryo, qui garde en plus ses distances a 250 px. Sans
attente, la rafale entiere se joue avant qu'il entre dans le cadre, et on conclut a tort qu'il ne
spawne pas (constate le 2026-07-28 : huit captures d'une arene vide).
"""
import os
import subprocess
import sys
import time

import pyautogui
import win32gui

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from window_capture import capture_window, wait_for_window_by_pid  # noqa: E402

pyautogui.FAILSAFE = False

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ = r"C:\CODE\JEUX\chimera-protocol"
OUTDIR = os.environ.get(
    "OUTDIR",
    r"C:\Users\drang\AppData\Local\Temp\claude\C--CODE-JEUX-chimera-protocol"
    r"\1cfff2eb-2534-4f31-8315-118ed1a0b5e4\scratchpad",
)
SHOTS = int(os.environ.get("SHOTS", "10"))
EVERY = float(os.environ.get("EVERY", "0.7"))
WARMUP = float(os.environ.get("WARMUP", "12"))

# id du mid-boss -> biome dans lequel il apparait (cf. data/enemies.json, champ `biomes`).
MIDBOSSES = {
    "molten_colossus": "fournaise",
    "cryo_sentinel":   "givre",
    "neon_warden":     "neon",
}


def capture_one(mid_id, biome):
    proc = subprocess.Popen([
        GODOT, "--path", PROJ, "--rendering-driver", "d3d12",
        "res://scenes/Game.tscn", "--",
        f"--debug-enemy={mid_id}", f"--biome={biome}", "--invuln",
    ])
    try:
        hwnd = wait_for_window_by_pid(proc.pid, timeout=25.0)
        if not hwnd:
            print(f"  {mid_id} : fenetre introuvable")
            return

        win_l, win_t, win_r, win_b = win32gui.GetWindowRect(hwnd)
        cx, cy = (win_l + win_r) // 2, (win_t + win_b) // 2
        pyautogui.click(win_l + (win_r - win_l) // 3, win_t + 15)   # focus OS reel
        time.sleep(1.2)

        # Absorbe les cartes de level-up qui masqueraient l'arene.
        for _ in range(3):
            pyautogui.click(cx, cy)
            time.sleep(0.2)

        # Laisse le champion traverser la distance qui le separe du joueur (cf. docstring).
        time.sleep(WARMUP)

        os.makedirs(OUTDIR, exist_ok=True)
        for i in range(SHOTS):
            img = capture_window(hwnd, client_only=True)
            out = os.path.join(OUTDIR, f"midboss_{mid_id}_{i:02d}.png")
            img.save(out)
            time.sleep(EVERY)
        print(f"  {mid_id} : {SHOTS} captures -> {OUTDIR}")
    finally:
        proc.terminate()
        try:
            proc.wait(timeout=5)
        except Exception:
            proc.kill()


def main():
    wanted = sys.argv[1:] or list(MIDBOSSES)
    for mid_id in wanted:
        if mid_id not in MIDBOSSES:
            print(f"id inconnu : {mid_id} (attendus : {', '.join(MIDBOSSES)})")
            continue
        print(f"{mid_id} ({MIDBOSSES[mid_id]})...")
        capture_one(mid_id, MIDBOSSES[mid_id])


if __name__ == "__main__":
    main()
