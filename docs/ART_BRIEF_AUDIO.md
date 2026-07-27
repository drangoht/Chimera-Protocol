# BRIEF DE DIRECTION SONORE — Musique adaptative par couches

> ⚠️ **Changement de direction acté le 2026-07-27 (après écoute) : le parti pris « Vangelis /
> Blade Runner » décrit ci-dessous est jugé trop lent et trop contemplatif pour un jeu aussi
> nerveux.** La bande-son cible est désormais du **metal industriel / synth-metal** — guitares
> down-tuned et batterie live au premier plan, synthés et chœurs conservés mais au service du riff
> (Mick Gordon, Carpenter Brut, Perturbator). Tempos relevés (plancher à 112 BPM au lieu de 70),
> percussion partout y compris menu et hub. **Tonalités, progressions d'accords, architecture en
> stems, règles de mixage (§4) et contraintes de bouclage (§5) restent valables** — seuls le parti
> pris (§1), la palette instrumentale (§2) et les tempos (§3) sont remplacés.
>
> **Direction et tempos en vigueur → `docs/AUDIO_AI_PROMPTS.md` (§0 et §1).** Le présent document
> reste la source de vérité de la **bande-son synthétisée par le dépôt** (`generate_music_v3.py`),
> qui n'a pas été refaite et sert de filet de sécurité sans contrainte de licence.

