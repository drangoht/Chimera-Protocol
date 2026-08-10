"""Genere les textures 9-slice des cadres d'UI "plaque blindee octogonale"
(docs/ART_BRIEF_UI_FRAMES.md, redige par directeur-artistique le 2026-07-26).

Un seul script parametre (family / accent / band / chamfer / weld / state) au lieu
de 20 dessins a la main (§5 du brief). Reutilise EXCLUSIVEMENT `pseudo3d_lib.shade()`
pour toute derivation highlight/shadow/contact/desaturation (meme physique de
lumiere LIGHT_DIR haut-gauche que les sprites de jeu) — aucune logique de teinte
HSV n'est reimplementee ici.

Sortie : unity/Assets/Resources/UiFrames/ui_frame_<family>_<accent>[_focus].png

Anatomie (§3.1), de l'exterieur vers l'interieur, empilee sur une bande de
`band_px` (16 boutons/cartes, 20 popups) :
    1 px  contact  (steel contact @ 70%)
    3 px  bevel    (highlight haut/gauche, shadow bas/droite)
    N px  plaque   (fill acier @ alpha selon etat)          N = band_px - 8
    3 px  lisere   (couleur de categorie/rarete @ alpha selon etat)
    1 px  separateur (steel contact @ 40%)
Au-dela de la bande : remplissage plat identique a la couche "plaque" (zone
centrale 9-slice etirable — c'est aussi ce qui permet au bord "soude" epais
(§3.1/§5) de fonctionner sans texture separee : regler texture_margin_bottom/
top plus grand cote Godot pioche simplement plus loin dans cette zone plate).

Coins chanfreines a `chamfer_px` du sommet (coupe diagonale x+y=chamfer_px,
donc un veritable 45°, staircase pixel-parfait, AUCUN anti-aliasing possible
par construction) ; teinte de la coupe = bevel du coin (§3.1).

Rivets 3x3 mini-bevel sur 2 coins selon la famille (bouton/carte : haut-gauche +
bas-droit ; popup : haut-gauche + haut-droit), absents en etat disabled (§3.2).
"""
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import pseudo3d_lib as _p3d
import unity_paths

ROOT = os.path.normpath(os.path.join(os.path.dirname(__file__), ".."))
OUT = str(unity_paths.sprite_dir("ui/frames"))

# --------------------------------------------------------------------------- #
# Palette §3.0 — derivee via shade(), jamais de HSV reimplemente ici.
# NB : shadow (#121223) et contact (#0B0B16) tombent pile sur les hex du brief.
# highlight calcule (#363656) differe legerement du hex indicatif du brief
# (#3A3A5C, ecart ~4-6/255 sur chaque canal) : ecart de transcription du
# brief (arrondi manuel), pas un bug — on garde la sortie REELLE de shade()
# pour que les cadres partagent *exactement* la meme physique que les
# sprites, conformement a la consigne explicite de la tache. Signale en fin
# de rapport.
STEEL_BASE = (0x24, 0x24, 0x40)
STEEL_HI = _p3d.shade(STEEL_BASE, "highlight")
STEEL_SH = _p3d.shade(STEEL_BASE, "shadow")
STEEL_CONTACT = _p3d.shade(STEEL_BASE, "contact")
DISABLED_NEUTRAL = (0x33, 0x33, 0x4A)  # gris neutre unique, donne tel quel par le brief

ACCENTS = {
    "cyan": (0x44, 0xFF, 0xEE),
    "violet": (0xAA, 0x44, 0xFF),
    "or": (0xFF, 0xCC, 0x44),
    # danger = shade(or, "shadow") -- meme teinte (44 deg) assombrie, §3.0
    "danger": _p3d.shade((0xFF, 0xCC, 0x44), "shadow"),
}

RARITY = {
    "common": (0xAA, 0xAA, 0xAA),
    "rare": (0x44, 0xAA, 0xFF),
    "epic": (0xCC, 0x44, 0xFF),
}

