# Chimera Protocol — Page de store itch.io

> Document prêt à copier-coller. Audience FR d'abord. Reste strictement factuel : tout
> ce qui est listé ici existe dans le build (sources : `README.md`, `CLAUDE.md`).
> N'ajoute aucune feature non vérifiée avant publication.

---

## 1. Titre + tagline

**Titre :** Chimera Protocol

**Tagline (accroche courte) :**
> Survive à la Rouille Vivante, fusionne tes armes, abats Le Noyau Rouillé.

Variantes d'accroche (au choix selon le slot) :
- « Un survivor roguelite où tes armes évoluent — et ne se contentent pas de monter de niveau. »
- « Bullet-heaven cyberpunk : 4 biomes, 3 Arpenteurs, 1 boss à vaincre pour s'extraire. »

---

## 2. Description courte (liste itch, 1–2 phrases)

> Survivor roguelite vue du dessus dans un univers cyberpunk-fantasy rongé par la Rouille
> Vivante. Affronte des nuées d'ennemis, fais évoluer tes armes par fusion, et survis assez
> longtemps pour vaincre le boss final et t'extraire.

---

## 3. Description longue (corps de page)

### Le pitch

Dans un monde où magie et machine ont fusionné en une corruption nommée **la Rouille Vivante**,
tu incarnes un **Arpenteur** envoyé en mission d'extraction dans un Sanctuaire en ruines.
Chaque run est une descente : les hordes se densifient, le temps presse, et **un boss final
finit par émerger**. Survivre ne suffit plus — il faut le terrasser pour s'extraire.

**Chimera Protocol** prend la boucle addictive de Vampire Survivors (nuées, montée en
puissance, level-up rapides) et lui ajoute son propre crochet : **des armes qui se transforment
réellement**. Quand une arme atteint son apogée et rencontre le bon module passif, elle **fusionne**
en une forme évoluée — visuellement et mécaniquement différente. Ton build ne fait pas que grossir :
il mute.

### La boucle de jeu

- **Survivor roguelite vue du dessus** : déplace-toi, kite les nuées, tes armes tirent toutes seules.
- **Nuées denses façon bullet-heaven** : jusqu'à 300 ennemis à l'écran, spawn par lots et par vagues,
  avec des i-frames qui rendent les hordes jouables plutôt que punitives.
- **Level-up à 3 cartes** : à chaque montée de niveau, choisis entre 3 cartes (nouvelle arme, module
  passif, ou fusion) — et récupère 25 % de tes PV max au passage.
- **Fusions qui transforment l'arme** : amène une arme à son niveau max + possède le passif prérequis,
  et débloque sa forme évoluée. La Lame Plasma devient un anneau de fusion continu ; le Canon à
  Impulsions devient un rail perforant ; et d'autres encore.
- **Une vraie condition de victoire** : la run ne se gagne pas au chronomètre. Le timer est un décompte
  avant l'arrivée du **boss final, Le Noyau Rouillé** (~13 min). L'abattre déclenche l'**EXTRACTION
  RÉUSSIE** et marque le biome d'un badge **« VAINCU »**.

### Les points forts

- **3 personnages jouables**, chacun avec ses stats, son aura et son arme de signature :
  **Chimera** (cyborg, Canon à Impulsions), **Titan-Gardien** (robot lourd, Essaim de Drones),
  **Vagabond** (humain, Lame Plasma).
- **4 biomes avec effets de gameplay** (pas juste cosmétiques) :
  - *Sanctuaire Rouillé* — terrain neutre, pour apprendre la boucle.
  - *Friche d'Aether* — **+20 % d'XP**, montée en puissance accélérée.
  - *Fournaise* — **ennemis +18 % de vitesse**, pour les téméraires.
  - *Givre Cryogénique* — **ennemis −18 % de vitesse**, du répit pour kiter et viser.
- **Boss final = condition de victoire + badge « VAINCU »** persistant par biome et par difficulté.
- **Difficulté réglable** : Facile / Normal / Difficile (multiplicateurs de dégâts, de PV et de spawn).
- **Un bestiaire qui monte en intensité** : 4 ennemis de base, **3 mini-boss** (Revenant d'Aether,
  Rôdeur de Rouille, Sentinelle Maîtresse) et **1 boss de fin** (Le Noyau Rouillé).
