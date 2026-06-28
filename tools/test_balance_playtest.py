"""
Playtest d'équilibrage — Chimera Protocol
3 runs : Run 1 standard / Run 2 avec hub upgrades / Run 3 focus fusions
Captures screenshots à intervalles réguliers pour analyse visuelle.

Usage : python -X utf8 tools/test_balance_playtest.py
"""

import subprocess
import time
import os
import sys
import datetime

import pyautogui
import pygetwindow as gw
from PIL import Image

# ---------------------------------------------------------------------------
# Config
# ---------------------------------------------------------------------------

EXE_PATH     = r"C:\CODE\JEUX\chimera-protocol\build\ChimeraProtocol.exe"
SCREENS_DIR  = r"C:\CODE\JEUX\chimera-protocol\docs\balance_screenshots"
WINDOW_TITLE = "Chimera Protocol"
STARTUP_WAIT = 7.0
SCENE_WAIT   = 2.5
KEY_WAIT     = 0.35

pyautogui.PAUSE    = 0.05
pyautogui.FAILSAFE = True

os.makedirs(SCREENS_DIR, exist_ok=True)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

_step    = 0
_results = []
_timings = []  # (label, elapsed_seconds, note)

def _log(msg: str):
    print(msg)
    _results.append(msg)

def _screenshot(label: str) -> str:
    global _step
    _step += 1
    filename = f"{_step:02d}_{label}.png"
    path = os.path.join(SCREENS_DIR, filename)
    pyautogui.screenshot(path)
    _log(f"  [screenshot] {filename}")
    return path

def _find_win():
    wins = gw.getWindowsWithTitle(WINDOW_TITLE)
    return wins[0] if wins else None

def _focus():
    win = _find_win()
    if win:
        try:
            win.activate()
            time.sleep(0.3)
            return True
        except Exception:
            pass
    _log("  [warn] fenetre introuvable")
    return False

def _alive(proc) -> bool:
    return proc.poll() is None

def _key(*keys, wait: float = KEY_WAIT):
    for k in keys:
        pyautogui.press(k)
        time.sleep(wait)

def _launch() -> subprocess.Popen:
    proc = subprocess.Popen([EXE_PATH])
    _log(f"  Jeu lance (PID {proc.pid})")
    time.sleep(STARTUP_WAIT)
    _focus()
    return proc

def _kill(proc):
    if _alive(proc):
        proc.terminate()
        time.sleep(1.0)
        _log("  Jeu termine.")

def _hold_move(direction: str, duration: float):
    """Maintient une touche de direction enfoncée pendant duration secondes."""
    pyautogui.keyDown(direction)
    time.sleep(duration)
    pyautogui.keyUp(direction)

def _circle_move(total_duration: float, step_duration: float = 0.5):
    """Mouvement circulaire (right/up/left/down) pendant total_duration secondes."""
    dirs = ["right", "up", "left", "down"]
    elapsed = 0.0
    i = 0
    while elapsed < total_duration:
        d = dirs[i % 4]
        actual = min(step_duration, total_duration - elapsed)
        pyautogui.keyDown(d)
        time.sleep(actual)
        pyautogui.keyUp(d)
        time.sleep(0.05)
        elapsed += actual + 0.05
        i += 1

def _game_screenshot_pil():
    win = _find_win()
    if win:
        region = (win.left, win.top, win.width, win.height)
        return pyautogui.screenshot(region=region)
    return pyautogui.screenshot()

def _overlay_present(r_max=40, g_max=40, b_max=60, blue_bias=8, threshold=0.35) -> bool:
    """Détecte un overlay sombre bleu-biaisé (LevelUpScreen ou RunEndScreen)."""
    img = _game_screenshot_pil()
    w, h = img.size
    xs = [int(w * f) for f in (0.20, 0.35, 0.50, 0.65, 0.80)]
    ys = [int(h * f) for f in (0.25, 0.40, 0.60, 0.75)]
    matched = total = 0
    for x in xs:
        for y in ys:
            px = img.getpixel((x, y))
            r, g, b = int(px[0]), int(px[1]), int(px[2])
            total += 1
            if r < r_max and g < g_max and b < b_max and b > r + blue_bias and b > g + blue_bias:
                matched += 1
    cov = matched / total if total > 0 else 0
    return cov >= threshold

def _detect_label_color() -> str:
    """Retourne 'cyan' (LevelUp), 'red' (mort), 'unknown'."""
    img = _game_screenshot_pil()
    w, h = img.size
    xs = [int(w * f) for f in (0.35, 0.39, 0.43, 0.47, 0.50, 0.53, 0.57, 0.61, 0.65)]
    y_start = int(h * 0.09)
    y_end   = int(h * 0.23)
    red = cyan = 0
    for x in xs:
        for y in range(y_start, y_end, 2):
            r, g, b = [int(v) for v in img.getpixel((x, y))[:3]]
            if r > 140 and g < 110 and b < 90:
                red += 1
            if g > 120 and b > 100 and r < 120 and g + b > r * 4 + 80:
                cyan += 1
    if red >= 3:
        return 'red'
    if cyan >= 3:
        return 'cyan'
    return 'unknown'