# Coefficients HSV additionnels (reutilisent shade(), pas de nouvelle logique) :
#  - "focus_tint" : eclat leger du fill en hover/focus (x1.15 V, §3.2 Hover)
#  - "half_sat"   : desaturation a 50% pour l'etat disabled (§3.2)
_FOCUS_TINT_COEFFS = {"t": (1.15, 1.00)}
_HALF_SAT_COEFFS = {"t": (1.00, 0.50)}
_DISABLED_FILL_COEFFS = {"t": (1.00, 0.60)}  # fill "desature" en disabled (§3.2)
# Lisere focus : un peu plus clair/moins sature qu'un simple alpha 100% de la
# teinte de base ("filet vivant" plus "electrique") -- renforce le signal
# couleur/opacite baked (2 des 3 signaux d'accessibilite du §3.2 -- forme
# via expand_margin et pulsation via Tween -- sont runtime, non bakes ici ;
# cf. rapport de fin de tache) sans changer sa largeur ni sa position.
_FOCUS_ACCENT_COEFFS = {"t": (1.30, 0.80)}


def focus_tint(rgb):
    return _p3d.shade(rgb, "t", coeffs=_FOCUS_TINT_COEFFS)


def focus_accent(rgb):
    return _p3d.shade(rgb, "t", coeffs=_FOCUS_ACCENT_COEFFS)


def half_sat(rgb):
    return _p3d.shade(rgb, "t", coeffs=_HALF_SAT_COEFFS)


def disabled_fill_tint(rgb):
    return _p3d.shade(rgb, "t", coeffs=_DISABLED_FILL_COEFFS)


# --------------------------------------------------------------------------- #
# Primitives bas niveau — set direct (PAS de blend) : chaque pixel du cadre
# n'est peint qu'UNE seule fois par la logique de bandes (pas de superposition
# de couches translucides), donc putpixel direct prevu ici plutot que
# pseudo3d_lib.put()/rect() (concus pour des traits additifs qui se
# superposent, ce qui premultiplierait a tort la couleur stockee — ecart
# deliberement documente, cf. rapport de fin de tache).
def _set(img, x, y, rgb, alpha=255):
    if 0 <= x < img.width and 0 <= y < img.height:
        img.putpixel((x, y), (rgb[0], rgb[1], rgb[2], int(round(alpha))))


def _canvas(size):
    from PIL import Image
    return Image.new("RGBA", (size, size), (0, 0, 0, 0))


# --------------------------------------------------------------------------- #
# Rendu du cadre
# --------------------------------------------------------------------------- #
def _state_colors(accent_rgb, state):
    """Calcule (fill_rgb, fill_alpha, accent_rgb_final, accent_alpha, bevel_flat,
    corner_flat) pour un etat donne (§3.2)."""
    if state == "focus":
        fill_rgb = focus_tint(STEEL_BASE)
        fill_alpha = 0.95 * 255
        accent_final = focus_accent(accent_rgb)
        accent_alpha = 1.00 * 255  # bake la geometrie/opacite MAX ; la pulsation
        # 60->100% est un Tween runtime cote developpeur (§5), jamais bakee ici.
        bevel_flat = None  # bevel "tel quel" (identique au normal)
        corner_flat = None
    elif state == "disabled":
        fill_rgb = disabled_fill_tint(STEEL_BASE)
        fill_alpha = 0.40 * 255
        accent_final = half_sat(accent_rgb)
        accent_alpha = 0.30 * 255
        bevel_flat = DISABLED_NEUTRAL
        corner_flat = DISABLED_NEUTRAL
    else:  # normal (sert aussi de base pour hover/pressed geres en Modulate runtime, §3.2)
        fill_rgb = STEEL_BASE
        fill_alpha = 0.85 * 255
        accent_final = accent_rgb
        accent_alpha = 0.55 * 255
        bevel_flat = None
        corner_flat = None
    return fill_rgb, fill_alpha, accent_final, accent_alpha, bevel_flat, corner_flat


def _mini_bevel(lx, ly, size=3):
    """Rivet 3x3 (ou NxN) : triangle haut-gauche = highlight, anti-diagonale =
    base neutre, triangle bas-droit = shadow (meme table de bevel, §3.1)."""
    s = lx + ly
    mid = size - 1
    if s < mid:
        return STEEL_HI
    if s > mid:
        return STEEL_SH
    return STEEL_BASE


