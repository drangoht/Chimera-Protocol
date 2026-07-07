"""Valide visuellement les props de silhouette-chimère (Phase B) : lance Game.tscn avec toutes les
greffes de base equipees d'office (--force-graft=all) OU les 2 fusions (--force-fusion=all), deplace
le joueur a droite puis a gauche (pour verifier le miroir des props selon le facing), et capture.

Usage :
    python tools/capture_graft_silhouette.py            # 5 greffes de base
    FUSION=1 python tools/capture_graft_silhouette.py    # les 2 fusions
Env :
    OUTDIR   dossier de sortie (defaut : scratchpad de session)
"""
import os
import subprocess
import sys
import time

import pyautogui
import win32gui
import win32process

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from window_capture import capture_window  # noqa: E402

pyautogui.FAILSAFE = False


def wait_for_pid_window(pid, timeout=25.0, min_width=400):
    """Cible la fenetre appartenant AU process Godot lance (par PID) : evite d'attraper une
    autre fenetre titree 'Chimera' (navigateur/editeur affichant le devlog)."""
    t0 = time.time()
    while time.time() - t0 < timeout:
        found = []

        def _enum(hwnd, _):
            if not win32gui.IsWindowVisible(hwnd):
                return
            _, wpid = win32process.GetWindowThreadProcessId(hwnd)
            if wpid == pid:
                l, t, r, b = win32gui.GetWindowRect(hwnd)
                if (r - l) > min_width:
                    found.append(hwnd)

        win32gui.EnumWindows(_enum, None)
        if found:
            return found[0]
        time.sleep(0.3)
    return None

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ = r"C:\CODE\JEUX\chimera-protocol"
OUTDIR = os.environ.get(
    "OUTDIR",
    r"C:\Users\drang\AppData\Local\Temp\claude\C--CODE-JEUX-chimera-protocol\ea3f2960-a507-4422-967f-b0e4d4db7a59\scratchpad",
)
FUSION = os.environ.get("FUSION")
flag = "--force-fusion=all" if FUSION else "--force-graft=all"
tag = "fusion" if FUSION else "graft"

proc = subprocess.Popen(
    [GODOT, "--path", PROJ, "--rendering-driver", "d3d12", "res://scenes/Game.tscn", "--", flag]
)
try:
    hwnd = wait_for_pid_window(proc.pid, timeout=25.0)
    if not hwnd:
        print("Fenetre du jeu (PID) introuvable")
        sys.exit(1)

    win_l, win_t, win_r, win_b = win32gui.GetWindowRect(hwnd)
    cx, cy = (win_l + win_r) // 2, (win_t + win_b) // 2
    pyautogui.click(win_l + (win_r - win_l) // 3, win_t + 15)  # focus OS reel
    time.sleep(0.8)

    os.makedirs(OUTDIR, exist_ok=True)

    DASH = os.environ.get("DASH")  # tape Shift (dash) pendant le mouvement -> déclenche la nova

    def kite_and_shot(key, label, hold=1.4):
        # Dissipe d'eventuelles cartes de level-up (clic centre = selection) avant de bouger.
        for _ in range(3):
            pyautogui.click(cx, cy)
            time.sleep(0.25)
        pyautogui.keyDown(key)
        time.sleep(hold)
        if DASH:
            pyautogui.press("shift")  # ruade -> nova a l'arrivee
            time.sleep(0.12)
        # Capture EN MOUVEMENT (le facing = key) : on shoote juste avant de relacher.
        img = capture_window(hwnd, client_only=True)
        pyautogui.keyUp(key)
        out = os.path.join(OUTDIR, f"silhouette_{tag}_{label}.png")
        img.save(out)
        print("SAVED", out, img.size)
        time.sleep(0.4)

    # Laisse la premiere seconde passer (spawn + equip differe des greffes).
    time.sleep(1.5)
    for _ in range(3):  # absorbe les premiers level-ups
        pyautogui.click(cx, cy)
        time.sleep(0.3)

    kite_and_shot("right", "facing_right")
    kite_and_shot("left", "facing_left")
    kite_and_shot("down", "facing_down")
finally:
    proc.terminate()
    try:
        proc.wait(timeout=5)
    except Exception:
        proc.kill()
