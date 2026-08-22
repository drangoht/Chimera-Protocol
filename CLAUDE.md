# CLAUDE.md — Mémoire de projet

Chargé automatiquement au démarrage de chaque session : **rester court et stable**. Le détail vit
dans des fichiers chargés **à la demande** (pointés ci-dessous) pour limiter le contexte par session.

## Le projet

"Chimera Protocol" — survivor roguelite vue du dessus, univers fantaisie-science-fiction (humains,
cyborgs, robots), inspiré de Vampire Survivors et Everything is Crab. **Moteur : Unity 6.5** (C#,
URP 2D). Le dépôt ne contient plus qu'un moteur : Godot a été retiré le **2026-08-10**.

- **Design complet → `docs/GDD.md`** : le consulter avant toute tâche de design/implémentation, et le tenir à jour à chaque décision.
- **Localiser du code** (système, écran, arme, ennemi, données, outil) → invoquer le skill **`/carte-projet`** plutôt que Glob/Grep à froid : il indexe l'arborescence + les checklists de câblage. Le maintenir à jour dans le même commit qu'un changement structurel.
- **Avant de coder** dans un domaine (armes, ennemis, UI/focus, VFX, assets, tests headless) → lire **`docs/PITFALLS_UNITY.md`**. Y ajouter tout nouveau piège découvert.
- **Comprendre l'architecture du portage** (principe logique-pure/moteur, ponts `Platform/`, contrats d'entités, cycle de vie d'une run) → **`docs/UNITY_MIGRATION_PLAN.md`**.
- **État d'implémentation détaillé & version courante → `docs/PROJECT_STATE.md`**.
- **Interroger un fichier trop gros pour être lu** → **MCP local** `mcp__local-llm__local_digest` (un ou plusieurs fichiers → une synthèse) / `local_map` (même question sur N fichiers). Outils **différés** : `ToolSearch` d'abord. Le serveur lit les fichiers côté LM Studio — **le contenu brut n'entre jamais en contexte** (mesuré : 83 173 tokens lus en local → 675 renvoyés).
  - **Les cibles** : `docs/TEST_REPORT.md` (~290 Ko) · `docs/GDD.md` (~220 Ko) · `docs/PITFALLS_UNITY.md` (~120 Ko) · `docs/DEVLOG.md` · `unity/Assets/StreamingAssets/localization/ui.csv` · `.../data/*.json`.
  - ⚠ **Lent** : ~6-7 min pour 290 Ko. Le lancer et continuer à travailler. Prévoir `max_tokens` large (1500-2500 pour un inventaire ; 900 a déjà été tronqué **sans le signaler**).
  - ⚠ **Bon sur du TEXTE, à proscrire sur des CHIFFRES.** Un LLM qui « lit » un CSV de mesures produit des nombres plausibles et faux. Règle : *s'il existe un outil déterministe, il gagne* (`tools/power_loop.py` pour `power_curve.log`).
  - ⚠ **Jamais pour du code qu'on s'apprête à éditer** — là, il faut le contenu réel. Ni pour localiser (`Grep` est instantané et exact).

## Phase actuelle