- **Un arsenal qui se construit** : **8 armes**, **4 passifs** et **4 fusions** à combiner.
- **Méta-progression** : les Échos d'Aether gagnés en run s'investissent en améliorations permanentes
  au Hub.
- **Intro narrative skippable** : le lore raconté en 5 temps au lancement — sautable d'une touche.
- **Navigation manette ET clavier** sur tous les menus.
- **Esthétique cyberpunk / CRT** : pixel art, néons, scanlines, HUD minimal façon Cyberpunk 2077
  coloré selon le biome, screen shake, bloom et aberration chromatique sur les fusions.

### Codex intégré

Un **Bestiaire** (8 ennemis avec sprite animé, rôle et description) et un **Arsenal** (8 armes +
4 passifs, icônes et descriptions) sont consultables directement depuis le menu principal.

---

## 4. Univers

> *La Convergence a tout changé.* Quand la magie et la machine se sont mêlées, elles ont engendré
> **la Rouille Vivante** : une corruption mi-organique, mi-mécanique qui dévore le monde et réanime
> ses carcasses. Les **Sanctuaires**, derniers bastions, abritent encore des **Noyaux d'Aether** —
> condensés d'énergie pure, seul espoir de repousser la Rouille.
>
> Tu es un **Arpenteur** : un combattant fusionné homme/machine/magie, envoyé en mission
> d'extraction dans un Sanctuaire tombé. Ta tâche : survivre aux marées de Rouille, récolter les
> Noyaux, et abattre ce qui les garde avant de t'extraire. *« Un jour, ce sera toi. »*

Les évolutions de ton personnage racontent cette fusion : chaque arme qui mute, chaque module greffé
est une étape de plus dans la chimère que tu deviens.

---

## 5. Configuration requise

| | |
|---|---|
| **OS** | Windows 10 / 11 (64 bits) |
| **Architecture** | x86_64 |
| **Runtime** | Aucune installation requise — **.NET 8 embarqué** dans le build |
| **Moteur** | Godot 4.7 .NET (intégré au .exe) |
| **Disque** | Quelques centaines de Mo (build + runtime .NET) |
| **Contrôles** | Clavier ou manette |

> Le téléchargement contient `ChimeraProtocol.exe` **et** le dossier runtime
> `data_ChimeraProtocol_windows_x86_64/`. **Les deux sont nécessaires** : lancer le `.exe` seul
> sans son dossier `data_*` plante au démarrage. Zipper l'ensemble.

---

## 6. Tags itch.io suggérés

`roguelite` · `survivor` · `bullet-heaven` · `pixel-art` · `sci-fi` · `cyberpunk` · `action`
· `top-down` · `roguelike` · `singleplayer` · `arcade` · `2D` · `fantasy` · `controller`

Genre principal recommandé : **Action**. Made with : **Godot**.

---

## 7. Plan de captures / GIF

### Visuels EXISTANTS à réutiliser (dans `docs/`)

À placer dans cet ordre de priorité sur la page (les premiers sont les plus « vendeurs ») :

| Ordre | Fichier | Ce qu'il montre | Légende suggérée |
|---|---|---|---|
| 1 | `docs/swarm_review.png` | **Nuée dense en plein run** — ennemis colorés, projectiles, aura joueur, HUD en jeu | « Tiens jusqu'à 300 ennemis à l'écran. » |
| 2 | `docs/levelselect_vaincu.png` | Écran de **sélection de biome** avec les 4 arènes, leurs effets et les **badges « VAINCU »** | « 4 biomes, 4 règles. Conquiers-les tous. » |
| 3 | `docs/menu6_review.png` | **Menu principal** stylé (titre néon, Bestiaire/Arsenal/Options) | Image de couverture / vitrine. |
| 4 | `docs/boss_combat.png` | Écran **EXTRACTION RÉUSSIE** (victoire par boss, HUD thématisé Aether) | « Abats Le Noyau Rouillé pour t'extraire. » |
| 5 | `docs/bestiary_review.png` | **Bestiaire** animé (Essaim, Drone, Sentinelle, Revenant) | « Un codex qui monte en intensité. » |
| 6 | `docs/hud_fournaise.png` | **HUD thématisé par biome** (accent orange Fournaise, barres arrondies) | « Un HUD minimal, coloré selon ton arène. » |
| 7 | `docs/hud_anim.gif` | **GIF du HUD animé** (liseré qui respire, XP lerp, pop des Noyaux) | Animation de page (montre le « juice »). |

