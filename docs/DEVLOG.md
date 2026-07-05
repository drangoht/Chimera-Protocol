# Devlog — Chimera Protocol

> Source de vérité des notes de version, réutilisée pour les devlogs itch.io (cf. l'agent
> `release-manager`). Entrées en ordre décroissant (la plus récente en haut). Ton orienté
> joueur, EN puis FR (audience itch surtout anglophone).

## v1.9.0 — Your keys, your way: ZQSD by default + full keyboard remapping (2026-07-05)

**New**
- **Move with ZQSD out of the box.** The default movement layout now matches an AZERTY keyboard
  natively — **Z Q S D** for up/left/down/right — alongside the **arrow keys** and full **gamepad**
  support (D-pad & left stick). No more fighting a QWERTY-centric default on the first run.
- **Rebind every direction.** A brand-new **Controls** section in the **Options** screen lets you
  remap each movement direction: click a direction, press the key you want ("press a key…", **Esc**
  cancels), done. Prefer WASD, ESDF, IJKL? It's yours in seconds.
- **One-click reset.** A **Default keys (ZQSD)** button restores the stock layout instantly if you
  ever want to start over.
- **Your bindings stick.** Custom keys are saved to your settings and re-applied automatically every
  time you launch the game.

**Why it matters**
- Movement is separated from the menu inputs under the hood, so remapping is clean and never breaks
  UI navigation. Whatever keyboard you play on, the controls now bend to you — not the other way around.

---

**Nouveautés**
- **Déplacement en ZQSD dès le départ.** La disposition de mouvement par défaut correspond désormais
  nativement à un clavier AZERTY — **Z Q S D** pour haut/gauche/bas/droite — en plus des **flèches
  directionnelles** et de la **manette** (croix directionnelle & stick gauche). Fini de subir un défaut
  pensé pour le QWERTY dès la première partie.
- **Remappez chaque direction.** Une toute nouvelle section **Contrôles** dans l'écran **Options**
  permet de réassigner chaque direction de déplacement : cliquez sur une direction, appuyez sur la
  touche voulue (« appuyez sur une touche… », **Échap** annule), c'est fait. Vous préférez ZQSD, ESDF,
  IJKL ? C'est à vous en quelques secondes.
- **Réinitialisation en un clic.** Un bouton **Touches par défaut (ZQSD)** rétablit instantanément la
  disposition d'origine si vous voulez repartir de zéro.
- **Vos touches sont conservées.** Vos raccourcis personnalisés sont enregistrés et ré-appliqués
  automatiquement à chaque lancement du jeu.

**Pourquoi c'est important**
- Le déplacement est désormais séparé des entrées de menu en interne : le remappage est propre et ne
  casse jamais la navigation de l'UI. Quel que soit votre clavier, les contrôles s'adaptent à vous —
  et non l'inverse.

---

## v1.8.1 — A real cold: reworked frost mist & visibly frozen enemies (2026-07-05)

**Polish**
- **Frost Veil is now a real cold front.** The cryo fusion no longer wraps you in a flat glowing ring —
  it billows into a **swirling mist of cold**: drifting fog banks, drifting frost particles, and a crisp
  **iced rim** at the edge of the aura. You can *see* the chill spread around you now.
- **Slowed enemies look frozen.** Anything caught by the **Frost Veil** or the **Cryo Lance** now takes
  on a **glacial blue tint** while it's slowed — so you can read at a glance exactly which foes are
  locked down and which are still coming in hot.
- **Clearer Vector Lance reticle.** The aiming triangle gains a **dark outline** for far better contrast
  against bright arenas and dense swarms — no more losing your aim in the chaos.

**Why it matters**
- Same weapons, more readable battlefield. The frost effects now *communicate* what they do: you feel the
  cold, you spot the frozen targets, and you always know where your next Vector shot is headed.

---

**Peaufinage**
- **Le Voile de Givre dégage enfin un vrai froid.** La fusion cryo ne vous entoure plus d'un simple
  anneau lumineux plat — elle déploie une **brume de froid tourbillonnante** : nappes de brume mouvantes,
  particules de givre, et un **liseré glacé** net au bord de l'aura. On *voit* désormais le froid se
  répandre autour de vous.
- **Les ennemis ralentis paraissent gelés.** Tout ce qui est pris par le **Voile de Givre** ou la
  **Lance Cryo** adopte une **teinte bleu glacé** tant qu'il est ralenti — vous lisez d'un coup d'œil
  quels ennemis sont figés et lesquels foncent encore sur vous.
- **Réticule de la Lance Vectorielle plus lisible.** Le triangle de visée gagne un **contour sombre**
  pour un bien meilleur contraste sur les arènes claires et les nuées denses — fini de perdre sa visée
  dans le chaos.

**Pourquoi c'est important**
- Mêmes armes, champ de bataille plus lisible. Les effets de givre *communiquent* enfin ce qu'ils font :
  vous sentez le froid, vous repérez les cibles gelées, et vous savez toujours où part votre prochain
  tir vectoriel.

---

## v1.8.0 — Take aim: mouse & right-stick aiming for the Vector Lance (2026-07-04)

**New**
- **Aim where you want with the Vector Lance.** The directional weapon (and its **Vector Ray** fusion)
  no longer fire along your movement direction. Now you *aim*:
  - **Mouse & keyboard** — the bolt fires toward your **cursor**. Point, shoot, carve.
  - **Gamepad** — aim with the **right stick**, fully independent of where you're moving.
  - The game **switches automatically** between the two based on the last device you touched — no menu,
    no toggle.