**2026-08-22 — LA MARÉE DE ROUILLE, PUBLIÉE en 2.4.0** (build itch #1905527, canal `html5`,
devlog à coller ; **Windows reste en 2.2.0**). Le rendu de la marée a été refait le jour même sur
retour de jeu — « trop carrée, dans la vraie vie la rouille n'est pas nette comme ça » — et **validé
en jouant** (« c'est bien mieux »). Tout le rendu tient désormais dans **un shader**
(`Resources/Shaders/RustTide.shader`, un seul quad : nappe, front, liseré, vagues, fumée).
**797 tests.** Design → `docs/GDD.md` §38.8 ; pièges → `docs/PITFALLS_UNITY.md` §Fin de partie.
⚠ **Une arête de sprite est droite par construction** : ce n'était pas un défaut de réglage. Le rendu
tenait dans ~20 `SpriteRenderer` et aucune couleur, opacité ou ordre de tri ne pouvait le ronger. La
découpe en segments ne fait que déplacer le problème — **on compte alors les segments**, comme on
compte les taches d'une brume faite de sprites doux. Même arbitrage que `AtmosphereFog`, même endroit
du moteur. ▶ Un **champ de distance évalué par pixel** n'a ni segment ni tache.
⚠⚠ **Le contour rongé appartient à la RÈGLE, pas au rendu** (`Rules/RustErosion`). Le dessiner
par-dessus une géométrie restée rectangulaire aurait été dix fois plus simple et aurait **menti au
joueur de 70 px** sur la seule information que la marée donne. Le shader en est une transcription
littérale → **toute retouche se fait DANS LES DEUX fichiers**. Corollaire contraignant : la formule
doit être reproductible CPU/GPU, donc **trois sinus** et non `frac(sin(dot(…)))` ; le fbm ne sert que
là où il n'engage rien (largeur du fondu, matière, fumée), **jamais la position du bord**.
⚠ **Une borne peut être ce qui fait EXISTER un effet** : l'amplitude est plafonnée par
`arenaHalf - safeHalf` (ce que la marée a déjà mangé). Sans elle, le bord était mordu de 72 px **dès
la première seconde d'overtime**, en pleine minute de grâce, et la règle rendait des dégâts **hors
overtime**. Attrapé par un test **déjà écrit** (`Hors_Overtime_La_Maree_Ne_Ronge_Rien`).
⚠⚠ **Descendre de la logique dans un shader COÛTE de la couverture, et rien ne le signale** : les
13 tests des vagues sont partis avec leur code, et **aucun test ne peut suivre du HLSL** — alors que
le sens de déplacement des vagues est **invisible à la capture d'écran**. Ce qui pouvait rester en C#
y est resté (la **phase**, qui doit s'accumuler là où un shader n'a pas d'état).
⚠ **Reste à faire** : la marée **n'a pas de son**, l'amortissement à 0,50 repose sur un compte de
kills d'overtime **non mesuré**, l'interaction avec le cran III (overtime dès la 8ᵉ min) n'a jamais
été jouée — et **le banc rend des chiffres FAUX sur l'overtime sans le dire** (`BenchAutoPilot` ignore
la marée : toute lecture de banc portant sur l'overtime est non valide tant qu'`AutoPilotPolicy`
n'aura pas appris à rejoindre le terrain sûr).

**2026-08-20 — LA MARÉE DE ROUILLE : l'overtime a une fin.** Demandé en jouant (« trop facile,
l'overtime ne doit pas durer indéfiniment, le joueur doit mourir à tous les coups ; le challenge c'est
de tenir le plus longtemps possible »). L'arène **se referme** en overtime : plus aucun terrain sûr à
**11 min** (`Rules/RustTide` + `Gameplay/RustTideZone`). **776 tests, banc 276/276.** Design → `docs/GDD.md` §38 ;
pièges → `docs/PITFALLS_UNITY.md` §Fin de partie. **Joué et validé le 2026-08-22 ; l'ÉQUILIBRAGE, lui, reste non mesuré — voir §38.6.**
⚠ **Le diagnostic n'était pas « la pente est trop douce ».** Les i-frames (0,45 s) bornent les dégâts
entrants à **2,2 coups/s, que 5 ennemis touchent le joueur ou 300** : la densité étant saturée dès la
8ᵉ minute, l'overtime n'avait plus qu'**une seule variable**, la valeur d'un coup — face à **trois**
croissances joueur sans plafond. **Ajouter des ennemis n'ajoutait pas de danger**, et le §31 a réglé
la pente **trois fois** (1,5 → 3 → 2,25) sans jamais toucher ce fond.
▶ **Une fin garantie se construit par une SOUSTRACTION, pas par une course.** Tant que la fin dépend
d'une croissance qui en dépasse une autre, elle dépend d'un réglage que le prochain build déplacera.
L'espace, lui, est fini. Trois propriétés portent tout : la marée **n'est pas un mur** (on la
traverse, sinon chaque coin devient un piège) · elle ronge **en continu donc hors i-frames** (c'est
ce qui contourne le plafond des 2,2 coups/s → `Player.TakeContinuousDamage`, **pas** un drapeau sur
`TakeDamage`) · elle se compte en **fraction des PV max**, donc le Blindage ne peut pas la distancer.
⚠⚠ **Le premier jet ne garantissait rien : un rectangle qui se ferme dégénère en un POINT**, et ce
point — le centre exact — restait sûr indéfiniment. La garantie tombait à l'instant même où elle
devait se refermer. **Une élégance qu'on s'impose (« une seule variable ») peut créer le seul cas que
la règle existe pour couvrir.** Le test qui l'attrape **balaie l'arène** au lieu de vérifier une
valeur — un test écrit sur la formule serait passé.
⚠ **Tenir n'était le but de personne** : `overtimeBonusCap` payait le joueur pour **arrêter** (et à
0,15 d'amortissement il n'était même pas atteint — ~50 Échos pour 11 min de survie), et
**`RUNEND_BEST` attendait dans `ui.csv`, traduite en trois langues, sans un seul appelant** — onzième
« déclaré non consommé ». Corrigé : amortissement 0,15 → **0,50**, plafond 100 → **600** (sûr, la
durée étant désormais bornée), record affiché à l'écran de fin, et rangé **par biome ET par cran**
(`SettingsData.SurvivalRecords` ; `HighScores` laissée **intacte**, en changer la clé aurait effacé
les records des joueurs).
⚠ **Le record se lit AVANT `ReportRun`**, qui l'écrase avec le temps de la run — il est donc **passé
en paramètre** à `RunEndScreen.Show`. Un écran qui le relirait comparerait la run à elle-même et
« record battu » ne s'afficherait **jamais**, sans qu'aucune erreur ne le dise.
⚠⚠ **Le banc d'équilibrage rend maintenant des chiffres FAUX sur l'overtime, sans le dire.**
`BenchAutoPilot` ne sait qu'esquiver la foule : il **ignore la marée**, ne revient pas au centre, et
ses runs d'overtime raccourcissent donc **pour une raison qui n'est pas celle qu'on mesure**. Toute
lecture de banc portant sur l'overtime est **non valide** tant que `AutoPilotPolicy` n'aura pas appris
à rejoindre le terrain sûr. ⚠ Et le banc de câblage `RunSmokeTest` **n'assemble
aucun décor** : il n'instancie jamais d'`ArenaRenderer`, donc l'**accroche** de la zone n'y est pas
couverte — il pose la marée à la main. Si le point d'accroche disparaissait, le chantier deviendrait
un fichier mort et **le banc resterait vert**. Le seul contrôle est de lancer le jeu.
⚠ **Reste à faire** : la marée **n'a pas de son** (aucun SFX existant ne dit « une menace lente
arrive » sans mentir), l'amortissement à 0,50 repose sur un compte de kills d'overtime **non mesuré**,
et l'interaction avec le cran III (overtime dès la 8ᵉ min) n'a jamais été jouée.