def render_frame(canvas_size, band_px, chamfer_px, accent_rgb, state, rivet_corners):
    """Peint un cadre complet (§3.1) et retourne une image PIL RGBA.

    canvas_size   : cote du canvas carre (48 boutons/cartes, 56 popups)
    band_px       : largeur totale de la bande de cadre (16 ou 20)
    chamfer_px    : longueur de coupe des coins chanfreines (10 ou 14)
    accent_rgb    : couleur du lisere de categorie/rarete
    state         : "normal" | "focus" | "disabled"
    rivet_corners : sous-ensemble de {"TL","TR","BL","BR"} — coins avec rivet
    """
    W = H = canvas_size
    img = _canvas(W)

    plaque_thick = band_px - 8  # 1 contact + 3 bevel + N plaque + 3 lisere + 1 separateur = band_px
    t_contact = 1
    t_bevel = t_contact + 3
    t_plaque = t_bevel + plaque_thick
    t_lisere = t_plaque + 3
    # separateur : depth == band_px - 1 (dernier pixel de la bande)

    fill_rgb, fill_alpha, accent_final, accent_alpha, bevel_flat, corner_flat = _state_colors(accent_rgb, state)

    for y in range(H):
        d_top, d_bottom = y, (H - 1 - y)
        for x in range(W):
            d_left, d_right = x, (W - 1 - x)
            depth = min(d_top, d_left, d_bottom, d_right)
            hi_side = (d_top == depth) or (d_left == depth)

            if depth < t_contact:
                _set(img, x, y, STEEL_CONTACT, alpha=0.70 * 255)
            elif depth < t_bevel:
                if bevel_flat is not None:
                    _set(img, x, y, bevel_flat, alpha=255)
                else:
                    _set(img, x, y, STEEL_HI if hi_side else STEEL_SH, alpha=255)
            elif depth < t_plaque:
                _set(img, x, y, fill_rgb, alpha=fill_alpha)
            elif depth < t_lisere:
                _set(img, x, y, accent_final, alpha=accent_alpha)
            elif depth < band_px:
                _set(img, x, y, STEEL_CONTACT, alpha=0.40 * 255)
            else:
                # zone centrale plate — meme remplissage que "plaque" (permet le
                # bord "soude" epais via texture_margin_* cote Godot, §5 astuce)
                _set(img, x, y, fill_rgb, alpha=fill_alpha)

    # --- Coins chanfreines : coupe diagonale (45 deg = staircase pixel-parfait,
    # AUCUN anti-aliasing possible par construction) + teinte de coupe (§3.1)
    corners = {
        "TL": (lambda x, y: x + y, STEEL_HI),
        "TR": (lambda x, y: (W - 1 - x) + y, STEEL_BASE),
        "BL": (lambda x, y: x + (H - 1 - y), STEEL_BASE),
        "BR": (lambda x, y: (W - 1 - x) + (H - 1 - y), STEEL_SH),
    }
    for name, (metric, tint) in corners.items():
        final_tint = corner_flat if corner_flat is not None else tint
        for y in range(H):
            for x in range(W):
                m = metric(x, y)
                if m < chamfer_px:
                    _set(img, x, y, (0, 0, 0), alpha=0)  # coupe -> transparent
        for y in range(H):
            for x in range(W):
                if metric(x, y) == chamfer_px:
                    _set(img, x, y, final_tint, alpha=255)

    # --- Rivets 3x3 (absents en disabled, §3.2 "pas de rivets visibles")
    if state != "disabled":
        rivet_size = 3
        offsets = {
            "TL": lambda: (round((chamfer_px + 4) / 2.0), round((chamfer_px + 4) / 2.0)),
            "TR": lambda: (W - rivet_size - round((chamfer_px + 4) / 2.0) + 1, round((chamfer_px + 4) / 2.0)),
            "BL": lambda: (round((chamfer_px + 4) / 2.0), H - rivet_size - round((chamfer_px + 4) / 2.0) + 1),
            "BR": lambda: (W - rivet_size - round((chamfer_px + 4) / 2.0) + 1, H - rivet_size - round((chamfer_px + 4) / 2.0) + 1),
        }
        for corner in rivet_corners:
            ox, oy = offsets[corner]()
            for ly in range(rivet_size):
                for lx in range(rivet_size):
                    _set(img, ox + lx, oy + ly, _mini_bevel(lx, ly, rivet_size), alpha=255)

    return img