> Rédigé par l'agent `musicien` le 2026-07-27. Destinataire : compositeur (humain ou pipeline de
> synthèse Python) chargé de produire les fichiers finaux, et `developpeur` pour le câblage du
> mixeur adaptatif dans `AudioSystem`. Complète `docs/NARRATIVE.md` (bible narrative, source de
> vérité de tout ce qui suit) et s'inscrit dans la palette actée par `CLAUDE.md` (`#1A1A2E`,
> `#44FFEE`, `#AA44FF`, `#FFCC44`) et `src/UI/BiomeCatalog.cs` (5 biomes). Même logique de rigueur
> chiffrée que `docs/ART_BRIEF_PSEUDO3D.md` et `docs/ART_BRIEF_UI_FRAMES.md` : un compositeur doit
> pouvoir s'asseoir et jouer sans interprétation supplémentaire.
>
> **Statut : PRODUIT ET INTÉGRÉ le 2026-07-27.** Ce brief n'est plus une cible : les 26 pistes
> décrites ici existent dans `assets/audio/music/` et tournent en jeu. Elles sont **synthétisées
> par le dépôt** — `tools/generate_music_v3.py` (partitions), `tools/synth_instruments.py`
> (timbres), `tools/synth_lib.py` (moteur DSP) — donc régénérables à l'identique et modifiables
> sans logiciel tiers. Le mixage adaptatif en 4 couches est assuré en jeu par `MusicDirector`
> (autoload) et `src/Core/Rules/MusicIntensity.cs`. Les anciens placeholders chiptune CC0 ont été
> supprimés. **Ce document reste la source de vérité de la composition** : modifier une tonalité,
> un tempo ou une progression ici ET dans `generate_music_v3.py` (les deux ensemble).
>
> Contrôle qualité : `tools/analyze_music.py` (niveaux, hiérarchie fréquentielle §4.1, écrêtage de
> la somme des stems §4.4), `tools/preview_adaptive_mix.py <biome>` (démo d'écoute des paliers
> d'intensité), `tools/check_music_assets.gd` (headless Godot : égalité des durées entre stems).

---

## 1. Parti pris

Chimera Protocol raconte une fusion qui a mal tourné : une technologie et une magie anciennes
qui devaient s'unir pour sauver le monde, et qui l'ont à la place réorganisé selon une logique
qui n'est ni humaine ni mécanique. Chaque piste doit porter cette tension : synthés analogiques
chauds (l'humanité d'avant, le souvenir d'un monde qui savait encore fabriquer de la beauté)
contre percussions métalliques et textures granuleuses (la Rouille Vivante qui recolonise tout
ce qu'elle touche). Le **chœur formantique** est l'élément le plus chargé de sens : ce ne sont
jamais des voix nettes — ce sont des voix qui se dissolvent, légèrement désaccordées, filtrées,
parfois coupées en plein souffle, parce que ce sont, dans la fiction, les dernières traces
sonores d'humains absorbés par la Rouille. Plus un biome est corrompu (Friche d'Aether, Secteur
Néon), plus ce chœur perd en clarté (formants qui dérivent, artefacts de dégradation légers,
harmoniques désaccordées). La Rouille elle-même se traduit en son par des harmoniques qui se
dégradent progressivement dans les percussions (métal qui grince, résonances qui claquent au
lieu de sonner net) — jamais du bruit gratuit, toujours un instrument "vivant" qui se corrompt.
L'Arpenteur, lui, est seul : le Menu et le Hub portent cette solitude avec un CS-80 chaud et un
tempo qui respire, sans percussion insistante — entre deux runs, il n'y a que de la fatigue et
un peu de chaleur artisanale. En combat, jamais de tapis passif : la musique pousse au même
rythme que le joueur, c'est un allié discret, jamais un simple décor.

---

## 2. Palette instrumentale

Timbres autorisés — aucun autre patch/instrument sans validation, même logique de discipline
que la palette 32 couleurs du pixel art (`docs/ART_BRIEF_PSEUDO3D.md`).

| Timbre | Registre | Rôle dans le mix | Stem(s) | Signification narrative |
|---|---|---|---|---|
| Nappe CS-80 (pad principal analogique) | C2–C5 | Fondation harmonique chaude | `bed` | Le monde d'avant, la chaleur qui subsiste |
| Basse analogique (sub + mid, mono synth type Moog/Jupiter) | E0–E2 | Fondation (drone) + motif (séquencé) | `bed` (tenue) / `pulse` (séquence) | Le poids mécanique, le corps augmenté de l'Arpenteur |
| Chœur formantique "aah"/"ooh" (pad vocal synthétique type Solina/CS-80 choir) | A3–A5 | Nappe expressive, jamais lead net | `lead` (principal), `boss` (dissonant) | Voix humaines de l'enclave absorbées par la Rouille — jamais nettes |
| Arpège séquencé (arpeggiator mono/poly type Jupiter-8, Juno) | C4–C6 | Motif mélodique identifiant le biome | `lead` | Le flux d'Aether qui circule ; densité = saturation du lieu |
| Percussion métallique/industrielle (tôle, enclume, tuyaux, caisse claire saturée) | large-bande, transitoires | Groove, pulsation | `pulse` (légère), `boss` (lourde) | Les rouages de la Rouille au travail, le pas des Sentinelles/Colosses |
| Drone d'Aether (texture granulaire, filtre résonant lent, sub oscillant) | 20 Hz–8 kHz filtré | Atmosphère continue | `bed` | L'énergie Aether ambiante, la "respiration" du lieu |
| Cordes synthétiques (ensemble type CS-80 strings/Solina) | A2–A5 | Renfort dramatique | `boss` (surtout), intro/hub ponctuel | La grandeur tragique de la Convergence |
| Cluster/grappe dissonante (cordes ou chœur resserrés) | grave-medium | Tension pure, exclusif boss | `boss` | La Rouille à son paroxysme, présence du boss |
| Cloche/métal accordé (FM bell, reverb longue) | C5–C7 | Accent ponctuel, ornementation | `lead` (ponctuel), stingers `levelup`/`victory` | Cristallisation d'un Noyau d'Aether |
| Texture climatique (vent filtré / crépitement / grain numérique, spécifique par biome) | variable | Couleur atmosphérique du biome | `bed` | Le climat propre à chaque biome (glace, braise, données) |

**Règle de production transversale (clé pour la tenue des stems)** : le stem `bed` de chaque
biome joue le drone en **quinte nue (fondamentale + quinte juste, sans tierce)**. La tierce
(majeure/mineure/altérée) qui donne sa couleur modale au biome n'apparaît que dans `lead` (arpège
et chœur). Conséquence : `bed` seul ne trahit jamais le mode exact (reste neutre, ambiant,
jouable en boucle sans lead), et l'identité modale de chaque biome est portée entièrement par la
couche qui monte à 0.6 d'intensité — c'est ce qui garantit qu'un biome "sonne juste" même si le
joueur reste longtemps à basse intensité (juste le bed) sans jamais entendre une harmonie fausse.

---

## 3. Grille des pistes

### 3.1 Table de référence

| Piste | Tonalité / Mode | BPM | Mesures/boucle | Durée boucle | Stems |
|---|---|---|---|---|---|
| `menu` | La Aeolien (La mineur naturel) | 70 | 16 (4/4) | 54,9 s | mono, non adaptatif |
| `intro` | Ré mineur → point d'orgue sur La (ouverture atonale libre) | ~66 (rubato) | libre (~40 mes.) | ~90–100 s, non bouclé | mono, non adaptatif |
| `hub` | Fa majeur / Ré mineur relatif | 84 | 16 (4/4) | 45,7 s | mono, non adaptatif |
| `run_sanctuaire` | Do mineur naturel (Aeolien) | 100 | 16 (4/4) | 38,4 s | bed / pulse / lead / boss |
| `run_aether` | Ré Phrygien dominant | 112 | 16 (4/4) | 34,3 s | bed / pulse / lead / boss |
| `run_givre` | La Dorien | 72 | 16 (4/4) | 53,3 s | bed / pulse / lead / boss |
| `run_fournaise` | Sol Phrygien (naturel) | 136 | 16 (4/4) | 28,2 s | bed / pulse / lead / boss |
| `run_neon` | Mi Mixolydien | 128 | 16 (4/4) | 30,0 s | bed / pulse / lead / boss |
| `stinger_death` | libre, glissando descendant (pas de tonalité fixe) | — | — | ~4–5 s | mono, one-shot |
| `stinger_victory` | Do majeur | — | — | ~6–7 s | mono, one-shot |
| `stinger_levelup` | Do majeur | — | — | ~1,5–2 s | mono, one-shot |

Calcul de durée : `secondes = mesures × 4 temps × 60 / BPM`. Toutes les pistes de run partagent
la même architecture — **progression de 4 accords à 2 mesures chacun (8 mesures = 1 cycle),
jouée 2 fois avec variation au second cycle** pour atteindre les 16 mesures de boucle (cf. §5).

### 3.2 `menu` — thème principal, doux

Progression : `Am — F — C — G` (i–VI–III–VII), 2 mesures/accord, 8 mesures ×2 avec variation
(voix intérieures qui bougent légèrement au 2e passage, cf. §5) = 16 mesures, 54,9 s.

Composition en une seule passe (pas de stems adaptatifs) : nappe CS-80 (attaque lente, ~2 s de
fondu) entre dès la mesure 1 ; arpège épars (noires, jamais de croches, pas de motif "occupé")
entre en mesure 3 ; chœur "ooh" très lointain (reverb longue, presque un souffle) entre en
mesure 5 et ne gonfle qu'en fin de phrase. **Aucune percussion.** Intention : la respiration
entre deux missions — jamais un tapis, juste une présence, mélancolique sans être désespérée
(cf. `docs/NARRATIVE.md` : "l'univers est abîmé, pas sans espoir").

### 3.3 `intro` — cinématique, narrative

Piste libre, non bouclée, ~90–100 s, calée sur la cut-scene (`IntroScreen`, actuellement
`music_intro.ogg` ~94 s en placeholder). Structure suggérée, en rubato (~66 BPM indicatif pour
caler les impacts) :

- **0:00–0:20** — texture froide, cluster ambigu (aucune tonalité claire), seul le drone
  d'Aether joue : "le monde d'avant, les lignes profondes".
- **0:20–0:45** — montée progressive : cordes synthétiques et chœur entrent en fondu, la
  dissonance croît (la Convergence qui approche).
- **0:45–0:60 (climax)** — cluster fortissimo + un impact de percussion métallique unique (le
  basculement) ; le chœur se brise/se filtre brutalement (l'humain absorbé).
- **0:60–0:90** — retombée : le drone se stabilise en Ré mineur, le chœur persiste très filtré
  en arrière-plan (la Rouille Vivante qui s'installe, permanente).
- **Fin (~0:90–0:95)** — point d'orgue sur **La** (dominante de Ré mineur, non résolue), tenu
  jusqu'au fondu vers le Menu — transition harmonique directe vers le **La mineur** du thème de
  Menu (§3.2).

### 3.4 `hub` — l'enclave, chaleureux mais fatigué

Progression : `Dm — Bb — F — C` (vi–IV–I–V de Fa majeur), 2 mesures/accord, 16 mesures, 45,7 s.

Composition en une seule passe : CS-80 en accords joués (pas juste un pad tenu — quelque chose
de plus "habité", comme des mains qui réparent), percussion métallique très légère et éparse
(un tintement d'outil lointain, jamais un beat), chœur "aah" chaud et proche (moins de reverb
que le Menu — l'enclave est un lieu physique, pas un souvenir). Intention : on souffle, on
répare, on prépare la prochaine sortie — fatigué mais pas résigné.

### 3.5 `run_sanctuaire` — terrain neutre, biome de référence

Progression : `Cm — Ab — Eb — Bb` (i–VI–III–VII), Do mineur naturel, 100 BPM, 16 mesures = 38,4 s.

- **bed** : drone C2 (quinte nue C–G) + drone d'Aether discret (souffle filtré bas). Aucune
  couleur climatique marquée — c'est la référence neutre du jeu.
- **pulse** (entre ~0.3) : basse séquencée en croches sur la fondamentale de chaque accord +
  percussion métallique légère (kick synthétique sourd, caisse claire filtrée), motif 4 mesures
  répété ×4.
- **lead** (entre ~0.6) : arpège CS-80 ascendant sur chaque accord (porte la tierce mineure,
  cf. règle §2) + chœur "ooh" en simple soutien harmonique, pas de mélodie propre.
- **boss** (exclusif combat de boss) : cluster de cordes synthétiques graves + percussion lourde
  (enclume double-vitesse) + chœur grave "aah" dissonant.
- Cohérence : `bed` seul = drone ambiant complet, sonne comme "un silence habité" ; `bed+pulse`
  = groove neutre jouable sans mélodie ; `+lead` apporte la couleur harmonique identifiable ;
  `+boss` bascule tout en tension sans jamais couper les trois autres couches.

### 3.6 `run_aether` — friche corrompue, +20% XP

Progression : `D — Eb — Gm — A7` (I–bII–iv–V7), Ré Phrygien dominant, 112 BPM, 16 mesures = 34,3 s.

Le mode Phrygien dominant (tierce majeure F# sur la tonique) donne la couleur "exotique/magique"
du biome — le mouvement I→bII (D→Eb, seconde mineure) est le motif de corruption du morceau,
un frottement qui ne se résout jamais complètement.

- **bed** : drone D2 (quinte nue D–A, sans le F#) + texture granulaire Aether (filtre résonant
  qui monte/descend lentement sur 8 mesures, pulsation magique).
- **pulse** (~0.3) : basse séquencée en doubles-croches syncopées + percussion métallique
  résonante légèrement désaccordée (cloches non tempérées).
- **lead** (~0.6) : arpège rapide en triples-croches qui expose le F# caractéristique + chœur
  "aah" en portamento lent (jamais staccato), légèrement désaccordé (±15 cents) et filtré
  passe-bas mobile — l'humain "avalé", en mouvement constant.
- **boss** : cluster de cordes + chœur dissonant à l'octave désaccordée (dégradation la plus
  marquée du jeu sur ce biome, cohérent avec la saturation Aether) + percussion lourde.

### 3.7 `run_givre` — cryogénique, ennemis -18% lents

Progression : `Am — D — G — Em` (i–IV–bVII–v), La Dorien, 72 BPM, 16 mesures = 53,3 s.

Le Dorien se distingue de l'Aeolien du Sanctuaire par sa **sixte majeure** (F#, présente dans
l'accord IV = Ré majeur) : une lueur froide qui ne résout jamais en confort. Tempo le plus lent
du jeu — le biome est suspendu, tout respire plus lentement.

- **bed** : drone A1/A2 (quinte nue) quasi immobile + texture "vent glacé" (bruit filtré
  passe-haut, montée/descente sur 8 mesures).
- **pulse** (~0.3) : reste **clairsemé** — percussion métallique = carillon de glace espacé
  (pas un kick dense), basse en rondes/blanches (jamais de croches serrées) : "un tic qui
  s'égrène, pas un groove".
- **lead** (~0.6) : arpège lent (croches espacées, silences qui respirent) portant le F# Dorien
  (le "scintillement du glaçon") + chœur "ooh" très soutenu, quasi choral, figé.
- **boss** : cluster de cordes graves + percussion lourde, **mais sans accélération de tempo** —
  seule piste du jeu où le boss ne pousse qu'en densité/volume, jamais en énergie rythmique,
  cohérent avec le ralentissement du biome.

### 3.8 `run_fournaise` — infernal, ennemis +18% rapides

Progression : `Gm — Ab — Cm — Ab` (i–bII–iv–bII), Sol Phrygien naturel (tierce **mineure**,
à ne pas confondre avec le Phrygien dominant de l'Aether), 136 BPM, 16 mesures = 28,2 s. Tempo
le plus rapide du jeu, rythme le plus syncopé.

- **bed** : drone G1/G2 (quinte nue) + texture "crépitement de braises" (bruit granulaire haute
  fréquence, craquements irréguliers).
- **pulse** (~0.3) : la couche la plus dense du jeu — basse séquencée en doubles-croches
  staccato + percussion métallique syncopée (frappes d'enclume décalées sur les contre-temps).
- **lead** (~0.6) : arpège agressif qui martèle l'intervalle de seconde mineure Ab–G (motif
  signature du biome) + chœur "aah" légèrement saturé (distorsion douce, jamais propre) — "le
  chœur brûle".
- **boss** : percussion la plus lourde et la plus rapide du jeu (frappes en double-temps),
  cordes synthétiques distordues, cluster grave.

### 3.9 `run_neon` — secteur de données overclocké

Progression : `E — D — A — E` (I–bVII–IV–I, vamp Mixolydien classique), Mi Mixolydien, 128 BPM,
16 mesures = 30,0 s exactement — le seul biome en 4-à-la-noire strict.

- **bed** : drone E2 (quinte nue) + nappe légèrement chorusée/flangée — seul biome où le bed a
  un caractère assumé "propre/artificiel" plutôt qu'organique.
- **pulse** (~0.3) : kick 4-à-la-noire strict (un kick sur chaque temps — signature du secteur
  overclocké, jamais interrompu) + basse séquencée doubles-croches très serrée + percussion =
  claps/hi-hats synthétiques "numériques" (pas de métal organique ici).
- **lead** (~0.6) : arpège seizièmes très dense (proche synthwave/house) + chœur "aah" traité
  avec un vocodeur léger — ici le chœur sonne délibérément comme des données, pas comme une voix
  qui se dissout : contraste volontaire avec les autres biomes, cohérent avec le thème "secteur
  de données".
- **boss** : cluster + percussion lourde, **mais le kick 4-à-la-noire continue en dessous, sans
  interruption** — "l'overclock ne s'arrête jamais, même face au danger".

### 3.10 Stingers

- **`death`** (~4–5 s, pas de tonalité fixe) : chœur "aah" en glissando chromatique descendant
  + basse sub qui plonge, se termine sur un souffle qui s'éteint (pas de note franche —
  dissolution, cohérent avec "être absorbé par la Rouille"), aucune résolution harmonique.
- **`victory`** (Do majeur, ~6–7 s) : arpège CS-80 ascendant + chœur "ooh" qui monte et s'épanouit
  + cloche métallique qui sonne juste. Un des seuls moments franchement lumineux du jeu — ce
  stinger **ignore délibérément** la tonalité du biome en cours (toujours Do majeur, hors
  diégèse).
- **`levelup`** (Do majeur, ~1,5–2 s) : 4 notes montantes rapides (C–E–G–C) sur un timbre
  cloche/FM + chœur "ah" bref en soutien, pas de basse. Priorité fréquentielle haute (2–6 kHz,
  cf. §4) pour rester audible même en combat dense.

---

## 4. Règles de mixage

### 4.1 Hiérarchie fréquentielle par stem (pour qu'aucune couche ne se batte)

| Stem | Bande occupée | Contenu | Bande évitée (laissée aux autres) |
|---|---|---|---|
| `bed` | 20–400 Hz (sub/drone) + 8–12 kHz (air/shimmer) | Drone racine+quinte, texture climatique | 400 Hz–2 kHz (scoop — laissé à `pulse`/`lead`) |
| `pulse` | 60–250 Hz (kick/basse) + 2–5 kHz (transitoire percussion) | Basse séquencée, percussion métallique légère | 300 Hz–1,5 kHz (scoop) |
| `lead` | 500 Hz–4 kHz | Arpège + chœur (formants voix) | Sub (< 100 Hz) — jamais de basse propre sur ce stem |
| `boss` | 40–120 Hz (cluster/sub) + 800 Hz–3 kHz (métal/cordes dissonantes) | Cluster, percussion lourde, chœur grave | Occupe volontairement le haut-medium de `lead` — priorité absolue quand actif |

### 4.2 Niveaux cibles relatifs

Cible export : mix run intégré à **-16 LUFS** (menu/hub **-18 LUFS**, plus calmes). Niveaux
relatifs entre stems (avant automation d'intensité en jeu) :

| Stem | Niveau | Note |
|---|---|---|
| `bed` | -6 dB | Toujours audible, jamais la couche la plus forte |
| `pulse` | -4 dB (quand actif) | Monte en volume, pas en tonalité, quand l'intensité franchit 0.3 |
| `lead` | -3 dB (quand actif) | Monte quand l'intensité franchit 0.6 |
| `boss` | 0 dB (référence) | La couche la plus forte, jamais partagée avec un autre pic |

### 4.3 Sidechain

- `pulse` : le transitoire du kick/percussion applique un léger duck de **-2 dB / 80 ms** sur
  `bed` — pompe discrète qui donne une sensation d'avancée sans être un effet EDM appuyé.
- `boss` (quand actif) : duck plus lent, **-3 dB / 250 ms release**, appliqué sur `bed` + `pulse`
  + `lead` — le boss doit toujours ressortir sans jamais réduire les trois autres couches au
  silence (contrainte : à intensité 1.0 + boss, les 4 stems jouent ensemble sans bouillie).

### 4.4 Headroom

Chaque stem exporté avec **-6 dB de headroom au pic** (pour que la somme des 4 stems à pleine
intensité ne clippe jamais). Si un bounce mono (fallback IA, cf. `docs/AUDIO_AI_PROMPTS.md`) est
produit, cible **true peak ≤ -1 dBTP**.

### 4.5 Longueur de reverb par biome

| Piste | RT60 | Type / note |
|---|---|---|
| `run_sanctuaire` | 1,8 s | Hall moyen — ruines à échelle humaine |
| `run_aether` | 3,2 s | Cathédrale saturée — "cathédrale corrompue" |
| `run_givre` | 4,0 s | Très longue, mais atténuation HF rapide dès ~2 kHz — la glace absorbe les aigus, la traîne sonne étouffée/vitreuse |
| `run_fournaise` | 0,9 s | Courte/serrée — l'agressivité a besoin de direct ; préférer une modulation/chorus léger à la taille pour donner de l'espace sans ramollir la syncope |
| `run_neon` | 1,2 s | Plate courte, premières réflexions nettes — espace "club/data-center", synthétique et précis, jamais cathédrale |
| `menu` | 2,2 s | Plate chaude, intime |
| `hub` | 2,5 s | Légèrement plus large que le menu — un lieu physique, pas un souvenir |
| `boss` (stem, tous biomes) | + traîne longue additionnelle réservée aux éléments cluster/chœur uniquement | Jamais sur les percussions, qui doivent rester sèches/précises même en boss |

---

## 5. Contrainte de bouclage

Toutes les pistes de run (et `menu`/`hub`) sont construites sur **2 cycles de 8 mesures**
(progression complète de 4 accords à 2 mesures chacun = 1 cycle) pour atteindre 16 mesures de
boucle. Pour qu'une boucle de cette longueur ne s'entende jamais comme une boucle :

- **Variation obligatoire entre le cycle 1 (mesures 1–8) et le cycle 2 (mesures 9–16)** :
  ajouter/retirer une note dans l'arpège, inverser l'ordre d'un motif, transposer une ligne à
  l'octave, ou changer une voix du chœur. Jamais une répétition strictement identique des deux
  cycles.
- **Aucun élément "signature" qui n'apparaît qu'une seule fois** dans la boucle (un accord
  orchestral isolé, une phrase de chœur unique) : c'est le repère qui trahit le point de boucle
  à l'oreille. Soit l'élément revient de façon régulière (toutes les 8 mesures, il devient un
  repère volontaire), soit il est réservé aux transitions/stingers, jamais aux stems bouclés.
- **La traîne de reverb ne doit jamais dépasser la fenêtre de recollement de la boucle** :
  prévoir 0,3–0,4 s de matière de raccord (le stem `bed`, en particulier, doit démarrer et finir
  sur la **même note de drone tenue**, sans transitoire d'attaque au premier temps de la mesure
  1) — le redémarrage doit être harmoniquement indolore même sans crossfade sample-accurate.
- **Pas de montée/riser qui "veut" résoudre exactement au point de boucle** (ex. un riser qui
  grimpe en mesures 15–16 comme s'il menait vers une transition boss) — ce type d'élément est
  réservé aux stingers et aux transitions scriptées, jamais à une couche qui boucle indéfiniment.
- **Le motif de percussion (`pulse`) doit se diviser proprement** en un pattern de 4 ou 8 mesures
  répété (4× ou 2× dans la boucle de 16), plutôt qu'un pattern qui évolue sur 16 mesures avec un
  fill final unique en mesure 16 — sauf si ce fill est explicitement écrit comme un **enchaînement**
  qui ramène proprement sur le temps 1 de la mesure 1 (jamais un fill qui s'arrête net).
- **Le stem `bed` doit être audible et sur la fondamentale à la toute première et à la toute
  dernière fraction de seconde de la boucle** — c'est la couche qui "raccroche" les wagons même
  quand `pulse`/`lead`/`boss` ont des rythmiques plus complexes.