**2026-08-14 — TACTILE : la version web se joue au doigt. PUBLIÉ en 2.3.1** (build itch #1883172,
canal `html5`, devlog collé ; Windows reste en 2.2.0 — le tactile ne le concerne pas). Schéma retenu (décision de l'auteur) :
**joystick flottant à gauche + visée automatique**, bouton d'esquive et bouton de pause à droite,
**paysage forcé** (`OrientationGate`). Le tactile est le **troisième** fichier d'entrées
(`Platform/TouchInput.cs`), à part parce qu'il a une **mémoire** — un stick flottant n'existe que par
l'endroit où le doigt s'est posé, là où clavier et manette se lisent sans état. Géométrie pure et
testée : `Rules/VirtualStick` + `Rules/TouchZones`. **759 tests.** Détail →
`docs/PITFALLS_UNITY.md` §Tactile.
⚠ **La moitié du portage tactile n'est PAS dans Unity.** Le gabarit web par défaut donne un jeu qui
démarre sur téléphone et ne s'y joue pas : le double-appui **zoome**, le glissement **fait défiler**,
le geste depuis le bord **revient en arrière**, l'appui long ouvre un menu système, et la barre d'URL
**recouvre le bouton d'esquive**. Rien de tout cela n'appartient au moteur, rien ne lève d'erreur →
`Assets/WebGLTemplates/ChimeraMobile/index.html`, posé par le build.
⚠ **Quatre défauts qui ne se voient pas au code** : `Touchscreen.current != null` **ne dit pas** que
le joueur se sert de ses doigts (un portable tactile désarmerait la Lance Vectorielle) ·
`EventSystem.pixelDragThreshold` vaut 10 px, calibré pour une souris — au doigt **les boutons ne
reçoivent jamais leur clic**, le menu paraît mort · **Échap n'existe pas** sur mobile, donc une run
n'était ni interruptible ni abandonnable · et l'**intro n'était pas passable au doigt** alors que son
invite disait « Touchez l'écran pour passer » (signalé en jouant — *un texte changé n'est pas une
action câblée*, la leçon du chantier reproduite dans le chantier lui-même).
▶ **`--touch` (`?touch` en web) force le mode tactile et simule la souris en doigt** — sans lui, il
n'y a rien à regarder sur la machine où l'on développe, et une interface qu'on ne peut pas afficher
est une interface qu'on juge sur son code.
⚠⚠ **Le navigateur MÉLANGE deux builds, et le symptôme est un crash illisible.** Les fichiers de
sortie portent toujours le même nom : le cache HTTP peut associer le `.data` d'un build au `.wasm`
d'un autre → `RuntimeError: memory access out of bounds` + 300 offsets wasm, **au démarrage**, sans un
nom de méthode. Une heure perdue à le chercher dans le code. ▶ **Un message d'erreur qui ne bouge pas
alors que le binaire a changé ne vient pas du binaire qu'on croit exécuter** ; le test qui tranche en
30 s est de **servir sur un autre port** (origine neuve = cache vierge). Corrigé par un jeton
`__BUILD_ID__` que le build remplace (SHA **+ horodatage**) et pose en paramètre d'URL — **plus**
`Cache-Control: no-store` sur la page hôte, sans quoi le garde-cache s'auto-annule : *un mécanisme
d'invalidation transporté par une ressource cachable ne s'applique jamais.*
⚠ Et `ExplicitlyThrownExceptionsOnly` **désactive les vérifications de bornes et de nullité** : le
réglage censé rendre les défauts instruisibles rend les plus graves illisibles. Pour instruire :
passer à `FullWithStacktrace`, rebuilder, revenir.
⚠ **Trois rappels de touche devenus des mensonges au doigt** : « MAJ — esquive » (HUD),
« Reprendre **[Échap]** » (pause) et « **Appuyez sur une touche** pour passer » (intro — la première
phrase que lit un joueur). La règle « une capacité annonce sa touche » dit en fait **annonce comment
on la déclenche** ; sans clavier, la réponse est le bouton. ▶ Contrôle : `grep` dans `ui.csv` sur les
crochets **et** sur le mot « touche ». Un texte peut être **correct et faux** — l'audit de
localisation le déclare parfait.
⚠ **Puis l'écran de pause enfermait le joueur** : son panneau débordait, « Reprendre » et
« Abandonner » **hors de l'écran** — et la pause n'étant pas dans `ModalQueue`, le joystick restait
actif par-dessus. **Une condition « une modale est-elle ouverte ? » doit énumérer ce qui met le jeu
en attente, pas ce qui est inscrit dans un registre.**
⚠⚠ **Le bouton de pause était parfaitement placé et ne répondait pas** — deux façons d'avaler un
appui, indépendantes : filtrer sur `isPressed` **avant** `wasPressedThisFrame` avale le tapotement
(down et up dans la même image) ; et publier un appui comme « cette image-ci » le perd, **l'ordre des
`Update` entre objets n'étant pas garanti** — le `RunHud` lisait une image trop tôt. Un événement
d'entrée n'a pas la nature d'un état : il **survit** deux images et se **consomme** à la lecture.
⚠⚠ **Grossir l'interface sur petite dalle : l'idée paraissait juste et ne l'était pas.** Un bouton
de menu ne fait que 22 pixels **logiques** sur un téléphone en paysage — j'en ai conclu « 4 mm » et
rétréci la maquette. **Essayé sur un Pixel 9 : textes hors de leurs cadres, menu énorme.** Mécanisme
**retiré** le 2026-08-14 sur décision de l'auteur ; la maquette reste 1920 × 1080 partout.
**Un pixel logique de téléphone n'est pas un pixel d'écran de bureau** — le calcul était cohérent,
l'unité ne l'était pas, et aucun test ne pouvait le dire puisque le nombre était juste. ▶ Une
conclusion tirée d'un calcul d'unités se vérifie **sur l'appareil visé**.
⚠ Le détour a exhumé **quatre défauts de mise en page bien réels** (textes chevauchés, deux boutons
du Hub empilés dont un destructeur, colonne du menu sur le logo, « Reprendre » hors écran). Ils ne se
déclenchent plus, mais **ils sont toujours là** : toute mise en page à dimensions absolues les
rejouera au prochain canevas étroit.

