# PROMPTS IA — Bande-son de Chimera Protocol

> À utiliser sur **Suno**, **Udio**, **Google Lyria / MusicFX**, **Stable Audio** ou **Riffusion**.
> Univers : `docs/NARRATIVE.md`. Architecture technique (stems, mixage, bouclage) :
> `docs/ART_BRIEF_AUDIO.md`.
>
> **Tu génères, tu déposes les fichiers dans `music_ai/`, je m'occupe du reste** — conversion,
> normalisation de volume, création d'une boucle propre, nommage, import Godot et câblage.
> Ne te préoccupe ni du format, ni du volume, ni du bouclage. Voir §5.

---

## 0. Changement de direction — version 2 de ce document

**La direction « Vangelis / Blade Runner » de la v1 est abandonnée : trop lente, trop
contemplative pour un jeu où l'écran se remplit de monstres.** Ce qu'on cherche maintenant :
**du metal industriel / synth-metal** — guitares électriques down-tuned et batterie live au
premier plan, synthés analogiques et chœurs conservés mais **au service du riff**, jamais
l'inverse. Références : **Mick Gordon (DOOM 2016)**, **Carpenter Brut**, **Perturbator**,
**Dan Terminus**, **Celldweller**, **Ruiner**.

Conséquences concrètes :

- **Tous les tempos montent** (voir la table du §1) — le plus lent du jeu passe de 70 à 112 BPM.
- **Il y a de la batterie partout**, y compris au menu et au hub. La v1 interdisait la
  percussion sur ces deux pistes : cette règle saute.
- **Les paires `_calm` / `_combat` changent de sens.** Ce ne sont plus « ambient » vs « rythmé »,
  mais **couplet vs refrain du même morceau** : le `_calm` a déjà un riff et une batterie, en
  retenue (guitare en palm-mute, charleston fermée) ; le `_combat` ouvre tout (guitares pleines,
  cymbales, double pédale). Aucune des deux versions ne doit être molle.
- **Ce qui ne change pas** : les tonalités et les progressions d'accords (l'identité harmonique
  de chaque biome tient), le chœur sans paroles (les voix absorbées par la Rouille Vivante), le
  100 % instrumental.

**Les pistes déjà déposées dans `music_ai/` (menu, hub, intro, sanctuaire) sont issues de la v1 :
elles sont à regénérer avec les prompts ci-dessous.**

---

## 1. Ce qu'il faut générer

**14 pistes.** Chaque biome a deux ambiances (couplet / refrain) que le jeu fait se fondre l'une
dans l'autre selon l'action. Un seul thème de boss, commun à tous les biomes.

| # | Nom du fichier à déposer | Rôle | Tonalité | BPM | Durée utile |
|---|---|---|---|---|---|
| 1 | `menu` | Thème principal, sombre et tendu | La mineur | 112 | 1:30 – 3:00 |
| 2 | `hub` | L'enclave, entre deux missions | Ré mineur / Fa majeur | 118 | 1:30 – 3:00 |
| 3 | `intro` | Cinématique d'ouverture | Ré mineur → La | libre → 150 | **≥ 1:35** |
| 4 | `sanctuaire_calm` | Ruines, couplet | Do mineur | 140 | 1:30 – 3:00 |
| 5 | `sanctuaire_combat` | Ruines, refrain | Do mineur | 140 | 1:30 – 3:00 |
| 6 | `aether_calm` | Friche magique, couplet | Ré phrygien dominant | 152 | 1:30 – 3:00 |
| 7 | `aether_combat` | Friche magique, refrain | Ré phrygien dominant | 152 | 1:30 – 3:00 |
| 8 | `givre_calm` | Glace, couplet | La dorien | 130 (half-time) | 1:30 – 3:00 |
| 9 | `givre_combat` | Glace, refrain | La dorien | 130 | 1:30 – 3:00 |
| 10 | `fournaise_calm` | Braises, couplet | Sol phrygien | 176 | 1:30 – 3:00 |
| 11 | `fournaise_combat` | Braises, refrain | Sol phrygien | 176 | 1:30 – 3:00 |
| 12 | `neon_calm` | Secteur de données, couplet | Mi mixolydien | 160 | 1:30 – 3:00 |
| 13 | `neon_combat` | Secteur de données, refrain | Mi mixolydien | 160 | 1:30 – 3:00 |
| 14 | `boss` | Combat de boss (tous biomes) | Do mineur chromatique | 150 | 1:30 – 3:00 |

