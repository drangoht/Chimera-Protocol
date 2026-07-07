"""Lance Game.tscn avec --force-fusion=all (equipe d'office les 2 fusions de greffes),
dissipe les cartes de level-up qui peuvent apparaitre tot en run (clic carte du milieu),
laisse quelques ennemis approcher pour donner du contexte, puis capture le HUD.

Usage : python tools/capture_hud_fusion.py
Env : WAIT (defaut 16), OUT (defaut docs/store_screens/hud_fusion.png)
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
OUT = os.environ.get("OUT", os.path.join(PROJ, "docs", "store_screens", "hud_fusion.png"))
WAIT = float(os.environ.get("WAIT", "16"))

proc = subprocess.Popen(
    [GODOT, "--path", PROJ, "--rendering-driver", "d3d12", "res://scenes/Game.tscn",
     "--force-fusion=all"]
)
try:
    hwnd = wait_for_window("Chimera", timeout=25.0)
    if not hwnd:
        print("Fenetre introuvable")
        sys.exit(1)

    import win32gui

    win_l, win_t, win_r, win_b = win32gui.GetWindowRect(hwnd)
    cx, cy = (win_l + win_r) // 2, (win_t + win_b) // 2
    pyautogui.click(win_l + (win_r - win_l) // 3, win_t + 15)  # focus OS reel
    time.sleep(0.5)

    t0 = time.time()
    # clique periodiquement le centre (carte du milieu d'un eventuel level-up) tout en
    # laissant le temps filer -- inoffensif si aucune carte n'est affichee
    while time.time() - t0 < WAIT:
        pyautogui.click(cx, cy)
        time.sleep(1.5)

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