**2026-08-13 (4) — PORTAGE WEB (WebGL) : le jeu tourne dans un navigateur.** Build
`BuildBench.WebGame` → `unity/Build/web/` (**35,4 Mo** : 26,9 de données + 8,2 de wasm) ; publication
`tools/release_unity.ps1 -Target web`, canal itch **`html5`** (⚠ c'est le NOM du canal qui décide si
le jeu se lance dans la page ou se télécharge). Vérifié dans Chrome : intro, menu, run sur le biome
Néon, textes traduits. **684 tests** (772 depuis le tactile). Détail des six blocages → `docs/PITFALLS_UNITY.md` §Web.
⚠ **Aucun des six ne lève d'erreur au build** — `streamingAssetsPath` est une **URL** (→ scène `Boot`
en tête de `GameScenes.All` + manifeste écrit par le build) · `persistentDataPath` **s'écrit en
mémoire** et l'onglet emporte tout sans `FS.syncfs` · une DLL au `.meta` minimal part sur **toutes**
les plateformes · `GetCommandLineArgs` n'existe pas (→ `LaunchArgs` lit la **query string** :
`?biome=neon&invuln` vaut les drapeaux, tous vérifiés) · WebGL a le **stripping le plus agressif**,
fatal à la seule sauvegarde (→ `link.xml`) · `Streaming` audio n'existe pas.
⚠⚠ **Le défaut du premier essai navigateur : un invariant qu'un tiers pouvait annuler.** Le
préchargement était porté par la coroutine de `BootScreen` ; `--auto-play` change de scène à la
première image et **tuait la coroutine à mi-chemin, sans erreur** → tout le texte du jeu **en clés**
(`HUD_LEVEL` en plein HUD). Invisible sur Windows, systématique en web. Corrigé au niveau de la
classe : chargement lancé en `BeforeSceneLoad` sur un objet `DontDestroyOnLoad`, `BootScreen`
**attend** au lieu de porter, et tout pilote consulte `StreamingText.Preloaded`.
✅ **Cadence mesurée : 60,0 images/s, tenues.** ~190 s de relevé continu (~11 400 images) à
**200 ennemis** — le plafond de foule — arsenal saturé, cran 3, en overtime et pendant un boss. Pire
image **18 ms**, **0 %** sous 30. Le jeu est plafonné par la synchronisation verticale du navigateur,
pas par la machine : *le risque de performance du portage web n'existe pas.*
⚠⚠ **La première mesure annonçait 1,0 image/s** — Chrome bride les onglets en arrière-plan à 1 Hz.
Elle ne bougeait pas quand la population passait de 10 à 125 ennemis : **une mesure de performance
insensible à la charge ne mesure pas la performance.** L'onglet doit être au premier plan, et
l'instrument doit être *dans le jeu* (`?show-fps` → `FpsTelemetry` + `Rules/FrameStats`, trois
chiffres : moyenne, **pire image**, part sous 30) — rien ne peut le mesurer de l'extérieur, aucune
injection de script n'aboutissant tant que le canevas tourne.