def _detect_levelup() -> bool:
    if not _overlay_present(r_max=30, g_max=30, b_max=40, blue_bias=3):
        return False
    return _detect_label_color() == 'cyan'

def _detect_runend() -> bool:
    if not _overlay_present():
        return False
    return _detect_label_color() == 'red'

def _play_and_watch(run_label: str, run_start_time: float, max_seconds: int,
                    capture_at: list, stop_on_death: bool = True) -> dict:
    """
    Joue la run en effectuant un mouvement circulaire.
    - capture_at : liste de secondes depuis run_start pour les captures
    - Arrêt si mort détectée (stop_on_death=True)
    Retourne {'levelups': [t1, t2,...], 'death_time': t_ou_None}
    """
    result = {'levelups': [], 'death_time': None, 'alive': True}
    dirs = ["right", "up", "left", "down"]
    i = 0
    run_elapsed = 0.0
    last_capture = -1.0
    last_check   = 0.0
    check_interval = 2.0  # toutes les 2s : vérifier overlay

    _log(f"\n  [play_and_watch] {run_label} — max {max_seconds}s")

    while run_elapsed < max_seconds:
        # Capture programmée
        for cap_t in capture_at:
            if last_capture < cap_t <= run_elapsed:
                _focus()
                _screenshot(f"{run_label}_t{int(run_elapsed)}s")
                last_capture = cap_t

        # Vérification overlay
        elapsed_since_check = run_elapsed - last_check
        if elapsed_since_check >= check_interval:
            last_check = run_elapsed
            if _detect_levelup():
                t = run_elapsed
                result['levelups'].append(t)
                _log(f"  [LEVELUP] détecté à t={t:.0f}s")
                _focus()
                _screenshot(f"{run_label}_levelup_t{int(t)}s")
                # Choisir la première carte (Card0 avec Enter)
                _focus()
                _key("left", "left", "left")  # reset Card0
                time.sleep(0.3)
                _key("return")
                time.sleep(0.6)
                continue
            if stop_on_death and _detect_runend():
                t = run_elapsed
                result['death_time'] = t
                result['alive'] = False
                _log(f"  [MORT] détectée à t={t:.0f}s")
                _focus()
                _screenshot(f"{run_label}_mort_t{int(t)}s")
                return result

        # Mouvement directionnel
        d = dirs[i % 4]
        pyautogui.keyDown(d)
        time.sleep(0.4)
        pyautogui.keyUp(d)
        time.sleep(0.05)
        run_elapsed += 0.45
        i += 1

    # Fin de boucle sans mort
    _focus()
    _screenshot(f"{run_label}_fin")
    return result


# ---------------------------------------------------------------------------
# RUN 1 — Standard (sans hub upgrades)
# ---------------------------------------------------------------------------

def run1_standard(proc: subprocess.Popen) -> dict:
    _log("\n\n=== RUN 1 : Standard (sans hub upgrades) ===")
    _focus()

    # MainMenu → Jouer directement (PlayButton est focus par défaut)
    _key("return")
    time.sleep(3.5)
    _focus()
    _screenshot("run1_ingame_start")

    run_start = time.time()
    # Captures à 0:30, 1:00, 1:45, 3:00, 5:00, 7:00, 10:00
    caps = [30, 60, 105, 180, 300, 420, 600]
    result = _play_and_watch("run1", run_start, max_seconds=660, capture_at=caps,
                             stop_on_death=True)

    _log(f"\n  RUN 1 RÉSULTATS:")
    _log(f"    Level-ups: {len(result['levelups'])} détectés à t={[f'{t:.0f}s' for t in result['levelups']]}")
    if result['death_time']:
        _log(f"    Mort à t={result['death_time']:.0f}s ({result['death_time']/60:.1f} min)")
    else:
        _log(f"    Toujours en vie après {660}s")

    return result


# ---------------------------------------------------------------------------
# RUN 2 — Hub upgrades
# ---------------------------------------------------------------------------