**Règle importante pour les paires `_calm` / `_combat`** : ce sont **deux versions du même
morceau**, pas deux morceaux différents. Même tonalité, même tempo, même riff — la version
combat monte d'un cran en saturation, en densité de batterie et en aigus. Le jeu passe de l'une à
l'autre par un fondu enchaîné de 3 secondes : si les tonalités ou les tempos jurent, ça s'entend.

> **Astuce qui change tout** : sur Suno et Udio, génère d'abord la version `_calm`, puis utilise
> **« Cover » / « Remix » / « Extend » sur cette même piste** en changeant seulement le prompt pour
> la version combat. Tu gardes le riff, le tempo et la tonalité, tu ne changes que l'intensité.
> C'est bien plus fiable que deux générations indépendantes.

---

## 2. Identité sonore commune

À garder dans **toutes** les pistes — c'est ce qui fait tenir la bande-son ensemble :

```
industrial synth-metal, heavy down-tuned electric guitar riffs, hard-hitting live drums,
driving analog synth bass, wordless ethereal choir, metallic percussion, dark fantasy
sci-fi, Mick Gordon meets Carpenter Brut, aggressive and relentless, instrumental
```

Et à **exclure systématiquement** (champ « Exclude styles » sur Suno, sinon à la fin du prompt) :

```
lyrics, sung vocals, screaming vocals, rap, chiptune, 8-bit, beatless ambient, downtempo,
chillout, slow ballad, lo-fi hip hop, upbeat pop, cheerful, country, acoustic, jazz
```

Trois garde-fous :

- **Le chœur reste sans paroles** (« wordless », « vocalise », « aah/ooh choir »). Dans la
  fiction, ce sont les voix d'humains absorbés par la Rouille Vivante — jamais des voix nettes
  qui chantent un texte, et **jamais de chant metal hurlé** : c'est une texture, pas un chanteur.
- **La guitare porte le riff, pas le solo.** On veut des riffs répétables qui tiennent en boucle
  pendant dix minutes de run, pas une démonstration technique qui appelle l'attention.
- **Rien ne s'arrête jamais.** Aucune piste de jeu ne doit contenir de breakdown silencieux, de
  fondu final ou de longue intro atmosphérique : ça se transforme en trou au milieu d'une vague
  d'ennemis (le `intro` est la seule exception, c'est une cinématique).

---

## 3. Les prompts, piste par piste

Chaque bloc contient un **prompt court** (champ « Style » de Suno, ~200 caractères) et un
**prompt long** (Udio, Lyria, Stable Audio, ou le mode description de Suno).
Coche **Instrumental** partout.

---

### 1. `menu` — thème principal

> Un homme seul, entre deux missions suicides, dans un monde abîmé mais pas sans espoir.
> C'est la première chose que le joueur entend : ça doit donner envie d'appuyer sur Start.
> Sombre et tendu, pas triste — un morceau qui avance.

**Court :**
```
dark industrial rock theme, palm-muted down-tuned guitar riff, driving mid-tempo drums, analog
synth bass, distant wordless choir, cold neon atmosphere, A minor, 112 BPM, instrumental
```

