# Devlog — Chimera Protocol

> Source de vérité des notes de version, réutilisée pour les devlogs itch.io (cf. l'agent
> `release-manager`). Entrées en ordre décroissant (la plus récente en haut). Ton orienté
> joueur, EN puis FR (audience itch surtout anglophone).

## v1.22.0 — The Capacitor was quietly flattening every weapon (2026-07-28)

**Fixed — one passive was erasing the difference between your weapons**
- The four passives only define **3 levels** but can be taken up to **20**. Past level 3, each
  extra level was granting the exact same bonus again, forever. The Capacitor added 14 % cooldown
  reduction per level — so from **level 8 onwards it reached 100 %**, and every weapon you owned
  dropped to the same **0.15 s** floor.
- A heavy weapon on a 1.2 s cooldown was firing exactly as fast as a light one on 0.4 s. The
  Singularity, built around its slow, heavy shot, became a machine gun. Cooldown — one of the things
  that makes a weapon *itself* — simply stopped existing.
- It also explains why the final boss felt so wildly inconsistent: the very same fight measured
  anywhere between **14.8 and 42 seconds** depending on whether you had taken that one card.
- Passives now keep improving past their defined levels, but with **diminishing returns**, and
  cooldown reduction is capped at **75 %**. A heavy weapon stays heavy. On top of that, a passive
  whose stat has hit its cap **stops being offered** — the Capacitor and the Servo Motors were still
  showing up as cards long after they could do anything for you.

**Fixed — mini-boss rewards vanished half the time**
- Killing a mini-boss is supposed to offer you a weapon card. One time out of two, **nothing
  appeared**: an internal shuffle was picking an invalid card and the whole reward was silently
  dropped. Nine occurrences in a single test session. It now shows up every time.
- Also silenced a flood of errors caused by the biome golems, which were trying to play an attack
  animation their sprites never had.

**Changed — the final boss, recalibrated once more**
- The Rusted Core goes from **8000 to 5000 base health**. With the Capacitor no longer doubling
  everyone's fire rate, late-run damage came down — the boss needed to follow.
- Measured on a played run and on the bench: **26 to 35 seconds**, from the end-of-timer fight all
  the way through its overtime returns. Previously those overtime rematches collapsed to **under
  15 seconds**, which made the escalation meaningless.

---

**Corrigé — un passif effaçait la différence entre vos armes**
- Les quatre passifs ne définissent que **3 niveaux** mais montent jusqu'à **20**. Au-delà du
  niveau 3, chaque niveau supplémentaire réappliquait exactement le même bonus, indéfiniment. Le
  Capaciteur ajoutait 14 % de réduction de recharge par niveau : **dès le niveau 8, il atteignait
  100 %** et toutes vos armes tombaient au même plancher de **0,15 s**.
- Une arme lourde à 1,2 s de recharge tirait exactement aussi vite qu'une arme légère à 0,4 s. La
  Singularité, construite autour de son tir lent et massif, devenait une mitrailleuse. La cadence —
  l'une des choses qui font qu'une arme *est* elle-même — cessait purement et simplement d'exister.
- Cela explique aussi pourquoi le boss de fin semblait si inconstant : le même combat se mesurait
  entre **14,8 et 42 secondes** selon que vous aviez pris cette carte ou non.
- Les passifs continuent de progresser au-delà de leurs niveaux définis, mais en **rendements
  décroissants**, et la réduction de recharge est plafonnée à **75 %**. Une arme lourde reste
  lourde. Et un passif dont la statistique est au plafond **cesse d'être proposé** — le Capaciteur
  et les Servomoteurs apparaissaient encore en carte longtemps après ne plus rien pouvoir vous
  apporter.

**Corrigé — les récompenses de mini-boss disparaissaient une fois sur deux**
- Tuer un mini-boss doit vous proposer une carte d'arme. Une fois sur deux, **rien n'apparaissait** :
  un mélange interne tirait une carte invalide et toute la récompense passait à la trappe, sans
  aucun signe. Neuf occurrences sur une seule session de test. Elle arrive désormais à tous les coups.
- Au passage, disparition d'un déluge d'erreurs provoqué par les golems de biome, qui tentaient de
  jouer une animation d'attaque absente de leurs sprites.

**Modifié — le boss de fin, recalibré une fois de plus**
- Le Noyau Rouillé passe de **8000 à 5000 points de vie de base**. Le Capaciteur ne doublant plus la
  cadence de tout le monde, les dégâts de fin de partie ont baissé : le boss devait suivre.
- Mesuré sur une partie jouée et au banc d'essai : **26 à 35 secondes**, du combat de fin de timer
  jusqu'à ses réapparitions en overtime. Ces dernières s'effondraient auparavant **sous les
  15 secondes**, ce qui vidait l'escalade de son sens.

---

## v1.21.0 — Weapon fusions are finally an upgrade (2026-07-28)