# --------------------------------------------------------------------------- #
# Familles — canvas / bande / chanfrein / rivets (§5)
# --------------------------------------------------------------------------- #
FAMILY_SPECS = {
    "button": dict(canvas=48, band=16, chamfer=10, rivets=("TL", "BR")),
    "card": dict(canvas=48, band=16, chamfer=10, rivets=("TL", "BR")),
    "popup": dict(canvas=56, band=20, chamfer=14, rivets=("TL", "TR")),
}


def _save(img, filename):
    path = os.path.join(OUT, filename)
    _p3d.save(img, path)  # simple makedirs+save, PAS de wrap_save/apply_by_category
    print(filename)


def gen(family, accent_rgb, state, filename):
    spec = FAMILY_SPECS[family]
    img = render_frame(spec["canvas"], spec["band"], spec["chamfer"], accent_rgb, state, spec["rivets"])
    _save(img, filename)


# --------------------------------------------------------------------------- #
# Lot de test (§5 / discipline §7 ART_BRIEF_PSEUDO3D) — a valider AVANT la
# matrice complete.
# --------------------------------------------------------------------------- #
def gen_test_batch():
    gen("button", ACCENTS["violet"], "normal", "ui_frame_button_violet.png")
    gen("button", ACCENTS["violet"], "focus", "ui_frame_button_violet_focus.png")
    gen("popup", ACCENTS["violet"], "normal", "ui_frame_popup_violet.png")


# --------------------------------------------------------------------------- #
# Matrice complete (§5) — 9 bouton + 7 carte + 3 popup = 19 fichiers.
# NB : le brief annonce "8 textures carte" dans son total (9+8+3=20) mais son
# arborescence explicite n'en liste que 6 (common/rare/epic x normal/focus).
# Ecart de comptage dans le brief lui-meme (6 vs 8) signale au rapport de fin
# de tache plutot que resolu par une invention arbitraire ; on ajoute ici un
# 7e fichier "card_disabled" partage (coherent avec button/popup qui ont
# chacun exactement un disabled partage) sans atteindre les 8 annonces.
# --------------------------------------------------------------------------- #
def gen_full_matrix():
    # -- Boutons (9) --
    for name in ("cyan", "violet", "or", "danger"):
        gen("button", ACCENTS[name], "normal", f"ui_frame_button_{name}.png")
        gen("button", ACCENTS[name], "focus", f"ui_frame_button_{name}_focus.png")
    gen("button", STEEL_BASE, "disabled", "ui_frame_button_disabled.png")

    # -- Cartes (7 : 3 raretes x normal/focus + 1 disabled partage) --
    for name in ("common", "rare", "epic"):
        gen("card", RARITY[name], "normal", f"ui_frame_card_{name}.png")
        gen("card", RARITY[name], "focus", f"ui_frame_card_{name}_focus.png")
    gen("card", STEEL_BASE, "disabled", "ui_frame_card_disabled.png")

    # -- Popups (3) --
    gen("popup", ACCENTS["violet"], "normal", "ui_frame_popup_violet.png")
    gen("popup", ACCENTS["cyan"], "normal", "ui_frame_popup_cyan.png")
    gen("popup", ACCENTS["violet"], "disabled", "ui_frame_popup_disabled.png")


def print_margin_report():
    print()
    print("=== texture_margin_* recommandes (StyleBoxTexture, cote Godot) ===")
    print("Bouton / Carte -- normal : left=top=right=16, bottom=22 (bord soude, §3.1)")
    print("Bouton / Carte -- focus  : idem + expand_margin_left/top/right/bottom=+3 (§3.2,")
    print("                           debordement de forme -- PAS un texture_margin)")
    print("Popup          : left=right=bottom=20, top=28 (bord soude renforce, §3.4)")


if __name__ == "__main__":
    mode = sys.argv[1] if len(sys.argv) > 1 else "all"
    if mode == "test":
        gen_test_batch()
    elif mode == "all":
        gen_test_batch()
        gen_full_matrix()
    else:
        print(f"Mode inconnu: {mode} (attendu: test|all)")
        sys.exit(1)
    print_margin_report()
