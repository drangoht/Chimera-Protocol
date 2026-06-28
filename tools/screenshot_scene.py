"""Lance une scene Godot arbitraire et capture la fenetre. Env: SCENE, OUT, WAIT."""
import subprocess, time, os
import pyautogui, pygetwindow as gw
pyautogui.FAILSAFE = False

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ  = r"C:\CODE\JEUX\cyborg-survivor"
SCENE = os.environ.get("SCENE", "res://scenes/MainMenu.tscn")
OUT   = os.path.join(PROJ, "docs", os.environ.get("OUT", "scene_review.png"))
WAIT  = float(os.environ.get("WAIT", "4"))

proc = subprocess.Popen([GODOT, "--path", PROJ, "--rendering-driver", "d3d12", SCENE])
time.sleep(WAIT)
win = next((w for w in gw.getAllWindows() if "Chimera" in w.title), None)
if win:
    try:
        win.moveTo(40, 40); win.activate()
    except Exception:
        pass
    time.sleep(1)
    scroll = int(os.environ.get("SCROLL", "0"))
    if scroll:
        mx, my = win.left + win.width // 2, win.top + win.height // 2 - 60
        pyautogui.moveTo(mx, my)
        pyautogui.click(mx, my)  # focus la fenetre/zone scroll
        time.sleep(0.2)
        for _ in range(abs(scroll)):
            pyautogui.scroll(-120 if scroll < 0 else 120, x=mx, y=my)
            time.sleep(0.05)
        time.sleep(0.6)
    img = pyautogui.screenshot(region=(max(win.left,0), max(win.top,0), win.width, win.height))
else:
    img = pyautogui.screenshot()
img.save(OUT)
print("SAVED", OUT, img.size)
time.sleep(0.3)
proc.terminate()
