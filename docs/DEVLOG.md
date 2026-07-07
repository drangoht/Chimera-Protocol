# Devlog — Chimera Protocol

> Source de vérité des notes de version, réutilisée pour les devlogs itch.io (cf. l'agent
> `release-manager`). Entrées en ordre décroissant (la plus récente en haut). Ton orienté
> joueur, EN puis FR (audience itch surtout anglophone).

## v1.12.0 — Assimilation: don't kill the monsters, become them (2026-07-07)

**New**
- **A third progression axis: Assimilation.** Alongside XP levels and meta-upgrades, every kill now
  feeds a per-archetype gauge (Swarm / Drone / Sentinel / Colossus). Fill one and you're offered a
  **graft** — a piece of that monster, grafted onto your own body. Accept it and it's yours for the
  run, in a dedicated slot on the HUD.
- **5 grafts to hunt.** **Symbiotic Swarm** (three orbiting mini-swarms + lifesteal), **Erratic
  Servos** (an invulnerable dash), **Aiming Eye** (a self-aiming turret), **Grafted Carapace**
  (damage reduction, bonus HP and thorns, at the cost of speed) and **Stalker's Wave** (a periodic
  knockback shockwave). **3 slots** by default, up to **5** via two new Hub upgrades — **Graft
  Slots** and **Graft Metabolism** (lowers gauge thresholds).
- **Fusions: two grafts become one, and stronger.** Carry both prerequisite grafts long enough and
  a **fusion** gauge fills instead, offering a single evolved form that **frees a slot**. **Armored
  Charge** (Carapace + Servos) turns your dash into a **240px armored charge** that deals impact
  damage and knockback along the way, while easing the Carapace's speed penalty. **Turret Hive**
  (Eye + Swarm) turns your four orbiting swarms into **four auto-turrets** ringing you at range,
  covering 360° instead of relying on risky melee contact.
- **A new Codex screen: Chimera.** Browse every graft and fusion — effect, requirements, lore —
  from the main menu, the same way the Bestiary and Arsenal already work.

**Fixes**
- **Graft slots are readable now.** The buff-icon row used to overlap the graft slot row on the
  HUD; slots are bigger, tinted with a magenta accent, and icons no longer spill outside their
  frame.

**Why it matters**
- This is the game's core differentiator, live for the first time: *"Don't kill the monsters.
  Become them."* It's not cosmetic — grafts change your kit (a new dash, a new turret, contact
  thorns), and fusions are the payoff for committing to a combo instead of chasing whatever drops.
  Full design rationale in `docs/DESIGN_ASSIMILATION.md`.

---

**Nouveautés**
- **Un troisième axe de progression : l'Assimilation.** En plus des niveaux d'XP et des
  améliorations méta, chaque kill alimente désormais une jauge par archétype (Nuée / Drone /
  Sentinelle / Colosse). Une jauge pleine propose une **greffe** — un fragment du monstre, greffé
  sur votre propre corps. Acceptez-la et elle est à vous pour la run, dans un emplacement dédié au
  HUD.
- **5 greffes à traquer.** **Nuée Symbiotique** (trois mini-essaims orbitants + vol de vie),
  **Servos Erratiques** (un dash invulnérable), **Œil de Visée** (une tourelle auto-visée),
  **Carapace Greffée** (réduction de dégâts, PV bonus et épines, au prix de la vitesse) et **Onde
  du Rôdeur** (une onde de choc périodique à knockback). **3 emplacements** par défaut, jusqu'à
  **5** via deux nouvelles améliorations du Hub — **Emplacements de Greffe** et **Métabolisme de
  Greffe** (abaisse les seuils de jauge).
- **Fusions : deux greffes deviennent une, et plus fortes.** Portez les 2 greffes prérequises
  assez longtemps et une jauge de **fusion** se remplit à son tour, proposant une forme évoluée
  unique qui **libère un emplacement**. **Charge Blindée** (Carapace + Servos) transforme votre
  dash en une **charge blindée de 240px** infligeant dégâts et knockback sur son passage, tout en
  allégeant le malus de vitesse de la Carapace. **Ruche de Tourelles** (Œil + Nuée) transforme vos
  quatre mini-essaims orbitants en **quatre tourelles automatiques** postées autour de vous,
  couvrant 360° au lieu de dépendre d'un contact de mêlée risqué.
- **Un nouvel écran Codex : Chimère.** Parcourez toutes les greffes et fusions — effet,
  prérequis, lore — depuis le menu principal, sur le même principe que le Bestiaire et l'Arsenal.

