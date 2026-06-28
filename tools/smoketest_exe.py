"""Lance le .exe exporte, attend, capture le menu pour confirmer qu'il demarre."""
import subprocess, time, os
import pyautogui, pygetwindow as gw
pyautogui.FAILSAFE = False

EXE = r"C:\CODE\JEUX\cyborg-survivor\build\ChimeraProtocol.exe"
OUT = r"C:\CODE\JEUX\cyborg-survivor\docs\exe_smoketest.png"

proc = subprocess.Popen([EXE])
time.sleep(7)
alive = proc.poll() is None
win = next((w for w in gw.getAllWindows() if "Chimera" in w.title), None)
if win:
    try:
        win.moveTo(40, 40); win.activate()
    except Exception:
        pass
    time.sleep(1)
    img = pyautogui.screenshot(region=(max(win.left,0), max(win.top,0), win.width, win.height))
else:
    img = pyautogui.screenshot()
img.save(OUT)
print("ALIVE" if alive else "CRASHED", "| window:", bool(win), "| SAVED", OUT)
time.sleep(0.3)
proc.terminate()