**Migration Unity terminée. 2.1.0 publiée le 2026-08-13** (2.0.0 le 08-10, 2.0.1 et 2.0.2 le 08-11) —
build itch **#1880415**. Le jeu est jouable de bout en bout, avec son, validé en jouant.
**673 tests.** ▶ Toujours essayer `tools/release_unity.ps1 -DryRun` avant de publier pour de bon.
⚠ **Publiée sans mesure au banc du renforcement de 4 armes** (§36) — décision de l'auteur. La zone du
Champ de Surcharge et celle de la Singularité ne sont bornées par **aucun plafond** : premier endroit
à regarder si la fin de partie paraît plus facile.

**2.0.1 — la leçon du portage a frappé une dixième fois, sur le TEXTE.** Tout le contenu nommé
(armes, greffes, améliorations du Hub, ennemis) sortait **en français dans les trois langues** :
il venait des JSON, et `ui.csv` portait **109 clés traduites que rien ne lisait**. Corrigé par
`Platform/ContentText.cs` + `tools/audit_loc_keys.py`. Trouvé **en regardant les rushes du
trailer** — le pipeline vidéo a été reporté sous Unity dans la foulée (`Bench/TrailerRecorder.cs`,
`tools/record_trailer.py`, chaque plan mis en scène, timecodes stables d'une recapture à l'autre).

**2026-08-12 — quatre défauts signalés en jouant, corrigés — publiés en 2.1.0.** (1) La **Lame Boomerang**
mettait 6 s à revenir face à un joueur rapide, et son arme cessait donc de tirer : la parade du
2026-08-11 (`PickupMagnet`) avait été appliquée au *site trouvé*, pas à la *classe de défauts* →
`BoomerangReturn`. (2) L'**Aimant n'existait pas** sous Unity, alors que `bonus_magnet` restait
achetable à 770 Échos — porté avec `MagnetSchedule` / `MagnetPickup` / `MagnetSpawner` /
`MagnetSprite`. (3) Les **Noyaux d'Aether s'aimantent** (renversement de design assumé) et
`core_magnetism` a été redirigé vers le rayon d'aimantation, sans quoi il devenait payant et sans
effet. (4) Le **menu de montée de niveau proposait 1 ou 2 cartes** en fin de run — un test
verrouillait même ce comportement. **673 tests, banc 271/0.**