**Corrections**
- **Les emplacements de greffe sont enfin lisibles.** La rangée d'icônes de buffs recouvrait
  auparavant la rangée d'emplacements de greffe au HUD ; les emplacements sont désormais plus
  grands, teintés d'un liseré magenta, et les icônes ne débordent plus de leur cadre.

**Pourquoi c'est important**
- C'est le vrai différenciateur du jeu, en ligne pour la première fois : *« Ne tue pas les
  monstres. Deviens-les. »* Ce n'est pas cosmétique — les greffes changent votre kit (nouveau
  dash, nouvelle tourelle, épines de contact), et les fusions récompensent l'engagement dans un
  combo plutôt que la course au drop. Détail du design complet dans
  `docs/DESIGN_ASSIMILATION.md`.

---

## v1.11.4 — The end boss is beatable again (2026-07-06)

**Fixes**
- **Rusted Core HP cut from 18,000 to 12,000.** The end boss (Rusted Core, ~13 min) had become
  effectively unkillable for an average build — its effective HP at 13 minutes in Normal was
  ~32,040, requiring 700-900 mono-target DPS to hit a reasonable time-to-kill. It's now ~21,360
  effective HP, putting the fight back in reach: measured time-to-kill is ~36-40 s on a reference
  build, with an average build expected around 43-61 s.
- **Fixed overtime boss stacking.** In overtime, a second (or third) Rusted Core could spawn
  before the first one died, because the boss's `maxSimultaneous: 1` cap was being bypassed on
  respawn. Multiple 21k-HP bosses piling up was the main reason the fight *felt* impossible — it
  now respects the cap: exactly one Rusted Core alive at a time, and the next one only appears
  after the current one is actually defeated.
- Dead-code cleanup in `RustedCore` (stale placeholder stats overwritten by the JSON tuning at
  runtime anyway) — no gameplay effect.

**Why it matters**
- Pure balance/bugfix pass, no new content. This was the top player complaint on the end boss:
  "impossible to kill." Both root causes are addressed — HP was tuned too high for the intended
  TTK band, and the stacking bug compounded it by throwing multiple full-HP bosses at the player
  at once. Validated via `--debug-boss` TTK measurement + code-level non-regression check on the
  spawn cap — see `docs/TEST_REPORT.md` (session 2026-07-06).

---

**Corrections**
- **PV du Noyau Rouillé réduits de 18 000 à 12 000.** Le boss de fin (Le Noyau Rouille, ~13 min)
  était devenu quasi impossible à tuer pour un build moyen — ses PV effectifs à 13 minutes en
  Normal atteignaient ~32 040, exigeant 700-900 DPS mono-cible pour respecter un temps de mise à
  mort raisonnable. Ils sont désormais à ~21 360 PV effectifs : le combat redevient jouable, avec
  un TTK mesuré de ~36-40 s sur un build de référence, et ~43-61 s attendu pour un build moyen.
- **Fix de l'empilement de boss en overtime.** En overtime, un deuxième (voire un troisième) Noyau
  Rouille pouvait apparaître avant que le premier ne soit mort, car le plafond `maxSimultaneous: 1`
  du boss était contourné au respawn. L'empilement de plusieurs boss à 21k PV était la cause
  principale du ressenti « impossible à tuer » — le plafond est désormais respecté : un seul Noyau
  Rouille vivant à la fois, le suivant n'apparaissant qu'après la mort effective du précédent.
