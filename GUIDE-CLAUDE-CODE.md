# Guide — l'équipe d'agents de Chimera Protocol

Comment sont organisés les agents et les skills du projet, et quand invoquer lequel.

> Ce fichier décrivait à l'origine un **kit de démarrage** (installation, « phases 0 à 5 », choix du
> moteur à trancher). Tout cela est fait depuis longtemps — le jeu est publié en 1.25.x. Réécrit le
> 2026-08-02 pour décrire l'équipe telle qu'elle sert **aujourd'hui**.

## Les 9 agents (`.claude/agents/`)

| Agent | Quand l'invoquer | Modèle |
|---|---|---|
| **`developpeur`** | Code, architecture, build, tests | opus |
| **`game-designer`** | Design, équilibrage, valeurs de tuning, scope | opus |
| **`game-tester`** | Après toute implémentation majeure — joue et documente | opus |
| **`release-manager`** | Publier une version de bout en bout + rédiger le devlog | sonnet |
| **`directeur-artistique`** | Identité visuelle, cohérence, briefs graphiques | sonnet |
| **`graphiste`** | Sprites, VFX, icônes — via les générateurs Python | sonnet |
| **`musicien`** | Musique, SFX, mixage, pipeline audio | sonnet |
| **`story-teller`** | Lore, textes en jeu, noms, descriptions | sonnet |
| **`marketing`** | Page itch, pitch, briefs de captures | sonnet |

## Les 2 skills (`.claude/skills/`)

- **`/carte-projet`** — index du code : où vit tel système, écran, arme, ennemi, donnée, outil, plus
  les checklists de câblage. **À invoquer avant toute exploration** plutôt que Glob/Grep à froid.
- **`/publier-itch`** — la procédure de publication en version courte (l'agent `release-manager` fait
  la même chose de bout en bout, devlog compris).

## Comment ça s'enchaîne réellement

Le projet n'avance plus par « phases » mais par **chantiers**, et un chantier suit presque toujours
la même forme :

```
constat (session jouée ou mesure)
   → game-designer  : diagnostic + règle proposée, reportée dans le GDD
   → developpeur    : implémentation + tests (logique pure dans src/Core/Rules/)
   → banc de mesure : tools/power_curve_multi.py puis power_loop.py --paired
   → game-tester    : ce que la mesure ne peut pas dire — le ressenti
   → release-manager: publication + devlog
```

**L'ordre compte.** Le raccourci « implémenter puis mesurer après coup » a coûté plusieurs
allers-retours : un cran de difficulté a été publié sans avoir jamais été joué, et le testeur n'a
rien senti.

## Les trois règles apprises à la dure

1. **Une run isolée ne tranche rien.** La variance inter-run atteint un facteur 2,4 avant même que
   le réglage testé n'agisse. Un verdict d'équilibrage se prend au **banc apparié**, sur le test des
   signes.
2. **Le banc ne dit pas ce qui se *sent*.** Il mesure la pression que le contenu exerce, pas
   l'expérience. Les deux se sont déjà contredits — le testeur avait raison.
3. **Quand un correctif ne déplace pas la métrique, suspecte l'instrument.** Continuer à doser est
   la manière la plus coûteuse de se tromper.

## Le LLM local (LM Studio) — à quoi il sert vraiment

Un serveur MCP `local-llm` expose **qwen3-coder-30b** tournant en local. Il est enregistré en scope
*user*, donc disponible dans tous les projets, et démarre seul avec Claude Code.

**Le levier n'est pas le coût du modèle, c'est le contexte** : le serveur lit les fichiers **chez
lui** et ne renvoie que la réponse. Mesuré le 2026-08-02 sur `docs/TEST_REPORT.md` :
**83 173 tokens lus en local → 675 renvoyés**, soit ~82 500 tokens de contexte cloud jamais consommés.

**Pourquoi il n'a servi à rien pendant des mois** — et c'est instructif : les agents déclarent une
liste `tools:` **fermée**, qui n'incluait aucun outil MCP. Ils ne *pouvaient pas* l'appeler, quelle
qu'ait été la consigne dans `CLAUDE.md`. Corrigé le 2026-08-02 : `game-designer`, `game-tester`,
`developpeur`, `story-teller` et `marketing` déclarent désormais `mcp__local-llm__local_digest`
(et `local_map` pour les quatre premiers). *Une capacité qu'on documente sans la câbler n'existe pas.*

**Quand l'utiliser** — uniquement quand un fichier est trop gros pour être lu :

| Fichier | Taille | Question type |
|---|---|---|
| `docs/TEST_REPORT.md` | ~290 Ko | « cette question a-t-elle déjà été mesurée ? » |
| `docs/GDD.md` | ~200 Ko | « qu'est-ce qui est acté sur ce système ? » |
| `docs/DEVLOG.md` | ~93 Ko | « qu'est-ce qui est réellement sorti ? » |
| `docs/PITFALLS.md` | ~90 Ko | « quels pièges sur ce domaine ? » |
| `localization/ui.csv` | ~72 Ko | audit de cohérence EN/FR/ES |
| `data/*.json` | — | inventaire transverse |

**Trois garde-fous, appris par la mesure :**

1. **Lent — ~6-7 min pour 290 Ko** (13,5 tok/s, map-reduce en 6 appels). L'appel bascule seul en
   tâche de fond après 120 s : lance-le **avant** ce que tu allais faire, pas à la place.
2. **`max_tokens` trop bas tronque la réponse sans lever d'erreur.** 900 a été insuffisant pour un
   inventaire ; viser 1500-2500.
3. **Bon sur du texte, à proscrire sur des chiffres.** Pour `power_curve.log` (1 Mo),
   `tools/power_loop.py` calcule médianes et tests de signes sans se tromper ; un LLM qui « lit » un
   CSV de mesures produit des nombres plausibles et faux. **S'il existe un outil déterministe, il
   gagne.** Jamais non plus pour du code à éditer (il faut le contenu réel), ni pour localiser
   (`Grep` est instantané et exact).

Diagnostic en cas de souci : `mcp__local-llm__local_status` (modèle chargé, taille de contexte).

## Documentation — qui répond à quoi

| Question | Document |
|---|---|
| Phase courante, conventions | `CLAUDE.md` (chargé automatiquement) |
| *Pourquoi* le jeu est réglé ainsi | `docs/GDD.md` |
| *Comment* le code est organisé | `docs/ARCHITECTURE.md` |
| *Où* se trouve quoi | skill `/carte-projet` |
| Quels pièges guettent | `docs/PITFALLS.md` |
| Ce qui a été mesuré | `docs/TEST_REPORT.md` |
| État d'implémentation détaillé | `docs/PROJECT_STATE.md` |
| Publier | `docs/RELEASE.md` + `/publier-itch` |

## Faire évoluer un agent

Si un agent prend systématiquement une mauvaise décision sur un point, **enrichis son fichier `.md`**
— c'est le mécanisme prévu pour capitaliser l'expérience, et c'est moins coûteux que de le corriger
à chaque session. Les fichiers `.claude/` sont versionnés au même titre que le code.

⚠ **Un agent qui décrit un état périmé du projet est pire qu'un agent absent** : il donne des
instructions fausses avec autorité. Quand une phase se termine, relis les agents qu'elle concerne.
