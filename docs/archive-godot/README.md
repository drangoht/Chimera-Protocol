# Archive — documents de l'ère Godot

Ces documents décrivent le jeu **tel qu'il était sous Godot 4.7 .NET**, moteur retiré du dépôt le
**2026-08-10**. Ils sont conservés parce qu'ils expliquent des décisions encore vivantes dans le
jeu Unity — mais **les chemins, les noms de fichiers et les API qu'ils citent n'existent plus**.

| Document | Ce qu'il garde de valable | Ce qui est périmé |
|---|---|---|
| `ARCHITECTURE.md` | le principe **logique pure / moteur**, le cycle de vie d'une run, les contrats `EnemyBase` / `Player` / `WeaponBase`, la persistance | l'arborescence `src/`, les singletons AutoLoad, tout ce qui touche à l'API Godot |
| `PITFALLS.md` | les pièges de **conception** (câblage d'une arme, d'un ennemi, d'un affixe ; règles de mixage ; lisibilité) | tous les pièges d'**API Godot** — leurs équivalents Unity sont dans `docs/PITFALLS_UNITY.md` |
| `EXPANSION_PLAN.md` | le plan d'expansion, **entièrement livré** | — |
| `LEVEL_PROGRESSION_PLAN.md` | le plan de progression des niveaux, **entièrement livré** | — |
| `WEB_EXPORT_ANALYSIS.md` | le raisonnement sur la faisabilité web | son verdict portait sur l'export Godot .NET — **à refaire pour Unity**, dont le WebGL a d'autres contraintes |

**Documents vivants correspondants** : `docs/PITFALLS_UNITY.md` (pièges), `docs/GDD.md` (design),
`docs/PROJECT_STATE.md` (état), `docs/UNITY_MIGRATION_PLAN.md` (architecture du portage).
