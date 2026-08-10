# Chimera Protocol

> Survivor roguelite vue du dessus — univers fantaisie-science-fiction, inspiré de Vampire Survivors et Everything is Crab.

Dans un monde ravagé par **la Rouille Vivante** (corruption mi-organique, mi-mécanique née de la fusion de la magie et de la technologie), tu incarnes un **Arpenteur** envoyé en mission d'extraction dans un Sanctuaire en ruines. Survive, évolue, et rapporte des **Noyaux d'Aether** avant d'être submergé.

---

## État du projet

**Bande-son metal & musique adaptative** *(2026-07-27, v1.17.0)* — la bande-son chiptune est
entièrement remplacée par du **metal industriel** : guitares down-tuned, batterie qui cogne, basse
saturée, avec des synthés analogiques et des **chœurs sans paroles** par-dessus. Chaque biome a sa
tonalité, son tempo et son riff — du groove glacial du Givre (130 BPM) au thrash de la Fournaise
(176 BPM) — le menu porte un thème sombre et tendu, et la cinématique d'intro sa pièce dédiée.
Surtout, la musique de combat est **adaptative** : chaque biome existe en deux versions du même
morceau, couplet et refrain, et le jeu passe de l'une à l'autre en fondu selon l'action — densité
d'ennemis, temps de survie, points de vie — avant de basculer sur le **thème de boss** quand un
colosse débarque.