**Long :**
```
A dark, brooding industrial rock main theme for a science-fiction action game. It opens with a
clean, cold arpeggiated guitar over a low analog drone for about fifteen seconds, then the full
band drops in: a palm-muted down-tuned guitar riff, a punchy mid-tempo drum groove with a solid
backbeat, and a growling analog synth bass locked to the kick. Warm CS-80 pads and a distant
wordless choir (aah/ooh) sit behind the riff, giving the piece a lonely, cinematic edge without
ever slowing it down. Chord progression A minor – F – C – G, key of A natural minor, 112 BPM.
Mood: solitude and exhaustion, but with momentum — a man getting up to go back out. Fully
instrumental, no vocals, no lyrics.
```

---

### 2. `hub` — l'enclave

> On répare, on souffle, on prépare la prochaine sortie. Plus chaud et plus « groove » que le
> menu : c'est l'atelier, pas le champ de bataille — mais on tape quand même du pied.

**Court :**
```
heavy stoner rock groove, fuzz bass, warm distorted guitar riff, relaxed swaggering drums, analog
synth pads, close wordless choir, workshop atmosphere, D minor, 118 BPM, instrumental
```

**Long :**
```
A warm, mid-tempo stoner rock groove — the sound of a fortified workshop between two dangerous
missions. A thick fuzz bass carries the main line under a warm, moderately distorted guitar riff.
The drums swagger rather than rush: a relaxed but solid groove with a fat snare, hi-hat riding
loosely, occasional metallic hits like a tool struck against a workbench. Analog synth pads and a
close, warm wordless choir (aah) fill the background, nearer to the listener than in the main
theme. Chord progression D minor – B flat – F – C, key of D minor / F major, 118 BPM. Mood:
fatigue, quiet competence, gearing up. Confident, never sad. Fully instrumental, no vocals.
```

---

### 3. `intro` — cinématique d'ouverture

> Raconte la Convergence : une fusion technologie/magie qui devait sauver le monde et l'a
> réorganisé. **Doit durer au moins 1:35** (je cale le montage sur la cut-scene).
> Seule piste du jeu qui a le droit de commencer lentement — parce qu'elle explose ensuite.
> Se termine **non résolu**, en suspens, pour enchaîner sur le thème du menu.

**Court :**
```
cinematic industrial metal build-up, cold drone opening, rising dissonant choir, massive drop with
down-tuned guitars and double kick drums, then broken filtered choir, unresolved ending, D minor
```

**Long :**
```
A cinematic industrial metal piece telling a catastrophe in four movements. It opens cold and
ambiguous: a low resonant drone alone, no clear key, deep energy lines beneath the earth. Around
twenty seconds in, synthetic strings and a wordless choir fade in and build dissonance while a
distant war drum starts pounding — something enormous is approaching. At the climax, everything
detonates: a wall of heavily distorted down-tuned guitars, double kick drums, and a massive
metallic impact, while the choir breaks off mid-breath as if the voices were swallowed. Afterwards
the guitars fall away, the drone settles into D minor, and a heavily filtered, corrupted choir
persists in the background, permanent now, over a slow dying heartbeat of toms. The piece ends on
a long unresolved suspended chord that never comes home. Huge cavernous reverb throughout.
Minimum 95 seconds. Fully instrumental, no vocals, no lyrics.
```

---

### 4 & 5. `sanctuaire_calm` / `sanctuaire_combat`

> Ruines d'un centre technologique à échelle humaine. **C'est le biome de référence** — à générer
> en premier, il donne le ton de tous les autres. Riff carré, tempo qui pousse sans s'affoler.

**Couplet — court :**
```
industrial metal verse, palm-muted drop-C guitar riff, tight driving drums, analog synth bass,
distant wordless choir, cold ruined sci-fi hall, C minor, 140 BPM, instrumental
```