def run2_with_hub(proc: subprocess.Popen) -> dict:
    _log("\n\n=== RUN 2 : Avec hub upgrades ===")
    _focus()

    # On arrive sur RunEndScreen ou MainMenu selon Run 1
    # Appuyer Escape pour revenir au Hub ou utiliser le bouton Hub
    time.sleep(4.0)  # laisser le countup finir
    _focus()
    _screenshot("run2_runendscreen")

    # Left = HubButton
    _key("left")
    time.sleep(0.5)
    _key("return")
    time.sleep(SCENE_WAIT)
    _focus()
    _screenshot("run2_hubscreen")

    # Au Hub : acheter les upgrades disponibles
    # BackButton focus par défaut → Down = 1er Acheter
    _log("  Tentative d'achat upgrades au Hub...")
    _key("down")   # 1er upgrade (Corps Renforcé)
    time.sleep(0.3)
    _key("return")  # Acheter
    time.sleep(0.5)
    _screenshot("run2_hub_buy1")

    _key("down", "down")  # 2e upgrade (Calibration Offensive)
    time.sleep(0.3)
    _key("return")
    time.sleep(0.5)
    _screenshot("run2_hub_buy2")

    # PlayButton : 6 Down depuis le 3e upgrade
    for _ in range(5):
        _key("down")
    _screenshot("run2_hub_before_play")
    _key("return")  # Lancer la partie
    time.sleep(3.5)
    _focus()
    _screenshot("run2_ingame_start")

    run_start = time.time()
    caps = [30, 60, 120, 180, 300, 420, 480]
    result = _play_and_watch("run2", run_start, max_seconds=540, capture_at=caps,
                             stop_on_death=True)

    _log(f"\n  RUN 2 RÉSULTATS:")
    _log(f"    Level-ups: {len(result['levelups'])} détectés")
    if result['death_time']:
        _log(f"    Mort à t={result['death_time']:.0f}s ({result['death_time']/60:.1f} min)")
    else:
        _log(f"    Toujours en vie après 540s")

    return result


# ---------------------------------------------------------------------------
# RUN 3 — Focus fusions (jouer longtemps pour essayer d'atteindre une fusion)
# ---------------------------------------------------------------------------

def run3_fusion_focus(proc: subprocess.Popen) -> dict:
    _log("\n\n=== RUN 3 : Focus fusions ===")
    _focus()

    # Depuis RunEndScreen → Hub ou Replay
    time.sleep(4.0)
    _focus()
    _screenshot("run3_runendscreen")

    # Rejouer directement (ReplayButton est focus par défaut)
    _key("return")
    time.sleep(3.5)
    _focus()
    _screenshot("run3_ingame_start")

    run_start = time.time()
    # Run plus longue (10 min) pour essayer d'atteindre les fusions
    caps = [60, 120, 180, 300, 420, 540, 600]
    result = _play_and_watch("run3", run_start, max_seconds=660, capture_at=caps,
                             stop_on_death=True)

    _log(f"\n  RUN 3 RÉSULTATS:")
    _log(f"    Level-ups: {len(result['levelups'])} détectés")
    if result['death_time']:
        _log(f"    Mort à t={result['death_time']:.0f}s ({result['death_time']/60:.1f} min)")
    else:
        _log(f"    Toujours en vie après 660s")

    return result


# ---------------------------------------------------------------------------
# Rapport JSON + console
# ---------------------------------------------------------------------------

def write_report(r1: dict, r2: dict, r3: dict):
    report_path = os.path.join(SCREENS_DIR, "BALANCE_REPORT.txt")
    date = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    lines = [
        f"Playtest Équilibrage — {date}",
        "=" * 60,
        "",
        "=== RUN 1 (standard) ===",
        f"  Level-ups détectés : {len(r1['levelups'])}",
        f"  Temps par level-up : {[f'{t:.0f}s' for t in r1['levelups']]}",
        f"  Mort à : {r1['death_time']:.0f}s ({r1['death_time']/60:.1f} min)" if r1['death_time'] else "  Survie totale",
        "",
        "=== RUN 2 (hub upgrades) ===",
        f"  Level-ups détectés : {len(r2['levelups'])}",
        f"  Temps par level-up : {[f'{t:.0f}s' for t in r2['levelups']]}",
        f"  Mort à : {r2['death_time']:.0f}s ({r2['death_time']/60:.1f} min)" if r2['death_time'] else "  Survie totale",
        "",
        "=== RUN 3 (focus fusions) ===",
        f"  Level-ups détectés : {len(r3['levelups'])}",
        f"  Temps par level-up : {[f'{t:.0f}s' for t in r3['levelups']]}",
        f"  Mort à : {r3['death_time']:.0f}s ({r3['death_time']/60:.1f} min)" if r3['death_time'] else "  Survie totale",
        "",
        "=" * 60,
        "Logs complets :",
        "",
    ] + _results

    with open(report_path, "w", encoding="utf-8") as f:
        f.write("\n".join(lines))
    _log(f"\nRapport écrit : {report_path}")


# ---------------------------------------------------------------------------
# Main
# ---------------------------------------------------------------------------

if __name__ == "__main__":
    proc = _launch()

    if not _alive(proc):
        _log("ERREUR : jeu n'a pas démarré")
        sys.exit(1)

    _screenshot("00_mainmenu_initial")
    _log("Jeu démarré — MainMenu visible")

    r1 = r2 = r3 = {'levelups': [], 'death_time': None, 'alive': True}

    try:
        r1 = run1_standard(proc)
        r2 = run2_with_hub(proc)
        r3 = run3_fusion_focus(proc)
    except Exception as e:
        _log(f"\nERREUR PENDANT LE TEST : {e}")
        import traceback
        _log(traceback.format_exc())
    finally:
        _screenshot("final_state")
        _kill(proc)

    write_report(r1, r2, r3)

    _log(f"\nTerminé. Screenshots dans : {SCREENS_DIR}")