**Fixed — fusing a weapon was quietly ruining your run**
- Evolving a weapon **reset it to level 1** and it could never be levelled again: it stopped
  appearing in level-up cards, and the base weapon was gone from the pool too. That slot was dead
  for the rest of the run.
- Worse, fusions ignored **every damage bonus you own** — Thermal Core, Capacitor, and all the
  permanent upgrades bought with Aether Echoes.
- The result: taking the shiniest card in the game (epic, white flash, its own sound) **divided
  your late-run damage by 3 to 6**. Measured on full runs: 105 DPS with a fully fused build against
  410 for the same level and the same upgrades, but keeping one ordinary weapon.
- Fusions now **inherit the level** of the weapon they replace, keep levelling up like any other
  weapon (up to 20), and receive your damage and cooldown bonuses. Same measurement after the fix:
  **368 to 539 DPS**.
- Two of them needed extra care: the **Overcharged Rail** was firing its base damage no matter what,
  and the **Solar Column**'s burn never scaled — half of what makes that weapon what it is.

**Changed — the final boss is no longer a slog**
- The Rusted Core drops from **12000 to 8000 base health**. Measured on a real, played run, it was
  taking **44 seconds** to bring down with a strong build — right at the edge of "exhausting".
  It now lands around **23 to 32 seconds** depending on the biome.
- This is only fair now that fusions work: before the fix, a fused build simply could not deal
  enough damage, and lowering the boss's health would have hidden the real problem.

---

**Corrigé — fusionner une arme sabotait votre partie**
- Faire évoluer une arme la **ramenait au niveau 1**, définitivement : elle ne réapparaissait plus
  dans les cartes de montée de niveau, et l'arme de base en avait disparu aussi. L'emplacement était
  mort pour le reste de la partie.
- Pire, les fusions ignoraient **tous vos bonus de dégâts** — Noyau Thermique, Capaciteur, et toutes
  les améliorations permanentes achetées avec les Échos d'Aether.
- Résultat : prendre la carte la plus spectaculaire du jeu (épique, flash blanc, son dédié)
  **divisait vos dégâts de fin de partie par 3 à 6**. Mesuré sur des parties complètes : 105 DPS
  pour un build entièrement fusionné, contre 410 à niveau et améliorations identiques en gardant une
  arme ordinaire.
- Les fusions **héritent désormais du niveau** de l'arme qu'elles remplacent, continuent de monter
  comme n'importe quelle arme (jusqu'à 20) et reçoivent vos bonus de dégâts et de recharge. Même
  mesure après correctif : **368 à 539 DPS**.
- Deux d'entre elles demandaient un soin particulier : le **Rail Surchargé** tirait ses dégâts de
  base quoi qu'il arrive, et la brûlure de la **Colonne Solaire** ne progressait pas — la moitié de
  l'identité de cette arme.

**Modifié — le boss de fin n'est plus une épreuve d'endurance**
- Le Noyau Rouillé passe de **12000 à 8000 points de vie de base**. Mesuré sur une vraie partie
  jouée, il demandait **44 secondes** à abattre avec un bon build — à la limite de l'épuisant. Il
  tombe désormais en **23 à 32 secondes** selon le biome.
- Cet ajustement n'a de sens que maintenant que les fusions fonctionnent : avant le correctif, un
  build fusionné ne pouvait tout simplement pas infliger assez de dégâts, et baisser les points de
  vie du boss aurait masqué le vrai problème.

---

## v1.20.0 — The Rusted Core fights back, and wears a different face in every biome (2026-07-28)

**Changed — the final boss now has three phases**
- The Rusted Core no longer fights the same way from full health to zero. At **66%** and **33%**
  it **overloads**: it freezes, stops shooting, takes no damage for a second — then comes back
  faster. Volleys, shockwaves and its signature move all tighten with every phase.
- In the **final phase** it calls in reinforcements from the local wildlife, every 12 seconds.
- The overload is your window: it can't hurt you while it's charging. Use it to reposition
  before the pace goes up.
- Its health and the time it takes to kill are unchanged — the fight is not longer, it's less flat.

**Added — five incarnations, one per biome**
- The Core has spread through all five zones and taken on what it found there. Same creature,
  same victory condition, but each biome now has its own version, with its own sprite, name and
  **one extra move**:
  - **Sanctuary — The Rusted Core**: a tight **directed fan** of shots. Stop running in straight lines.
  - **Aether — The Spectral Core**: **blinks** next to you and opens with a spiral volley. Kiting won't save you.
  - **Frost — The Frostbound Core**: a **cryo nova** that slows you, and frost patches that stay on the ground.
  - **Furnace — The Molten Core**: telegraphed **magma pools** that shrink the safe ground around you.
  - **Neon — The Prismatic Core**: two to four **rotating beams**. Keep circling.

**Added — boss health bar**
- A dedicated bar at the top of the screen while a boss is alive: its name, notches carved at the
  phase thresholds so you can see the switch coming, and the current phase.