- **New aiming reticle.** A small **triangle** orbits your character and points, in real time, at your
  current aim direction — so you always know exactly where the next shot is headed.

**Why it matters**
- The Vector Lance and Vector Ray were powerful but blunt: you had to *walk* toward your target to hit
  it. Now you can strafe one way and fire another, kite while keeping your aim locked on a boss, and
  thread bolts through gaps in the swarm. Far more control and skill on these directional weapons.

---

**Nouveautés**
- **Visez où vous voulez avec la Lance Vectorielle.** L'arme dirigée (et sa fusion **Rayon Vecteur**) ne
  tirent plus dans votre direction de déplacement. Désormais vous *visez* :
  - **Clavier & souris** — le trait part vers le **curseur**. Pointez, tirez, taillez.
  - **Manette** — visez au **stick droit**, indépendamment de votre déplacement.
  - Le jeu **bascule automatiquement** entre les deux selon le dernier périphérique utilisé — aucun menu,
    aucune option à cocher.
- **Nouveau réticule de visée.** Un petit **triangle** gravite autour de votre personnage et pointe, en
  temps réel, la direction visée — vous savez toujours exactement où part le prochain tir.

**Pourquoi c'est important**
- La Lance Vectorielle et le Rayon Vecteur frappaient fort mais restaient rigides : il fallait *marcher*
  vers la cible pour l'atteindre. Vous pouvez maintenant vous déplacer d'un côté et tirer de l'autre,
  kiter un boss en gardant la visée verrouillée, et faufiler vos traits dans les brèches de la nuée.
  Bien plus de contrôle et de skill sur ces armes dirigées.

---

## v1.7.0 — Frost Veil: freeze the swarm in place (2026-07-04)

**New**
- **Frost Veil** — a new *defensive control* fusion. Take the **Cryo Lance** to level 5, pick up the
  **Reinforced Plating** passive, and the icy beam stops firing in a line — instead it wraps around you
  as a **permanent AURA of frost**. Every enemy caught in range takes continuous damage *and* is hit
  with a massive slow, reapplied without pause. The swarm crawls toward you, frozen to a standstill,
  while the aura grinds it down. Turn the tide from "outrun the horde" to "let it come and freeze."

---

**Nouveautés**
- **Voile de Givre** — une nouvelle fusion de *contrôle défensif*. Montez la **Lance Cryo** au niveau 5,
  ramassez le passif **Plaque Renforcée**, et le rayon glacé cesse de tirer en ligne — il s'enroule
  autour de vous en **AURA de givre PERMANENTE**. Tout ennemi à portée subit des dégâts continus *et* un
  ralentissement massif, réappliqué sans relâche. La nuée rampe vers vous, engluée au ralenti, pendant
  que l'aura la broie. Passez de « fuir la horde » à « la laisser venir et la geler ».

---

## v1.6.0 — Vector Ray: the first aimed fusion (2026-07-04)

**New**
- **Vector Ray** — the first *aiming-skill* fusion. Take the **Vector Lance** to level 5, pick up the
  **Servo-Motors** passive, and the aimed bolt evolves into a **continuous piercing RAY**: no more
  cooldown, no more single shots. The beam locks to your movement direction and sweeps across the
  battlefield, punching straight through the entire line of enemies it touches. Steer it like a
  searchlight and carve lanes through the swarm.

---

**Nouveautés**
- **Rayon Vecteur** — la première fusion *skill de visée*. Montez la **Lance Vectorielle** au niveau 5,
  ramassez le passif **Servo-Moteurs**, et le trait dirigé évolue en **RAYON perforant CONTINU** : plus
  de cooldown, fini les tirs isolés. Le rayon s'oriente selon votre direction de déplacement et balaie
  l'arène en traversant de part en part toute la ligne d'ennemis qu'il touche. Pilotez-le comme un
  projecteur et taillez des couloirs dans la nuée.

---

## v1.5.0 — Aimed weapon & smarter difficulty curve (2026-07-04)

**New**
- **Vector Lance** — a new *aimed* weapon (Rare). Unlike the rest of your arsenal, it fires a piercing
  bolt in **your movement direction** instead of auto-targeting the nearest enemy. Line up your shots:
  it pierces from level 1, and higher levels add a tight spread of extra bolts. A bit of skill amid the
  auto-aim chaos.

**Balance**
- **Reworked difficulty curve.** The first minute is now a little more forgiving, but survivors no
  longer coast to god-mode: basic enemies ramp up faster in the mid/late game to keep the pressure on.
  Bosses and mini-bosses keep their carefully tuned health — their fight windows are unchanged.

**Fixes**
- Your character now stays **visible above flames and weapon VFX** (no more disappearing inside your
  own Pyre Stream in the heat of battle).

---

**Nouveautés**
- **Lance Vectorielle** — une nouvelle arme *dirigée* (Rare). Contrairement au reste de l'arsenal, elle
  tire un trait perforant dans **ta direction de déplacement** au lieu de viser l'ennemi le plus proche.
  Aligne tes tirs : perforant dès le niveau 1, avec un éventail de traits supplémentaires aux niveaux
  élevés. Un peu de skill au milieu de l'auto-visée.

**Équilibrage**
- **Courbe de difficulté revue.** La première minute est un peu plus permissive, mais survivre ne suffit
  plus à devenir invincible : les ennemis de base montent plus vite en milieu/fin de partie pour
  maintenir la pression. Les boss et mini-boss gardent leurs PV calibrés — leur fenêtre de combat est
  inchangée.

**Corrections**
- Ton personnage reste désormais **visible au-dessus des flammes et des effets d'armes** (fini le perso
  qui disparaît dans son propre Jet de Pyre en plein combat).
