# Extrait des valeurs de référence de la RNG globale de Godot, pour vérifier que le shim PCG32
# du port Unity reproduit exactement les mêmes tirages (docs/UNITY_MIGRATION_PLAN.md §4.3).
#
# Sans ces valeurs, « le port utilise la même RNG » resterait une affirmation. Avec elles, c'est
# un test unitaire : les graines deviennent comparables entre les deux moteurs, donc une campagne
# de banc Godot peut être rejouée sous Unity et comparée run à run.
#
# Usage :
#   godot --headless --path <projet> --script res://tools/unity/dump_godot_rng.gd
#
# Ce fichier est un OUTIL de migration : il ne participe pas au jeu et ne rompt pas le gel.

extends SceneTree

const SEEDS := [1, 42, 12345, 2026, 4294967295]
const N := 8


func _init() -> void:
	print("# valeurs de reference RNG Godot — moteur ", Engine.get_version_info().string)
	print("# format : SEED <graine> <fonction> <v1> <v2> ... <v%d>" % N)

	for s in SEEDS:
		seed(s)
		var ri := []
		for i in range(N):
			ri.append(str(randi()))
		print("SEED %d randi %s" % [s, " ".join(ri)])

		seed(s)
		var rf := []
		for i in range(N):
			rf.append("%.9f" % randf())
		print("SEED %d randf %s" % [s, " ".join(rf)])

		seed(s)
		var rr := []
		for i in range(N):
			rr.append("%.9f" % randf_range(-5.0, 12.5))
		print("SEED %d randf_range_-5_12.5 %s" % [s, " ".join(rr)])

		seed(s)
		var rir := []
		for i in range(N):
			rir.append(str(randi_range(0, 99)))
		print("SEED %d randi_range_0_99 %s" % [s, " ".join(rir)])

	# RandomNumberGenerator explicite : c'est CE type que PowerUpSpawner sème depuis --seed.
	# Vérifier qu'il coïncide avec la RNG globale (ou pas) fait partie du contrat à reproduire.
	for s in SEEDS:
		var rng := RandomNumberGenerator.new()
		rng.seed = s
		var v := []
		for i in range(N):
			v.append(str(rng.randi()))
		print("SEED %d rng_obj_randi %s" % [s, " ".join(v)])

	quit()