**Fixed**
- Buttons on the character select, level select and Hub screens were sitting right on top of their
  panel's inner border. They now have room to breathe.
- The fourth character card (Vector) had its last line of text cut off. All four now fit on screen
  without scrolling, in every language.

---

**Modifié — le boss de fin combat désormais en trois phases**
- Le Noyau Rouillé ne se bat plus de la même façon du début à la fin. À **66 %** et **33 %** de sa
  vie, il entre en **surcharge** : il se fige, cesse de tirer, n'encaisse plus rien pendant une
  seconde — puis repart plus vite. Salves, ondes de choc et attaque signature se resserrent à
  chaque phase.
- En **dernière phase**, il appelle des renforts parmi la faune locale, toutes les 12 secondes.
- La surcharge est votre fenêtre : il ne peut pas vous blesser pendant qu'il se recharge.
  Profitez-en pour vous replacer avant que la cadence monte.
- Ses points de vie et le temps nécessaire pour l'abattre n'ont pas changé — le combat n'est pas
  plus long, il est moins plat.

**Ajouté — cinq incarnations, une par biome**
- Le Noyau s'est propagé dans les cinq zones et a assimilé ce qu'il y a trouvé. Même créature,
  même condition de victoire, mais chaque biome a maintenant sa version, avec son sprite, son nom
  et **une attaque en plus** :
  - **Sanctuaire — Le Noyau Rouillé** : un **éventail dirigé** et resserré. Fini les lignes droites.
  - **Aether — Le Noyau Spectral** : il **se téléporte** près de vous et enchaîne sur une salve en spirale. Le kiting ne suffit plus.
  - **Givre — Le Noyau de Givre** : une **nova cryogénique** qui vous ralentit, et des plaques de givre qui restent au sol.
  - **Fournaise — Le Noyau en Fusion** : des **flaques de magma** télégraphiées qui réduisent le terrain sûr.
  - **Néon — Le Noyau Prismatique** : deux à quatre **faisceaux rotatifs**. Tournez autour, sans vous arrêter.

**Ajouté — barre de vie du boss**
- Une barre dédiée en haut de l'écran tant qu'un boss est vivant : son nom, des crans gravés aux
  seuils de phase pour voir venir la bascule, et la phase en cours.

**Corrigé**
- Sur les écrans de choix du personnage, de choix du niveau et du Hub, les boutons étaient posés
  sur la bordure intérieure de leur cadre. Ils respirent enfin.
- La quatrième carte de personnage (Vecteur) avait sa dernière ligne coupée. Les quatre tiennent
  désormais à l'écran sans défilement, dans toutes les langues.

---

## v1.19.0 — Settings worth opening, and reachable mid-run (2026-07-28)

**Added — the options screen you actually expected**
- Options are now sorted into five sections — **Audio, Display, Game, Interface, Controls** —
  instead of one flat list.
- **Display**: window mode (**windowed / borderless / fullscreen**, replacing the old on-off
  toggle), **window resolution**, **V-Sync**, an **FPS limit** (60/120/144/240/unlimited) and an
  **FPS counter**.
- **Game**: screen shake is now a **slider** (0% turns it off entirely), plus **reduce flashes**
  for photosensitivity — the fusion flash is dimmed and the chromatic aberration is cut — and
  **controller rumble**, wired to taking damage and dying.
- **Interface**: toggle the **version stamp** and **Discord Rich Presence** on or off.
- Everything applies instantly and is saved to `settings.cfg`. Your existing settings carry over.

**Added — options from the pause menu**
- The pause menu has an **Options** button that opens the same screen **as an overlay**: no scene
  change, your run stays exactly where it was. Turn the music down, go fullscreen or rebind a key
  in the middle of a fight.
- Two things stay out of reach mid-run: **difficulty** (your run and its high score are already
  committed to it) and **Reset everything**.

**Fixed**
- Borderless mode used to be a trap on Windows: you could enter it, but the game refused to go
  back to windowed. It now uses the engine's native window modes and switches both ways.

---

**Ajouté — l'écran d'options qu'on attendait**
- Les options sont désormais rangées en cinq sections — **Audio, Affichage, Jeu, Interface,
  Contrôles** — au lieu d'une liste unique.
- **Affichage** : mode de fenêtre (**fenêtré / sans bordure / plein écran**, qui remplace l'ancien
  interrupteur), **résolution** de la fenêtre, **synchro verticale**, **limite d'images/s**
  (60/120/144/240/illimitée) et **compteur d'images/s**.
- **Jeu** : les secousses d'écran passent en **slider** (0 % les coupe complètement), avec en plus
  **réduire les flashs** pour la photosensibilité — le flash de fusion est atténué et l'aberration
  chromatique coupée — et la **vibration manette**, branchée sur les dégâts reçus et la mort.
