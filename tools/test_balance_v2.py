"""
Playtest équilibrage v2 — Chimera Protocol
Session ciblée : métriques XP/DPS/survie
- Run 1 : observer la mort + RunEndScreen (scores Echos)
- Run 2 : depuis le Hub, observer la partie avec upgrades
- Captures régulières pour analyse visuelle HUD (HP, XP, timer, ennemis)

Usage : python -X utf8 tools/test_balance_v2.py
"""

import subprocess
import time
import os
import datetime

import pyautogui
import pygetwindow as gw

pyautogui.PAUSE    = 0.05
pyautogui.FAILSAFE = False  # désactivé pour éviter l'interruption par la souris

EXE_PATH    = r"C:\CODE\JEUX\cyborg-survivor\build\ChimeraProtocol.exe"
SCREENS_DIR = r"C:\CODE\JEUX\cyborg-survivor\docs\balance_v2_screenshots"
TITLE       = "Chimera Protocol"
os.makedirs(SCREENS_DIR, exist_ok=True)

_step = 0
_log_lines = []

def _log(msg):
    print(msg)
    _log_lines.append(msg)

def _ss(label):
    global _step
    _step += 1
    path = os.path.join(SCREENS_DIR, f"{_step:02d}_{label}.png")
    pyautogui.screenshot(path)
    _log(f"  [ss] {_step:02d}_{label}.png")
    return path

def _focus():
    wins = gw.getWindowsWithTitle(TITLE)
    if wins:
        try:
            wins[0].activate()
            time.sleep(0.4)
        except Exception:
            pass

def _k(*keys, w=0.35):
    for k in keys:
        pyautogui.press(k)
        time.sleep(w)

def _alive(p):
    return p.poll() is None

def _move(seconds, step=0.45):
    """Mouvement circulaire simple pendant `seconds` secondes."""
    dirs = ["right", "up", "left", "down"]
    i = 0
    t0 = time.time()
    while time.time() - t0 < seconds:
        d = dirs[i % 4]
        pyautogui.keyDown(d)
        time.sleep(min(step, (t0 + seconds) - time.time()))
        pyautogui.keyUp(d)
        time.sleep(0.05)
        i += 1

def _detect_overlay():
    """Détecte un overlay (LevelUp ou RunEnd) par profil couleur sombre bleu-biaisé."""
    import pyautogui as pg
    wins = gw.getWindowsWithTitle(TITLE)
    if wins:
        w = wins[0]
        region = (w.left, w.top, w.width, w.height)
        img = pg.screenshot(region=region)
    else:
        img = pg.screenshot()
    ww, h = img.size
    xs = [int(ww * f) for f in (0.2, 0.35, 0.5, 0.65, 0.8)]
    ys = [int(h * f) for f in (0.25, 0.4, 0.6, 0.75)]
    matched = total = 0
    for x in xs:
        for y in ys:
            r, g, b = [int(v) for v in img.getpixel((x, y))[:3]]
            total += 1
            if r < 40 and g < 40 and b < 60 and b > r + 8 and b > g + 8:
                matched += 1
    return (matched / total) >= 0.35 if total else False

def _detect_label_color():
    wins = gw.getWindowsWithTitle(TITLE)
    if wins:
        w = wins[0]
        region = (w.left, w.top, w.width, w.height)
        img = pyautogui.screenshot(region=region)
    else:
        img = pyautogui.screenshot()
    ww, h = img.size
    xs = [int(ww * f) for f in (0.35, 0.43, 0.50, 0.57, 0.65)]
    y0, y1 = int(h * 0.09), int(h * 0.23)
    red = cyan = 0
    for x in xs:
        for y in range(y0, y1, 2):
            r, g, b = [int(v) for v in img.getpixel((x, y))[:3]]
            if r > 140 and g < 110 and b < 90:
                red += 1
            if g > 120 and b > 100 and r < 120 and g + b > r * 4 + 80:
                cyan += 1
    if red >= 3: return 'red'
    if cyan >= 3: return 'cyan'
    return 'unknown'

def _is_levelup():
    if not _detect_overlay(): return False
    return _detect_label_color() == 'cyan'

def _is_runend():
    if not _detect_overlay(): return False
    return _detect_label_color() == 'red'

