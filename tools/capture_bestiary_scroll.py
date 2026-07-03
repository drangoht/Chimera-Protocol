"""Capture le Bestiaire en anglais, scrolle via un DRAG SOURIS sur la scrollbar (les
touches clavier n'atteignent pas la fenetre Godot dans cet environnement, cf. notes
graphiste 2026-07-03) jusqu'a voir un ennemi de la faune par biome (ex. aether_golem),
puis sauvegarde dans docs/store_screens/bestiary.png.

Etapes :
1. Force settings.cfg language=en.
2. Lance BestiaryScreen.tscn, capture initiale (CLIENT_ONLY) pour reperer la scrollbar.
3. Drag progressif de la scrollbar vers le bas, capture apres chaque drag, jusqu'a
   trouver un nouvel ennemi (biome) visible ou defilement max atteint.
4. Sauvegarde la derniere capture utile.
"""
import configparser
import os
import sys
import time

import pyautogui

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from window_capture import capture_window, wait_for_window  # noqa: E402

pyautogui.FAILSAFE = False

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ = r"C:\CODE\JEUX\chimera-protocol"
SETTINGS = r"C:\Users\drang\AppData\Roaming\Godot\app_userdata\Chimera Protocol\settings.cfg"
SCRATCH = r"C:\Users\drang\AppData\Local\Temp\claude\C--CODE-JEUX-chimera-protocol\911f3186-6fd5-46fb-acc4-344a0adaf844\scratchpad"
OUT = os.path.join(PROJ, "docs", "store_screens", "bestiary.png")

# 1. Force la langue anglaise (residu de session possible, cf. notes graphiste).
cfg = configparser.ConfigParser()
cfg.optionxform = str
cfg.read(SETTINGS)
if "display" not in cfg:
    cfg["display"] = {}
cfg["display"]["language"] = '"en"'
with open(SETTINGS, "w") as f:
    cfg.write(f, space_around_delimiters=False)
print("settings.cfg language forced to en")

import subprocess

cmd = [GODOT, "--path", PROJ, "--rendering-driver", "d3d12", "res://scenes/ui/BestiaryScreen.tscn"]
proc = subprocess.Popen(cmd)
time.sleep(4.5)

hwnd = wait_for_window("Chimera", timeout=15.0)
if hwnd is None:
    hwnd = wait_for_window("Godot", timeout=5.0)

if not hwnd:
    print("WINDOW NOT FOUND")
    sys.exit(1)

import win32gui

win_l, win_t, win_r, win_b = win32gui.GetWindowRect(hwnd)
try:
    win32gui.SetForegroundWindow(hwnd)
except Exception:
    pass
title_x = win_l + (win_r - win_l) // 3
title_y = win_t + 15
pyautogui.click(title_x, title_y)
time.sleep(0.5)

os.makedirs(SCRATCH, exist_ok=True)

img0 = capture_window(hwnd, client_only=True)
img0.save(os.path.join(SCRATCH, "bestiary_scroll_probe.png"))
print("probe saved", img0.size)

# Repere la colonne de la scrollbar : scan horizontal a y=150 (dans la 1ere carte) pour
# le pixel le plus clair dans la bande x in [1190, 1279] (bord droit, marge=60 -> conteneur
# jusqu'a x=1220, scrollbar flush a droite de la ScrollContainer).
w, h = img0.size
px = img0.load()
y_probe = 150
best_x, best_lum = None, -1
for x in range(min(1190, w - 1), w):
    r, g, b = px[x, y_probe][:3]
    lum = r + g + b
    if lum > best_lum:
        best_lum = lum
        best_x = x
print("scrollbar probe column:", best_x, "lum", best_lum)

client_l, client_t = win32gui.ClientToScreen(hwnd, (0, 0))


def to_screen(x, y):
    return client_l + x, client_t + y


# Position de la scrollbar (colonne detectee). Drag progressif vers le bas.
sx = best_x if best_x else (w - 10)
start_screen = to_screen(sx, 150)
found = False
for step in range(1, 8):
    target_y = min(150 + step * 90, h - 20)
    end_screen = to_screen(sx, target_y)
    pyautogui.moveTo(*start_screen)
    pyautogui.mouseDown()
    time.sleep(0.1)
    pyautogui.moveTo(*end_screen, duration=0.3)
    pyautogui.mouseUp()
    time.sleep(0.4)
    img = capture_window(hwnd, client_only=True)
    probe_path = os.path.join(SCRATCH, f"bestiary_scroll_{step:02d}.png")
    img.save(probe_path)
    print("step", step, "->", probe_path, "drag to y=", target_y)
    start_screen = to_screen(sx, target_y)

proc.terminate()
print("DONE — inspect scratchpad screenshots to pick the right frame")