Visuels secondaires disponibles si besoin de remplir la galerie :
`docs/hud_biomechip.png`, `docs/hud_style.png`, `docs/levelsel.png`, `docs/options_review.png`,
`docs/pt_before.png` / `docs/pt_after.png` (avant/après équilibrage — plutôt pour devlog).

### Visuels MANQUANTS à capturer avant publication

Ces moments forts ne sont pas encore dans `docs/` et feraient la différence — à demander en brief
à `graphiste` / `developpeur` :

1. **GIF de gameplay « money shot »** (4–8 s) : kite au milieu d'une grosse vague, armes qui tirent,
   impacts et death bursts. C'est LE visuel le plus important d'une page de bullet-heaven — actuellement
   on n'a qu'une capture fixe (`swarm_review.png`).
2. **GIF / capture d'une FUSION** : le moment où l'arme se transforme (FusionFlash + aberration
   chromatique). C'est le différenciateur du jeu — il doit être montré en mouvement.
3. **Capture de combat de boss en cours** (Le Noyau Rouillé en train de tirer ses salves radiales) :
   `boss_combat.png` montre l'écran de victoire, pas l'affrontement lui-même.
4. **Capture d'un choix de level-up** (les 3 cartes avec icônes) : montre la mécanique de build.
5. **Trailer 30–60 s** (optionnel mais fortement recommandé) : intro lore (3 s) → montée en puissance
   → fusion spectaculaire → boss → « EXTRACTION RÉUSSIE ». Brief à transmettre à
   `graphiste` + `developpeur` + `musicien`.

> Format itch recommandé : bannière / cover **630×500 px**, captures en **16:9** (1280×720 ou plus),
> GIF < 3 Mo si possible pour le chargement de page.

---

## 8. Checklist de publication

- [ ] **Build à jour** exporté (`build/ChimeraProtocol.exe` + dossier `data_*` présents).
- [ ] **Vérifier le `.sln`** à la racine AVANT export (sinon le .exe crashe — assemblée C# omise).
- [ ] **Zipper l'ensemble** (`.exe` + `data_ChimeraProtocol_windows_x86_64/`) en `.zip`.
- [ ] **Tester le zip sur une machine/dossier propre** (hors environnement de dev) pour confirmer
      que le runtime embarqué suffit.
- [ ] **Retirer le tag « DEBUG »** du titre de fenêtre pour le build de release (les captures actuelles
      affichent « Chimera Protocol (DEBUG) » — re-capturer si on veut des visuels propres).
- [ ] **Renseigner le prix** : gratuit, « payez ce que vous voulez », ou prix fixe (à décider).
- [ ] **Remplir la page** : titre, tagline, descriptions courte + longue (sections 1–4 ci-dessus).
- [ ] **Uploader les visuels** (cover + galerie, section 7) ; ajouter le GIF/trailer dès qu'il existe.
- [ ] **Régler les métadonnées itch** : plateforme **Windows**, genre **Action**, « Made with Godot »,
      tags (section 6), classification du contenu.
- [ ] **Mentionner les crédits** : audio CC0 (Juhani Junkala — musiques ; Kenney.nl — SFX), polices
      OFL (Share Tech Mono / VT323 / Press Start 2P). Référencer `assets/audio/CREDITS.md` et
      `assets/fonts/CREDITS.md`.
- [ ] **Préciser les contrôles** sur la page (clavier + manette).
- [ ] **Configurer le téléchargement** comme exécutable Windows (case « This file will be played in
      the browser » décochée).
- [ ] **Relire** la page depuis un compte tiers / navigation privée avant de passer en public.
- [ ] **Plan de contenu de sortie** (à coordonner avec `marketing`) : annonce + 1 devlog + teaser/GIF.

---

*Document marketing — ne décrit que des fonctionnalités présentes dans le build au 2026-06-28.
Toute promesse nécessitant une feature non listée doit être signalée à `game-designer` avant d'être
ajoutée à la page.*
