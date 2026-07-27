# Vérification headless des assets musicaux — à lancer AVANT toute release audio.
#
#   "…/Godot_v4.7-stable_mono_win64.exe" --headless --script tools/check_music_assets.gd
#
# Contrôle trois choses qui, si elles cassent, ne s'entendent qu'en jeu :
#   1. chaque piste attendue existe et se charge réellement (ResourceLoader, pas FileAccess) —
#      un .ogg présent sur le disque mais jamais importé par Godot n'est PAS embarqué à l'export ;
#   2. chaque biome a bien ses deux versions `calm` et `combat` : s'il en manque une,
#      `MusicDirector.PlayBiome` refuse le biome entier et retombe sur la musique simple ;
#   3. les pistes sont des AudioStreamOggVorbis, donc bouclables nativement
#      (`MusicDirector` force `loop = true` dessus au chargement).
#
# Les durées n'ont plus à être égales : depuis le passage aux pistes générées (couplet/refrain
# + thème de boss commun), une seule piste est audible à la fois et la bascule se fait par
# fondu croisé. Il n'y a plus de stems synchronisés à l'échantillon.
extends SceneTree

const MUSIC_DIR := "res://assets/audio/music/"
const BIOMES := ["sanctuaire", "aether", "givre", "fournaise", "neon"]
const LAYERS := ["calm", "combat"]
const SINGLES := [
	"music_menu", "music_hub", "music_intro",
	"music_stinger_death", "music_stinger_victory", "music_stinger_levelup",
	"music_run_boss",
]

# En dessous, la boucle s'entend comme une boucle au bout de deux tours.
const SHORT_LOOP_SEC := 25.0


func _init() -> void:
	var errors := 0
	var warnings := 0

	print("— Pistes simples —")
	for id in SINGLES:
		errors += _check_one(id)

	print("\n— Pistes de run —")
	for biome in BIOMES:
		for layer in LAYERS:
			var id := "music_run_%s_%s" % [biome, layer]
			var stream := _load(id)
			if stream == null:
				printerr("  MANQUANT : %s — le biome '%s' retombera sur la musique simple"
					% [id, biome])
				errors += 1
				continue

			if not (stream is AudioStreamOggVorbis):
				printerr("  %s n'est pas un OGG Vorbis (%s) — bouclage natif impossible"
					% [id, stream.get_class()])
				errors += 1

			var length := stream.get_length()
			if length < SHORT_LOOP_SEC:
				print("  %-30s %6.2f s   BOUCLE COURTE" % [id, length])
				warnings += 1
			else:
				print("  %-30s %6.2f s" % [id, length])

	print("")
	if warnings > 0:
		print("%d boucle(s) sous %.0f s — envisager `--loop-tolerance 0.8` à la réimportation."
			% [warnings, SHORT_LOOP_SEC])

	if errors == 0:
		print("Tous les assets musicaux sont conformes.")
	else:
		printerr("%d problème(s) détecté(s)." % errors)

	quit(1 if errors > 0 else 0)


func _load(track_id: String) -> AudioStream:
	var path := MUSIC_DIR + track_id + ".ogg"
	if not ResourceLoader.exists(path):
		return null
	return load(path) as AudioStream


func _check_one(track_id: String) -> int:
	var stream := _load(track_id)
	if stream == null:
		printerr("  MANQUANT : %s" % track_id)
		return 1
	print("  %-30s %6.2f s" % [track_id, stream.get_length()])
	return 0
