"""Lance la cut-scene d'intro et capture plusieurs frames a des instants espaces
pour verifier visuellement chaque plan. Sauvegarde dans docs/intro_shot_XX.png.
"""
import os
import subprocess
import sys
import time

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from window_capture import capture_window, wait_for_window  # noqa: E402

GODOT = r"C:\CODE\JEUX\Godot_v4.7-stable_mono_win64\Godot_v4.7-stable_mono_win64.exe"
PROJ = r"C:\CODE\JEUX\chimera-protocol"
OUT = os.path.join(PROJ, "docs")

# Instants de capture (s depuis l'apparition de la fenetre) -> un par plan environ.
SHOTS = [2.0, 6.5, 11.0, 15.5, 20.0, 24.5]

proc = subprocess.Popen(
    [GODOT, "--rendering-driver", "d3d12", "--path", PROJ, "res://scenes/ui/IntroScreen.tscn"],
)
try:
    hwnd = wait_for_window("Chimera", timeout=25.0)
    if not hwnd:
        print("Fenetre introuvable")
        sys.exit(1)
    t0 = time.time()
    for i, when in enumerate(SHOTS):
        target = t0 + when
        time.sleep(max(0.0, target - time.time()))
        try:
            img = capture_window(hwnd, client_only=True)
            path = os.path.join(OUT, f"intro_shot_{i:02d}.png")
            img.save(path)
            print(f"[{when:5.1f}s] -> {path} ({img.size[0]}x{img.size[1]})")
        except Exception as e:
            print(f"[{when:5.1f}s] capture echouee: {e}")
finally:
    proc.terminate()
    try:
        proc.wait(timeout=5)
    except Exception:
        proc.kill()
