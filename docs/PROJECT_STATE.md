# État du projet — Chimera Protocol

> Détail chargé **à la demande** (pointé depuis `CLAUDE.md`). Liste évolutive de ce qui est
> implémenté — la mettre à jour à chaque ajout/refonte majeur. Le résumé de phase reste dans
> `CLAUDE.md` ; le design complet dans `docs/GDD.md` ; la carte du code dans `/carte-projet`.

- Pile technique : **Unity 6.5 (C#, URP 2D)** depuis la 2.0.0. **Versions en ligne :
  web 2.3.1** (2026-08-14, build itch #1883172, canal `html5`) et **Windows 2.2.0** (build #1881127).
  ⚠ Les deux canaux divergent volontairement : le portage tactile ne concerne que le web, et le
  manifeste `version.json` — que lit le bandeau de nouvelle version — décrit la version
  **téléchargeable**. Une release web ne le touche pas, sinon elle annoncerait aux joueurs Windows
  une mise à jour qui n'existe pas.
- **Le jeu se joue au DOIGT (2026-08-14).** Schéma retenu par l'auteur : **joystick flottant à
  gauche + visée automatique**, esquive et pause en boutons à droite, **paysage forcé**. Détail des
  pièges : **`docs/PITFALLS_UNITY.md` §Tactile**.
  - **Trois fichiers** touchent maintenant un périphérique : `InputRemap`, `RawInput`, et
    **`TouchInput`** — à part parce qu'un joystick flottant a une **mémoire** (l'endroit où le doigt
    s'est posé) là où clavier et manette se lisent sans état. Sa géométrie est pure et testée :
    `Rules/VirtualStick` (dosage, zone morte, **recentrage**) et `Rules/TouchZones` (zones, boutons,
    portrait, grossissement d'interface). **772 tests** (684 → 772).
  - **`UI/TouchHud`** dessine les contrôles *et* ouvre leur capture (`TouchInput.SetGameControls`) :
    celui qui montre est celui qui écoute, ils ne peuvent pas diverger. Fermé par défaut, refermé dès
    qu'une modale s'ouvre — sinon un stick poussé au moment du menu de niveau resterait poussé.
  - **`UI/OrientationGate`** refuse le portrait : l'arène est en 16/9 et la caméra montre une hauteur
    de monde fixe, donc en portrait les nuées arriveraient **hors champ**. Ce n'est pas un problème de
    mise en page, c'est le champ de vision.
  - **La moitié du travail est hors d'Unity** : `Assets/WebGLTemplates/ChimeraMobile/index.html`
    désarme le zoom au double-appui, le défilement, le geste de retour, l'appui long, et plafonne
    `devicePixelRatio` à 1 (un téléphone en annonce 3 → **neuf fois** trop de pixels rendus).
  - ⚠ **`EventSystem.pixelDragThreshold` (10 px) est calibré pour une souris.** Au doigt, le seuil
    est franchi presque à chaque appui : uGUI requalifie en glissement et **le bouton ne reçoit
    jamais son clic**. Porté à 24 px au premier contact tactile. Aucune erreur, menu qui paraît mort.
  - ⚠⚠ **Le grossissement d'interface a été essayé puis RETIRÉ** (2026-08-14, décision de l'auteur
    après essai sur Pixel 9) : rétrécir la maquette pour agrandir les cibles faisait **sortir les
    textes de leurs cadres** et rendait le menu principal énorme. Le raisonnement partait de « 22
    pixels logiques = 4 mm » — mais **un pixel logique de téléphone n'est pas un pixel d'écran de
    bureau**. Le calcul était cohérent, l'unité ne l'était pas, et aucun test ne pouvait le dire
    puisque le nombre était juste. La maquette reste 1920 × 1080 partout.
  - ⚠ **Sur mobile, Échap n'existe pas** : une run n'était ni interruptible ni abandonnable.
    `RawInput.PauseRequestedThisFrame()` réunit Échap et le bouton tactile.
  - ▶ **`--touch` / `?touch`** force le mode tactile et **simule la souris en doigt** (via
    `TouchSimulation` du paquet Input System, donc le vrai chemin de code). Sans lui, il n'y a rien à
    regarder sur une machine de bureau. Ne couvre pas le **multi-touch**.
  - ⚠⚠ **Le bouton de pause était parfaitement placé et ne répondait pas.** Deux façons d'avaler un
    appui : filtrer sur `isPressed` avant `wasPressedThisFrame` (avale le tapotement), et publier un
    appui comme « cette image-ci » (**l'ordre des `Update` entre objets n'est pas garanti**, le
    lecteur passe avant le pompage une fois sur deux). L'événement survit 2 images et se consomme.
  - ⚠⚠ **Le grossissement a cassé quatre choses, toutes trouvées SUR IMAGE** : textes **chevauchés**
    au choix du niveau · deux boutons du Hub **empilés**, dont un destructeur · le cadrage du menu
    calculé sur `1920/1080` en dur et sa colonne qui **recouvrait le logo** · le bouton de pause posé
    **exactement sur** le compteur de Noyaux. Ce dernier est le cas général : **deux repères qui ne se
    voient pas l'un l'autre** (pixels écran pour les contrôles tactiles, unités de maquette pour le
    HUD). Le **HUD refuse désormais le grossissement** (`enlargeForTouch: false`) : il n'a aucune
    cible tactile et se superpose à l'arène.
  - ⚠⚠ **Le navigateur mélangeait deux builds** (mêmes noms de fichiers, cache HTTP) → crash wasm
    illisible au démarrage. Corrigé par `BuildBench.StampWebCacheBuster` (jeton `__BUILD_ID__` =
    SHA + horodatage, posé en paramètre d'URL sur loader/framework/code/données).
  - ⚠ **Trois rappels de touche mensongers au doigt** : esquive (HUD), « Reprendre [Échap] »
    (pause) et « Appuyez sur une touche pour passer » (intro). Contrôle : `grep` dans `ui.csv` sur les
    crochets **et** sur le mot « touche » — un texte peut être **correct et faux**.
  - ⚠ **L'écran de pause enfermait le joueur** : son panneau (760 × 700) débordait d'un canevas de
    téléphone (954 × 537), « Reprendre » et « Abandonner » **hors de l'écran**. Et la pause n'étant
    pas dans `ModalQueue`, les contrôles tactiles restaient actifs par-dessus — un pouce resté sur le
    joystick poussait pendant la pause. Corrigés tous les deux.
  - ▶ **Reste à juger en jouant sur un vrai téléphone** : la lisibilité des sprites (la caméra montre
    720 unités de haut sur 360 px de dalle, donc tout est rendu à moitié taille), la densité des
    cartes de montée de niveau une fois le panneau borné, et le confort du recentrage du stick.

- **Entrées portées sur le paquet Input System — publié en 2.1.1** (2026-08-13, build itch #1880556). L'Input Manager est
  marqué pour dépréciation par Unity ; `com.unity.inputsystem` 1.20.0 installé,
  `activeInputHandler` passé à **1** (Input System seul), ce qui éteint l'avertissement. Détail des
  pièges : **`docs/PITFALLS_UNITY.md` §Entrées**.
  - Toutes les lectures de périphérique tiennent désormais dans **deux fichiers** :
    `Platform/InputRemap.cs` (actions remappables) et `Platform/RawInput.cs` (Échap, touche
    quelconque, clic, curseur, stick droit). Avant, une vingtaine d'appels `Input.*` étaient
    dispersés dans 10 fichiers — rien n'était vérifiable d'un grep.
  - Les trois `EventSystem` passent de `StandaloneInputModule` à `InputSystemUIInputModule`, via une
    fabrique unique `BuildGameScene.NewEventSystem()` qui appelle **`AssignDefaultActions()`** —
    sans quoi le module est **inerte sans lever d'erreur**. Scènes régénérées.
  - **`DisplayName` s'améliore au passage** : le libellé d'une touche vient maintenant de la
    disposition installée (`InputControl.displayName`), donc « Z » sur un clavier AZERTY là où
    l'ancien `KeyCode.ToString()` affichait « W ». C'est ce que la règle « une capacité annonce sa
    touche » exigeait depuis le début.
  - ⚠ **Un défaut réel trouvé en migrant : la visée ne fonctionnait pas.** `Player.UpdateAim()`
    lisait `Input.GetAxisRaw("RightStickX"/"RightStickY")` — **deux axes jamais déclarés** dans
    `InputManager.asset`. L'`ArgumentException` levée à chaque frame interrompait la méthode :
    **la branche de visée à la souris et la pose du réticule n'étaient jamais atteintes**. La plainte
    correspondante côté joueur est « la Lance Vectorielle ne se vise pas » — exactement ce que le
    commentaire de la méthode prétendait avoir corrigé. **Onzième fois** qu'une règle écrite et
    commentée n'est atteinte par personne. ▶ **À valider en jouant** : viser à la souris avec la
    Lance Vectorielle, et vérifier que le réticule apparaît.
  - **673 tests, banc 273/0.** ⚠ Le banc a d'abord rendu **9/263**, puis **1/273**, puis **273/0**
    sur le **même binaire** — l'instabilité déjà relevée le 2026-08-13, sans rapport avec ce
    chantier. Le compte de vérifications qui *baisse* (263 < 273) signale la cascade, pas une
    régression.
- **Le Champ de Surcharge ne grandissait pas (2026-08-13, publié en 2.1.0).** Signalé en jouant (« trop
  discret »). Détail : **`docs/GDD.md` §36**.
  - `weapons.json` déclare `radius` **100 → 200 px** et `knockbackPx` **40 → 60** sur cinq paliers ;
    la classe n'implémentait pas `ApplyLevelStats`, donc **aucune des deux n'était lue**. Le
    croisement déclaré/consommé en a sorti **quatre autres** : `duration` + `radius` (Singularité),
    `slowMult` (Lance Cryo), `burnDps` (Flux de Braise). Toutes branchées.
  - ⚠ **C'est un renforcement de quatre armes, pas un correctif d'affichage.** Deux restent bornées
    par des plafonds en place (`CrowdControlCaps`, plafond de brûlure) ; la zone du Champ et celle du
    puits ne le sont pas — **à mesurer au banc de puissance** avant publication.
  - VFX : aura permanente qui se charge (le carré de l'avancement de recharge), arc électrique vers
    chaque cible (plafonné à 10, visant le point d'arrivée du recul), deux ondes dont la plus grande
    atteint exactement le rayon. Et une **signature dorée** commune à toute arme fusionnée
    (`FusionMark`, appelée depuis `WeaponBase` au même point que le son de tir).
  - ⚠ **Le banc est tombé à 9 échecs sur 263 — et la RÉFÉRENCE les reproduit à l'identique.**
    `git stash` + reconstruction de `HEAD` : mêmes neuf lignes, même ordre. Donc **rien à voir avec
    ces changements**, et rien à voir non plus avec le réglage de difficulté (rejoué à 0 : identique).
    Le banc passait pourtant à **273/0** en début de journée, sur le même dépôt. Ce qui a changé
    entre-temps est **`settings.json`, réécrit à 17:19 par la tournée de captures** — qui y a laissé
    `completions.sanctuaire = 1` et des records. Le banc lit cet état ; l'outil de captures est donc
    en train de **calibrer le banc**, exactement ce que le piège « un outil ne laisse pas sa mise en
    scène dans la sauvegarde du joueur » interdit. **À instruire séparément** : le symptôme est une
    mort prématurée du personnage, qui saborde en cascade quatre blocs de vérifications.
  - ⚠⚠ **`audit_json_keys.py` a été aveugle deux fois** sur ce défaut, qu'il existe pourtant pour
    attraper : comparer aux littéraux *globaux* ne relie une clé ni à son fichier ni à son
    consommateur (`"knockbackPx"` lu par `GraftManager` pour une **greffe** couvrait celle des
    **armes**) ; puis restreindre aux `Shape()` *globaux* laissait le `radius` de `Singularity`
    couvrir celui du Champ. Le contrôle est désormais **arme par arme**, héritage suivi, et vérifié
    en réintroduisant le défaut.
- **Mise en scène de la rareté et de la fusion (2026-08-13, publiée en 2.1.0).** Demandé en jouant :
  « les items Épiques et les évolutions d'armes devraient être plus juicy ». Toute la hiérarchie
  reposait sur des signaux **immobiles** — un cadre et un mot — dans un écran qui met le jeu en pause
  au milieu d'une nuée. Détail complet : **`docs/GDD.md` §35**.
  - **Cartes** : aura respirante à la couleur de rareté (`UiRarityFlare`), arrivée en cascade avec
    dépassement pour l'épique, étiquette colorée, second son quand la main contient un épique ou une
    fusion. **La commune ne reçoit rien** — c'est l'écart qui porte l'information.
  - **Fusion** : `FusionFanfare` (trois ondes, deux couronnes de rayons, ralenti court) +
    `FusionBanner` (titre, icône, nom — sans modale ni pause) + ligne d'arsenal **dorée** au HUD.
  - Une fusion monte à `legendary` **à l'affichage seulement** : la rareté que possède l'inventaire
    décide aussi du poids de tirage, et `RarityWeights` ne connaît que trois crans.
  - ⚠ **Deux défauts trouvés à la capture, invisibles au code** : l'aura employait le dégradé
    *radial* des effets et n'existait qu'au centre, sous la carte qui la cache (corrigé par un halo
    9-slice, `UiPrimitives.GlowBox`) ; et la capture censée juger la hiérarchie photographiait la main
    *précédente*, `Present` ne rouvrant pas une modale déjà ouverte. **673 tests, banc 273/0** (+2 : la
    fanfare émet réellement des effets, une fusion est reconnue comme telle), build sans erreur.
  - ⚠ **Un run de banc a rendu 9 échecs sur 261 — aucun n'était réel.** Le joueur y est mort tôt, et
    quatre blocs se sont sabordés en cascade (« pas de joueur vivant »), en emportant leurs
    vérifications. Le run suivant, sur le *même* binaire, est passé à 273/0. La survie du joueur au
    banc n'est pas déterministe : **rejouer avant de conclure**, ici comme le 2026-08-11.
- **Quatre défauts signalés en jouant, corrigés le 2026-08-12 (publiés en 2.1.0).** Aucun n'a été trouvé
  par l'automatisation ; les quatre l'ont été manette en main.
  1. **La Lame Boomerang mettait six secondes à revenir.** Sa vitesse était posée à 420 px/s, aller
     *et* retour, contre un joueur qui plafonne à 380 : **40 px/s de gain**. Comme la recharge de
     l'arme attend le retour, elle **cessait progressivement de tirer à mesure que le joueur achetait
     de la vitesse**. C'est mot pour mot le défaut des orbes d'XP corrigé la veille — la parade avait
     été appliquée au *site trouvé*, pas à la *classe de défauts*. Nouvelle règle pure
     `BoomerangReturn`, jumelle de `PickupMagnet` : retour ≤ 0,8 s à toute vitesse (contre 1,1 → 6,0).
  2. **L'Aimant n'existait pas sous Unity.** Ni l'objet, ni son spawner, ni son effet — alors que
     l'amélioration `bonus_magnet` restait achetable au Hub à **770 Échos cumulés** pour
     « +1 apparition par run ». **Quatrième occurrence de « déclaré n'est pas consommé »**, et la
     deuxième où le joueur *payait* pour du vide (cf. le cran IV de saturation). Porté avec sa règle
     pure (`MagnetSchedule` : 3 fenêtres + 1 par niveau, en overtime), sa silhouette dessinée à
     l'exécution (`MagnetSprite`, aucun PNG donc aucun maillon d'import) et son effet global.
  3. **Les Noyaux d'Aether s'aimantent** comme les orbes d'XP, sur demande du joueur. Renversement
     assumé d'un parti pris de design (le Noyau était le seul objet exigeant de traverser le danger) :
     en fin de partie il était perdu, pas arbitré. `core_magnetism` a été **redirigé** vers le rayon
     d'aimantation (100 → 150 px) au lieu du rayon de contact devenu sans objet — sans quoi
     l'amélioration devenait à son tour payante et sans effet. Le rôle « aller le chercher » passe à
     l'Aimant, désormais seul ramassage non magnétisé.
  4. **Le menu de montée de niveau proposait 1 ou 2 cartes en fin de run.** La bascule sur les cartes
     de surcharge ne répondait qu'au pool *totalement* vide, or un pool **s'assèche**. Un test
     verrouillait même le comportement (`Assert.Single`) : le défaut était documenté comme
     intentionnel. La complétion se fait désormais **carte par carte manquante**, et l'invariant testé
     est « une main est toujours pleine ».

  **673 tests** (+36), build Unity sans erreur, `RunSmokeTest` **271 OK / 0 échec** (+6 : la chaîne
  entière de l'Aimant, du calendrier à l'orbe qui bouge). Silhouette de l'Aimant **jugée sur image**.
- **Passe de code mort & refacto SOLID (2026-08-11, non publiée).** `tools/audit_unused_members.py`
  sortait **68 candidats** ; après vérification un par un, **40 étaient morts pour de bon**
  (supprimés) et les autres étaient l'inverse — des règles **testées que le moteur n'appelait pas**,
  parce qu'il en tenait une seconde copie. Recâblages : `StatCaps.EffectiveCooldown` (les deux bornes
  d'équilibrage étaient recopiées dans `WeaponBase`), `MusicIntensity.Select` (machine à états de la
  musique réécrite à la main dans `MusicDirector`), `EliteAffixTable.ShouldBeElite`/`Pick`,
  `RegenReserve.Suppress`/`TickSuppression`/`IsSuppressed` (le HUD et le joueur testaient le **même**
  état de deux façons), `EliteAffixTable.RegenDelaySeconds`, `EnemyBase.ResetStatusCounters`, et la
  convention de clés de traduction, qui vivait en double dans `LevelUpScreen` alors que
  `tools/audit_loc_keys.py` n'en contrôle qu'une. Duplications structurelles retirées :
  `UiStyle.ScreenCanvas` + `UiStyle.VerticalList` (≈40 lignes recopiées dans 5 écrans),
  `EnemyBase.Nearest` (4 copies mot pour mot), `EchoSettings` (jumeau de
  `MetaUpgradeTable.EchoParams`, supprimé). **626 tests verts, build Unity sans erreur.** Deux
  défauts trouvés au passage ont été corrigés dans la foulée, sur décision de l'auteur (voir
  l'entrée suivante).
- **Trois défauts d'usage corrigés (2026-08-11, non publiés).** Ils venaient tous de la passe
  ci-dessus, et aucun n'était une refacto — chacun déplace quelque chose dans le jeu :
  1. **Le Bonus de Surcharge n'existait pas.** L'appel de la formule d'Échos passait le temps
     survécu comme `capTimeSecs` : le temps n'était **jamais plafonné** et le bonus **toujours nul**.
     Une run d'une heure rapportait **452 Échos au lieu de 311**, sans borne — le farm que le
     GDD §9.2 avait été réécrit pour fermer. Les runs standard, elles, ne bougent pas d'un Écho. Les
     **sept scénarios de calibration du fichier de données sont devenus des tests**, et la ligne
     « Bonus de Surcharge » de l'écran de fin s'affiche enfin (sa clé attendait dans `ui.csv`, dans
     les trois langues). `capTimeSecs` suit la frontière **effective** : le cran « Compte à rebours »
     la ramène à 484 s.
  2. **31 descriptions d'ennemis** écrites et traduites, jamais montrées : le bestiaire les affiche,
     en troisième ligne sourde sous la fiche technique. La ligne du Codex s'ajuste désormais à son
     contenu au lieu d'une hauteur fixe — **vérifié sur capture**, pas sur le code.
  3. **Le tirage de spawn suit enfin la graine de la run.** `RunBootstrap` amorce `EnemySpawner` avec
     une valeur dérivée de `Gd` ; le flux reste **séparé** (le fondre dans le flux global ferait
     dépendre le spawn du décor, et **cassait 9 vérifications sur 255**). Relevé sur la trace du
     jeu : `--seed=1` rend deux fois la même graine de spawn, `--seed=2` une autre, et trois runs
     sans graine en donnent trois différentes.

  **637 tests** (+11), build Unity sans erreur, `RunSmokeTest` **265 OK / 0 échec**.
- **Localisation réparée — 2.0.1 (2026-08-11).** Tout le contenu nommé (12 armes, 9 fusions,
  8 greffes, 19 améliorations du Hub, 51 ennemis) s'affichait **en français dans les trois langues** :
  les écrans lisaient le champ `name` des JSON de `StreamingAssets/data`, écrits en français, pendant
  que `ui.csv` portait **109 clés traduites que rien ne lisait** (16 `GRAFT_`, 93 `ENEMY_`) et que
  `LEVELUP_TITLE`, `RUNEND_VICTORY`, `HUD_DASH_HINT`… dormaient à côté de leur équivalent **codé en
  dur en français**. Résolution centralisée dans **`Platform/ContentText.cs`** (clé déduite de
  l'identifiant, repli sur le JSON) ; contrôle permanent par **`tools/audit_loc_keys.py`**, qui
  vérifie **les deux sens** — clé absente *et* clé orpheline (ce second contrôle a immédiatement
  attrapé un préfixe `WEAPON_` créé en doublon de `WPN_`, déjà lu par les cartes de montée de niveau).
  `data/texts.json`, **troisième** source de texte branchée sur rien, supprimé ; sa matière narrative
  est dans `docs/NARRATIVE.md` §4 bis. ⚠ Trouvé **en regardant les rushes du trailer**, pas par un
  test — une mauvaise langue ne plante pas et paraît normale à qui l'a écrite.
- **Pipeline du trailer porté sous Unity (2026-08-11).** La capture vit dans le jeu
  (`Bench/TrailerRecorder.cs` + `tools/record_trailer.py`, 11 prises) : Unity n'a pas d'équivalent au
  Movie Maker de Godot dans un build. Chaque plan est **mis en scène**, donc les timecodes de
  `tools/build_trailer.py` survivent à une recapture — là où les runs aléatoires de Godot
  invalidaient tout le montage à chaque tournée. ⚠ Les rushes sont **muets** (l'horloge de capture
  et le mix audio ne cohabitent pas) : toute la bande-son du montage vient de `MUSIC_EDL`.
  `trailer/` est ignoré par git (~350 Mo, régénérable).
- **Dépôt devenu mono-moteur le 2026-08-10.** `src/`, `scenes/`, `project.godot`, `assets/`, `data/`
  et `localization/` racine ont été supprimés : les données et les assets n'existent plus qu'en un
  exemplaire, sous `unity/Assets/`. Les générateurs Python écrivent désormais **là où le jeu lit**
  (`tools/unity_paths.py`, `tools/spriteframes.py`) ; les scripts qui pilotaient l'éditeur Godot
  (captures, bancs, `release_itch.ps1`) ont été retirés. Doc de l'ère Godot →
  `docs/archive-godot/` (dont l'historique complet des chantiers 1.20 → 1.26).
  ⚠ **Les entrées ci-dessous antérieures à la 2.0.0 citent des chemins qui n'existent plus.** Leur
  contenu de jeu, lui, a été porté.
- **Saturation de Rouille — lot 1, l'échelle de challenge de fin de partie (2026-07-31, PUBLIÉ en
  1.25.0).** Plan : `docs/ENDGAME_PLAN.md` ; design : `docs/GDD.md` §34 ; logique pure :
  `SaturationTable`. **Cinq crans nommés et cumulatifs** — I Hémorragie (soins reçus −65 % **et le
  passage de niveau ne soigne plus** : le second levier pesait ~158 % des PV max rendus par minute
  d'overtime) · II Meute (statistiques : PV ×1,45, dégâts ×1,80, spawn ×1,40) · III Compte à rebours
  (overtime à la 8ᵉ min) · IV Sans filet (les trois filets méta **achetés** coupés, dont le
  Stabilisateur de Surcharge — ⚠ ce cran n'a plus de levier universel, cf. GDD §34.9) ·
  V Élite ordinaire (élites ×4, plafond relevé à 0,70).
  ⚠ **Valeurs relevées le 2026-08-02 (GDD §34.8), publiées en 1.26.0 le 2026-08-03** — les valeurs
  d'origine (−40 % · 1,30/1,35/1,25 · 10ᵉ min · ×3 et 0,55) sont celles auxquelles se rapportent les
  mesures citées plus bas, et non ce qui est en ligne.
  ⚠ **Publié sans avoir été joué** (décision de l'auteur) : à surveiller en priorité, le **cran III**
  (le boss arrive à la 8ᵉ minute face à un arsenal amputé, et le battre débloque le cran suivant —
  s'il devient inbattable, l'échelle se bloque) puis les **IPS au cran V** (0,70 d'élites). Conséquence assumée : l'égalité « cran II = ancien Difficile » est rompue.
  **Un cran = une règle lisible avant de lancer**, jamais un
  multiplicateur de plus : le principe est qu'on doit pouvoir dire *pourquoi* on est mort. Le cran se
  règle et se débloque **par niveau** (sélecteur sur la carte du biome, la liste défilant), rapporte
  **+20 % d'Échos** cumulatifs via une source unique (`TotalEchoMult` — `EchoFormula` et
  `RunEndScreen` doivent appliquer le même facteur), et **absorbe** l'ancien axe de difficulté :
  « Difficile » disparaît des options, « Facile » survit comme **assistance** hors échelle.
  `settings.cfg` passe en `save_version=2` (tables `biome:cran`, migrations v0→v1→v2 testées, plus
  une vérification de bout en bout sur une sauvegarde forgée « Difficile » avant publication).
  Nouveau flag **`--saturation=<n>`** (sans lui aucun cran n'est mesurable : le bot ne traverse pas
  l'écran de sélection ; ni le choix ni le déblocage ne sont persistés sous ce flag). **Mesuré au banc
  apparié** : temps soutenable **60,7 % → 39,9 %** cumulé, progressivité I **53,6 %** → II **50,0 %**
  → IV **−16 %** relatif ; le **cran III n'est mesurable par aucun banc** (il déplace le temps).
  ⚠ Aucun cran n'a encore été **joué** par un humain.
- **Mid-boss par biome — un rendez-vous de mi-run par niveau (2026-07-29, non publié).** Dernier point
  non livré de `docs/EXPANSION_PLAN.md` (B.3). **Trou constaté** : la faune par biome était complète
  (§21) mais les champions n'avaient jamais été répartis — `aether_revenant` (7 min) couvrait aether
  et néon, `rust_stalker` arrivait à **12 min** pour une run de 13, et `master_sentinel` à **16 min**,
  c'est-à-dire **jamais en run normale** (un mini-boss entier, avec sa scène, son sprite et son entrée
  de bestiaire, réservé de fait à l'overtime). **Trois niveaux sur cinq** n'avaient donc aucun
  rendez-vous entre la montée en puissance du début et le boss de fin. **Livré** : 3 mid-boss dédiés —
  **Colosse en Fusion** (Fournaise, charges télégraphiées laissant un sillage de flaques de magma),
  **Sentinelle Cryo** (Givre, cône de gel dirigé + plaques de givre dans l'axe, kite à 250 px),
  **Gardien Néon** (Néon, bouclier orbital couvrant 230° qui n'absorbe que 20 % des dégâts venus du
  secteur couvert, + renforts locaux) — plus le tag de biome des deux existants et `master_sentinel`
  ramenée à 11 min comme second rendez-vous commun. **Contrainte de conception** : le boss de fin ayant
  déjà une signature par biome depuis la 1.20.0 (§29), chaque mid-boss demande le réflexe **inverse**
  de l'incarnation finale de son biome (nova radiale → cône dirigé ; flaques projetées → flaques
  déposées sous ses pas ; faisceaux offensifs → bouclier défensif). **Deux règles de rendu apprises en
  mesurant** : (1) un effet dessiné en code ne peut pas vivre dans l'arbre du champion — `HitFlash`
  anime `Modulate` depuis `(5,5,5,1)`, qui se propage au `_Draw` et **sature les couleurs à blanc**
  (bouclier magenta mesuré à (142,142,145) en jeu, et le joueur tirant en continu, l'état flashé *est*
  l'état normal) → `ChampionOverlay` parenté à la racine ; (2) un champion doit **contraster** avec son
  biome, pas en reprendre la palette. Outillage : flag **`--debug-enemy=<id>`** (spawn isolé au scaling
  de sa propre fenêtre — `--debug-boss` ne savait spawner que `rusted_core`) et
  `tools/capture_midboss.py`. Design → `docs/GDD.md` §32 ; pièges → `docs/PITFALLS.md` §Mid-boss.
- **Survie en overtime — escalade de densité découplée du scaling (2026-07-28, non publié).** Le point
  laissé « à surveiller » à la publication de la 1.22.0 : le testeur meurt **1 minute après l'entrée
  en overtime**, alors que l'économie d'Échos est dimensionnée sur des runs d'overtime de **5 à 10
  minutes** (GDD §9.2, bonus de surcharge jusqu'à +100 Échos). Le levier de méta-progression était
  donc inatteignable. **Mesure** (session jouée, Fournaise palier 3) : le joueur remplit la condition
  de victoire (Noyau Rouillé vaincu, TTK 18,7 s) puis voit ses dégâts subis passer de **30/s à
  92,5/s en 54 s** — pendant que sa survie est **triplement plafonnée** depuis la 10ᵉ minute
  (`reinforced_plating` à son **niveau maximum 20**, réduction de dégâts au cap `StatCaps` 0,40,
  vitesse au cap 380). Aucun levier de survie ne restait disponible. **Cause** : `EnemySpawner`
  dérivait ses deux temps de référence l'un de l'autre (`tStat = tDensity + offset`), si bien que
  l'accélérateur d'overtime **×4 destiné à la densité** se déversait *en entier* sur les PV et les
  dégâts — via le terme **quadratique** de `EnemyScaling.CurvedFactor`, qui recevait donc un temps
  déjà multiplié par 4 et l'élevait au carré. Or à l'entrée en overtime **tous les leviers de densité
  sont saturés depuis plusieurs minutes** (cap de 300 dès la 8ᵉ, intervalle de spawn au plancher dès
  la 11ᵉ, taille de lot clampée dès la 4ᵉ) : ce ×4 ne densifiait plus rien, il ne faisait que gonfler
  les stats. **Correction** : règle pure **`OvertimeEscalation`** — la densité conserve son ×4
  (`DensityMinutes`), le scaling passe à **×1,5** (`StatMinutes`) et `tStat` ne dérive plus de
  `tDensity`. **Contre-mesure** (dégâts entrants rapportés à l'entrée en overtime) : ×1,9 → **×1,3**
  à 2 min, ×4,5 → **×2,1** à 5 min, **×10,9 → ×4,5** à 10 min. La fenêtre visée (5-10 min) est
  encodée en test, pas seulement écrite au GDD. Design : `docs/GDD.md` §31 ; pièges :
  `docs/PITFALLS.md` §Escalade d'overtime. **237 tests unitaires.**
- **Courbe de puissance du joueur assainie (PUBLIÉ 1.22.0, 2026-07-28).** Le point resté ouvert après
  la 1.21.0 (« la puissance explose en overtime ») est instruit et corrigé. Nouvel outil de mesure :
  **`PowerTelemetry`** (flag `--power-curve`, journal `user://power_curve.log` écrit au fil de l'eau)
  qui échantillonne toute la run — DPS infligé, dégâts subis, population, **indice de puissance du
  loadout** (`InventorySystem.PowerIndex()`) et build complet — plus `tools/power_curve_session.ps1`
  (session jouée ou banc headless) et le flag **`--run-limit=<s>`** (la survie est sans fin, un banc
  ne s'arrêtait jamais tout seul). **Mesure** : ×6,42 de puissance en 12 minutes d'overtime contre
  ×2,8 de PV pour le boss, population saturée au cap de 300 dès la 8ᵉ minute. **Cause** : les 4
  passifs ne définissent que **3 niveaux** pour un plafond de **20**, et au-delà le delta du dernier
  niveau était réappliqué en **additif non borné** — `capacitor` franchissait **100 % de réduction de
  recharge dès son niveau 8**, ce qui faisait tomber *toutes* les armes au plancher de 0,15 s : une
  arme lourde à 1,2 s tirait exactement aussi vite qu'un canon léger, et la cadence de fiche cessait
  d'exister. C'était aussi la cause de la dispersion du TTK (14,8 s à 42 s sur le même boss selon une
  seule carte prise). **Correction** : `PassiveScaling` (règle pure — rendements décroissants
  `delta / (1 + 0,20 n)` au-delà des niveaux définis : `thermal_core` ×4,00 → ×2,51 à L20,
  `reinforced_plating` +500 → +251 PV), **`StatCaps.MaxCooldownReduction = 0,75`** (le plafond était
  à 1,00, c'est-à-dire à rien — appliqué aussi aux améliorations du Hub), et retrait du pool des
  passifs dont l'unique stat est au plafond (`IsPassiveSaturated` : proposer une carte sans effet
  vole un choix au joueur). **Contre-mesure** : ratio ramené de ×6,42 à **×2,73** ; à build égal, DPS
  **×0,50** (multiplicateur 3,40 → 2,36, cadence cumulée −28 %). D'où le **recalibrage du boss en
  cascade : `rusted_core.maxHp` 8000 → 4000**, qui *resserre* la fenêtre de TTK (~21 s sans
  Capaciteur, ~29 s avec) au lieu de la déplacer. Design : `docs/GDD.md` §30 ; mesures :
  `docs/TEST_REPORT.md` ; pièges : `docs/PITFALLS.md` §Passifs. **231 tests unitaires.**
  **Session jouée de validation** (Fournaise, niveau 124, 488 DPS) : le Capaciteur **s'arrête de
  lui-même à L7** (la carte quitte le pool au plafond, comportement visé), puissance **×1,33** sur la
  première minute d'overtime contre ×3,8 en deux minutes avant, et TTK **18,7 s** à 4000 PV — sous la
  fenêtre, d'où **`maxHp` porté à 5000** (~23 s pour ce build). Le calcul analytique sous-estimait le
  DPS réel de 40 % : *ce boss ne se calibre que sur un TTK joué* (GDD §20.6). **Deux bugs corrigés au
  passage**, relevés dans le journal de cette session et sans rapport avec l'équilibrage :
  (1) `(int)GD.Randi() % n` est **négatif une fois sur deux** (le cast précède le modulo) — le shuffle
  de `LevelUpSystem.BuildWeaponCards` levait, et la **récompense de mini-boss était perdue une fois
  sur deux sans aucun signe à l'écran** (l'exception est avalée par le callback Godot) ;
  (2) les 5 golems `slow_hunter` de la faune par biome n'exposent pas d'animation `attack` alors
  qu'ils partagent la scène du Colosse — 144 erreurs par session, d'où `EnemyBase.PlayAnim` (ne joue
  que si l'animation existe, et **renvoie si elle a démarré** : le `QueueFree` de `death` en dépend).
- **Fusions d'armes réparées + boss recalibré (PUBLIÉ 1.21.0, 2026-07-28).** Le principal
  déséquilibre du jeu : les **9 fusions d'armes divisaient le DPS de fin de run par 3 à 6**. Trois
  défauts cumulés — dégâts posés en dur par leur classe C# et jamais multipliés (`ApplyWeaponStats`
  ne parcourait que la section `weapons` du JSON, donc ni Noyau Thermique ni améliorations du Hub),
  retour au **niveau 1** en fusionnant (l'arme remplacée atteint L11-L20 par extrapolation en fin de
  run), et **absence de tout pool de cartes** — l'arme de base en étant retirée, le slot était mort
  pour le reste de la run. La carte la plus spectaculaire du jeu (épique, flash blanc, SFX dédié) en
  était le pire choix. Corrigé par `WeaponBase.BaseDamage`/`BaseCooldown` (capture idempotente) +
  `InventorySystem.ApplyFusionStats` (niveau extrapolé × multiplicateur de dégâts, recharge depuis la
  valeur de fiche) + héritage du niveau + entrée des fusions dans les deux pools de cartes.
  Mesuré : build tout fusionné **105 → 368 DPS** au Sanctuaire, **103 → 490/539** au Néon.
  **Boss** : `maxHp` **12000 → 8000**, calibré sur la première mesure de TTK *jouée* de bout en bout
  (niveau 126, 3 fusions L20, 617 DPS, TTK 44,2 s → ~23-32 s selon le biome). **Outillage de mesure**
  livré au passage : `BossTelemetry` (journal `user://boss_ttk.log` : PV effectifs, build, bascules
  de phase, TTK, DPS), `tools/boss_ttk_session.ps1`, et les flags **`--auto-play`** (level-up et
  assimilation résolus seuls) + **`--timescale`** — sans eux une run en banc se fige au premier
  niveau, ce qui explique que le déséquilibre soit passé inaperçu si longtemps. Protocole :
  `docs/GDD.md` §20.6 ; mesures : `docs/TEST_REPORT.md` ; pièges : `docs/PITFALLS.md` §Fusions.
  **Ouvert** : la puissance du joueur explose en overtime (DPS 617 → 2322 en deux minutes, tous les
  passifs au plafond L20) — les boss d'overtime en deviennent triviaux.
- **Boss de fin — trois phases et cinq incarnations (PUBLIÉ 1.20.0, 2026-07-28).** Les cinq niveaux
  se terminaient sur **exactement le même combat** : même sprite, même pattern, intensité plate du
  premier au dernier point de vie. Depuis les paliers de menace (1.18.0), le Néon est ~45 % plus dur
  que le Sanctuaire mais offre la même fin — le moment le plus mémorable d'une run était le seul qui
  ne changeait jamais. Le Noyau Rouillé **reste l'unique condition de victoire** (groupe
  `rusted_core`, `onDeath.endsRunVictory`, verrou `EXPANSION_PLAN.md` §B.3 préservé) mais gagne deux
  couches. **(1) Trois phases** (100→66→33→0 % de PV, logique pure `BossPhases`, 25 tests) qui
  resserrent salves (2,00 → 1,20 s), ondes de choc (3,50 → 2,20 s), cadence de la signature (×1,00 →
  ×1,70) et vitesse (×1,00 → ×1,18) ; la phase III **invoque 4 adds** de la faune locale toutes les
  12 s via `EnemySpawner.SummonAdds`, plafonnés par le **cap simultané global** (le boss ne peut pas
  faire exploser la population). Chaque bascule ouvre **1 s de surcharge** télégraphiée : le boss
  s'immobilise, cesse de tirer, **n'inflige plus de contact** et **ne perd plus de PV** (le HitFlash
  est conservé — sans lui le joueur croit ses armes cassées), puis repart sur une onde de choc.
  **(2) Cinq incarnations** (`BossIncarnations`, 12 tests) résolues depuis le biome joué, chacune
  avec une mécanique signature : **éventail dirigé** (Sanctuaire, punit la ligne droite),
  **translocation** + salve spiralée (Aether, casse le kiting), **nova cryogénique** + plaques de
  givre au sol (Givre, punit l'immobilité), **flaques de magma** télégraphiées 0,7 s (Fournaise,
  réduit l'espace sûr), **2 à 4 faisceaux rotatifs** (Néon, impose la rotation). Les cinq partagent
  PV, TTK, socle d'attaques et mort — un joueur qui a appris le boss du Sanctuaire a *une* chose de
  plus à gérer, pas un boss à réapprendre. Sprites : les 4 variantes sont le **même dessin sous une
  autre palette** (`tools/generate_boss_sprites.py`, ombrage toujours dérivé par `pseudo3d_lib`),
  ce qui garantit visuellement qu'il s'agit de la même entité. Nouveau **HUD : barre de boss** (nom
  localisé, crans gravés aux seuils 66/33 %, numéro de phase en chiffres romains, flash à la
  bascule) — sans elle les phases sont invisibles. Nouvelles briques : `BossHazard` (zones au sol
  persistantes magma/givre, détection par distance et non `Area2D`, télégraphe avant armement),
  `Player.ApplyChill` (ralentissement environnemental, multiplicatif et séparé des greffes et de la
  Célérité), flag de debug **`--invuln`**. **Playtest** (`--debug-boss --invuln`, 5 biomes) :
  bascules mesurées à 25,4 s et 51,0 s, 0 erreur console sur 55 s. **Mesure de TTK humaine restant à
  faire** — un bot qui kite en cercle ne représente pas le DPS d'un vrai build. Détail et chiffres :
  `docs/GDD.md` §29. **222 tests unitaires.**
- **Options enrichies + accès depuis le menu pause (PUBLIÉ 1.19.0, 2026-07-28).** L'écran Options
  se limitait à 3 volumes, un toggle plein écran, un toggle secousses, difficulté, langue et le
  remap ZQSD — et n'était atteignable que depuis le menu principal : impossible de baisser la
  musique ou de passer en plein écran sans abandonner sa run. Il est désormais **découpé en cinq
  sections** (Audio / Affichage / Jeu / Interface / Contrôles) et gagne : **mode de fenêtre**
  (fenêtré / sans bordure / plein écran, remplace le toggle), **résolution** de la fenêtre (grisée
  hors mode fenêtré), **VSync**, **limite d'IPS** (60/120/144/240/illimitée), **compteur d'IPS**
  (affiché au-dessus du tampon de version), **intensité des secousses** en slider (remplace le
  toggle, 0 % = coupées), **réduction des flashs** (photosensibilité : flash de fusion atténué +
  aberration chromatique coupée), **vibration manette** en slider (branchée sur les dégâts et la
  mort du joueur), **affichage du tampon de version** et **Discord Rich Presence** (coupable à
  chaud). Le **menu pause** ouvre le même écran en **surcouche** (`OptionsScreen.OpenOverlay`) :
  pas de changement de scène, l'arbre reste en pause, la difficulté y est grisée (la run est déjà
  engagée) et « Tout réinitialiser » masqué ; au retour le panneau de pause se reconstruit (langue).
  Migration transparente des anciennes clés de `settings.cfg` (`display/fullscreen` → `display/mode`,
  `gameplay/shake` → `gameplay/shake_intensity`). Piège documenté dans `docs/PITFALLS.md` : le mode
  « sans bordure » passe par `DisplayServer.WindowMode.Fullscreen` (mode natif Godot) et **jamais**
  par un flag `Borderless` posé à la main — sinon le retour en fenêtré est impossible.
- **Paliers de menace — la difficulté suit les niveaux débloqués (PUBLIÉ 1.18.0, 2026-07-28).** Les 5
  niveaux se débloquent en séquence et le Hub rend le joueur 2 à 3 fois plus fort entre-temps, mais
  **tous tournaient sur la même courbe** : le dernier niveau était plus facile que le premier, et
  farmer le Sanctuaire restait le meilleur ratio Échos/risque. Chaque niveau porte désormais un
  **palier** (= son index dans l'ordre de déblocage) qui module PV, dégâts, densité de spawn,
  décalage de courbe et **récompense en Échos** (×1,00 → ×1,45 du Sanctuaire au Néon). Tables dans
  `src/Core/Rules/LevelThreat.cs` (logique pure, 6 tests dédiés) ; multiplicatif avec le réglage de
  difficulté du joueur et avec l'escalade d'overtime. Trois précautions de design : (1)
  `EnemySpawner` sépare `tDensity` (cadence/densité, temps réel) de `tStat` (scaling + variété +
  élites, décalé par le palier) — sinon le Néon démarrerait à la densité du mid-game dès la 10ᵉ
  seconde ; (2) les **champions** (mini-boss, boss de fin) ne prennent que 55 % du bonus de PV
  (`ChampionHpSoftening`) car battre le boss est la condition de déblocage du niveau suivant — au
  taux plein, le palier deviendrait un mur ; (3) le multiplicateur d'Échos s'applique **composante
  par composante** (`EchoFormula.ApplyTier`), la même opération que fait `RunEndScreen`, pour que la
  somme animée tombe pile sur le total crédité. Lisibilité : `Menace ★★★ · Échos ×1,20` sur les
  cartes de sélection de niveau et sur la ligne de survie de l'écran de fin. Écarté : indexer la
  difficulté sur la puissance méta réelle (rubber banding — acheter un upgrade rendrait le jeu plus
  dur). Détail et chiffres : `docs/GDD.md` §28. **184 tests unitaires.**
- **Bande-son metal industriel & musique adaptative (PUBLIÉE 1.17.0, 2026-07-27).** Le jeu tournait
  sur des placeholders chiptune CC0 (Juhani Junkala) enchaînés par bascules de piste à 5 et 10 min
  de run. Une première refonte l'a remplacé par 26 pistes synthétisées par le dépôt (ambiance
  Blade Runner / Vangelis, 4 stems synchronisés par biome) — **écoutée puis écartée le même jour :
  trop lente et trop contemplative** pour un jeu où l'écran se remplit de monstres.
  **Bande-son en vigueur : 14 pistes générées sur Suno**, direction metal industriel / synth-metal
  (guitares down-tuned + batterie live au premier plan, synthés et chœurs sans paroles au service
  du riff — Mick Gordon, Carpenter Brut). Tempos 112 à 176 BPM. Prompts par piste :
  `docs/AUDIO_AI_PROMPTS.md`. Les 3 stingers (mort/victoire/level-up) restent synthétisés par le
  dépôt. **Licence Suno : pistes générées sous plan gratuit = usage non commercial.** Le jeu étant
  distribué gratuitement sur itch.io, la 1.17.0 sort sur cette base ; **monétiser le jeu (ou une
  vidéo) imposerait de regénérer les pistes sous plan payant** ou de revenir à la bande-son
  synthétisée par le dépôt — `assets/audio/CREDITS.md`.
  **Pipeline d'intégration** : `tools/import_ai_music.py` prend les MP3 déposés dans `music_ai/`
  et fait ce qu'aucun générateur ne fait — détection du meilleur point de boucle par corrélation
  FFT (en écartant les baisses d'énergie, pour ne jamais boucler sur une outro), fondu de raccord,
  calage EBU R128, encodage OGG et nommage attendu par le moteur.
  **Musique adaptative** : chaque biome fournit deux versions du même morceau — `calm` (couplet,
  riff en retenue) et `combat` (refrain, tout ouvert) — plus `music_run_boss.ogg` commun aux cinq
  biomes. Les pistes étant des générations indépendantes (donc non synchronisées), `MusicDirector`
  (autoload) n'en rend **qu'une audible à la fois** et bascule par **fondu croisé à puissance
  constante** (3 s ; 2 s pour le boss). L'intensité (0-1) est calculée par la logique pure
  `MusicIntensity` depuis les ennemis à l'écran (poids 0,5, en racine : les premiers ennemis
  comptent le plus), le temps écoulé (0,3) et les PV du joueur (0,2), lissée 3× plus lentement en
  descente qu'en montée. Le choix de piste est protégé par une **hystérésis** (entrée 0,42 /
  sortie 0,26) et une durée de maintien de 10 s — sans quoi une intensité qui oscille ferait
  battre les pistes ; le boss court-circuite ce délai et démarre à son premier temps.
  Tonalités/tempos par biome : Sanctuaire Do min 140 BPM · Aether Ré phrygien dominant 152 ·
  Givre La dorien 130 (groove half-time) · Fournaise Sol phrygien 176 · Néon Mi mixolydien 160 ·
  boss Do min chromatique 150. Contrôle : `tools/check_music_assets.gd` (headless). Pièges
  (superposition interdite, fondu à puissance constante, hystérésis) : `docs/PITFALLS.md`.
  28 tests unitaires dédiés (167 au total). La bande-son synthétisée reste **régénérable** par
  `tools/generate_music_v3.py` et sert de filet de sécurité sans contrainte de licence
  (`docs/ART_BRIEF_AUDIO.md`).
- **Refonte des cadres d'UI — « plaque blindée » (PUBLIÉE 1.16.0, 2026-07-26).** Les cadres de
  boutons, cartes et popups recopiaient tous la même recette `StyleBoxFlat` (bordure uniforme +
  `corner_radius` arrondi, rayons 3/4/6/8/10 sans règle) sur ~300 sites : aucune identité, et
  l'arrondi anti-aliasé jurait avec le rendu `Nearest` des sprites. Désormais : coins chanfreinés
  (jamais arrondis), bevel reprenant la direction de lumière des sprites (`LIGHT_DIR` haut-gauche
  via `pseudo3d_lib.shade()`), rivets d'angle, bord « soudé » asymétrique, et un focus signalé par
  trois signaux cumulés — débordement de forme, liseré allumé, pulsation — au lieu d'un passage de
  bordure de 2 à 3 px. Socle : `src/UI/UiPalette.cs` (palette unique — la charte n'existait que
  dans la doc, le cyan était réécrit dans 8 blocs et ~20 littéraux, avec deux « fonds officiels »
  concurrents) et `src/UI/UiStyle.cs` (fabrique unique). 19 textures 9-slice dans
  `assets/sprites/ui/frames/`, régénérables par `tools/generate_ui_frames.py`. 21 `StyleBoxFlat`
  inline purgées de 4 scènes (`tools/strip_tscn_styleboxes.py`) — les laisser aurait rendu le
  nouveau style invisible. Étendue ensuite aux modales, à l'écran de level-up et aux écrans de
  sélection, puis aux **sliders/toggles/dropdowns d'Options** (rail creusé + poignée en plaque pour
  les curseurs, lecture par position du pavé pour les interrupteurs, `PopupMenu` câblé via
  `ApplyDropdownFrames` pour les listes déroulantes) — `tools/generate_ui_widgets.py`,
  `UiStyle.ApplySliderStyles`/`ApplyToggleStyles`/`ApplyPopupMenuStyles`. Parti pris et specs
  chiffrées : `docs/ART_BRIEF_UI_FRAMES.md`. Hors périmètre assumé : le HUD in-game, les puces de
  buff (couleur dynamique par buff), les drapeaux de langue (44×30 px, trop petits pour la plaque).
- **Fix audio (1.16.0)** : `AudioSystem` héritait du `ProcessMode` Pausable de la racine — musique et
  pool SFX se coupaient dès qu'une modale mettait le jeu en pause (level-up, pause, Assimilation, fin
  de run). `ProcessMode = Always` sur l'autoload corrige le problème et fait sonner les SFX d'UI dans
  les menus pausés (`src/Systems/AudioSystem.cs`).
- **Phase actuelle : libre** — dernière livraison majeure : **Assimilation en ligne (1.12.0,
  2026-07-07)** — 3e axe de progression publié pour la première fois (Phase A + Phase B volet 1 +
  écran Codex Chimère, tout ce qui suit était encore « non publié » à la sortie de 1.11.4) : 5
  greffes (Nuée Symbiotique, Servos Erratiques, Œil de Visée, Carapace Greffée, Onde du Rôdeur),
  2 fusions (**Charge Blindée** = Carapace+Servos → dash devient charge blindée ; **Ruche de
  Tourelles** = Œil+Nuée → 4 tourelles 360° + lifesteal), nouvel écran **`ChimeraCodexScreen`**
  (menu principal) expliquant greffes/fusions, HUD des greffes agrandi + liseré magenta (fin du
  recouvrement par la BuffBar). Détail chiffré : `docs/DESIGN_ASSIMILATION.md`. Version publiée
  itch : **1.12.0**.
- Dernières livraisons précédentes : **Discord Rich Presence** (`DiscordPresence`,
  statut « joue à Chimera Protocol » + tampon de version `v<ver>-<sha>` bas-droite `VersionStamp`, 2026-07-05),
  **nouveau perso Vecteur** (cyborg de précision, arme de base Lance Vectorielle dirigée, 2026-07-05),
  **remap clavier + ZQSD par défaut**
  (`src/Systems/InputRemap.cs`, section Contrôles des Options, 2026-07-05), visée souris/stick + réticule
  Lance Vectorielle (1.8.0), fusions Rayon Vecteur & Voile de Givre (brume de froid + ennemis gelés,
  1.8.1), affixes d'élite. **Correctif carte de level-up** (texte ancré sous l'icône, fini le chevauchement
  sur descriptions longues type fusions, 1.11.1). **Polish VFX biome Givre** (rendu ennemi gelé refait par
  shader `enemy_frost.gdshader` — lerp vers bleu glacial franc au lieu d'un multiply qui ternit ; brume du
  Voile de Givre densifiée en 6 puffs volumétriques, `src/Weapons/FrostVeil.cs`, 1.11.2). **Poussée d'ennemis**
  (le joueur écarte les ennemis qui chevauchent son corps au lieu de les traverser, sans perte de vitesse ni
  perte des dégâts de contact, `Player.PushEnemiesAside()`, poussée dans le sens du déplacement si ennemi centré,
  1.11.3) et **fix occultation obstacles** (correction de z-index : les obstacles infranchissables dessinent
  désormais au-dessus du joueur, ombre re-ancrée au sol, dans les 5 biomes, 1.11.3). **Rééquilibrage boss de fin**
  (Le Noyau Rouille jugé impossible à tuer : PV de base 18000→12000 dans `data/enemies.json` — PV effectifs à
  13 min ~21360 en Normal au lieu de ~32040 — + fix `EnemySpawner.SpawnOvertimeBoss` qui bypassait
  `maxSimultaneous:1` et laissait plusieurs boss s'empiler en overtime, cause principale du ressenti
  « impossible » ; TTK mesuré ~36-40 s sur build de référence, cible ~43-61 s build moyen, 1.11.4).

### Défis / Succès — **Lot 1 socle** (✅ 2026-07-08, non publié)

4e levier de rétention (après arsenal / Hub / Assimilation). Objectifs explicites évalués à la fin de
run, récompensés en Échos (immédiat) ou en perks/cosmétiques débloqués (équipables aux lots 3-4). Cf.
`docs/DESIGN_CHALLENGES.md`.
- **`ChallengeTable`** (`src/Core/Rules/`, logique pure — **+16 tests**, suite à **140**) : parse
  `data/challenges.json`, `ChallengeContext` (instantané plat fin de run), `IsMet`/`NewlyCompleted`.
- **`ChallengeSystem`** (autoload) : agrège le contexte (RunStatsTracker + AssimilationSystem +
  GameSettings + compteurs cumulés), octroie les récompenses, émet `ChallengeUnlocked`. **Ne charge
  jamais sa propre SaveData** — mute `MetaProgressionSystem.Meta` puis `PersistMeta()` (piège save.json).
- **Persistance** : `MetaSaveData` étendu (`UnlockedChallenges`/`UnlockedPerks`/`UnlockedCosmetics`/
  `LifetimeKills`/`LifetimeRuns`). Hook dans `RunStatsTracker.EndRun`. `RunEndScreen` affiche une ligne
  dorée « ★ Défi accompli ». **13 défis** (combat/survie/assimilation/maîtrise), loc `CHAL_*` EN/FR/ES.
- **Lot 2 écran Défis** (✅ 2026-07-08) : `ChallengesScreen` (sous-classe `CodexScreenBase`) — liste
  tous les défis (objectif + récompense + statut accompli/à faire), progression `X/N`, icône par
  récompense ; bouton « Défis » au MainMenu. Vérifié visuellement (capture). Loc EN/FR/ES.
- **Lot 3 perks de départ** (✅ 2026-07-08) : `MetaSaveData.EquippedPerk` (1 seul équipé) ; registre
  `StartingPerks` ; section « Perk de départ » au Hub (chips sélectionnables, masquée si aucun débloqué) ;
  application au run start (`GameManager.ApplyStartingPerkHook`) — greffe offerte (`GrantStartingGraft`),
  glaive en 2e arme, ou +1 slot (`AddBonusSlots`). Vérifié en boot Game.tscn. Loc EN/FR/ES.
- **Lot 4 cosmétiques/titres** (✅ 2026-07-08) : `MetaSaveData.EquippedCosmetic` ; registre `Titles`
  (La Chimère / Prédateur Alpha / Exterminateur) ; section « Titre » au Hub (infra chips générique
  partagée avec les perks) ; flair affiché sous le logo du MainMenu (`ApplyTitleFlair`). Aucun effet
  gameplay. Vérifié visuellement. **Boucle de rétention complète de bout en bout** (défi → récompense →
  équipement → effet).
- **Retouches playtest** (✅ 2026-07-08, retours de l'utilisateur après 1re session réelle — 9/13 défis
  débloqués en jeu) : (1) le voile de recharge du HUD gérait `dash`/`charge` mais pas l'effet `novaDash`
  → la **Frappe Nova** affiche enfin son cooldown (`HUD.RefreshGraftSlots`) ; (2) **sélecteur de langue**
  déplacé en haut à droite avec **drapeaux** FR/EN/ES (`tools/gen_lang_flags.py` → `assets/sprites/ui/
  flag_*.png`) ; (3) **menu principal désencombré** : Bestiaire/Arsenal/Chimère/Défis regroupés sous un
  **sous-menu Codex** (`CodexMenuScreen`) → MainMenu à 5 entrées ; (4) nouvel écran **Perks**
  (`PerksScreen`) décrivant les perks de départ, ajouté au sous-menu Codex. `CodexScreenBase.BackScenePath`
  → retour au sous-menu Codex.
- **Lot 5 — PUBLIÉ 1.15.0 le 2026-07-08** : seuil `exterminator` 10000→30000 (jalon prestige) ; README +
  DEVLOG (« Challenges & rewards » EN/FR) ; export .NET (189 fichiers) + `butler push` OK (patch 633 KiB,
  99,76% éco, build processing) ; `version.json`→1.15.0 (bandeau MAJ web) ; tampon `464a7e4` ; tout poussé
  sur `origin/main`. **Chantier Défis & Récompenses COMPLET et EN LIGNE.** Reste côté utilisateur : coller
  le devlog v1.15.0 sur itch (l'agent ne pilote pas le navigateur).

### Système d'Assimilation / Greffes — **Phase A + Phase B volet 1** (✅ publié 1.12.0, 2026-07-07)

Troisième axe de progression (« deviens la chimère »), cf. `docs/DESIGN_ASSIMILATION.md` Partie II.
Livré en Phase A :
- **`GraftTable`** (`src/Core/Rules/`, logique pure testée — **+25 tests xUnit**, suite à 112) : parse
  `data/grafts.json`, routage kill→jauge (`RouteKill` : basique/élite/mini-boss/boss → jauge d'archétype
  et/ou `stalker`), seuils effectifs (bonus méta `graft_metabolism`) et de refus (×1,5), `SlotCount`.
- **`AssimilationSystem`** (autoload) : jauges de points par archétype (`Dictionary<string,float>`),
  slots équipés + remplacement, pause de jauge d'une greffe possédée + reprise depuis valeur mémorisée,
  émet `GaugeFilled`. `Reset()` par run lit `graft_slots`/`graft_metabolism`.
- **`AssimilationScreen`** (`src/UI/`, scène `scenes/ui/AssimilationScreen.tscn`) : écran modal magenta,
  slot libre → ASSIMILER/REJETER, slots pleins → remplacer/CONSERVER. Partage **`ModalQueue`** avec le
  LevelUpScreen (un seul `Paused`, level-up prioritaire, jamais simultanés).
- **5 greffes** (`GraftManager`, enfant du Player) : Nuée Symbiotique (3 mini-essaims orbitants +
  lifesteal), Servos Erratiques (dash invulnérable, action d'entrée `dash` = Maj gauche/RB), Œil de Visée
  (tourelle auto réutilisant `Bullet`), Carapace Greffée (+DR/+PV/thorns, malus −18% via
  `Player.GraftSpeedMultiplier`), Onde du Rôdeur (onde de choc périodique + knockback, réutilise
  `ShockwaveRing`). Retrait propre au remplacement (deltas de stat réversibles, hardcaps respectés).
- **Rendu Phase A minimal** : rangée d'emplacements de greffe au HUD (sous la barre XP), teinte additive
  cumulée sur `SelfModulate` du joueur, `FusionFlash` à l'assimilation.
  - **Icônes de greffe au HUD livrées (2026-07-06)** : `HUD.RefreshGraftSlots()` affiche la texture
    `def.HudIcon` (`assets/sprites/grafts/<id>_icon.png`) via un `TextureRect` (Nearest,
    `KeepAspectCentered`, ~16-18 px dans le slot de 20 px), même pattern de chargement que
    `AssimilationScreen.LoadGraftIcon` (`Godot.FileAccess.FileExists` + `GD.Load<Texture2D>`).
    **Fallback carré teinté conservé** si l'icône est absente ; slot vide toujours grisé.
- **Méta Hub** : `graft_slots` (500/950, +1 slot, max 5) et `graft_metabolism` (180/320/520, −30% seuil max)
  dans `meta_upgrades.json` (arbre → 19 items). Codex : découvertes persistées (`GameSettings.DiscoverGraft`).
- **Phase B volet 1 — Fusions de greffes (2026-07-06)** : 2 greffes prérequises se lient en 1 fusion
  (occupation 2→1, un slot libéré). **Charge Blindée** (Carapace+Servos : le dash devient une charge
  240 px / 45 dmg + knockback, tank conservé, malus vitesse allégé) et **Ruche de Tourelles**
  (Œil+Nuée : 4 essaims → 4 tourelles en suivi lerp, ~48 DPS 360° + lifesteal). Jauge de fusion dédiée
  (`fusion_<id>`) qui n'accumule que si les 2 prérequis sont équipés (routage `AssimilationSystem.
  RouteFusionKill` + garde) ; carte de fusion sur `AssimilationScreen` (2 boutons, jamais de
  remplacement) ; `FusionFlash` à l'acceptation. Data-driven (`data/grafts.json` → section `fusions`),
  logique pure `GraftTable.FusionDef` (+7 tests xUnit → **119**). Comportements côté nœuds : charge
  (`Player` — couloir de dégâts, contourne `MaxSpeed`, i-frames en max), tourelles (`GraftManager`,
  réutilise `Bullet`). Détail chiffré : `docs/DESIGN_ASSIMILATION.md` §15. Clés loc `GRAFT_FUSION_*`/
  `ASSIM_FUSE`/`ASSIM_FUSION_*` posées (placeholder à finaliser `story-teller`) ; icônes
  `fusion_*_icon.png` à produire (`graphiste`, fallback carré teinté).
- **Écran Codex « Chimère » (2026-07-07)** : `ChimeraCodexScreen` (`src/UI/`, scène
  `scenes/ui/ChimeraCodexScreen.tscn`), accessible depuis le menu principal au même rang que
  Bestiaire/Arsenal — liste les 5 greffes + 2 fusions (icône, effet, prérequis), même socle
  `CodexScreenBase` (scroll clavier/manette).
- **Lisibilité HUD des greffes (2026-07-07)** : la rangée de la `BuffBar` (power-ups temporaires)
  recouvrait la rangée d'emplacements de greffe — emplacements agrandis + liseré magenta, fin du
  chevauchement (`f1c7431`, `21f18c4`).
- **Silhouette-chimère — Phase B volet 2 (2026-07-07)** : le corps du joueur **change visuellement**
  selon les greffes/fusions équipées (fini la simple teinte). **Props attachés** procéduraux ombrés
  pseudo-3D, indépendants du personnage (4 corps jouables) : Carapace (plastron+pauldrons), Servos
  (tuyères+vents qui s'embrasent au dash), Œil (orbe flottant, pupille qui vise), Onde (couronne-
  résonateur qui enfle avant l'onde), Charge Blindée (proue orientée au facing), Ruche (cœur de ruche).
  La Nuée/Ruche utilisaient déjà leurs essaims/tourelles comme silhouette. Impl. `GraftManager`
  (`GraftProp`/`BuildPropFor`/`UpdateProps`, ombrage `Shade`/`BaseColorFromTint`), miroir via
  `Player.FacingLeft`. Flag debug `--force-graft=<id|all>`, outil `tools/capture_graft_silhouette.py`.
  Détail : `docs/DESIGN_ASSIMILATION.md` §19. Validé visuellement, 119 tests verts. **Non publié.**
- **3e fusion — Frappe Nova (2026-07-07, `fusion_nova_rodeur`)** : Onde du Rôdeur + Servos Erratiques.
  Le dash devient une **téléportation offensive** : blink 190 px + i-frames, puis **nova** au point
  d'arrivée (onde de choc 175 px / 80 dmg / knockback 90, gatée par la recharge du dash). L'onde passive
  devient un burst positionnel visé. **Partage `erratic_servos` avec Charge Blindée → mutuellement
  exclusives** (choix de build ram blindé vs blink-nova ; l'infra fusion existante absorbe le partage).
  Data-driven (effet `novaDash`, 0 changement `GraftTable`), helper partagé `EmitShockwave`, prop cœur
  d'étoile pulsant. Détail : `docs/DESIGN_ASSIMILATION.md` §15.8. **Non publié.**
- **Variantes de greffe par biome — affinités (2026-07-07)** : **où** tu assimiles compte. Une greffe/
  fusion capture le biome courant et gagne son **affinité** (5 leviers) : Sanctuaire +12% dégâts,
  Aether +20% portée, Fournaise **brûlure** on-hit, Givre **ralentissement** on-hit, Néon −18% cooldown.
  damage/radius/cooldown sur toutes les greffes ; burn/slow sur dégâts directs (Nuée/thorns/onde/nova)
  + balles (Œil/Ruche, `Bullet.BurnDps/SlowMult`). Data-driven (`biomeAffinities` de `grafts.json`),
  logique pure `GraftTable.BiomeAffinity`/`GetAffinity` (+5 tests → **124**). Carte d'assimilation
  affiche l'affinité gagnée ici ; accent biome baké dans le prop de silhouette. Rejouabilité : une Nuée
  brûle en Fournaise, gèle en Givre. Détail : `docs/DESIGN_ASSIMILATION.md` §21. **Non publié.**
- **Phase B TERMINÉE.** Reste optionnel : textes/lore/loc à peaufiner par `story-teller` ; icônes de
  greffe/fusion à produire par `graphiste` (fallback carré teinté en attendant).

## Ce qui est implémenté

- Direction artistique **pseudo-3D avec ombres** (`docs/ART_BRIEF_PSEUDO3D.md`) appliquée à TOUS les sprites via `tools/pseudo3d_lib.py` (lumière fixe haut-gauche 45°, dérivation shadow/highlight HSV, ombre portée elliptique) : 3 persos joueurs, 8 ennemis/mini-boss/boss existants, 20 nouveaux ennemis, obstacles, tuiles de biome, icônes d'armes/UI (640 PNG régénérés, `.import` à jour). Validé game-tester PASS 2026-07-03 (cohérence lumière, lisibilité joueur en nuée).
- 4 personnages jouables (Chimera/Canon à Impulsions, Titan/Essaim de Drones, Vagabond/Lame Plasma, **Vecteur/Lance Vectorielle** — cyborg de précision, arme dirigée, ajouté 2026-07-05), 5 biomes (Sanctuaire, Aether, Fournaise, Givre, Néon)
- 12 armes actives + 9 fusions + 4 passifs ; power-ups temporaires (4 types)
  (`vector_lance` = 1re arme **dirigée** : tire vers `Player.AimDirection`, pas l'ennemi le plus proche — cf. GDD §23 ;
  sa fusion `vector_beam` + servo_motors = **rayon perforant continu dirigé** ;
  `frost_veil` = cryo_lance + reinforced_plating → **aura de givre continue** (dégâts + slow radial, contrôle défensif))
  (fusions : fusion_blade, rail_overcharged, orbital_swarm, overload_aegis, ionic_storm,
  solar_column, hornet_swarm — chaque évolution = arme de base niv.5 + passif requis, remplace l'arme)
- Fin de niveau complète : survie sans fin, overtime, boss en boucle, déblocage progressif, high scores (temps+difficulté), arsenal à découverte
- Hub méta rééquilibré (2026-07-02) : 17 upgrades (7 rééquilibrés + 10 nouveaux ; `starting_weapon_alt` retiré 2026-07-04 car aucun sélecteur d'arme de départ n'est câblé), formule d'Échos plafonnée standard/overtime (`EchoFormula.Calculate`, caps + `overtimeDampening`/`overtimeBonusCap`), 5e composante "Bonus de Surcharge" sur `RunEndScreen`, `UpgradesList` scrollable
- Cinématique d'intro (2026-07-03, **plan Assimilation ajouté 2026-07-06**) : `src/UI/IntroScreen.cs` (scène de boot) — cut-scene 2D scriptée en **6 plans narratifs + reveal du titre** (noyau d'Aether, corruption d'un drone, nuée + colosse, sanctuaire, descente de l'Arpenteur, **assimilation**), sprites animés réutilisant les `SpriteFrames` existants + particules `CpuParticles2D` + zoom caméra via `Tween`, synchronisée sur la narration `INTRO_BEAT_1..6` (EN/FR/ES) et la musique dédiée `music_intro` (CC0, "Transmission"/SRG774, cf. `assets/audio/CREDITS.md`). Skippable. Reveal via clés `INTRO_TITLE`/`INTRO_TAGLINE` (tagline alignée sur le pitch : « Ne tue pas les monstres. Deviens-les. »). **Plan 6 `ShotAssimilation`** (`INTRO_BEAT_6`, 4,0 s, cf. `docs/DESIGN_ASSIMILATION.md` §20) : mise à mort d'un Rust Swarm → arrachement d'un fragment (particules rouille→cyan vers le joueur) → mutation (aura `FusionFlash`/`FusionAura` + teinte subtile du joueur), n'utilise que des assets déjà chargés. Outil de capture : `tools/capture_intro.py`
- Localisation EN/FR/ES (`localization/ui.csv` → clé `Loc.T("CLÉ")`) ; support manette complet
- HUD thématisé par biome, atmosphère (brume/rais/parallaxe), scanlines CRT
- Arènes : obstacles thématisés par biome (`BiomeObstacles.cs`), features de sol (`FloorFeatures.cs` — lave/rivières/chemin pavé/conduits), gabarits structurés, décor rouillé réservé au Sanctuaire ; flag `--biome=<id>` pour forcer un biome (tests/captures)
- Faune par biome (2026-07-03) : **28 ennemis basiques au total** (8 d'origine + 20 nouveaux, 4/biome), câblés via sprite data-driven (`EnemyBase.SetSpriteFrames` + `EnemySpawnData.FramesPath`/`AiType`) — aucune nouvelle scène/sous-classe, réutilise les 4 scènes archétype existantes (cf. `docs/GDD.md` §21). Sprites générés (`tools/generate_new_enemies.py`). Densité par biome doublée (spawnWeight dilué mais compensé par les ids globaux toujours actifs) — validé game-tester PASS. Limite connue : les 5 variantes d'un même archétype partagent une silhouette recolorée (pas de nouvelle forme par ennemi) — à arbitrer si plus de variété visuelle est souhaitée
- **Affixes d'élite (2026-07-04)** : une fraction des ennemis *basiques* (jamais mini-boss/boss) est promue élite avec 1 affixe parmi 5 (Blindé/Régénérant/Explosif/Frénétique/Vampirique) — cf. `docs/GDD.md` §22. Logique pure testée `src/Core/Rules/EliteAffixTable.cs` (fréquence `clamp(0.03+0.02×t, 0, 0.28)`), appliquée par `EnemyBase.ApplyElite` (stats après `ApplyScaling` + comportement + rendu teinté/agrandi + halo `EliteAura`), tirée dans `EnemySpawner.SpawnEnemy`. Répond à la limite « silhouettes recolorées » (variété = comportement). Flag debug `--force-elites`. Répond au brainstorm « inspirations d'autres jeux » (élites façon Risk of Rain 2 / Diablo)
- **Correctifs 2026-07-04** : purge des VFX/projectiles résiduels par-dessus le menu/Hub à la sortie de run (`SceneCleanup.ClearWorldVfx`, cf. `docs/PITFALLS.md`) ; retrait de l'upgrade Hub sans effet `starting_weapon_alt`
- **Retours testeur 2026-07-04** (cf. `docs/GDD.md` §23) : (1) `Player.ZIndex=5` — le joueur reste visible au-dessus des flammes/VFX d'armes ; (2) **Lance Vectorielle** (`vector_lance`, Rare) — arme dirigée vers `Player.AimDirection` (skill de visée : **souris** en clavier/souris, **stick droit** en manette, + réticule autour du joueur — MAJ 2026-07-04), réutilise `Bullet`, éventail aux niv. 4-5 ; (3) **courbe de difficulté non-linéaire** `EnemyScaling.CurvedFactor`/`ScaledCurved` (early grace −15% à t=0 puis accélération quadratique après 4 min) branchée dans `EnemySpawner` — le late rattrape le power-creep du build. 4 tests ajoutés (87 au total)

Voir aussi `docs/EXPANSION_PLAN.md` et `docs/LEVEL_PROGRESSION_PLAN.md` pour le détail des plans.