**2026-08-13 — la rareté et la fusion se SENTENT enfin, publié en 2.1.0** (détail : `docs/GDD.md` §35).
Demandé en jouant. Toute la hiérarchie du jeu reposait sur des signaux **immobiles** : un cadre et un
mot, dans un écran qui met le jeu en pause au milieu d'une nuée. Ajouté : aura respirante par rareté
(`UiRarityFlare`), arrivée en cascade avec dépassement pour l'épique, étiquette colorée — et pour la
fusion, `FusionFanfare` (trois ondes + ralenti court), `FusionBanner` (titre/icône/nom, sans modale)
et une ligne d'arsenal dorée. **La commune ne reçoit rien : c'est l'écart qui informe.**
⚠ **Deux défauts trouvés à la CAPTURE, invisibles au code** : l'aura employait le dégradé *radial*
des effets, dont il ne reste rien à 85 % du rayon — elle ne brillait que sous la carte qui la cache
(→ halo 9-slice `UiPrimitives.GlowBox`) ; et la capture censée juger la hiérarchie photographiait la
main *précédente*, `Present` ne rouvrant pas une modale déjà ouverte. **Un outil de contrôle qui se
trompe de sujet valide ce qu'il n'a pas regardé.** **673 tests, banc 273/0.**
⚠ Un run de banc a rendu **9 échecs sur 261, tous faux** : le joueur y est mort tôt et quatre blocs se
sont sabordés en cascade. Le run suivant, **même binaire**, est passé à 273/0 — la survie du joueur au
banc n'est pas déterministe. **Rejouer avant de conclure.**

**2026-08-13 (2) — « l'overload field est trop discret », publié en 2.1.0** (détail : `docs/GDD.md` §36).
Deux causes : l'arme **ne grandissait pas** (`radius` 100→200 et `knockbackPx` 40→60 déclarés,
**lus par personne**) et n'existait à l'écran que 9 % du temps. Le croisement déclaré/consommé a sorti
**4 autres clés mortes** (Singularité, Lance Cryo, Flux de Braise) — toutes branchées. ⚠ **C'est un
renforcement de 4 armes** : 2 restent bornées par les plafonds existants, la zone du Champ et celle du
puits **restent à mesurer au banc**. Ajouté aussi : `FusionMark`, signature **dorée** au tir de toute
arme fusionnée (appelée depuis `WeaponBase`, au point du son de tir).
⚠ **Le banc est tombé à 9/263, et la RÉFÉRENCE reproduit les neuf lignes à l'identique** (`git stash`
+ rebuild de `HEAD`) — donc **rien à voir avec ces changements**. Il passait à 273/0 le matin même.
Seul changement entre les deux : **`settings.json` réécrit à 17:19 par la tournée de captures**
(`completions.sanctuaire = 1`, records). Le banc lit cet état → **l'outil de captures calibre le
banc**, ce que le piège « un outil ne laisse pas sa mise en scène dans la sauvegarde » interdit.
**À instruire séparément.**
⚠⚠ **`audit_json_keys.py` a raté ce défaut DEUX FOIS** — il existe pourtant pour ça. Comparer aux
littéraux *globaux* ne relie une clé ni à son fichier ni à son consommateur (`"knockbackPx"` lu pour
une **greffe** couvrait celui des **armes**) ; puis les `Shape()` globaux laissaient le `radius` de la
Singularité couvrir celui du Champ. Contrôle désormais **arme par arme**. **Un audit qui rate ce
qu'il vise est pire qu'absent : il rend un verdict rassurant. Tout durcissement se valide en
réintroduisant le défaut.**

