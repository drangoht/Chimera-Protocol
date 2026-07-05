# Devlog — Chimera Protocol

> Source de vérité des notes de version, réutilisée pour les devlogs itch.io (cf. l'agent
> `release-manager`). Entrées en ordre décroissant (la plus récente en haut). Ton orienté
> joueur, EN puis FR (audience itch surtout anglophone).

## v1.11.1 — Level-up cards read clean again (2026-07-05)

**Fixes**
- **No more overlap on the level-up screen.** On longer upgrade descriptions — the **Frost Veil**,
  **Vector Beam** and **Vector Lance** fusions were the worst offenders — the text could creep upward
  and collide with the icon at the top of the card. The description now lives in its own slot anchored
  right below the icon, top-aligned, wrapping and clipping cleanly. The two can no longer touch: pick
  your upgrades without squinting through a pile-up.

**Why it matters**
- Pure polish, no gameplay change. This was a full pass across every screen — the Bestiary, Arsenal,
  Character Select, Intro, Hub and end-of-run screens already used separate containers and were clean,
  so the level-up card was the last one standing.

---

**Corrections**
- **Fini le chevauchement sur l'écran de montée de niveau.** Sur les descriptions d'amélioration un peu
  longues — les fusions **Voile de Givre**, **Rayon Vecteur** et **Lance Vectorielle** en tête — le texte
  pouvait remonter et venir chevaucher l'icône en haut de la carte. La description occupe désormais son
  propre emplacement ancré juste sous l'icône, aligné en haut, avec retour à la ligne et découpe propres.
  Les deux ne peuvent plus se toucher : choisissez vos améliorations sans déchiffrer un empilement.

**Pourquoi c'est important**
- Du polish pur, aucun changement de gameplay. C'était une passe complète sur tous les écrans — le
  Bestiaire, l'Arsenal, la Sélection de perso, l'Intro, le Hub et les écrans de fin de run utilisaient
  déjà des conteneurs séparés et étaient sains ; la carte de level-up était la dernière concernée.

---

## v1.11.0 — Show the arena on your Discord + a version stamp on every screen (2026-07-05)

**New**
- **Discord Rich Presence.** Fire up the game and your Discord status now reads **"Playing Chimera
  Protocol"** — with the game icon, a contextual line (browsing the menus, or *in a run*: your
  character and current biome) and a session timer ticking up. It's fully optional and completely
  silent if Discord isn't running — no prompt, no slowdown, never a crash.
- **A version stamp on every screen.** A small `v<version>-<commit>` tag now sits in the bottom-right
  corner of every screen. When you report a bug or share a clip, that stamp tells us exactly which
  build you were on — no guessing.

**Why it matters**
- No gameplay changes this time: this build makes Chimera Protocol easier to *share* and easier to
  *support*. Your friends see what you're playing, and every screenshot carries its own build number.

---

**Nouveautés**
- **Discord Rich Presence.** Lancez le jeu et votre statut Discord affiche désormais **« joue à Chimera
  Protocol »** — avec l'icône du jeu, une ligne contextuelle (navigation dans les menus, ou *en run* :
  votre personnage et le biome en cours) et un chrono de session qui tourne. C'est entièrement optionnel
  et totalement silencieux si Discord n'est pas lancé — aucune demande, aucun ralentissement, jamais de
  plantage.
- **Un tampon de version sur chaque écran.** Une petite étiquette `v<version>-<commit>` s'affiche
  maintenant en bas à droite de tous les écrans. Quand vous remontez un bug ou partagez un clip, ce
  tampon nous dit exactement sur quel build vous étiez — fini les devinettes.

**Pourquoi c'est important**
- Aucun changement de gameplay cette fois : ce build rend Chimera Protocol plus facile à *partager* et
  plus facile à *suivre*. Vos amis voient à quoi vous jouez, et chaque capture porte son propre numéro
  de build.

---

## v1.10.0 — Meet Vector: a precision cyborg with a guided lance (2026-07-05)

**New**
- **A fourth playable character: Vector.** A precision-built cyborg — lean violet chassis, scanner
  visor — who trades bulk for reach. Medium-fragile frame (**90 HP**, **speed 210**) that rewards
  positioning over brute force. Pick him from the character select screen and play the arena at arm's
  length.
- **The Vector Lance — your first *aimed* signature weapon.** Vector starts with a fully **directed**
  weapon: an on-screen **aiming reticle** (mouse or right stick) is live from the very first second,
  tinted to his identity. Line up your shot and the lance **pierces straight through every enemy in
  its path** — reward for lining up the crowd instead of spraying blind.
- **Already in your arsenal.** The Vector Lance is a signature weapon, so it's **always available in
  the arsenal** — no unlock grind to try the new playstyle.

**Fixes**
- **Options screen scrolls now.** The **Controls** section added in 1.9.0 could overflow at 720p and
  hide the **Back** and **Reset all** buttons — the whole screen is now scrollable (keyboard focus
  auto-scrolls to the selected item), so everything is reachable again.

**Why it matters**
- Vector is the first character built entirely around aimed fire: a new, skill-forward way to play the
  same arenas. No new engine tricks under the hood — he rides the existing character pipeline — just a
  sharper way to kill.

---

**Nouveautés**
- **Un quatrième personnage jouable : Vecteur.** Un cyborg de précision — châssis violet élancé, visière
  scanner — qui échange le blindage contre l'allonge. Cadre médian-fragile (**90 PV**, **vitesse 210**)
  qui récompense le placement plutôt que la force brute. Choisissez-le à l'écran de sélection et jouez
  l'arène à distance.
- **La Lance Vectorielle — votre première arme de signature *dirigée*.** Vecteur démarre avec une arme
  entièrement **dirigée** : un **réticule de visée** à l'écran (souris ou stick droit) est actif dès la
  première seconde, teinté à son identité. Alignez votre tir et la lance **transperce d'un trait tous les
  ennemis sur sa trajectoire** — la récompense d'un alignement propre plutôt que d'un tir à l'aveugle.
- **Déjà dans votre arsenal.** La Lance Vectorielle est une arme de signature : elle est **toujours
  disponible à l'arsenal** — aucun déblocage à farmer pour essayer le nouveau style.

**Corrections**
- **L'écran Options défile désormais.** La section **Contrôles** ajoutée en 1.9.0 pouvait déborder en
  720p et masquer les boutons **Retour** et **Tout réinitialiser** — l'écran est maintenant défilable
  (le focus clavier fait défiler automatiquement vers l'élément sélectionné), tout redevient atteignable.

**Pourquoi c'est important**
- Vecteur est le premier personnage entièrement construit autour du tir visé : une nouvelle façon de jouer
  les mêmes arènes, plus technique. Aucune nouvelle mécanique moteur sous le capot — il réutilise le
  pipeline de personnage existant — juste une manière plus chirurgicale de faire le ménage.

---

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