**Couplet — long :**
```
Industrial metal for exploring the ruins of an abandoned technological sanctuary. A palm-muted,
down-tuned guitar riff in drop C chugs steadily under a driving drum groove — kick and snare tight
and forward, hi-hat closed, nothing washy. An analog synth bass doubles the guitar an octave below.
The chords move C minor – A flat – E flat – B flat. Behind the riff, cold synth pads and a distant
wordless choir hold the harmony, and occasional metallic industrial hits (struck steel, a pipe)
punctuate the bars. Medium hall reverb, human-scale ruins. C natural minor, 140 BPM. Tense,
controlled, always moving forward. Fully instrumental, no vocals.
```

**Refrain — court :**
```
heavy industrial metal chorus, full distorted drop-C guitars, double kick drums, crashing cymbals,
soaring wordless choir, relentless momentum, C minor, 140 BPM, instrumental
```

**Refrain — long :**
```
Same key, tempo, riff and chord progression as the verse version (C minor – A flat – E flat –
B flat, C natural minor, 140 BPM), now at full power. The guitars open from palm-muted chugs into
full distorted power chords, the drums switch to crashing cymbals and double kick runs, and a lead
synth line rises over the top. The wordless choir swells behind everything, huge and desperate.
It never lets up and it never resolves — the pressure is constant, wave after wave. Fully
instrumental, no vocals.
```

---

### 6 & 7. `aether_calm` / `aether_combat`

> Ruines saturées d'énergie magique corrompue. Le mode **phrygien dominant** (tierce majeure sur
> une seconde mineure) donne la couleur « magie exotique » — c'est le riff oriental-metal du jeu.
> Le frottement Ré→Mi bémol ne se résout jamais : c'est le motif de la corruption.

**Couplet — court :**
```
exotic industrial metal verse, phrygian dominant guitar riff, detuned bells, granular shimmer,
warped wordless choir, cathedral reverb, D phrygian dominant, 152 BPM, instrumental
```

**Couplet — long :**
```
Exotic, unsettling industrial metal for ruins saturated with corrupted magical energy. Built on
D phrygian dominant — a major third over a flattened second, an ancient, snake-charmer colour
played on a heavily down-tuned guitar. The riff slides from D to E flat and never resolves: that
half step is the signature of the piece. Drums drive in a fast, syncopated groove with tom accents.
Detuned, non-tempered bells shimmer at random over resonant filter sweeps. The wordless choir is
slightly out of tune and drifts in slow portamento, as if the voices were being pulled apart. The
chords move D – E flat – G minor – A7. Enormous cathedral reverb, but a corrupted one. 152 BPM.
Fully instrumental, no vocals.
```

**Refrain — court :**
```
aggressive exotic metal chorus, twin harmonized phrygian dominant guitars, blast-adjacent double
kick, resonant detuned bells, warped wordless choir, D phrygian dominant, 152 BPM, instrumental
```

**Refrain — long :**
```
Same key, tempo, riff and progression as the verse version (D phrygian dominant, D – E flat –
G minor – A7, 152 BPM), now unleashed. Twin harmonized guitars hammer the exotic major third
against the flattened second, double kick drums roll underneath, and the metallic percussion turns
into ringing, slightly detuned bell strikes. The warped, out-of-tune wordless choir rides on top in
long glides, never staccato. Magical, corrupted, relentless — like a ritual going wrong at high
speed. Fully instrumental, no vocals.
```

---

### 8 & 9. `givre_calm` / `givre_combat`

> Biome cryogénique. C'est le seul biome où le poids compte plus que la vitesse : **groove
> half-time très lourd**, batterie qui frappe fort et espacé, mais charleston et guitare en
> doubles-croches par-dessous — lourd, jamais mou. La glace absorbe les aigus : réverbération
> longue mais **étouffée**, jamais brillante.

**Couplet — court :**
```
glacial sludge metal verse, half-time heavy drums, down-tuned crushing guitar riff, sixteenth-note
hi-hat, frozen wordless choir, muffled long reverb, A dorian, 130 BPM, instrumental
```