**2026-08-13 (3) — entrées portées sur le paquet Input System, publié en 2.1.1** (build itch
#1880556 ; détail : `docs/PITFALLS_UNITY.md` §Entrées). L'Input Manager est marqué pour dépréciation :
`com.unity.inputsystem` 1.20.0, `activeInputHandler` à **1**, les trois `EventSystem` sur
`InputSystemUIInputModule`. Tout ce qui touche un périphérique tient désormais dans **deux
fichiers** — `Platform/InputRemap.cs` et `Platform/RawInput.cs` — au lieu de ~20 appels dispersés.
⚠ **Trois des quatre pièges du domaine ne lèvent rien** : un module d'entrée sans
`AssignDefaultActions()` est *inerte sans erreur* ; `Keyboard.current` est **nul** sans clavier ; et
un paquet installé reste invisible tant que l'`.asmdef` ne le référence pas.
⚠⚠ **Onzième « déclaré non consommé », trouvé en migrant : la visée était morte.**
`Player.UpdateAim()` lisait deux axes — `RightStickX`/`RightStickY` — **jamais déclarés** dans
`InputManager.asset` ; l'exception levée chaque frame **sautait la fin de la méthode**, donc la visée
souris et le réticule. ▶ **À valider en jouant : la Lance Vectorielle se vise-t-elle ?**
**673 tests, banc 273/0** (après 9/263 puis 1/273 sur le **même binaire** — instabilité connue).

**Le dépôt est mono-moteur depuis le 2026-08-10.** Ce qui a changé :
- `src/`, `scenes/`, `project.godot`, le `.csproj`/`.sln` Godot, `assets/`, `data/` et
  `localization/` racine ont été **supprimés** — tout vit sous `unity/Assets/`.
- **Source unique** pour les données (`StreamingAssets/data`, `.../localization/ui.csv`) et pour les
  assets : plus de copie racine, donc plus de dérive possible.
- Les générateurs Python écrivent **directement là où le jeu lit**, via **`tools/unity_paths.py`**
  (table de destination) et **`tools/spriteframes.py`** (manifestes d'animation, ex-`.tres`).
- Doc de l'ère Godot conservée sous **`docs/archive-godot/`** (fond valable, chemins périmés).

⚠ **La leçon du portage, toujours en vigueur : déclaré n'est pas consommé.** Dix fois une donnée,
une règle ou un système entier existait, était testé, et n'était appelé par rien — trouvé en jouant,
jamais par l'automatisation. Trois outils sont nés de là : `tools/audit_json_keys.py`,
`tools/audit_unused_members.py` et **`tools/audit_loc_keys.py`** (tout le contenu nommé était affiché
**en français dans les trois langues**, alors que 109 clés traduites dormaient dans `ui.csv`).
**Les lancer après tout ajout de données, de règle ou de texte affiché.**
⚠ **Aucun des trois n'aurait attrapé la dixième** (l'Aimant, 2026-08-12) : le système entier était
*absent*, pas déclaré-non-lu — rien à comparer. Le seul signal était côté **joueur**, qui pouvait
acheter `bonus_magnet` (770 Échos) pour étendre un objet inexistant. **Deuxième fois qu'une
amélioration du Hub se paie sur du vide** (cf. le cran IV de saturation). Contrôle à faire à la main :
toute entrée de `meta_upgrades.json` doit pointer un système qui **existe** côté Unity.

## Équipe d'agents

Agents dans `.claude/agents/` : `game-designer`, `directeur-artistique`, `graphiste`, `developpeur`,
`musicien`, `story-teller`, `marketing`, `game-tester`, `release-manager`. Déléguer proactivement à
l'agent compétent — **qui fait quoi et dans quel ordre : `GUIDE-CLAUDE-CODE.md`**.
⚠ **Un agent qui décrit un état périmé du projet donne des instructions fausses avec autorité** :
quand une phase se termine, relire les agents qu'elle concerne (dernière passe : 2026-08-10).

- **`game-tester`** : construit et lance le binaire Unity, joue le jeu, documente les bugs dans `docs/TEST_REPORT.md`. À invoquer après chaque implémentation majeure.
- **`release-manager`** : publie la release binaire de bout en bout (semver, release notes, `tools/release_unity.ps1`, MAJ doc) puis **rédige le devlog** — l'utilisateur le publie lui-même sur itch (l'agent ne pilote pas le navigateur). Source des notes : `docs/DEVLOG.md`.

## Maintenance de la doc

- `README.md` (racine) — MAJ à chaque changement de phase / ajout majeur.
- `docs/PROJECT_STATE.md` + `docs/GDD.md` + `/carte-projet` + `docs/PITFALLS_UNITY.md` — MAJ dans le commit qui change ce qu'ils décrivent.

## Conventions

- Plateforme cible : Windows (.exe). Moteur : **Unity 6.5** (`6000.5.6f1`), C#, URP 2D.
- **Build** : `Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBench.Windows64Game`
  → `unity/Build/game/ChimeraProtocol.exe` (ignoré par git, régénéré).
  **Web** : `… -buildTarget WebGL -executeMethod BuildBench.WebGame` → `unity/Build/web/`.
  ⚠ Le **premier** build d'une plateforme réimporte tous les assets (~20 min) ; les suivants ~3 min.
  ⚠ Lancer Unity par `&` en PowerShell rend la main **immédiatement sans rien faire** :
  `Start-Process -Wait`.
