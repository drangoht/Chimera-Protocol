# Dépose ici les musiques générées par IA

Ce dossier est la **boîte d'entrée** des pistes générées sur Suno / Udio / Lyria / Stable Audio.
Les prompts à utiliser et la liste des 14 pistes attendues : **`docs/AUDIO_AI_PROMPTS.md`**.

> ⚠️ **Direction changée le 2026-07-27** : on abandonne le style Vangelis lent pour du **metal
> industriel / synth-metal** (guitares down-tuned + batterie, tempos relevés). Les fichiers
> déposés ici avant ce changement (`menu`, `hub`, `intro`, `sanctuaire_*`) sont **à regénérer**
> avec les nouveaux prompts.

## Comment déposer

Un fichier par piste, nommé avec l'identifiant attendu, dans **n'importe quel format**
(`.mp3`, `.wav`, `.ogg`, `.flac`, `.m4a`) :

```
menu.mp3
hub.mp3
intro.wav
sanctuaire_calm.mp3      sanctuaire_combat.mp3
aether_calm.mp3          aether_combat.mp3
givre_calm.mp3           givre_combat.mp3
fournaise_calm.mp3       fournaise_combat.mp3
neon_calm.mp3            neon_combat.mp3
boss.mp3
```

Plusieurs candidats pour une même piste ? Suffixe-les `_v1`, `_v2` : ils sont conservés et
comparés, seul celui que tu retiens part en jeu.

Pas besoin de tout fournir d'un coup — l'intégration se fait piste par piste.

## Ce qui se passe ensuite

```
python tools/import_ai_music.py            # traite tout ce qui est ici
python tools/import_ai_music.py --only sanctuaire_calm
python tools/import_ai_music.py --list     # état : présent / manquant / déjà intégré
```

Le script convertit en OGG 44,1 kHz, harmonise les volumes entre toutes les pistes, **fabrique un
point de boucle propre** (les IA ne produisent pas de boucles), cale l'intro sur la durée de la
cut-scene, puis installe le résultat dans `assets/audio/music/`.

**Les fichiers déposés ici ne sont pas versionnés** (ils sont volumineux et régénérables) : seuls
les OGG finaux installés dans `assets/audio/music/` le sont. Garde donc une copie de tes sources
ailleurs si tu y tiens.