**Couplet — long :**
```
Glacial sludge metal for a cryogenic ruin. A heavy half-time drum groove: the kick and snare land
hard and wide apart, giving enormous weight, while sixteenth-note hi-hat and a down-tuned guitar
riff keep the pulse relentless underneath — heavy, never sluggish. The chords are A minor – D –
G – E minor in A dorian, whose major sixth gives a cold glow that never becomes warmth. High
filtered wind and rare crystalline chimes drift over the riff, with a sustained, almost choral
wordless choir holding frozen chords. The reverb is very long, four seconds or more, but muffled
and glassy: the ice swallows the high frequencies. 130 BPM. Fully instrumental, no vocals.
```

**Refrain — court :**
```
crushing glacial metal chorus, full down-tuned guitar wall, double kick under half-time snare,
crystalline percussion, thick frozen wordless choir, glassy muffled reverb, A dorian, 130 BPM
```

**Refrain — long :**
```
Same key, tempo, riff and progression as the verse version (A dorian, A minor – D – G – E minor,
130 BPM). The danger here grows in weight and thickness more than in speed: the snare stays
half-time and monumental while double kick drums fill everything underneath, and the guitars open
into a full wall of down-tuned distortion. Crystalline percussion scatters over the top and the
frozen choir grows louder and thicker. Glassy, muffled long reverb. Cold, crushing, patient
menace — an avalanche, not a chase. Fully instrumental, no vocals.
```

---

### 10 & 11. `fournaise_calm` / `fournaise_combat`

> Chaleur infernale qui surexcite la Rouille. **La piste la plus rapide et la plus violente du
> jeu** — thrash metal. Réverbération courte : l'agressivité a besoin de son direct.

**Couplet — court :**
```
thrash metal verse, fast down-picked drop-G guitar riff, d-beat drums, crackling ember texture,
distorted wordless choir, dry tight mix, G phrygian, 176 BPM, instrumental
```

**Couplet — long :**
```
Fast, oppressive thrash metal for a furnace-hot ruin. A relentless down-picked guitar riff in
drop G grinds the minor second A flat against G — that interval is the signature of the biome.
Drums drive a fast d-beat with a cracking snare and a busy ride. Under everything, a low burning
drone and a constant crackle of embers, irregular high-frequency pops and snaps. The wordless
choir is saturated and distorted, as if the voices themselves were burning. Chords G minor –
A flat – C minor – A flat, key of G phrygian. Short, tight, dry reverb — this place is close and
airless, not cavernous. 176 BPM. Fully instrumental, no vocals.
```

**Refrain — court :**
```
brutal thrash metal chorus, full-speed tremolo drop-G guitars, double kick blast, syncopated anvil
hits, burning distorted wordless choir, dry and in your face, G phrygian, 176 BPM, instrumental
```

**Refrain — long :**
```
Same key, tempo, riff and progression as the verse version (G phrygian, G minor – A flat –
C minor – A flat, 176 BPM) — the fastest and densest track in the game. Tremolo-picked down-tuned
guitars, full double kick drums, and heavy syncopated metallic percussion like anvil strikes
landing off the beat. The riff hammers the minor second A flat–G over and over without mercy. The
distorted wordless choir burns on top, never clean. Dry, tight, in your face — very short reverb,
everything hitting the listener directly. Fully instrumental, no vocals.
```

---

### 12 & 13. `neon_calm` / `neon_combat`

> Secteur de données overclocké. **Le seul biome assumé « propre et artificiel »** : darksynth
> et guitare, batterie électronique tranchante, espace de club/data-center et non de cathédrale.
> Le chœur y sonne volontairement comme **des données**, pas comme une voix qui se dissout.

**Couplet — court :**
```
darksynth punk verse, motorik four-on-the-floor kick, tight sixteenth synth bass, clean chorused
guitar riff, vocoded wordless choir, bright plate reverb, E mixolydian, 160 BPM, instrumental
```