- **Interface** : afficher ou masquer le **tampon de version** et le **statut Discord**.
- Tout s'applique immédiatement et se sauvegarde dans `settings.cfg`. Vos réglages existants sont
  conservés.

**Ajouté — les options depuis le menu pause**
- Le menu pause gagne un bouton **Options** qui ouvre le même écran **en surcouche** : aucun
  changement de scène, votre run reste exactement où elle en était. Baissez la musique, passez en
  plein écran ou changez une touche en plein combat.
- Deux exceptions en cours de partie : la **difficulté** (votre run et son record y sont déjà
  engagés) et « **Tout réinitialiser** ».

**Corrigé**
- Le mode sans bordure était un piège sous Windows : on pouvait y entrer, mais le jeu refusait de
  revenir en fenêtré. Il s'appuie désormais sur les modes natifs du moteur et fait l'aller-retour.

---

## v1.18.0 — Threat tiers: later levels finally fight back (2026-07-28)

**Changed — difficulty now follows the levels you unlock**
- The five levels unlock in sequence, and the Hub makes you **two to three times stronger** along
  the way — but every level ran on the **same difficulty curve**. The result was backwards: the last
  level was *easier* than the first, and grinding the Rusted Sanctuary was the best Echoes-per-risk
  ratio in the game. Not anymore.
- Each level now carries a **threat tier** (Sanctuary → Neon Sector). Higher tiers mean tougher
  enemies, harder hits, a denser arena, and **dangerous enemy types showing up earlier** — and they
  **pay more Echoes**, up to **×1.45** in the Neon Sector. Pushing to the next level is now the
  optimal play, not farming the first one.
- **The contract is on the card**: every level in the selection screen shows `Threat ★★★ · Echoes
  ×1.20` before you commit, and the end-of-run screen reminds you which tier paid out.
- **Bosses were handled with care**: mini-bosses and the level boss only take 55% of the tier's
  health bonus. Beating the boss is what unlocks the next level — at the full rate, the tier would
  have turned into a wall instead of a challenge. Their damage does scale fully: they are threats,
  not sponges.
- Threat tiers stack on top of your **difficulty setting** (Easy/Normal/Hard), which is untouched.
  If a tier feels rough, Easy still does what it always did.

---

**Modifié — la difficulté suit les niveaux que vous débloquez**
- Les cinq niveaux se débloquent en séquence et le Hub vous rend **deux à trois fois plus fort** en
  chemin — mais tous les niveaux tournaient sur la **même courbe de difficulté**. Le résultat était
  à l'envers : le dernier niveau était *plus facile* que le premier, et farmer le Sanctuaire Rouillé
  offrait le meilleur ratio Échos/risque du jeu. C'est terminé.
- Chaque niveau porte désormais un **palier de menace** (Sanctuaire → Secteur Néon). Plus le palier
  est élevé, plus les ennemis sont coriaces, frappent fort, remplissent l'arène — et **les types
  d'ennemis dangereux arrivent plus tôt**. En échange, le niveau **paie plus d'Échos**, jusqu'à
  **×1,45** au Secteur Néon. Monter d'un palier est maintenant le meilleur choix, plutôt que farmer
  le premier niveau.
- **Le contrat est affiché sur la carte** : chaque niveau de l'écran de sélection indique
  `Menace ★★★ · Échos ×1,20` avant de vous engager, et l'écran de fin de run rappelle quel palier a
  payé.
- **Les boss ont été traités avec précaution** : mini-boss et boss de niveau ne reçoivent que 55 %
  du bonus de PV du palier. Battre le boss est ce qui débloque le niveau suivant — au taux plein, le
  palier serait devenu un mur au lieu d'un défi. Leurs dégâts, eux, montent à taux plein : ce sont
  des menaces, pas des éponges.
- Les paliers se cumulent avec votre **réglage de difficulté** (Facile/Normal/Difficile), inchangé.
  Si un palier vous semble rude, Facile fait toujours son travail.

---

## v1.17.1 — A proper application icon (2026-07-28)

**New**
- **Dedicated app icon** for the Windows executable and editor, replacing Godot's default cyan
  square placeholder: a chimera head split down the middle — one half machine (steel, temple
  plate, cyan visor), one half organic (violet flesh, horn, violet eye) — with a golden seam down
  the center standing in for the Assimilation graft, set on the same beveled armored-plate frame
  language as the UI. Generated procedurally from the game's own palette, with separate detail
  levels so it stays legible from a 256px icon down to a 16px taskbar tile.
- Trailer: English-language cut with a `--lang` flag and a ready-to-paste YouTube description.

*No gameplay, balance or content changes in this release.*

---

