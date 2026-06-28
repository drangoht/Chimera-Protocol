"""Lance Game.tscn, fait kiter le joueur en cercle, capture la nuee + VFX."""
import subprocess, time, os
import pyautogui, pygetwindow as gw
pyautogui.FAILSAFE = False

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ  = r"C:\CODE\JEUX\chimera-protocol"
OUT   = os.path.join(PROJ, "docs", "swarm_review.png")
CAP_AT = float(os.environ.get("CAP_AT", "40"))  # secondes de jeu avant capture

proc = subprocess.Popen([GODOT, "--path", PROJ, "--rendering-driver", "d3d12",
                         "res://scenes/Game.tscn"])
time.sleep(6)
win = None
for w in gw.getAllWindows():
    if "Chimera" in w.title:
        win = w; break
if win:
    try:
        win.moveTo(40, 40); win.activate()
    except Exception:
        pass
time.sleep(1)

# Kite en cercle : maintient des fleches, change toutes les ~1.3 s
seq = ["right", "down", "left", "up", "right", "up", "left", "down"]
t0 = time.time()
i = 0
held = None
cx = (win.left + win.width // 2) if win else 688
cy = (win.top + win.height // 2) if win else 420
STILL_AT = CAP_AT - 3.5   # arret du kite en fin : la nuee converge sur le joueur centre
while time.time() - t0 < CAP_AT:
    elapsed = time.time() - t0
    if elapsed < STILL_AT:
        key = seq[i % len(seq)]
        if held and held != key:
            pyautogui.keyUp(held)
        pyautogui.keyDown(key); held = key
        i += 1
    elif held:
        pyautogui.keyUp(held); held = None
    time.sleep(1.0)
    # valide un eventuel level-up (clique la carte du milieu) sans gener le combat
    if not os.environ.get("NOCLICK"):
        pyautogui.click(cx, cy)
if held:
    pyautogui.keyUp(held)
time.sleep(0.2)

# capture recadree sur la fenetre
if win:
    img = pyautogui.screenshot(region=(max(win.left,0), max(win.top,0), win.width, win.height))
else:
    img = pyautogui.screenshot()
img.save(OUT)
print("SAVED", OUT, img.size)
time.sleep(0.3)
proc.terminate()
