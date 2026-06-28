"""Lance Game.tscn, attend, capture la fenetre pour juger le HUD."""
import subprocess, time, sys, os
import pyautogui, pygetwindow as gw

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ  = r"C:\CODE\JEUX\cyborg-survivor"
OUT   = os.path.join(PROJ, "docs", "menu_review.png")

proc = subprocess.Popen([GODOT, "--path", PROJ, "--rendering-driver", "d3d12",
                         "res://scenes/MainMenu.tscn"])
time.sleep(5)  # laisse le run demarrer + ennemis spawner
try:
    wins = [w for w in gw.getAllWindows() if "Chimera" in w.title]
    if not wins:
        wins = [w for w in gw.getAllWindows() if "Godot" in w.title and w.width > 400]
    if wins:
        w = wins[0]
        try:
            w.moveTo(40, 40)
            w.activate()
        except Exception:
            pass
        time.sleep(1.2)
        left, top, width, height = w.left, w.top, w.width, w.height
        img = pyautogui.screenshot(region=(max(left,0), max(top,0), width, height))
    else:
        print("WINDOW NOT FOUND, full desktop")
        img = pyautogui.screenshot()
    img.save(OUT)
    print("SAVED", OUT, img.size)
finally:
    time.sleep(0.5)
    proc.terminate()