**Nouveau**
- **Icône d'application dédiée** pour l'exécutable Windows et l'éditeur, en remplacement du carré
  cyan par défaut de Godot : une tête de chimère fendue en deux — moitié machine (acier, plaque de
  tempe, visière cyan) / moitié organique (chair violette, corne, œil violet) — avec une couture
  dorée au centre représentant la greffe de l'Assimilation, sur la même plaque blindée chanfreinée
  que le reste de l'UI. Générée procéduralement depuis la palette du jeu, avec plusieurs niveaux de
  détail pour rester lisible d'une icône 256px jusqu'à une tuile 16px dans la barre des tâches.
- Trailer : version anglaise avec un flag `--lang` et une description YouTube prête à coller.

*Aucun changement de gameplay, d'équilibrage ou de contenu dans cette version.*

---

## v1.17.0 — Industrial metal soundtrack & adaptive music (2026-07-27)

**New — a full metal soundtrack**
- **14 brand-new tracks** replace the old ambient score: down-tuned guitars and live drums up front,
  analog synths and wordless choirs serving the riff, 112 to 176 BPM. Main theme, the Enclave, intro
  cinematic, one track per biome and a boss theme.
- **Every biome has its own identity**: Sanctuary in C minor at 140, Aether in Phrygian dominant at
  152, Frost as a half-time groove at 130, Furnace at a punishing 176, Neon in Mixolydian at 160.

**New — the music reacts to your run**
- Each biome ships **two versions of the same track** — a restrained *verse* and a wide-open
  *chorus*. The game reads what is happening on screen (enemy density, time survived, how close to
  death you are) and **crossfades** between them. No cuts, no loops restarting: the track opens up
  when a swarm closes in and settles back down once you break through.
- **Bosses get their own theme**, common to all biomes, fading in as the fight starts.
- Intensity rises fast and falls back slowly on purpose: a wave dying in two seconds won't drop the
  music straight away, so it never pumps between waves.

**Fix — you can hear your weapons again**
- **Separate SFX and Music buses**, with the music **ducking under the sound effects** (sidechain
  compressor). Gunfire, XP pickups and UI clicks now cut through a wall of guitars instead of being
  masked by it — without turning the soundtrack into background noise.
- Music level rebalanced across the whole game, and three sound effects that sat too low were
  brought back up.

*Soundtrack generated with [Suno](https://suno.com) and processed in-repo (looping, loudness,
encoding). Sound effects: Kenney CC0.*

---

**Nouveau — une vraie bande-son metal**
- **14 morceaux inédits** remplacent l'ancienne partition d'ambiance : guitares down-tuned et
  batterie live au premier plan, synthés analogiques et chœurs sans paroles au service du riff, de
  112 à 176 BPM. Thème principal, l'Enclave, cinématique d'intro, un morceau par biome et un thème
  de boss.
- **Chaque biome a son identité** : Sanctuaire en do mineur à 140, Aether en phrygien dominant à
  152, Givre en groove half-time à 130, Fournaise à 176 sans répit, Néon en mixolydien à 160.

**Nouveau — la musique réagit à votre partie**
- Chaque biome embarque **deux versions du même morceau** — un *couplet* en retenue et un *refrain*
  tout ouvert. Le jeu lit ce qui se passe à l'écran (densité d'ennemis, temps de survie, proximité
  de la mort) et **fond l'un dans l'autre**. Aucune coupure, aucune boucle qui repart : le morceau
  s'ouvre quand la nuée se referme sur vous, et redescend une fois la percée faite.
- **Les boss ont leur propre thème**, commun à tous les biomes, qui monte à l'engagement du combat.
- L'intensité monte vite et redescend lentement, volontairement : une vague qui meurt en deux
  secondes ne fait pas retomber la musique aussitôt, donc elle ne pompe jamais entre deux vagues.

**Correctif — on entend de nouveau ses armes**
- **Bus SFX et Musique séparés**, la musique **s'efface sous les effets** (compresseur en
  sidechain). Tirs, ramassages d'XP et clics d'interface percent désormais le mur de guitares au
  lieu d'être masqués par lui — sans reléguer la bande-son au rang de bruit de fond.
- Niveau musical rééquilibré sur tout le jeu, et trois effets sonores trop en retrait remontés.

*Bande-son générée avec [Suno](https://suno.com) puis traitée par le dépôt (bouclage, loudness,
encodage). Effets sonores : Kenney CC0.*

---

## v1.16.0 — Armored-plate UI, everywhere (2026-07-26)

**New — the armored-plate frame treatment, extended**
- **Modals, level-up and selection screens** now wear the same beveled "armored plate" frame as the
  rest of the UI (chamfers, bevel, rivets, pulsed focus) — no more plain default panels breaking
  immersion mid-run.
- **Sliders, toggles and dropdowns** join the style: grooved rail with an accent-filled bar and a
  steel-plate grabber for sliders; toggles that read by the position of the pad, not just color, so
  state is never colorblind-ambiguous; dropdown popups now inherit the same frame automatically.
- Assorted frame polish: compact frames for small widgets (denser Hub), balanced short frames, dropdown
  arrow no longer sitting on the frame's edge.

