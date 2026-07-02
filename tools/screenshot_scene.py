"""Lance une scene Godot arbitraire et capture la fenetre.

Env :
    SCENE       res://scenes/... (defaut MainMenu.tscn)
    OUT         nom de fichier relatif a docs/ (defaut scene_review.png)
    WAIT        secondes avant capture (defaut 4)
    SCROLL      nb de "crans" de scroll avant capture (0 = aucun ; negatif = vers le haut)
    CLIENT_ONLY "1" pour ne capturer que la zone client (sans barre de titre/bordures,
                donne exactement la resolution du viewport Godot, ex. 1280x720) ;
                "0" (defaut) capture la fenetre complete comme les captures historiques
                menu/charsel/levelsel/bestiary/arsenal (~1294x726 avec decorations).
    EXTRA_ARGS  arguments supplementaires passes tels quels a Godot (ex. "--biome=neon")

Capture via `user32.PrintWindow` (tools/window_capture.py) plutot que
`pyautogui.screenshot(region=...)` : reste fiable meme si la fenetre Godot est occultee
par une autre application au premier plan (bug constate par le game-tester, cf.
docs/TEST_REPORT.md).
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
SCENE = os.environ.get("SCENE", "res://scenes/MainMenu.tscn")
OUT = os.path.join(PROJ, "docs", os.environ.get("OUT", "scene_review.png"))
WAIT = float(os.environ.get("WAIT", "4"))
CLIENT_ONLY = os.environ.get("CLIENT_ONLY", "0") == "1"
EXTRA_ARGS = os.environ.get("EXTRA_ARGS", "").split()

cmd = [GODOT, "--path", PROJ, "--rendering-driver", "d3d12", SCENE] + EXTRA_ARGS
proc = subprocess.Popen(cmd)
time.sleep(WAIT)

hwnd = wait_for_window("Chimera", timeout=15.0)
if hwnd is None:
    # Repli : n'importe quelle fenetre Godot suffisamment large.
    hwnd = wait_for_window("Godot", timeout=5.0)

if hwnd:
    import win32gui

    try:
        win32gui.SetForegroundWindow(hwnd)
    except Exception:
        pass

    # SetForegroundWindow echoue souvent silencieusement (restriction Windows : un
    # process ne peut voler le focus au premier plan que sous certaines conditions).
    # Un vrai clic materiel (SendInput, via pyautogui) sur la barre de titre force le
    # focus OS de facon fiable -- necessaire pour que les touches (KEYS) arrivent
    # bien a la fenetre Godot et non au terminal/IDE qui a lance ce script.
    win_l, win_t, win_r, _ = win32gui.GetWindowRect(hwnd)
    title_x = win_l + (win_r - win_l) // 3
    title_y = win_t + 15
    pyautogui.click(title_x, title_y)
    time.sleep(0.5)

    # NOTE : les listes Bestiaire/Arsenal ne sont PAS des ScrollContainer standard au
    # sens de la molette -- le defilement est pilote au clavier (ui_down/ui_page_down)
    # dans CodexScreenBase._UnhandledInput (aucune ligne n'est focalisable). La molette
    # pyautogui.scroll() n'a donc AUCUN effet ; il faut envoyer des touches.
    keys = os.environ.get("KEYS", "")
    if keys:
        for key in keys.split(","):
            key = key.strip()
            if not key:
                continue
            pyautogui.press(key)
            time.sleep(0.15)
        time.sleep(0.6)

    img = capture_window(hwnd, client_only=CLIENT_ONLY)
else:
    print("WINDOW NOT FOUND (PrintWindow) -- fallback plein ecran")
    img = pyautogui.screenshot()

os.makedirs(os.path.dirname(OUT), exist_ok=True)
img.save(OUT)
print("SAVED", OUT, img.size)
time.sleep(0.3)
proc.terminate()