def play_run(label, max_sec, cap_every=30):
    """
    Joue pendant max_sec secondes avec mouvement circulaire.
    Capture tous les cap_every secondes.
    Détecte LevelUps et RunEnd.
    Retourne dict{levelups:[], death_t:float|None}
    """
    result = {'levelups': [], 'death_t': None}
    dirs = ["right", "up", "left", "down"]
    i = 0
    t0 = time.time()
    last_cap = -1.0
    last_check = 0.0

    _log(f"\n  [play_run:{label}] max={max_sec}s, capture toutes les {cap_every}s")

    while True:
        elapsed = time.time() - t0
        if elapsed >= max_sec:
            break

        # Capture programmée
        cap_slot = int(elapsed // cap_every) * cap_every
        if cap_slot > last_cap:
            _focus()
            _ss(f"{label}_t{int(elapsed)}s")
            last_cap = cap_slot

        # Détection overlay toutes les 2s
        if elapsed - last_check >= 2.0:
            last_check = elapsed
            if _is_levelup():
                _log(f"  [LEVELUP] t={elapsed:.0f}s")
                result['levelups'].append(elapsed)
                _focus()
                _ss(f"{label}_levelup_t{int(elapsed)}s")
                # Choisir card0
                _k("left", "left", "left")
                time.sleep(0.3)
                _k("return")
                time.sleep(0.6)
                continue
            if _is_runend():
                _log(f"  [RUNEND/MORT] t={elapsed:.0f}s")
                result['death_t'] = elapsed
                _focus()
                _ss(f"{label}_runend_t{int(elapsed)}s")
                return result

        # Mouvement
        d = dirs[i % 4]
        pyautogui.keyDown(d)
        time.sleep(0.4)
        pyautogui.keyUp(d)
        time.sleep(0.05)
        i += 1

    _focus()
    _ss(f"{label}_end")
    return result


# ---------------------------------------------------------------------------
# MAIN
# ---------------------------------------------------------------------------

proc = subprocess.Popen([EXE_PATH])
_log(f"Jeu lancé PID {proc.pid}")
time.sleep(8.0)
_focus()
_ss("mainmenu")
_log("MainMenu visible")

# ===========================================================================
# RUN 1 — Standard (PlayButton focus par défaut → Enter)
# ===========================================================================
_log("\n=== RUN 1 : Standard ===")
_focus()
_k("return")  # Jouer
time.sleep(4.0)
_focus()
_ss("run1_start")

r1 = play_run("run1", max_sec=660, cap_every=30)

# Attendre countup RunEndScreen
time.sleep(5.0)
_focus()
_ss("run1_runend_total")
_log(f"Run1 — levelups={r1['levelups']}, mort={r1['death_t']}")

# ===========================================================================
# RUN 2 — Depuis Hub (acheter upgrades)
# ===========================================================================
_log("\n=== RUN 2 : Hub upgrades ===")
# On est sur RunEndScreen → Left = HubButton
_focus()
_k("left")
time.sleep(0.5)
_ss("run2_runend_hubfocus")
_k("return")
time.sleep(SCENE_WAIT := 2.5)
_focus()
_ss("run2_hub_initial")

# Tenter d'acheter Corps Renforcé (première ligne, bouton Acheter)
# BackButton a le focus → Down = 1re ligne Acheter
_k("down")
time.sleep(0.3)
_ss("run2_hub_corps_renforce_focus")
_k("return")  # Acheter (si Echos suffisants)
time.sleep(0.5)
_ss("run2_hub_apres_achat1")

# Lancer la partie : naviguer jusqu'au bouton Jouer
# Depuis la 1re ligne : 6× Down → PlayButton
for _ in range(7):
    _k("down")
_ss("run2_hub_jouer_focus")
_k("return")
time.sleep(4.0)
_focus()
_ss("run2_start")

r2 = play_run("run2", max_sec=480, cap_every=30)

time.sleep(5.0)
_focus()
_ss("run2_runend_total")
_log(f"Run2 — levelups={r2['levelups']}, mort={r2['death_t']}")

# ===========================================================================
# RUN 3 — Rejouer (ReplayButton)
# ===========================================================================
_log("\n=== RUN 3 : Replay (focus fusions) ===")
_focus()
# Réinitialiser le focus sur ReplayButton — il devrait être focusé par défaut
time.sleep(1.0)
_ss("run3_runend_before_replay")
_k("return")  # ReplayButton
time.sleep(4.0)
_focus()
_ss("run3_start")

r3 = play_run("run3", max_sec=660, cap_every=45)

time.sleep(5.0)
_focus()
_ss("run3_runend_total")
_log(f"Run3 — levelups={r3['levelups']}, mort={r3['death_t']}")

# ===========================================================================
# Fermeture propre
# ===========================================================================
proc.terminate()
_log("Jeu terminé")

# Rapport
report = os.path.join(SCREENS_DIR, "BALANCE_REPORT.txt")
with open(report, "w", encoding="utf-8") as f:
    date = datetime.datetime.now().strftime("%Y-%m-%d %H:%M")
    f.write(f"Playtest équilibrage v2 — {date}\n")
    f.write("=" * 60 + "\n\n")
    f.write(f"RUN 1 (standard)\n")
    f.write(f"  Level-ups: {len(r1['levelups'])} -> t={[f'{t:.0f}s' for t in r1['levelups']]}\n")
    f.write(f"  Mort: {r1['death_t']:.0f}s\n" if r1['death_t'] else "  Pas de mort\n")
    f.write(f"\nRUN 2 (hub upgrades)\n")
    f.write(f"  Level-ups: {len(r2['levelups'])} -> t={[f'{t:.0f}s' for t in r2['levelups']]}\n")
    f.write(f"  Mort: {r2['death_t']:.0f}s\n" if r2['death_t'] else "  Pas de mort\n")
    f.write(f"\nRUN 3 (replay)\n")
    f.write(f"  Level-ups: {len(r3['levelups'])} -> t={[f'{t:.0f}s' for t in r3['levelups']]}\n")
    f.write(f"  Mort: {r3['death_t']:.0f}s\n" if r3['death_t'] else "  Pas de mort\n")
    f.write("\n\nLogs:\n")
    f.write("\n".join(_log_lines))

_log(f"\nRapport: {report}")
_log(f"Screenshots: {SCREENS_DIR}")