**La Saturation de Rouille — cinq règles nommées plutôt qu'un chiffre plus gros** *(2026-07-31,
v1.25.0)* — « avec toutes les évolutions, le jeu devient facile » : c'est mesuré, et la cause n'est pas
un manque de puissance chez l'ennemi. La défense du joueur croît **sans plafond** (270 PV/min mesurés)
face à une menace à **courbe fixe**, densité saturée dès la 8ᵉ minute et plafond de difficulté à
**×1,35**. Surtout, la menace ne posait qu'**une question** — des statistiques — donc le joueur n'avait
qu'**une réponse**, et il gagnait toujours cet échange. La Saturation ne donne donc pas un
multiplicateur de plus : **un cran = une règle nommée, lue avant de lancer, qui retire une certitude**.
**I Hémorragie** (soins reçus −65 % et monter de niveau ne soigne plus — le canal de soin dominant
mesuré) · **II Meute** (statistiques d'ennemis) · **III Compte à rebours** (overtime à la 8ᵉ minute :
c'est le *temps de build* qui est attaqué) · **IV Sans filet** (aucun filet acheté ne survit) ·
**V Élite ordinaire**. Le cran **se règle et se débloque par niveau**, et rapporte **+20 % d'Échos** par palier.
*(Valeurs relevées le 2026-08-02 après que l'échelle entière ait été jouée et gagnée du premier coup —
GDD §34.8 ; en ligne depuis la 1.26.0, avec le cran **VI Purificateur** en haut d'échelle.)*
Validé au banc sur quatre graines appariées : temps soutenable **60,7 % → 39,9 %**, et **2 runs sur 4**
finissent par une mort réelle là où les quatre atteignaient le plafond du banc. Les records gagnés en
« Difficile » restent **exacts** — la Saturation absorbe l'ancien axe au lieu de s'y ajouter.

**L'Auto-réparation jetait 58 % de ce qu'elle soignait** *(2026-07-30, v1.24.0)* — c'était la carte que
personne ne prenait : **44 Blindages contre 1 Auto-réparation** sur une partie relevée. On avait cru à
un défaut de lisibilité et ajouté un indicateur au HUD ; insuffisant, donc cette fois on l'a
**instrumentée**. Elle tournait à **19,2 PV/s pour 8,2 réellement rendus** — le reste était **perdu**,
parce qu'on passe **100 % de l'overtime au-dessus de 90 % de ses PV** et qu'on meurt d'un **pic**, pas
d'usure. Monter le chiffre n'aurait fait que grossir la part jetée. Désormais, à PV pleins le débit
remplit une **réserve** (le liseré pâle sous la barre de vie) qui **encaisse le prochain coup** — un
coup entièrement absorbé se lit comme **paré** : flash cyan, aucun son de blessure. Mesuré sur quatre
runs de banc appariées : PV réellement rendus **8,2 → 15,9/s (+94 %)**, difficulté d'overtime
**inchangée**. Au passage, les **champions de biome** passent de 48 à **72 px** — leur hitbox
débordait de leur silhouette, et ils étaient les plus petits boss du jeu.

**On ne règle plus ce jeu sur une seule partie** *(2026-07-30, v1.24.0)* — trois passes d'équilibrage
d'affilée s'étaient décidées sur **une session jouée chacune**, alors que deux sessions du même joueur
différaient d'un facteur **2,4** en survie — mesuré *là où le réglage testé n'a encore aucun effet*.
Le bot de banc **se déplace** désormais (il kite, ramasse, esquive), donc il meurt pour de vrai et la
survie devient mesurable sans invulnérabilité ; les runs sont **reproductibles par graine**, ce qui
permet de comparer deux réglages sur des vagues et des tirages **identiques**. La dispersion tombe de
**240 % à 4-13 %** : un écart supérieur à ~6 % se tranche en moins d'une demi-heure, sans jouer.

**L'overtime devient une vraie partie** *(2026-07-29, v1.23.0)* — il est censé durer **5 à 10 minutes**
(toute l'économie d'Échos est dimensionnée dessus) ; les testeurs mouraient au bout de **74 secondes**.
Deux causes : l'accélérateur d'overtime visait la **densité** d'ennemis, mais tous les leviers de
densité sont déjà saturés à son déclenchement — il se déversait donc en entier sur les PV et les
dégâts, au carré ; et un bug amortissait les gains de **PV maximum** (la Plaque Renforcée donnait 251
PV au niveau 20 au lieu de 500). S'y ajoutent les **cartes de surcharge** : passé le niveau où tout
est au maximum, on gagnait des niveaux **pour rien** (124 → 140 en 74 s), le jeu distribuant des bonus
d'XP faute de mieux. Trois cartes **sans plafond** prennent le relais quand le pool est vide. Mesuré
sur une partie jouée : **74 secondes → 8 min 36**, sur une mort subie.

**Un champion de mi-partie par biome** *(2026-07-29, v1.23.0)* — trois niveaux sur cinq n'avaient
**aucun boss de mi-partie**. Le **Colosse en Fusion** (Fournaise) télégraphie ses charges et laisse un
sillage de magma — le danger est le terrain qui se referme ; la **Sentinelle Cryo** (Givre) tire un
cône de gel dirigé ; le **Gardien Néon** (Néon) porte un bouclier orbital absorbant 80 % des dégâts
venus du secteur couvert, qu'il faut contourner. Chacun demande le réflexe **inverse** du boss final
de son propre biome.

**Courbe de puissance assainie** *(2026-07-28, v1.22.0)* — le Capaciteur atteignait **100 % de
réduction de recharge** dès son niveau 8 : toutes les armes tombaient au même plancher de 0,15 s, et
une arme lourde tirait exactement aussi vite qu'une arme légère. Les passifs progressent désormais
en **rendements décroissants** au-delà de leurs niveaux définis, la réduction de recharge est
plafonnée à **75 %**, et un passif dont la statistique est au plafond **cesse d'être proposé** en
carte. Le boss suit (**5000 PV**) : le même combat se mesurait de 14,8 à 42 s selon une seule carte
tirée, il tient maintenant **26 à 35 s**, y compris sur ses réapparitions d'overtime. Corrigé au
passage : la **récompense de mini-boss disparaissait une fois sur deux**, sans aucun signe.

**Paliers de menace** *(2026-07-28)* — les 5 niveaux se débloquent en séquence et le Hub te rend
2 à 3 fois plus fort en chemin : chaque niveau porte désormais un **palier de menace** croissant
(ennemis plus coriaces, plus dangereux, plus nombreux et plus variés tôt) **et paie plus d'Échos**
(jusqu'à ×1,45 au Secteur Néon). Le contrat est affiché sur la carte du niveau — `Menace ★★★ ·
Échos ×1,20` — avant de lancer la run. Finie l'anomalie où le dernier niveau était plus facile que
le premier et où farmer le Sanctuaire restait optimal.

**Cadres d'UI « plaque blindée »** *(2026-07-26, v1.16.0)* — le style de cadre biseauté (chanfreins,
bevel, rivets, focus pulsé) déjà utilisé sur boutons/cartes/popups s'étend aux modales, à l'écran de
level-up, aux écrans de sélection, ainsi qu'aux **curseurs, interrupteurs et menus déroulants**
d'Options (derniers contrôles restés au thème Godot par défaut). Correctif : la musique et les SFX
d'UI ne se coupent plus à l'ouverture d'une popup (level-up, pause, Assimilation, fin de run).

**Défis & Récompenses** *(2026-07-08, v1.15.0)* — 4e levier de rétention : accomplis des **défis** en jeu (100 kills, survivre 13 min, forger une fusion, terminer un biome…) pour gagner des Échos et débloquer des **perks de départ** (greffe offerte, arme supplémentaire, +1 emplacement) et des **titres** cosmétiques. Nouvel écran **Défis** + section « Perk / Titre » au Hub. Le menu principal est réorganisé (sous-menu **Codex** regroupant Bestiaire/Arsenal/Chimère/Défis/Perks) et le sélecteur de langue passe en **drapeaux** en haut à droite.

**Assimilation** *(2026-07-07, v1.14.0)* — 3e axe de progression : tue des ennemis pour remplir une jauge d'archétype, deviens la chimère en assimilant leurs greffes, puis fusionne 2 greffes (**3 fusions** : Charge Blindée, Ruche de Tourelles, **Frappe Nova**) en une forme évoluée. La **silhouette du joueur change visuellement** selon les greffes/fusions (carapace, servos, œil, onde, proue de charge, cœur de ruche). **Affinités de biome** : où tu assimiles compte — Fournaise brûle, Givre gèle, Néon accélère…

| Phase | Statut | Contenu |
|---|---|---|
| **Paliers de menace** | ✅ Livré | La difficulté suit l'ordre de déblocage des niveaux : palier 0 (Sanctuaire) → 4 (Néon), qui module PV (×1,50), dégâts (×1,45), densité de spawn (×1,16), décalage de courbe (+2,4 min sur la variété/le scaling, jamais sur la cadence) et **récompense en Échos** (×1,45). Les champions n'encaissent que 55 % du bonus de PV (battre le boss débloque le niveau suivant). Logique pure testée (`LevelThreat`, +17 tests, 184 au total) — `docs/GDD.md` §28 |
| **Défis & Récompenses** | ✅ Livré | 4e levier de rétention (après arsenal / Hub / Assimilation). **13 défis** (combat/survie/assimilation/maîtrise) évalués en fin de run → Échos, **perks de départ** (Départ Symbiotique, Panoplie Glaive, Emplacement Bonus) ou **titres** cosmétiques. Écran **Défis** (progression X/N) + sélection perk/titre au Hub + flair du titre sur le menu. Logique pure testée (`ChallengeTable`, +16 tests). Menu principal réorganisé (sous-menu **Codex**) + sélecteur de langue à drapeaux |
| **Assimilation** | ✅ Livré | « Ne tue pas les monstres. Deviens-les. » — chaque kill remplit une jauge d'archétype (Nuée/Drone/Sentinelle/Colosse) ; jauge pleine → greffe proposée (5 disponibles : Nuée Symbiotique, Servos Erratiques, Œil de Visée, Carapace Greffée, Onde du Rôdeur), 3 emplacements de base (5 via méta-upgrades). 2 **fusions** (Charge Blindée, Ruche de Tourelles) combinent 2 greffes en 1 forme évoluée qui libère un emplacement. Nouvel écran Codex **Chimère** (menu principal). **Silhouette-chimère** (v1.13.0) : le corps du joueur accumule visuellement des props ombrés pseudo-3D selon les greffes/fusions portées |
| **Affixes d'élite** | ✅ Livré | Élites façon Risk of Rain 2 / Diablo : n'importe quel ennemi basique peut recevoir 1 affixe parmi 5 — **Blindé** (encaisse), **Régénérant** (se soigne hors combat), **Explosif** (AoE à la mort), **Frénétique** (rapide/fragile), **Vampirique** (vole des PV). Rendu teinté + agrandi + halo pulsant, XP/drops relevés. Fréquence croissante plafonnée (3 %→28 %). Logique pure testée (`EliteAffixTable`, +12 tests) |
| **Refonte visuelle pseudo-3D + faune par biome** | ✅ Livré | Direction artistique pseudo-3D avec ombres (`docs/ART_BRIEF_PSEUDO3D.md`, lib partagée `tools/pseudo3d_lib.py`) appliquée à **640 sprites** régénérés : 3 persos joueurs, 8 ennemis/mini-boss/boss existants, **20 nouveaux ennemis basiques** (4/biome, sprite data-driven sans nouvelle scène Godot), obstacles, tuiles de biome, icônes d'armes. Validé game-tester PASS |
| Phase 1 — Prototype | ✅ Terminé | Joueur, arène, 1 ennemi, 1 arme |
| Phase 2 — Gameplay core | ✅ Terminé | 4 ennemis, 4 armes, 4 passifs, 2 fusions, XP/level-up, Échos d'Aether, Hub, sauvegarde |
| Phase 3 — Contenu & polish | ✅ Terminé | Sprites pixel art, audio synthétique CC0, arène graphique, menu principal, UI complète, FusionFlash |
| Phase 4 — Arène + VFX | ✅ Terminé | Arène 1920×1216, obstacles A–D, lueur geysers, death burst, XpOrb trail/pulse, impact burst, ambiants Aether |
| Phase 5 — Navigation & audio | ✅ Terminé | Navigation clavier/manette complète, **bande-son metal industriel** (14 musiques, chaque biome en couplet/refrain adaptatif + thème de boss) + 24 SFX Kenney CC0 |
| Équilibrage MVP | ✅ Validé | 0 crash sur 3 runs (~25 min), XP ennemis différenciés par tiers |
| Polish visuel — base | ✅ Livré | Fond arène assombri, PointLight2D joueur/projectiles, notifications armes équipées avec flash coloré |
| Polish visuel — next-level | ✅ Livré | 4 shaders GLSL, screen shake, vignette dynamique (suit le joueur), grille holographique sol, shockwave Colosse, hit stop, trail joueur, chromatic aberration fusion, VFX armes (PlasmaBlade arc flash, muzzle flash, trails balles, OverloadField, drones lumineux) |
| Mini-boss & orbes XP | ✅ Livré | 2 mini-boss (Rôdeur de Rouille + Sentinelle Maîtresse, 64×64 px, écran choix d'arme à mort), orbes XP 4 tiers (vert/cyan/violet/or), revamp sprites 4 ennemis |
| Drops HP + Progression VS | ✅ Livré | Orbes HP rouges (8%/25% mini-boss, +15% MaxHP), heal 25% MaxHP à chaque level-up, courbe XP inspirée Vampire Survivors (L1=5 XP, linéaire +10/niveau, mur L20) |
| HUD "juicy" sci-fi | ✅ Livré | Panel sombre + bordure cyan, barres plus épaisses (HP 18px / XP 12px), glow derrière chaque barre, drain HP animé, pulsation rouge <25% HP, flash XP overexposé au level-up |
| HUD — assets concept cyberpunk | ✅ Livré | Extraction/retouche depuis concept `idea/idee_hud_chimera_core.png` (masquage HSV numpy) : barre XP 20 segments, hexagone LV 44×26, icône Chimera Core violet, cadre panneau stats tech (generated from scratch), cadre timer avec crochets, titres de panneau "CHIMERA PROTOCOL" / "NOYAUX AETHER" / "RUNTIME ENCRYPTED" |
| Typographie pixel | ✅ Livré | Police **VT323** (pixel/terminal CRT, OFL) en rendu net (anti-aliasing désactivé) appliquée globalement via Theme — fin du texte "baveux" ; tailles HUD ré-accordées, glyphes spéciaux → ASCII |
| Juice & densité VS | ✅ Livré | VFX scalés par niveau d'arme (brillance balles, impact bursts, flash), explosions de mort calibrées par tier + onde de choc, aura joueur croissante, screen shake d'impact ; arène éclaircie ; spawn façon Vampire Survivors (cap 300, courbe raide, lots + vagues) ; i-frames joueur (0.45 s) |
| Boss & nouvelles armes | ✅ Livré | 2 armes 100% VFX (Bobine Tesla = éclair en chaîne, Nova d'Aether = détonation dilatante) ; mini-boss de mi-temps **Revenant d'Aether** (7 min, ruades) ; **boss de fin Le Noyau Rouillé** (13 min, HP base 1600 — rééquilibré depuis, voir plus bas, salves radiales, 500 XP + 3 Noyaux + choix d'arme) |
| Sprites dédiés boss | ✅ Livré | Sprites pixel art 64×64 dédiés générés (`tools/generate_boss_sprites.py`) : Revenant (spectre cyborg violet, bras-lames, dissolution) + Noyau Rouillé (titan rouille-or, noyau en fusion, surcharge) — fin de la réutilisation teintée |
| Bestiaire & Arsenal | ✅ Livré | 2 rubriques au menu : **Bestiaire** (8 ennemis — sprite **animé** + tag + description) et **Arsenal** (11 armes + 4 passifs — icône + description). Icônes Tesla/Volée créées ; icônes sur les cartes de choix d'arme et dans les notifs HUD |
| Lisibilité UI | ✅ Livré | Police principale **Share Tech Mono** (mono techno lisible, anti-aliasée) en remplacement de VT323 — texte et HUD nettement moins pixelisés ; tailles ré-accordées |
| Personnages jouables | ✅ Livré | 4 personnages (registre `Characters.cs`) : **Chimera** (cyborg, impulse_cannon), **Titan-Gardien** (robot lourd, drone_swarm), **Vagabond** (humain, plasma_blade), **Vecteur** (cyborg de précision, vector_lance — 1re arme de signature *dirigée*, réticule dès le départ) — sprites pixel art dédiés, aura d'identité, sélecteur dans le Hub. Le perso garde toujours son arme de signature |
| Biomes d'arène | ✅ Livré | 4 biomes (Sanctuaire Rouillé, Friche d'Aether, Fournaise, Givre Cryogénique) — tuiles dédiées, obstacles colorés à l'accent, effets gameplay (XP +20%, vitesse ennemis ±18%), seed de layout randomisé |
| Sélection de niveau | ✅ Livré | « Jouer » mène à un écran de sélection de biome (4 cartes aperçu + Aléatoire) ; badge **« VAINCU »** sur les biomes déjà battus |
| Options & difficulté | ✅ Livré | Écran Options (sliders volume, plein écran, secousses) persisté dans `settings.cfg` ; sélecteur de difficulté Facile/Normal/Difficile (multiplicateurs dégâts/HP/spawn) ; équilibrage early-game assoupli |
| **Options complètes + accès en pause** | ✅ Livré | Écran Options en **5 sections** (Audio / Affichage / Jeu / Interface / Contrôles) : mode de fenêtre (fenêtré / sans bordure / plein écran), résolution, VSync, limite et **compteur d'IPS**, intensité des secousses, **réduction des flashs** (photosensibilité), **vibration manette**, tampon de version, Discord Rich Presence — tout persisté dans `settings.cfg`. Le **menu pause** ouvre les Options **en surcouche**, sans quitter la run |
| Intro narrative | ✅ Livré | Scène de boot jouant le lore en 5 temps, fondu enchaîné, **skippable** (toute touche → menu) |
| HUD thématisé par biome | ✅ Livré | HUD reconstruit 100% en code, look minimal Cyberpunk 2077 ; coloré par l'accent du biome ; **scanlines CRT**, bandeau de loadout, chip de biome, animations discrètes (liseré qui respire, XP lerp, pop des Noyaux) |
| **Victoire par boss final** | ✅ Livré | La run se gagne en **vainquant Le Noyau Rouillé** (plus d'auto-victoire au timer) ; **badge « VAINCU »** par biome/difficulté persisté dans `settings.cfg` — validé game-tester 5/5 |
| **Rééquilibrage boss final** | ✅ Livré | PV de base **1600 → 12000** (2026-06-28) puis **→ 18000** (2026-06-29, mid/end trop facile). Hook debug `--debug-boss` (loadout de test + spawn boss isolé) pour mesurer le TTK. Décision documentée GDD §20 — voir « Durcissement » plus bas pour les valeurs courantes |
| **Aimant aspirateur d'XP** | ✅ Livré | Nouvel item **`MagnetPickup`** qui, au contact, attire **toutes les orbes d'XP de l'arène** vers le joueur (façon Vampire Survivors). Apparition **programmée** (`MagnetSpawner`) : **max 3 fois/run**, à des moments aléatoires, dont **une proche de la fin** (~12-13 min). Fer à cheval gris+rouge, halo cyan |
| **Suppression Nova d'Aether** | ✅ Livré | L'arme `aether_nova` retirée partout (données, code, scène, icône) — arsenal actif ramené à **6 armes** |
| **Polish & fixes** | ✅ Livré | **Hitstops de mort retirés** sur mobs/mini-boss (le ralenti cassait le flow ; conservé sur le boss final = ponctuation de victoire) ; **musiques qui rebouclent** (menu/hub/run — `loop_mode=0` à l'import contourné par rebouclage code) ; page de store **itch.io** (`docs/ITCH_STORE_PAGE.md`) |
| **Ennemis bloqués par les obstacles** | ✅ Livré | Les obstacles infranchissables (piliers/épaves/caisses/arches) passent sur le `collision_layer 3` (bits 1+2) ; les ennemis (`mask` 0→2) sont **bloqués par les obstacles** mais traversent toujours les murs |
| **Hub : reset + retrait XP de départ** | ✅ Livré | Bouton **« Réinitialiser les améliorations »** (remboursement total des Échos, confirmation en 2 temps) ; amélioration **« +60 XP de départ » retirée** |
| **Améliorations Reroll & Skip** | ✅ Livré | 2 améliorations Hub (max 3 chacune) : **Recalibrage Tactique** (renouvelle les 3 cartes de level-up) et **Esquive de Sélection** (passe la sélection en gardant le niveau). Boutons « Renouveler »/« Passer » sur le LevelUpScreen, navigation clavier câblée |
| **Écran de choix du personnage** | ✅ Livré | « Jouer » mène à un **`CharacterSelectScreen`** (cartes avec image idle + nom + stats + description) AVANT le choix du niveau. Le Hub perd le bouton « Jouer » et le sélecteur de perso. Flux : Menu → Jouer → Perso → Niveau → Game |
| **Quitter la partie (pause)** | ✅ Livré | Bouton **« Quitter la partie »** dans le menu de pause → retour au menu principal |
| **Localisation EN / FR / ES** | ✅ Livré | **Anglais par défaut**, FR conservé, ES ajouté. Sélecteur de langue au **menu principal** + dans **Options**, persisté dans `settings.cfg`. Système via le `TranslationServer` de Godot (`localization/ui.csv`, helper `Loc.T`). **Tout traduit** : habillage UI, Codex (ennemis/armes/passifs/fusions), améliorations, persos, biomes, cartes de level-up, intro |
| **Refacto SOLID + tests unitaires** | ✅ Livré | Logique métier extraite dans `src/Core/Rules/` (9 classes pures sans Godot : courbe d'XP, Échos, scaling, difficulté, raretés, spawn, extrapolation d'arme, plafonds, tirage pondéré) — les nœuds délèguent (SRP). Fichiers multi-classes découpés. **Projet de tests xUnit `tests/` : 51 tests verts**. Validé non-régression |
| **Fix Vagabond + bonus méta** | ✅ Livré | La Lame Plasma (arme du Vagabond, mêlée) ne touchait jamais en kitant (l'arc suivait le déplacement) → vise désormais **l'ennemi le plus proche**. Bug secondaire : l'arme de départ ne recevait pas le **multiplicateur de dégâts méta** (instanciée avant les bonus) → corrigé |
| **3 améliorations UX/VFX** | ✅ Livré | Intro **skippable au clic souris** (racine `MouseFilter=Ignore`) ; **1er niveau présélectionné** dans le choix de biome ; **VFX Lame Plasma refondu** : croissant d'énergie tracé (`_Draw`) au lieu du nuage de carrés clignotants |
| **Support manette complet** | ✅ Livré | La map par défaut liait les directions à la manette mais pas la validation → ajout au boot de **A = valider**, **B = annuler**, **RB/LB = focus**, et action **`pause` = Start** (ouvre/ferme le menu de pause en jeu). Idempotent, clavier intact |
| **Durcissement mid/end & boss** | ✅ Livré | Le mid/end manquait de challenge : **`hpScalingPerMinute` ~+60 %** et **`damageScalingPerMinute` ~+50 %** sur tous les ennemis, densité de spawn relevée (cap `12+t·36`, vague `12+t·6`). **Boss final** : PV base **12000→18000** (·scaling 0.05→0.06, **~32 000 PV effectifs à 13 min Normal**), dégâts proj. 26→34, **sprite ×1.8→×2.4** (massif), salve **12→16** à 2.0 s |
| **Expansion P1 — biome Néon + spawn biome-aware** | ✅ Livré | **5ᵉ biome « Secteur Néon »** (grille néon magenta, ennemis +10 % rapides / +15 % XP) ; socle **spawn par biome** (`EnemySpawner` filtre par `CurrentBiomeId`, champ `biomes` optionnel dans `enemies.json`) prêt pour les ennemis/boss spécifiques |
| **Expansion P2 — refonte visuelle des arènes** | ✅ Livré | `BiomeAtmosphere` : **brume** (shader fbm), **rais de lumière** (god-rays additifs), **poussière en parallaxe** (2 couches décalées par la caméra) — thématisés par biome (Néon dramatique, Sanctuaire discret) |
| **Expansion P3 — 5 nouvelles armes (6 → 11)** | ✅ Livré | **Lame Boomerang** (revient, 2 hits), **Essaim Traqueur** (missiles homing), **Lance Cryo** (rayon perçant + ralentissement), **Jet de Pyre** (cône + brûlure DoT), **Singularité** (puits gravitationnel epic). Infra statut `EnemyBase` (slow/burn plafonnés, testés) |
| **Expansion P4 — power-ups temporaires** | ✅ Livré | 4 buffs ramassables à durée limitée : **Surcadence** (cadence ×1.6), **Furie** (+dégâts), **Égide** (invulnérabilité), **Célérité** (vitesse) ; apparition programmée, indicateur HUD de buff actif. Aucun power-creep permanent |
| **Expansion P5 — ennemis & boss par biome** | ✅ Livré | Socle biome-aware exploité : le **mid-boss varie selon le biome** (Revenant en Aether/Néon, Colosse ailleurs) |
| **Refonte fin de niveau** | ✅ Livré | **Survie sans fin** : à la fin du temps imparti la difficulté **escalade brutalement** (vagues + mini-boss + **boss en boucle**) ; **battre le boss = niveau TERMINÉ** (débloque le suivant) mais la run **continue** ; la run finit à la **mort**. **Déblocage progressif** des niveaux (Sanctuaire → Aether → Givre → Fournaise → Néon). **High score** = temps survécu max par niveau, **avec la difficulté** du record. **Arsenal à découverte** : armes non trouvées masquées **« ??? »** (sauf armes de signature). Bouton **« Tout réinitialiser »** dans les Options (Échos + progression) |
| **Boss de fin — phases & incarnations** | ✅ Livré | Le Noyau Rouillé combat en **3 phases** (100→66→33 % de PV : salves, ondes et signature qui se resserrent, **adds** en phase III, **1 s de surcharge** invulnérable et télégraphiée à chaque bascule) et prend une **incarnation par biome** — éventail dirigé (Sanctuaire), translocation (Aether), nova de givre (Givre), flaques de magma (Fournaise), faisceaux rotatifs (Néon) — avec sprite et nom propres. Nouvelle **barre de boss** au HUD (crans aux seuils, numéro de phase). PV et TTK inchangés |
| **Courbe de puissance & plafonds** | ✅ Livré | Les 4 passifs ne définissent que 3 niveaux pour un plafond de 20 : au-delà, le delta était réappliqué **sans borne** — le Capaciteur franchissait **100 % de réduction de recharge dès L8** (toutes les armes au plancher 0,15 s, cadence de fiche annulée) et le Noyau Thermique montait à ×4,00. Nouvelle règle pure `PassiveScaling` (rendements décroissants), plafond `MaxCooldownReduction = 0,75`, passifs saturés retirés du pool de cartes. Puissance sur 12 min d'overtime : **×6,42 → ×2,73**. Boss recalibré **8000 → 5000 PV** sur un TTK *joué*. Outillage : `PowerTelemetry` + `--power-curve` + `tools/power_curve_session.ps1` — `docs/GDD.md` §30 |
| **Fix — récompense de mini-boss & spam d'animation** | ✅ Livré | `(int)GD.Randi() % n` donnait un index **négatif une fois sur deux** : la carte offerte à la mort d'un mini-boss était **perdue une fois sur deux, sans aucun signe à l'écran** (exception avalée par le callback Godot). Et les 5 golems de biome, qui partagent la scène du Colosse, tentaient une animation `attack` absente de leurs sprites — 144 erreurs par session. `EnemyBase.PlayAnim` ne joue que si l'animation existe et **renvoie si elle a démarré** (le `QueueFree` de `death` en dépend) |
| **Fix scroll Codex** | ✅ Livré | Les écrans **Bestiaire** et **Arsenal** ne défilaient pas au clavier/manette (rangées non focalisables → le focus Godot ne scrollait pas) : `CodexScreenBase` pilote désormais le `ScrollContainer` à la main sur `ui_up`/`ui_down` (+ Page Up/Down) dans `_UnhandledInput` |

---

## Gameplay

- **Boucle run (survie sans fin)** : le décompte mène au **boss de fin de niveau** (~13 min). À la fin du temps imparti, la difficulté **escalade brutalement** (vagues massives + mini-boss + boss en boucle). **Battre le boss = niveau TERMINÉ** (débloque le suivant) mais la run **continue** ; elle finit à la **mort**. Le **temps survécu** est le **high score** du niveau (avec sa difficulté). Nuées denses façon Vampire Survivors (jusqu'à 300 ennemis), i-frames qui rendent les hordes jouables
- **Déblocage progressif** : 5 biomes (Sanctuaire → Aether → Givre → Fournaise → Néon) — chacun se débloque en terminant le précédent ; cartes verrouillées + record affiché. Effets gameplay propres par biome
- **Arsenal à découverte** : les armes non encore équipées sont masquées (« ??? ») dans l'Arsenal jusqu'à leur 1re découverte en partie (les armes de signature des persos restent toujours visibles)
- **4 personnages** : Chimera (cyborg), Titan-Gardien (robot), Vagabond (humain), Vecteur (cyborg de précision, arme dirigée) — chacun avec stats, arme de signature et aura propres
- **Montée de niveau** : choix entre 3 cartes (armes, passifs, fusions) à chaque level-up + restauration de 25% des HP max
- **Drops HP** : les ennemis droppent aléatoirement un orbe rouge (losange) qui restaure 15% des HP max au contact
- **Aimant** : un item (fer à cheval, halo cyan) apparaît jusqu'à 3 fois par run (dont une vers la fin) ; au contact, il aspire **toutes les orbes d'XP de l'arène** vers le joueur
- **Fusions** : atteindre le niveau max d'une arme + posséder le passif prérequis débloque une forme évoluée qui transforme visuellement et mécaniquement l'arme
- **Meta progression** : les Échos d'Aether gagnés en run s'investissent en améliorations permanentes (Hub)

### Ennemis (24 basiques + 3 mini-boss + 1 boss de fin)

> **28 ennemis basiques au total** depuis la refonte pseudo-3D (2026-07-03) : les 4 archétypes IA d'origine ci-dessous, déclinés en **20 variantes par biome** (4 par biome : Sanctuaire, Aether, Fournaise, Givre, Néon), chacune avec son propre sprite pseudo-3D. Liste complète consultable en jeu dans le **Bestiaire**.

| Ennemi | Rôle | HP | XP | Apparition |
|---|---|---|---|---|
| Essaim de Rouille | Fourrage — fonce en ligne droite | 20 | 3 🟢 | dès 0:00 |
| Drone Corrompu | Harceleur rapide — trajectoire erratique ±45° | 15 | 7 🔵 | dès 2:00 |
| Sentinelle Corrompue | Pression à distance — tire et recule | 45 | 20 🟣 | dès 5:00 |
| **Revenant d'Aether** *(mini-boss mi-temps)* | Poursuite rapide + ruades, aura violette — drop arme | 550 | 180 🟣 | dès 7:00 |
| Colosse Greffé | Bruiser lent, dégâts lourds + drop Noyau | 200 | 60 🟡 | dès 9:00 |
| **Rôdeur de Rouille** *(mini-boss)* | Araignée 64×64, très résistant — drop arme | 300 | 80 🟡 | dès 12:00 |
| **Le Noyau Rouillé** *(BOSS DE FIN)* | Salves radiales (16 proj.) + ondes de choc, **3 phases** et **1 incarnation par biome** — 3 Noyaux, niveau terminé | 12000¹ | 500 🟡 | dès 13:00 |
| **Sentinelle Maîtresse** *(mini-boss)* | Double tir ±12°, kiter — drop arme | 450 | 120 🟡 | dès 16:00 |

> ¹ **PV de base.** L'`EnemySpawner` applique un scaling temporel `PV = base × (1 + t_min × hpScaling) × difficulté` (boss `hpScaling = 0,06`). Le Noyau Rouillé arrivant à 13 min, son PV effectif est **≈21 400 en Normal** (≈17 100 Facile / ≈27 800 Difficile). Les **phases ne changent pas ses PV** : elles redistribuent l'intensité du combat (cf. `docs/GDD.md` §29). Idem pour les autres ennemis selon leur heure d'apparition (scalings relevés le 2026-06-29 pour durcir le mid/end).

### Armes & passifs (10 cartes + fusions)

**Actives** : Canon à Impulsions · Lame Plasma · Essaim de Drones · Champ de Surcharge · **Bobine Tesla** (éclair en chaîne) · **Volée Multiple** (tir multi-cible, +1 projectile/niveau)

**Passifs** : Noyau Thermique · Plaque Renforcée · Servo-Moteurs · Capaciteur

**Fusions MVP** :
- Lame Plasma (niv. 5) + Noyau Thermique → **Lame à Fusion** (anneau continu 55 dps)
- Canon à Impulsions (niv. 5) + Capaciteur → **Rail Surchargé** (rafale 3 projectiles perforants)

---

## Pile technique

| Outil | Version |
|---|---|
| Moteur | **Unity 6.5** (`6000.5.6f1`), URP 2D |
| Langage | **C#** |
| Cible | **Windows (.exe)** |
| Style graphique | Pixel art 32×32 px pseudo-3D avec ombres (import `Point`, `spritePixelsPerUnit = 1`) |
| Typographie | **Share Tech Mono** (OFL) — mono techno lisible (VT323 en réserve) |
| Langues | **Anglais (défaut) · Français · Espagnol** — `ui.csv` lu au runtime, choix persisté |

> Le jeu a été écrit sous **Godot 4.7 .NET** jusqu'à la 1.26.0, puis porté sous Unity (2.0.0). Le
> moteur Godot a été retiré du dépôt le **2026-08-10** ; sa documentation reste consultable sous
> `docs/archive-godot/`. Les entrées de phase ci-dessus antérieures à la 2.0.0 décrivent donc un
> code qui n'existe plus tel quel — leur **contenu de jeu**, lui, a été porté.

### Structure du projet

```
chimera-protocol/
├── unity/
│   ├── Assets/Scripts/
│   │   ├── Shared/Rules/        Logique PURE testable, sans dépendance moteur (45 classes)
│   │   ├── Shared/PlatformCore/ Socle déterministe : Pcg32, TimerWheel, Easing, TweenTimeline
│   │   ├── Platform/            Pont moteur : Spawner, AudioSystem, Loc, UiFrames, UserData…
│   │   ├── Gameplay/            Joueur, ennemis, armes (+ Fusions/), spawn, VFX, télémétrie
│   │   ├── UI/                  Écrans (menu, hub, codex, level-up, pause, options…)
│   │   └── Bench/               Banc headless : auto-play, smoke tests, tour de captures
│   ├── Assets/Editor/           Build, construction des SpriteFrames, réglages d'import
│   ├── Assets/Art/              Sprites sources, consommés par GUID (+ branding/icon.png)
│   ├── Assets/Resources/        Chargé par chemin à l'exécution : Ui, UiFrames, Audio, Vfx, Fonts…
│   └── Assets/StreamingAssets/  data/*.json (tuning) + localization/ui.csv
├── tests/                       xUnit — compile Shared/ par chemin (626 tests, aucun moteur requis)
├── tools/                       Générateurs d'assets, banc de mesure, audits, release (Python/PS)
└── docs/                        GDD.md, PITFALLS_UNITY.md, TEST_REPORT.md… + archive-godot/
```

---

## Lancer le projet

1. Installer **Unity 6.5** (`6000.5.6f1`) via Unity Hub
2. Ouvrir le dossier `unity/` comme projet
3. Ouvrir la scène de jeu et lancer

**Build Windows :**
```
Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBench.Windows64Game
```
> Produit `unity/Build/game/ChimeraProtocol.exe` (ignoré par git, régénéré).

**Tests** (aucun moteur nécessaire) :
```
dotnet test tests/ChimeraProtocol.Tests.csproj
```

---

## Backlog post-MVP

- [x] Personnages Humain et Robot jouables (Vagabond + Titan-Gardien) ✅
- [x] Mini-boss en run (Rôdeur de Rouille + Sentinelle Maîtresse + Revenant d'Aether) ✅
- [x] Boss de fin de run ("Le Noyau Rouillé") — désormais condition de victoire ✅
- [x] Biomes / arènes additionnelles (4 biomes + sélection de niveau) ✅
- [x] Fusions supplémentaires au-delà des 2 MVP (Essaim Orbital + Égide de Surcharge) ✅
- [ ] Support manette officiel (validation physique)
- [ ] Succès / intégration plateforme (itch.io, Steam)
- [ ] Publication sur itch.io

---

## Design document

Le GDD complet (pitch, univers, valeurs de tuning, direction artistique, décisions techniques) est dans [`docs/GDD.md`](docs/GDD.md).