**Fix**
- **Music no longer cuts out when a popup opens.** Level-up, pause, Assimilation and end-of-run screens
  were silencing the soundtrack (and UI SFX) because the audio player was paused along with the game.
  Music — and menu sounds — now keep playing under any modal.

---

**Nouveau — le style "plaque blindée" s'étend à toute l'UI**
- **Modales, écran de level-up et écrans de sélection** portent désormais le même cadre biseauté
  "plaque blindée" (chanfreins, bevel, rivets, focus pulsé) que le reste de l'interface — plus de
  panneaux Godot par défaut qui cassent l'immersion en pleine partie.
- **Curseurs, interrupteurs et menus déroulants** rejoignent la charte : rail creusé avec barre remplie
  à l'accent et poignée en plaque d'acier pour les curseurs ; interrupteurs qui se lisent à la
  *position* du pavé, pas seulement à la couleur, donc l'état reste lisible même en cas de
  daltonisme ; les menus déroulants héritent désormais automatiquement du même cadre.
- Fignolages divers : cadres compacts pour les petits éléments (Hub plus dense), équilibrage des
  cadres courts, flèche des listes déroulantes qui ne chevauche plus le liseré du cadre.

**Correctif**
- **La musique ne se coupe plus à l'ouverture d'une popup.** Level-up, pause, Assimilation et fin de
  run mettaient en pause la bande-son (et les SFX d'UI) en même temps que le jeu. Musique et sons de
  menu continuent désormais de jouer sous n'importe quelle modale.

---

## v1.15.0 — Challenges & rewards (2026-07-08)

**New — a 4th way to grow**
- **Challenges.** 13 in-game goals across combat, survival, assimilation and mastery — kill 100 enemies,
  survive 13 minutes, forge a fusion, clear a biome, and more. They're checked automatically at the end of
  every run.
- **Rewards you keep.** Completing a challenge grants **Echoes**, unlocks a **starting perk** (begin a run
  with a free graft, a second weapon, or an extra graft slot), or a cosmetic **title**.
- **New Challenges screen** shows every goal, your progress (X / N), and its reward. Equip your perk and
  title from the **Hub**; your title shows on the main menu.

**Menu cleanup**
- The main menu is tidier: Bestiary, Arsenal, Chimera and Challenges now live under a single **Codex**
  sub-menu, with a new **Perks** page describing every starting perk.
- Language selection moved to **flags** in the top-right corner.

**Fix**
- Nova Strike now shows its cooldown on its HUD graft icon (like the other dash grafts).

---

**Nouveau — une 4e façon de progresser**
- **Défis.** 13 objectifs en jeu (combat, survie, assimilation, maîtrise) — tue 100 ennemis, survis 13
  minutes, forge une fusion, termine un biome… Validés automatiquement à la fin de chaque run.
- **Des récompenses qui restent.** Accomplir un défi rapporte des **Échos**, débloque un **perk de départ**
  (commencer avec une greffe offerte, une seconde arme, ou un emplacement de greffe en plus), ou un **titre**
  cosmétique.
- **Nouvel écran Défis** : tous les objectifs, ta progression (X / N) et leur récompense. Équipe ton perk
  et ton titre au **Hub** ; ton titre s'affiche sur le menu principal.

**Ménage dans le menu**
- Menu principal plus clair : Bestiaire, Arsenal, Chimère et Défis sont désormais sous un sous-menu
  **Codex**, avec une nouvelle page **Perks** décrivant chaque perk de départ.
- Le choix de la langue passe en **drapeaux** en haut à droite.

**Correctif**
- La Frappe Nova affiche enfin sa recharge sur son icône de greffe au HUD (comme les autres greffes de dash).

---

## v1.14.2 — HUD fix: buff bar no longer overlaps weapons (2026-07-07)

**Fix**
- The temporary power-up bar was drawing on top of your weapon loadout on the in-game HUD. It now sits
  cleanly below the weapon row. No more overlap.

---

**Correctif**
- La barre de power-up temporaire s'affichait par-dessus le loadout d'armes sur le HUD en jeu. Elle se
  place désormais proprement sous la rangée d'armes. Fini le chevauchement.

---

## v1.14.1 — Nova Strike gets its icon (2026-07-07)

**Polish**
- **Nova Strike now has its own icon.** The third fusion was still showing a plain tinted square — it now
  gets a proper crafted icon: a violet-to-magenta star burst (its Stalker's Wave nova) trailing a cyan blink
  (its Servos dash). All 8 grafts & fusions now have dedicated, aligned icons — no more placeholders.
- **Lore.** Fleshed out the Chimera Protocol chapter with the missing bits: *why* two grafts can fuse (and
  free a slot), and *why* the biome you graft in leaves its mark. Flavor, no gameplay change.

---

**Fignolage**
- **La Frappe Nova a enfin son icône.** La 3e fusion affichait encore un carré teinté — elle a désormais une
  vraie icône dessinée : une étoile de détonation violet→magenta (sa nova, héritée de l'Onde du Rôdeur)
  traînant un blink cyan (sa ruade, héritée des Servos). Les 8 greffes et fusions ont maintenant toutes leur
  icône dédiée et alignée — fini les placeholders.
- **Lore.** Chapitre du Protocole Chimère complété : *pourquoi* deux greffes peuvent fusionner (et libérer un
  emplacement), et *pourquoi* le biome où tu greffes laisse sa marque. De la saveur, aucun changement de jeu.

---

## v1.14.0 — Nova Strike & biome affinities (2026-07-07)

**New**
- **Third graft fusion — Nova Strike.** Fuse *Stalker's Wave* + *Erratic Servos* and your dash becomes
  an **offensive blink**: you teleport in and **detonate a nova** where you land (a big shockwave, ~175px,
  with knockback), gated by your dash cooldown. The passive wave becomes a burst you aim. Heads-up — it
  shares the Servos graft with Armored Charge, so those two fusions are **mutually exclusive**: pick your
  dash (mobile ram vs. blink-nova).
- **Biome affinities — where you graft matters now.** A graft (or fusion) **captures the biome** you
  assimilate it in and gains its flavor:
  - **Sanctuary** — Stable: +12% graft damage.
  - **Aether** — Resonant: +20% graft range/area.
  - **Furnace** — Ardent: your grafts **set enemies ablaze** (burn).
  - **Frost** — Glacial: your grafts **slow** enemies.
  - **Neon** — Overcharged: −18% graft cooldown.
  The assimilation card tells you the affinity you'll get, and your silhouette parts take on the biome's
  tint. Same grafts, different runs: a swarm grafted in the Furnace burns; the same swarm in Frost chills.

**Why it matters**
- This closes the Assimilation expansion: three fusions, and a reason to think about *which biome* you
  build your chimera in. Every run shapes a different monster — now down to the elemental flavor.

---

**Nouveautés**
- **3e fusion de greffes — Frappe Nova.** Fusionne *Onde du Rôdeur* + *Servos Erratiques* et ta ruade
  devient une **téléportation offensive** : tu blinkes et **détones une nova** au point d'arrivée (grosse
  onde de choc ~175px + knockback), gatée par la recharge du dash. L'onde passive devient un burst que tu
  vises. À noter — elle partage la greffe Servos avec la Charge Blindée : ces deux fusions sont
  **mutuellement exclusives** (choisis ton dash : bélier mobile ou blink-nova).
- **Affinités de biome — où tu greffes compte, désormais.** Une greffe (ou fusion) **capture le biome**
  où tu l'assimiles et en prend la saveur :
  - **Sanctuaire** — Stable : +12% de dégâts de greffe.
  - **Aether** — Résonante : +20% de portée.
  - **Fournaise** — Ardente : tes greffes **enflamment** (brûlure).
  - **Givre** — Glaciale : tes greffes **ralentissent**.
  - **Néon** — Surchargée : −18% de cooldown de greffe.
  La carte d'assimilation t'indique l'affinité que tu obtiendras, et tes pièces de silhouette prennent la
  teinte du biome. Mêmes greffes, runs différents : une Nuée greffée en Fournaise brûle, la même en Givre gèle.

**Pourquoi c'est important**
- Ça referme l'expansion Assimilation : trois fusions, et une raison de réfléchir au *biome* où tu bâtis
  ta chimère. Chaque run façonne un monstre différent — jusqu'à la saveur élémentaire.

---

## v1.13.0 — Wear your chimera (2026-07-07)

**New**
- **Your body now shows what you've become.** Until now, assimilating grafts only tinted your
  character. From this version, each graft and fusion **grafts a visible part onto your body** — the
  chimera you build is the chimera you see:
  - **Grafted Carapace** — armored plating and pauldrons over your torso.
  - **Erratic Servos** — thruster fins on your flanks whose vents flare when you dash.
  - **Aiming Eye** — a floating eye above your head, its pupil tracking your nearest target.
  - **Stalker Wave** — a resonator crown that spins and swells right before each shockwave.
  - **Armored Charge** (fusion) — a heavy ram prow that faces the way you're heading and lights up on impact.
  - **Turret Hive** (fusion) — a hive core on your back (on top of the four turrets you already deploy).
- Parts are shaded to match the game's pseudo-3D lighting and stack up as you assimilate more —
  every run visibly builds a different monster.

**Why it matters**
- This is the payoff of "don't kill the monsters, become them": the transformation is now readable
  at a glance, not just a number on a HUD slot.

---

**Nouveautés**
- **Ton corps montre enfin ce que tu deviens.** Jusqu'ici, assimiler des greffes ne faisait que
  teinter ton personnage. À partir de cette version, chaque greffe et fusion **greffe une pièce
  visible sur ton corps** — la chimère que tu construis est celle que tu vois :
  - **Carapace Greffée** — plastron blindé et épaulières sur le torse.
  - **Servos Erratiques** — tuyères sur les flancs, dont les vents s'embrasent quand tu fais une ruade.
  - **Œil de Visée** — un œil flottant au-dessus de la tête, pupille braquée sur l'ennemi le plus proche.
  - **Onde du Rôdeur** — une couronne-résonateur qui tourne et enfle juste avant chaque onde de choc.
  - **Charge Blindée** (fusion) — une proue-bélier orientée dans ton sens de marche, qui s'illumine à l'impact.
  - **Ruche de Tourelles** (fusion) — un cœur de ruche dans le dos (en plus des quatre tourelles déployées).
- Les pièces sont ombrées pour coller à l'éclairage pseudo-3D du jeu et s'accumulent à mesure que tu
  assimiles — chaque run construit visiblement un monstre différent.

**Pourquoi c'est important**
- C'est la récompense de « ne tue pas les monstres, deviens-les » : la transformation se lit d'un
  coup d'œil, plus seulement dans un chiffre sur le HUD.

---

## v1.12.1 — Control comfort & readability (2026-07-07)

**New**
- **Dash cooldown gauge.** The dash graft (Erratic Servos, or the Armored Charge fusion) now shows
  its recharge right on its HUD slot: the square darkens on use and the icon refills from the bottom
  as the cooldown ticks down — so you can read at a glance when your evade is ready again.

**Fixes**
- **Dash is rebindable.** The Erratic Servos graft's dash was hard-wired to Shift/RB and missing
  from the Options screen. It's now a proper remap line — **"Dash (evade)"** — with its own save
  slot and covered by the "Reset to defaults" button.
- **Graft icons are centered again.** They were rendered oversized and clipped inside their HUD
  slots — you only saw a corner. Now each graft icon sits fully centered in its square.
- **WASD-style keys (ZQSD) now navigate menus.** Move keys and menu-navigation keys were separate
  under the hood, so pause, level-up, codex and other modals only responded to arrow keys. Movement
  keys now mirror onto menu navigation automatically, including after a remap — arrows, ZQSD and
  gamepad all work everywhere.
- **Graft icons were missing in the exported build.** The HUD's icon lookup used a file check that
  always returns false outside the editor, silently falling back to a plain tinted square for all
  7 grafts (including both fusions). Fixed — the real icons render in the shipped .exe.
- **HUD graft row no longer clips.** The panel was too short for the graft-slot row, which spilled
  past the rounded edge and crowded the weapon loadout below it. Panel enlarged, spacing cleaned up.

**Why it matters**
- All quality-of-life this time, no new content: the Assimilation grafts shipped last version are
  now fully visible and controllable the way they were meant to be.

---

**Nouveautés**
- **Jauge de recharge du dash.** La greffe de dash (Servos Erratiques, ou la fusion Charge Blindée)
  affiche désormais sa recharge directement sur son emplacement HUD : le carré s'assombrit à l'usage
  et l'icône se remplit par le bas au fil du cooldown — d'un coup d'œil, tu sais quand ton esquive
  est de nouveau prête.

**Corrections**
- **La ruade est enfin rebindable.** Le dash de la greffe Servos Erratiques était câblé en dur
  (Maj/RB) et absent de l'écran Options. C'est désormais une ligne de remap à part entière —
  **« Ruade (esquive) »** — avec sa propre persistance, couverte par le bouton « Touches par
  défaut ».
- **Les icônes de greffe sont de nouveau centrées.** Elles s'affichaient trop grandes et tronquées
  dans leurs emplacements HUD — on n'en voyait qu'un coin. Chaque icône est désormais entièrement
  centrée dans son carré.
- **ZQSD navigue enfin dans les menus.** Les touches de déplacement et les touches de navigation
  menu étaient séparées en interne : pause, level-up, codex et autres modals ne répondaient qu'aux
  flèches. Les touches de déplacement se miroitent désormais automatiquement vers la navigation
  menu, y compris après un remap — flèches, ZQSD et manette fonctionnent partout.
- **Les icônes de greffe manquaient en build exporté.** La détection d'icône du HUD reposait sur un
  test de fichier qui renvoie toujours faux hors éditeur, retombant silencieusement sur un simple
  carré teinté pour les 7 greffes (fusions comprises). Corrigé — les vraies icônes s'affichent dans
  l'exe publié.
- **La rangée de greffes du HUD ne déborde plus.** Le panneau était trop court pour la rangée
  d'emplacements de greffe, qui débordait du bord arrondi et empiétait sur le loadout d'armes juste
  en dessous. Panneau agrandi, espacement nettoyé.

**Pourquoi c'est important**
- Que du confort cette fois, pas de nouveau contenu : les greffes d'Assimilation sorties la version
  précédente sont désormais pleinement visibles et pilotables comme prévu.

---

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