**Couplet — long :**
```
Fast, glossy darksynth with a punk edge, for an overclocked data sector. A strict four-on-the-floor
electronic kick under a tight sixteenth-note analog synth bassline, with a clean chorused guitar
riff cutting across it and crisp synthetic claps and closed hats — no organic metal here, this
place is machine-made. The chords vamp E – D – A – E in E mixolydian, whose flattened seventh keeps
it modern and unresolved. The wordless choir is lightly vocoded, deliberately artificial, sounding
like data rather than like human voices. Short bright plate reverb with crisp early reflections: a
club or a server room, never a cathedral. 160 BPM. Fully instrumental, no vocals.
```

**Refrain — court :**
```
Carpenter Brut style darksynth metal chorus, distorted guitar over pounding four-on-the-floor,
dense sixteenth arpeggio, hard synth bass, vocoded choir, E mixolydian, 160 BPM, instrumental
```

**Refrain — long :**
```
Same key, tempo, riff and progression as the verse version (E mixolydian, E – D – A – E, 160 BPM),
now pounding in full Carpenter Brut mode. Distorted electric guitars slam power chords over an
unbroken four-on-the-floor kick — the kick never stops, it is the signature of the overclocked
sector — while a dense sixteenth-note arpeggio and a hard, saturated synth bass drive the top and
bottom. Synthetic claps, open hats, and a snare on the backbeat. The vocoded wordless choir is
layered over everything. Relentless, precise, machine-like. Fully instrumental, no vocals.
```

---

### 14. `boss` — thème de boss (commun à tous les biomes)

> Se déclenche à l'apparition d'un boss ou d'un mini-boss, quel que soit le biome. Doit être
> **nettement plus lourd** que n'importe quelle piste de combat, et reconnaissable en deux
> secondes. C'est le morceau le plus « DOOM » de la bande-son.

**Court :**
```
epic boss battle industrial metal, crushing 8-string guitar riff, double kick and taiko drums, low
dissonant string cluster, deep grinding wordless choir, half-time breakdown, C minor, 150 BPM
```

**Long :**
```
An epic, crushing boss battle theme in the style of Mick Gordon's DOOM score. A brutally
down-tuned eight-string guitar riff grinds in the lowest register, doubled by a distorted analog
synth bass, over relentless double kick drums and heavy taiko-like toms. Above it, a low dissonant
cluster of synthetic strings refuses to resolve — root, minor second and fifth held together — and
a deep wordless choir in the bass register sounds almost, but not quite, human. The piece alternates
between fast driving sections and monumental half-time breakdown riffs where every hit lands like a
piledriver. Anvil strikes stay dry and precise, never washed out, even though the strings and choir
sit in an enormous space. C minor with chromatic movement, 150 BPM. Mood: something enormous,
patient and inevitable has noticed you. Fully instrumental, no vocals, no lyrics.
```

---

## 4. Réglages par outil

### Suno (recommandé — meilleur compromis riffs / chœurs)
- Mode **Custom**, coche **Instrumental**.
- Colle le **prompt court** dans « Style of Music » (le champ est limité en longueur).
- Renseigne « **Exclude styles** » avec la liste d'exclusions du §2 — surtout `sung vocals` et
  `screaming vocals` : en metal, l'IA a tendance à coller un chanteur dès qu'on la laisse faire.
- Modèle **v4.5 ou plus récent**.
- Chaque génération donne 2 variantes : écoute les deux, garde la meilleure.
- Pour la version `_combat`, utilise **Cover** ou **Extend** sur la piste `_calm` retenue plutôt
  qu'une génération neuve — c'est ce qui garantit la cohérence de riff et de tonalité.
- Télécharge en **WAV** si ton offre le permet, sinon MP3 : les deux me conviennent.

### Udio
- Utilise le **prompt long**, active « Instrumental ».
- Baisse « Prompt strength » vers 60-70 % si le rendu sonne trop chargé.
- L'option **Extend** permet d'allonger jusqu'à la durée voulue.

