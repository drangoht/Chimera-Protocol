# YouTube — trailer (English)

> Copie prête à coller pour la mise en ligne du trailer anglais
> (`trailer/ChimeraProtocol_trailer_EN_1440p.mp4`, 2560×1440 @60 fps, ~55 s).
> **Strictement factuel : tout ce qui est annoncé existe et a été compté dans le build 2.5.0.**
> Version française de la page store : `docs/ITCH_STORE_PAGE.md` — anglaise : `docs/ITCH_STORE_PAGE_EN.md`.

---

## ⚠ Ce que la refonte du 2026-08-22 a corrigé

La description précédente datait de la **1.17.0**, sous Godot, et **trois de ses affirmations étaient
devenues fausses** — dont une qui l'était déjà à l'écriture :

| Annoncé avant | Réalité vérifiée dans les données | Où |
|---|---|---|
| « 4 playable characters » | ⚠ **Faux à l'écriture, redevenu VRAI cinq heures plus tard** — voir l'encadré 2.5.0 ci-dessous. | `Rules/Characters` |
| « 28 base enemies … 3 mini-bosses » | **24 ennemis de base, 6 mini-boss, 1 boss final** (31 entrées distinctes ; les 20 de `enemies_biome_expansion.json` sont **déjà** dans `enemies.json`, chevauchement total) | `data/enemies.json` |
| « Made solo with Godot 4.7 .NET » | **Unity 6.5** — le dépôt est mono-moteur depuis le 2026-08-10 | `CLAUDE.md` |
| « ENDLESS ESCALATION … your survival time is the score » | À moitié faux depuis la 2.4.0 : la run **a une fin garantie**. Le temps de survie reste le score, mais il est désormais **borné**. | GDD §38 |

▶ **Un trailer qui promet un contenu absent se paie en remboursements**, et une description se
périme sans que rien ne le signale. Tout chiffre ci-dessous est compté dans les fichiers de données,
pas recopié de la version précédente.

---

## ⚠ Puis la 2.5.0 a inversé la première ligne du tableau (2026-08-22, 14:17)

La refonte ci-dessus a **retiré** « 4 playable characters » à 12:56 parce que l'écran n'existait pas.
Il a été porté **le même jour**, cinq heures plus tard, et publié en 2.5.0 sur les deux canaux.
La description est donc désormais fausse **par défaut** : elle sous-annonce un contenu réel.

| Élément | État | Conséquence |
|---|---|---|
| **Les 4 profils jouables** | ✅ Existent (`Rules/Characters`, `UI/CharacterSelectScreen`, sur le chemin de « Jouer ») | Ligne à **remettre** dans FEATURES (faite ci-dessous) |
| Rushes de gameplay | ✅ Toujours valides | Le personnage par défaut est la Chimère, dont les valeurs sont **exactement** celles codées en dur avant elle — aucune image ne change |
| **La vidéo montée** (`ChimeraProtocol_trailer_EN_1440p.mp4`, 12:56) | ⚠ **Antérieure aux personnages** | Aucun plan de l'écran de choix ; le carton `CONTENT` dit « 5 BIOMES · 12 WEAPONS · 9 FUSIONS » — **vrai mais incomplet** |
| Prise `charsel` | ✅ Existe déjà (`record_trailer.py`, 5 s) | Elle est déclarée **prise de contrôle**, non montée : une seule prise à recapturer suffit pour l'ajouter au montage |
| Les 12 armes | ✅ Inchangé | Les 4 armes de signature (`impulse_cannon`, `drone_swarm`, `plasma_blade`, `vector_lance`) **font partie** des 12 — le chiffre reste juste |

▶ **Le piège n'est pas symétrique** : une description qui promet trop se paie en remboursements,
une description qui promet trop peu se paie en clics jamais faits. Les deux se périment en silence.

---

## 1. Titre de la vidéo

**Recommandé (≤ 60 caractères, mot-clé en tête) :**

```
Chimera Protocol — Survivor Roguelite Where Every Run Ends
```

