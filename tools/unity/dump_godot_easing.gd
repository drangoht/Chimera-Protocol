# Extrait les courbes d'interpolation de Godot 4.7 (11 transitions × 4 modes d'atténuation),
# pour que le shim GTween du port Unity les reproduise à l'identique.
#
# Pourquoi : Tween est l'idiome Godot le plus utilisé du projet (502 sites d'appel, dont 280 dans
# l'UI). Une courbe reproduite « à peu près » ne casse rien de fonctionnel mais fait dériver
# TOUTE l'animation du jeu — et c'est exactement le genre d'écart qu'une capture avant/après ne
# rattrape pas, parce qu'il ne se voit qu'en mouvement.
#
# Usage :
#   godot --headless --path <projet> --script res://tools/unity/dump_godot_easing.gd
#
# Outil de migration : ne participe pas au jeu, ne rompt pas le gel.

extends SceneTree

const TRANS := {
	"LINEAR": Tween.TRANS_LINEAR,
	"SINE": Tween.TRANS_SINE,
	"QUINT": Tween.TRANS_QUINT,
	"QUART": Tween.TRANS_QUART,
	"QUAD": Tween.TRANS_QUAD,
	"EXPO": Tween.TRANS_EXPO,
	"ELASTIC": Tween.TRANS_ELASTIC,
	"CUBIC": Tween.TRANS_CUBIC,
	"CIRC": Tween.TRANS_CIRC,
	"BOUNCE": Tween.TRANS_BOUNCE,
	"BACK": Tween.TRANS_BACK,
	"SPRING": Tween.TRANS_SPRING,
}

const EASE := {
	"IN": Tween.EASE_IN,
	"OUT": Tween.EASE_OUT,
	"IN_OUT": Tween.EASE_IN_OUT,
	"OUT_IN": Tween.EASE_OUT_IN,
}

const SAMPLES := [0.0, 0.125, 0.25, 0.375, 0.5, 0.625, 0.75, 0.875, 1.0]


func _init() -> void:
	print("# courbes d'interpolation Godot ", Engine.get_version_info().string)
	print("# interpolate_value(0, 1, t, 1, TRANS, EASE) pour t = ", SAMPLES)

	for tname in TRANS.keys():
		for ename in EASE.keys():
			var vals := []
			for t in SAMPLES:
				var v: float = Tween.interpolate_value(0.0, 1.0, t, 1.0, TRANS[tname], EASE[ename])
				vals.append("%.9f" % v)
			print("%s %s %s" % [tname, ename, " ".join(vals)])

	quit()