- Nettoyage de code mort dans `RustedCore` (statistiques placeholder obsolètes, de toute façon
  écrasées par le tuning JSON à l'exécution) — sans effet sur le gameplay.

**Pourquoi c'est important**
- Passe pure d'équilibrage/correctif, sans nouveau contenu. C'était la plainte joueur numéro un sur
  le boss de fin : « impossible à tuer ». Les deux causes racines sont traitées — des PV réglés trop
  haut pour la bande de TTK visée, et un bug d'empilement qui aggravait la situation en envoyant
  plusieurs boss à pleins PV simultanément. Validé via mesure de TTK (`--debug-boss`) et vérification
  de non-régression du plafond de spawn par analyse de code — voir `docs/TEST_REPORT.md` (session
  2026-07-06).

---

## v1.11.3 — Enemies don't ghost through you anymore (2026-07-05)

**Improvements**
- **The player now pushes enemies aside instead of passing through them.** Foes overlapping your
  body get shoved outward along a ring around you — you never lose speed or get stuck, and contact
  damage still applies exactly as before. A big target like the Colossus gets pushed further away
  than a small one, so heavies still *feel* heavy. When an enemy is dead-centered on you, it gets
  pushed along your current direction of travel instead of a random side, so the shove reads as a
  natural continuation of your movement rather than a jitter.
- **Solid obstacles now actually hide you when you're behind them.** A z-index bug let the player
  sprite render on top of impassable obstacles even while physically blocked by them, breaking the
  "solid wall" illusion. Obstacle bodies now draw above the player and their ground shadow is
  re-anchored correctly, so occlusion matches the physics in all five biomes.

**Why it matters**
- Pure game-feel and readability fixes, no balance change. The player-vs-enemy overlap used to look
  like ghosting through crowds; obstacles used to look transparent despite blocking movement. Both
  now read correctly. Validated in-game across all five biomes (push: no stalling, no ghosting,
  contact damage intact; occlusion: correct in every biome, shadow grounded, physics blocking
  unaffected) — see `docs/TEST_REPORT.md`.

---

**Améliorations**
- **Le joueur écarte désormais les ennemis au lieu de les traverser.** Les ennemis qui chevauchent
  votre corps sont repoussés vers l'extérieur sur un anneau autour de vous — vous ne perdez jamais
  de vitesse et ne restez jamais bloqué, et les dégâts de contact s'appliquent toujours exactement
  comme avant. Une grosse cible comme le Colosse est repoussée plus loin qu'une petite, pour que les
  poids lourds *se sentent* lourds. Quand un ennemi est parfaitement centré sur vous, il est repoussé
  dans le sens de votre déplacement plutôt que sur un côté aléatoire, pour que la poussée se lise
  comme un prolongement naturel du mouvement plutôt qu'un à-coup.
- **Les obstacles infranchissables vous cachent enfin quand vous passez derrière.** Un bug de z-index
  laissait le sprite du joueur s'afficher au-dessus des obstacles impassables alors même qu'il en
  était physiquement bloqué, brisant l'illusion de « mur solide ». Le corps des obstacles se dessine
  désormais au-dessus du joueur et leur ombre au sol est ré-ancrée correctement : l'occultation
  correspond enfin à la physique dans les cinq biomes.

**Pourquoi c'est important**
- Corrections pures de gamefeel et de lisibilité, aucun changement d'équilibrage. Le chevauchement
  joueur/ennemi ressemblait à du ghosting en pleine nuée ; les obstacles semblaient transparents
  malgré le blocage physique. Les deux se lisent maintenant correctement. Validé en jeu dans les
  cinq biomes (poussée : pas de blocage, pas de ghosting, dégâts de contact intacts ; occultation :
  correcte dans chaque biome, ombre au sol cohérente, blocage physique préservé) — voir
  `docs/TEST_REPORT.md`.

---

## v1.11.2 — The Frost biome finally looks cold (2026-07-05)

**Fixes**
- **Frozen enemies now actually turn to ice.** Freeze effects used to wash enemies with a blue tint —
  but a tint can only ever *darken* a warm sprite, so orange foes just went muddy instead of frosty.
  The frost state is now driven by a dedicated shader that pulls each pixel toward a crisp glacial blue,
  so a burning-orange brute reads as unmistakably *frozen* while keeping its pseudo-3D shading. Hit
  flashes and elite tints still layer cleanly on top.
- **The Frost Veil reads like real mist.** Chimera's frost aura went from two thin concentric rings to
  a proper churning bank — six offset puffs plus denser frost particles — so it looks like a
  volumetric cloud of cold even when you stand still, instead of a flat halo.

**Why it matters**
- Visual polish only, no gameplay or balance changes. This closes the two outstanding readability notes
  on the Frost biome: freezes and the Frost Veil now sell the cold instead of hinting at it.

---

**Corrections**
- **Les ennemis gelés virent enfin à la glace.** Le gel appliquait jusqu'ici une teinte bleue sur les
  ennemis — mais une teinte ne peut qu'*assombrir* un sprite chaud, si bien qu'un ennemi orange tournait
  au terne plutôt qu'au givré. L'état gelé passe désormais par un shader dédié qui tire chaque pixel vers
  un bleu glacial franc : une brute orange incandescente se lit comme réellement *gelée*, en conservant
  son relief pseudo-3D. Le flash de dégâts et la teinte d'élite se composent proprement par-dessus.
- **Le Voile de Givre se lit comme une vraie brume.** L'aura de givre de Chimera passe de deux fines
  nappes concentriques à un banc dense qui tournoie — six bouffées décalées et des particules de givre
  densifiées — pour donner un nuage de froid volumétrique même à l'arrêt, au lieu d'un simple halo plat.

**Pourquoi c'est important**
- Du polish visuel uniquement, aucun changement de gameplay ni d'équilibrage. Cela ferme les deux
  réserves de lisibilité sur le biome Givre : le gel et le Voile de Givre vendent enfin le froid au lieu
  de le suggérer.

---

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