- **Publication (itch.io + Butler)** : skill **`/publier-itch`** ou `tools/release_unity.ps1 -Version X.Y.Z`
  (essayer d'abord avec `-DryRun`), ou déléguer à l'agent **`release-manager`**. Le script pose lui-même
  `bundleVersion` — ne pas l'éditer à la main. Runbook : `docs/RELEASE.md` ; notes cumulées : `docs/DEVLOG.md`.
- Style de code : PascalCase classes/méthodes, `_camelCase` champs privés, `readonly` par défaut.
- **Logique pure testable** : `unity/Assets/Scripts/Shared/Rules/` — classes statiques **sans
  dépendance moteur** (`XpCurve`, `EnemyScaling`, `SaturationTable`…). Les `MonoBehaviour` y délèguent.
  `Shared/PlatformCore/` porte le socle déterministe (`Pcg32`, `TimerWheel`, `Easing`).
- **Tests unitaires** : xUnit, `dotnet test tests/ChimeraProtocol.Tests.csproj` — **776 tests**.
  Ils compilent `Shared/` **par chemin** : aucun moteur, aucun build requis.
- ⚠ **`Art/` ≠ `Resources/`** : `Art/` est consommé par **GUID** (planches d'animation), `Resources/`
  **par chemin** (`Resources.Load`) et embarqué en entier dans le binaire. Se tromper de dossier ne
  lève rien — le jeu affiche l'ancienne image. La table qui fait autorité : `tools/unity_paths.py`.
- **Difficulté** : trois axes multiplicatifs — réglage du joueur (`DifficultyTuning`), **palier de
  menace du niveau joué** (`LevelThreat`, cf. `docs/GDD.md` §28) et escalade d'overtime — plus
  l'**échelle de saturation** (`SaturationTable`, §34), un cran = une règle nommée.
- Sauvegarde : `%USERPROFILE%\AppData\LocalLow\drangoht\Chimera Protocol\` (méta/Échos + préférences,
  records, complétions, découvertes). Les sauvegardes Godot des joueurs sont migrées (`GodotConfig`,
  `SaveMigration`) — **c'est le seul endroit du portage dont l'échec est irréversible pour le joueur**.
- Sprites : PNG transparent, grille 32×32 px (Colosse 48×48, mid-boss 72, boss 154). Import en
  `Point`, `spritePixelsPerUnit = 1` (1 px = 1 unité : toutes les valeurs du jeu se transposent telles
  quelles). Style **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) via `tools/pseudo3d_lib.py`
  — toujours dériver shadow/highlight avec `shade()`/`shade_sprite()`/`shade_tile()`/`shade_icon()`.
- **Audio** : musique **générée sur Suno** (prompts : `docs/AUDIO_AI_PROMPTS.md`) — jamais éditer un
  `.ogg` à la main. Pour en remplacer une : regénérer, déposer dans `music_ai/`, puis
  `python tools/import_ai_music.py [--only <id>]`. Bande-son de secours synthétisée (sans contrainte
  de licence) : `tools/generate_music_v3.py`. SFX = WAV Kenney CC0. Crédits et **licence Suno (plan
  gratuit, non commercial)** : `docs/AUDIO_CREDITS.md`.
  ⚠ Une arme absente de la table **`WeaponSfx`** est **muette** — 14 l'ont été sans que rien ne le dise.
- Localisation EN/FR/ES : `unity/Assets/StreamingAssets/localization/ui.csv` → `Loc.T("CLÉ")`
  (lu tel quel, aucune étape d'import). Le **contenu nommé** (armes, greffes, améliorations, ennemis)
  passe par `Platform/ContentText.cs`, qui déduit la clé de l'identifiant (`tesla_coil` →
  `WPN_TESLA_COIL_NAME`) et **replie sur le texte français du JSON** si elle manque.
  ⚠ Ce repli est silencieux : le contrôle est `tools/audit_loc_keys.py`, à lancer après tout ajout
  de contenu. Il vérifie les deux sens — clé absente **et** clé orpheline.
- Performance cible : 200–300 entités simultanées ; I-frames joueur 0,45 s (CRITIQUE pour les nuées).
- Palette UI : fond `#1A1A2E`, cyan `#44FFEE`, violet `#AA44FF`, or `#FFCC44`, blanc cassé `#D9D9F2`.
  Police : Share Tech Mono ; VT323 en réserve.
- **Cadres & couleurs d'UI** : toujours via `Scripts/UI/UiPalette.cs` (couleurs) et `UiStyle.cs`
  (cadres « plaque blindée »). Jamais de couleur en dur. Parti pris → `docs/ART_BRIEF_UI_FRAMES.md` ;
  assets → `tools/generate_ui_frames.py`.
- Python : `C:\Users\drang\AppData\Local\Programs\Python\Python313\python.exe` (pas dans le PATH).

## Pièges critiques → `docs/PITFALLS_UNITY.md`

Tous les pièges non évidents (import d'assets et `.meta`, `Resources` vs `Art`, cycle de vie des
scènes, navigation clavier/manette, calques d'UI, VFX, tests headless, export .NET, tampon de build)
sont dans **`docs/PITFALLS_UNITY.md`**. **Le consulter avant de coder dans le domaine concerné.**
Les pièges de **conception** hérités de Godot (câblage d'une arme, d'un ennemi, d'un affixe ; mixage ;
lisibilité) restent valables dans `docs/archive-godot/PITFALLS.md` — leurs chemins, non.
