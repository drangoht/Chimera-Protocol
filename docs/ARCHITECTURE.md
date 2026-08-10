# ARCHITECTURE.md — déplacé

Ce document décrivait l'organisation du code **sous Godot** (arborescence `src/`, singletons AutoLoad,
API GodotSharp). Le moteur a été retiré du dépôt le **2026-08-10** ; le document est conservé tel quel
sous **`docs/archive-godot/ARCHITECTURE.md`**.

- **Architecture en vigueur (Unity)** → **`docs/UNITY_MIGRATION_PLAN.md`** (principe logique
  pure / moteur, ponts `Platform/`, contrats d'entités, cycle de vie d'une run)
- **Où se trouve quoi** → skill **`/carte-projet`**
- **Pièges** → `docs/PITFALLS_UNITY.md`

Ce qui reste vrai de l'ancien document : le **principe** logique-pure/moteur (les règles chiffrées
vivent dans des classes statiques sans dépendance moteur, les nœuds délèguent) — c'est précisément ce
qui a rendu le portage possible, et le découpage a survécu tel quel sous `unity/Assets/Scripts/Shared/`.