### Google Lyria / MusicFX (AI Studio ou labs.google)
- Utilise le **prompt court** : Lyria répond mieux à des listes de descripteurs qu'à des phrases.
- Il ne respecte pas fiablement les indications de tonalité ni de tempo — vérifie à l'oreille que
  les paires `_calm` / `_combat` d'un même biome s'accordent, sinon regénère.

### Stable Audio
- Prompt long, et renseigne explicitement le champ de durée.
- Bon sur les textures, plus faible sur les guitares saturées — à réserver aux couches
  atmosphériques si les riffs te déçoivent ailleurs.

### Comment juger une génération en dix secondes
1. **Est-ce que ça pousse ?** Si tu peux hocher la tête dessus, c'est bon. Si ça flotte, jette.
2. **Y a-t-il une voix qui chante ?** Regénère (ou renforce les exclusions).
3. **Le riff tient-il en boucle ?** Écoute le même passage trois fois d'affilée : si ça devient
   fatigant au bout de trois, ça le sera au bout de dix minutes de run.
4. **La piste s'arrête-t-elle quelque part ?** Un breakdown silencieux ou un fondu final au milieu
   du morceau = trou dans le jeu. Préfère une prise qui roule sans interruption.

---

## 5. Où déposer les fichiers

Dépose tout dans le dossier **`music_ai/`** à la racine du projet (déjà créé) :

```
C:\CODE\JEUX\chimera-protocol\music_ai\
```

**Nomme chaque fichier avec l'identifiant de la colonne « Nom du fichier » du §1**, dans n'importe
quel format (`.mp3`, `.wav`, `.ogg`, `.flac`) :

```
music_ai/
  menu.mp3
  hub.mp3
  intro.wav
  sanctuaire_calm.mp3
  sanctuaire_combat.mp3
  …
  boss.mp3
```

Si tu as plusieurs variantes d'une même piste et que tu hésites, dépose-les en suffixant `_v1`,
`_v2` — je te dirai laquelle tient le mieux dans le mix, ou tu trancheras.

**Tu n'as à te soucier de rien d'autre.** Je prends en charge :
- la conversion en OGG Vorbis 44,1 kHz stéréo ;
- l'harmonisation des volumes entre toutes les pistes (normalisation EBU R128) ;
- la **création d'un point de boucle propre** — les IA ne produisent pas de boucles, je découpe et
  raccorde par fondu enchaîné pour que ça tourne indéfiniment sans blanc ni couture ;
- le calage de l'intro sur la durée exacte de la cut-scene ;
- le renommage, l'import Godot et le câblage dans le jeu.

Tu n'es pas obligé de tout fournir d'un coup : je peux intégrer piste par piste. **Commence par
`sanctuaire_calm` et `sanctuaire_combat`** — je les intègre, tu joues, et on ajuste la direction
avant que tu génères les douze autres.

---

## 6. Licence — à vérifier avant publication

Les conditions varient énormément selon le service **et selon ton abonnement** :

- **Suno** : plan gratuit = usage non commercial uniquement. Les plans payants accordent des droits
  commerciaux sur les morceaux générés pendant l'abonnement actif.
- **Udio** : mêmes distinctions ; vérifie les droits attachés à ton offre.
- **Google Lyria / MusicFX** : conditions spécifiques, souvent restrictives sur le commercial.
- **Stable Audio** : droits commerciaux selon le tier.

Chimera Protocol est publié sur itch.io. **Avant la première release intégrant ces musiques**,
relis les CGU au moment de l'export (elles évoluent vite) et dis-moi le service et le plan retenus :
je les consigne dans `assets/audio/CREDITS.md` avec la date et le lien, pour qu'on puisse le
justifier plus tard.

En attendant, la bande-son synthétisée par le dépôt (`tools/generate_music_v3.py`) reste en place et
n'a, elle, aucune contrainte de licence — elle sert de filet de sécurité si un doute juridique
apparaissait.
