# Guide — lancer ce projet avec Claude Code CLI

Ce kit contient :

```
chimera-protocol/
├── CLAUDE.md                     # mémoire de projet, chargée automatiquement
├── docs/
│   └── GDD.md                    # game design document complet (la référence)
└── .claude/
    └── agents/                   # 7 subagents dédiés
        ├── game-designer.md
        ├── directeur-artistique.md
        ├── graphiste.md
        ├── developpeur.md
        ├── musicien.md
        ├── story-teller.md
        └── marketing.md
```

## 0. Installation

1. Place le dossier `chimera-protocol/` (ou son contenu) à la racine de ton dépôt de jeu — l'arbo
   `.claude/agents/` doit être directement sous la racine du projet pour être détectée
   automatiquement par Claude Code (subagents de scope "projet").
2. Lance `claude` dans ce dossier. `CLAUDE.md` se charge automatiquement.
3. Vérifie que les agents sont bien détectés : tape `/agents`, onglet **Library** → tu dois voir
   les 7 agents listés sous "Project".

Comme tu utilises déjà Claude Code dans Visual Studio 2026 via VsAgentic, tu peux aussi ouvrir ce
dossier comme projet dans cet environnement plutôt qu'en CLI pure — le fonctionnement des
subagents et de `CLAUDE.md` est identique.

## 1. Ordre de lancement recommandé

Le projet est volontairement séquencé pour éviter qu'un agent ne parte dans une direction qui
contredit un autre. Claude Code te posera probablement des questions à chaque phase pour affiner
des points laissés ouverts dans le GDD (notamment le choix de la pile technique en Phase 0, et la
confirmation du personnage du MVP) — réponds-y directement, les agents reporteront les décisions
dans `docs/GDD.md` et `CLAUDE.md`.

### Phase 0 — Cadrage technique
```
Lis docs/GDD.md en entier. Utilise l'agent developpeur pour trancher la pile technique
(section 15 du GDD) en tenant compte du fait que je suis développeur C# senior et que le jeu
doit tourner sur Windows. Fais-le valider par game-designer et directeur-artistique sur les
critères de pipeline sprite/animation et de vitesse d'itération, puis documente la décision dans
CLAUDE.md et le GDD.
```

### Phase 1 — Prototype de boucle cœur
```
Utilise l'agent developpeur pour mettre en place le projet vide et implémenter : déplacement du
personnage, une arène de test minimale avec collisions, spawn d'un seul type d'ennemi
(Essaim de Rouille) et une arme de base (Canon à Impulsions). Objectif : avoir quelque chose de
jouable au clavier le plus vite possible, sans art ni audio.
```

### Phase 2 — Systèmes de progression
```
Utilise game-designer pour spécifier précisément les 4 ennemis et les 6-8 power-ups du GDD
(valeurs, comportements, conditions de fusion), puis developpeur pour les implémenter, avec le
système de montée de niveau et l'écran de choix.
```

### Phase 3 — Monnaie meta et Hub
```
Utilise game-designer puis developpeur pour implémenter les Échos d'Aether : calcul de fin de
run, sauvegarde locale persistante, écran Hub avec arbre d'améliorations permanentes.
```

### Phase 4 — Habillage graphique et sonore
```
Utilise directeur-artistique pour finaliser le guide de style (GDD section 12), puis graphiste
pour produire/intégrer les sprites, animations et VFX, et musicien pour la musique et les SFX du
MVP. Utilise story-teller en parallèle pour les textes courts (descriptions d'armes, ambiance du
menu) et le splash screen.
```

### Phase 5 — Polish, packaging et com'
```
Utilise developpeur pour produire un build Windows autonome et vérifier la checklist de
Definition of Done du GDD section 13. Utilise marketing pour préparer le pitch et la liste de
captures à faire une fois le build stable.
```

## 2. Comment invoquer un agent précis

Trois façons, de la plus souple à la plus stricte :

- **Langage naturel** : `Utilise l'agent game-designer pour...` — Claude décide quand même de
  déléguer ou non.
- **Mention explicite** : tape `@` puis choisis l'agent dans la liste (ex. `@"game-designer
  (agent)" ...`) — garantit que cet agent précis traite la tâche.
- **Session entière sous un agent** : `claude --agent developpeur` si tu veux une session de
  travail dédiée uniquement au code, par exemple.

## 3. Bonnes pratiques pendant le développement

- À chaque fin de session significative, demande explicitement : *"Mets à jour `docs/GDD.md` et
  `CLAUDE.md` avec les décisions prises dans cette session."* — c'est ce qui permet aux sessions
  suivantes (et aux autres agents) de repartir avec le bon contexte.
- Si deux agents se contredisent (ex. `graphiste` propose un sprite illisible pour
  `directeur-artistique`), demande l'arbitrage explicitement plutôt que de trancher toi-même à
  l'aveugle — ce sont les rôles prévus pour ça.
- Commite `.claude/agents/` et `docs/GDD.md` dans Git dès le départ : ce sont des artefacts de
  projet à versionner au même titre que le code.
- N'hésite pas à enrichir un agent (son fichier `.md`) au fil du projet si tu remarques qu'il
  prend systématiquement de mauvaises décisions sur un point précis — c'est le mécanisme prévu
  pour capitaliser l'expérience.

## 4. Limites à avoir en tête

- `graphiste` et `musicien` ne produisent pas d'art/musique "à la main" : ils s'appuient sur de
  la génération procédurale en code, des assets libres de droits, ou un outil de génération
  externe si tu en connectes un. Voir le détail dans leurs fichiers d'agent respectifs.
- Le choix de moteur n'est pas fait dans ce kit : c'est volontaire (cf. consigne initiale), la
  Phase 0 sert exactement à ça.
