"""Lance Game.tscn, kite en cercle assez longtemps pour remplir la jauge de greffe la plus
rapide (jauge "swarm", seuil 30 kills straight_chase — cf. data/grafts.json gauges.thresholds),
et capture l'ecran quand l'AssimilationScreen apparait.

Pas besoin de detecter precisement l'instant d'apparition : des que le modal s'ouvre, le jeu
se met en PAUSE (GetTree().Paused = true, cf. ModalQueue) et le reste, donc une capture en toute
fin de script (apres une duree large) suffit -- si le modal est deja ouvert, les touches de
direction envoyees ensuite n'ont plus aucun effet (jeu fige).

Usage :
    python tools/capture_assimilation.py
Env :
    DURATION   secondes de kite avant capture (defaut 180 -- large marge sur les ~120s estimees)
    OUT        chemin de sortie (defaut docs/store_screens/assimilation_graft.png)
"""
import os
import subprocess
import sys
import time

import pyautogui

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from window_capture import capture_window, wait_for_window  # noqa: E402

pyautogui.FAILSAFE = False

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ = r"C:\CODE\JEUX\chimera-protocol"
OUT = os.environ.get("OUT", os.path.join(PROJ, "docs", "store_screens", "assimilation_graft.png"))
DURATION = float(os.environ.get("DURATION", "180"))

proc = subprocess.Popen(
    [GODOT, "--path", PROJ, "--rendering-driver", "d3d12", "res://scenes/Game.tscn"]
)
try:
    hwnd = wait_for_window("Chimera", timeout=25.0)
    if not hwnd:
        print("Fenetre introuvable")
        sys.exit(1)

    import win32gui

    win_l, win_t, win_r, _ = win32gui.GetWindowRect(hwnd)
    pyautogui.click(win_l + (win_r - win_l) // 3, win_t + 15)  # focus OS reel
    time.sleep(0.5)

    win_l2, win_t2, win_r2, win_b2 = win32gui.GetWindowRect(hwnd)
    cx, cy = (win_l2 + win_r2) // 2, (win_t2 + win_b2) // 2

    seq = ["right", "down", "left", "up", "right", "up", "left", "down"]
    t0 = time.time()
    i = 0
    held = None
    while time.time() - t0 < DURATION:
        key = seq[i % len(seq)]
        if held and held != key:
            pyautogui.keyUp(held)
        pyautogui.keyDown(key)
        held = key
        i += 1
        # Dissipe une eventuelle LevelUpScreen (prioritaire dans la ModalQueue devant
        # l'AssimilationScreen -- sans ca le jeu resterait fige sur le premier level-up
        # et plus aucun kill ne s'accumulerait). Inoffensif si aucune carte n'est affichee.
        pyautogui.click(cx, cy)
        time.sleep(1.1)
    if held:
        pyautogui.keyUp(held)
    time.sleep(1.0)

    img = capture_window(hwnd, client_only=True)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    img.save(OUT)
    print("SAVED", OUT, img.size)
finally:
    proc.terminate()
    try:
        proc.wait(timeout=5)
    except Exception:
        proc.kill()