Variantes selon l'angle voulu :
- `Chimera Protocol — Don't Kill the Monsters. Become Them.` (accroche d'origine, moins « searchable »)
- `Chimera Protocol — Play Free in Your Browser | Survivor Roguelite` (met en avant le zéro friction)

---

## 2. Description (à coller telle quelle)

```
Don't kill the monsters. Become them.

Chimera Protocol is a top-down survivor roguelite set in a cyberpunk-fantasy world eaten by the Living Rust. Kite endless swarms, level up every few seconds, and turn the creatures hunting you into parts of your own body — until the arena itself closes in and ends the run.

▶ Play free in your browser, no download: https://drangoht.itch.io/chimera-protocol

━━━━━━━━━━━━━━━━━━━━━━
WHAT MAKES IT DIFFERENT

• ASSIMILATION — every kill fills an assimilation gauge tied to that enemy's archetype. Fill it and you graft a piece of the creature onto yourself: 5 grafts, 3 graft fusions, 13 gauges, 4 slots. Your build doesn't just grow, it mutates — and your character visibly changes shape.
• WEAPON FUSIONS — max out a weapon while holding the right passive module and it transforms into an evolved form: 12 weapons, 4 passives, 9 fusions.
• THE RUST TIDE — past the time limit, rust starts eating the arena from every side, and eleven minutes later there is no safe ground left. It walks through your invincibility frames and scales with your max health, so no build outruns it. Every run has a finish line: the challenge is how long you hold.

━━━━━━━━━━━━━━━━━━━━━━
FEATURES

• 4 playable characters, each with its own health, speed and signature weapon — Chimera (balanced cyborg), Titan-Guardian (heavy robot), Vagabond (fast, fragile human), Vector (precision cyborg)
• 5 biomes that change the rules — bonus XP, faster or slower enemies, risk/reward
• 24 base enemies with per-biome fauna, 5 elite affixes, 6 mini-bosses, 1 final boss
• 13 challenges unlocking Echoes, starting perks and cosmetic titles
• Permanent meta-progression at the Hub: 19 upgrades bought with Aether Echoes
• Rust Saturation — 6 endgame ranks, each removing a certainty rather than adding a multiplier
• Adaptive industrial metal soundtrack that shifts between calm, combat and boss
• Pseudo-3D shaded pixel art, per-biome atmosphere, CRT/neon presentation
• Play in a browser, or download for Windows — same game, same content
• Touch controls on phones: floating stick, auto-aim, landscape
• Keyboard or gamepad, fully rebindable movement keys
• Fully localized in English, French and Spanish
• Free, no ads, no microtransactions

━━━━━━━━━━━━━━━━━━━━━━
THE SETTING

Two centuries ago, the world's networks were linked to the Aether — the magical energy buried in the depths. The Convergence was neither war nor explosion, but a fusion: machines ceased to be tools. From that corruption was born the Living Rust. It does not destroy — it integrates. It transforms.

You are a Walker, sent down into a fallen Sanctuary to extract its Aether Core. Someone has to descend. It will be you.

━━━━━━━━━━━━━━━━━━━━━━
LINKS

Play free (browser or Windows): https://drangoht.itch.io/chimera-protocol
Source: https://github.com/drangoht/Chimera-Protocol

Made solo with Unity 6.5. Sound effects by Kenney (CC0). Soundtrack generated with Suno.

#indiegame #roguelite #bullethell #pixelart #survivorslike
```

---

## 3. Tags (champ « Tags » de YouTube, ≤ 500 caractères)

```
chimera protocol, survivor roguelite, bullet heaven, vampire survivors like, indie game trailer, pixel art game, cyberpunk roguelite, top down shooter, free browser game, free pc game, unity engine, roguelite 2026, horde survival, indie dev, bullet hell, itch io game
```

---

## 4. Réglages de mise en ligne

- **Miniature** : plan large de la Marée de Rouille (l'arène refermée) — c'est l'image que le jeu
  n'avait pas avant, et la seule qui ne ressemble pas à un autre survivor.
- **Résolution** : 2560×1440 @60 fps (upscale ×2 en NEAREST depuis 1280×720 — facteur **entier**,
  donc pixel art net ; un 1080p imposerait un ×1,5 qui baverait).
- **Langue** : cartons anglais montés sur rushes anglais. ⚠ `--lang` doit porter la même valeur dans
  `record_trailer.py` **et** `build_trailer.py` : le texte affiché par le jeu lui-même (narration,
  bannières, cartes) est gravé dans les rushes.
- **Fin de vidéo** : lien itch.io en écran de fin, pointant la page du jeu.

---

## 5. Regénérer le trailer

```bash
py tools/record_trailer.py --all      # ~22 min, la fenêtre du jeu doit rester au premier plan
py tools/build_trailer.py             # montage EN
py tools/build_trailer.py --lang=fr   # montage FR (rushes FR requis)
```

**Ajouter les personnages au montage sans tout recapturer** (situation du 2026-08-22) : les rushes de
gameplay restent valides — le personnage par défaut est la Chimère, aux valeurs inchangées — donc une
**seule** prise manque.

```bash
py tools/record_trailer.py charsel    # ~1 min, une seule ouverture du jeu
py tools/build_trailer.py             # remontage complet (quelques minutes, rushes réutilisés)
```

Puis dans `build_trailer.py` : carton `CONTENT` → `4 CHARACTERS · 5 BIOMES · 12 WEAPONS · 9 FUSIONS`
(⚠ vérifier sur planche qu'il ne déborde pas du cadre à 1440p), et **un** plan de `charsel` en
section F. ▶ Préférer un **échange** à un ajout : la section F compte déjà trois écrans de menu après
le boss et la Marée, ce que le commentaire de l'EDL décrit comme « la limite ». Quatre silhouettes à
choisir se lit mieux qu'une carte des niveaux.

⚠ **La vidéo actuelle (12:56) est antérieure au portage des personnages (14:17)** : la republier
telle quelle ne ment sur rien, mais laisse hors champ un contenu fini et traduit en trois langues.

⚠ **Recapturer après tout changement visuel du jeu.** Les rushes du 2026-08-11 montraient une marée
qui n'existait pas encore ; ceux d'avant le 2026-08-22 montraient une marée **rectangulaire**. Un
rush est daté, et rien dans le montage ne le signale.

⚠ **Ne pas lancer la capture juste après un build sans vérifier le tampon** : la date d'un fichier
de build ne prouve rien sous Unity, seul son contenu tranche (`unity/Build/game/build_stamp.json`).
Une tournée entière a déjà filmé l'ancien binaire.
