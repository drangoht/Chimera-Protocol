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
