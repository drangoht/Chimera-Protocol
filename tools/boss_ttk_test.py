"""Mesure empirique du TTK boss : lance Game.tscn avec --debug-boss (loadout de
reference + boss isole), kite le joueur pour survivre, capture des frames horodatees
pour encadrer l'apparition de l'ecran « EXTRACTION REUSSIE »."""
import subprocess, time, os
import pyautogui, pygetwindow as gw
pyautogui.FAILSAFE = False

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ  = r"C:\CODE\JEUX\chimera-protocol"
OUTDIR = os.path.join(PROJ, "docs", "boss_ttk")
os.makedirs(OUTDIR, exist_ok=True)
SHOTS = [12, 20, 28, 36, 44]  # secondes de jeu

proc = subprocess.Popen([GODOT, "--path", PROJ, "--rendering-driver", "d3d12",
                         "res://scenes/Game.tscn", "--", "--debug-boss"])
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

cx = (win.left + win.width // 2) if win else 688
cy = (win.top + win.height // 2) if win else 420

# Phase 0 : purge des level-ups de depart (XP « Memoire Residuelle ») par rafale de
# clics sur la carte centrale. Le jeu reste en pause tant qu'il en reste -> on vide la file
# AVANT de demarrer le chrono, pour que le TTK mesure du temps de combat reel.
for _ in range(12):
    pyautogui.click(cx, cy)
    time.sleep(0.3)

seq = ["right", "down", "left", "up", "right", "up", "left", "down"]
t0 = time.time()
i = 0
held = None
next_shot = 0
while next_shot < len(SHOTS):
    elapsed = time.time() - t0
    # kite continu pour rester en vie + clic de securite (level-up residuel eventuel)
    pyautogui.click(cx, cy)
    key = seq[i % len(seq)]
    if held and held != key:
        pyautogui.keyUp(held)
    pyautogui.keyDown(key); held = key
    i += 1
    # capture si on a atteint le prochain jalon
    if next_shot < len(SHOTS) and elapsed >= SHOTS[next_shot]:
        if held:
            pyautogui.keyUp(held); held = None
        time.sleep(0.15)
        if win:
            img = pyautogui.screenshot(region=(max(win.left,0), max(win.top,0), win.width, win.height))
        else:
            img = pyautogui.screenshot()
        path = os.path.join(OUTDIR, f"t{SHOTS[next_shot]:02d}.png")
        img.save(path)
        print("SAVED", path, img.size)
        next_shot += 1
    time.sleep(1.0)
if held:
    pyautogui.keyUp(held)
time.sleep(0.3)
proc.terminate()
print("DONE")
