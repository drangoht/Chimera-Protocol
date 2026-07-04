# Analyse — Version Web (HTML5/WebAssembly) de Chimera Protocol

> Branche : `feat/web-export`. Document d'analyse, **aucune modification du build existant**.
> État vérifié : **juillet 2026** (Godot 4.7 .NET, projet en C# / .NET 8).

## TL;DR — Verdict

**Non faisable proprement en l'état.** Chimera Protocol est écrit **100 % en C# (.NET)**, et
l'export Web de la variante **.NET de Godot 4.x n'est pas supporté officiellement**. Preuve
locale directe : les templates d'export installés (`4.7.stable.mono/`) ne contiennent **aucune
cible `web`/`wasm`** — uniquement Android, iOS, Linux, macOS, Windows.

Trois voies existent, toutes avec un coût ou un risque notable :
1. **Attendre le support officiel** (.NET web, PR en cours, viserait Godot 4.6+/ultérieur) — 0 effort, échéance incertaine ;
2. **Fork community expérimental** (`ComplexRobot/godot-dotnet-web-export`) — faisable *aujourd'hui* mais non officiel, migration .NET 8→9 requise, limitations ;
3. **Portage GDScript** — le seul chemin « web natif garanti », mais réécriture massive, hors de proportion.

**Recommandation : option 1 (suivre/attendre), avec option 2 en preuve de concept optionnelle.**
Garder Windows comme cible principale ; ne pas s'engager sur une 2ᵉ base de code.

---

## 1. Pourquoi c'est bloqué

Godot a **deux éditions** :
- **Standard (GDScript)** → exporte parfaitement en Web (WebAssembly + WebGL2), c'est un cas d'usage courant.
- **.NET (C#/Mono)** → **pas d'export Web** en 4.x. Le runtime .NET n'est pas (encore) embarqué de façon
  stable dans la cible WebAssembly du moteur.

Notre projet est **entièrement en C#** (Godot 4.7 **.NET**, .NET 8, GodotSharp) : ~160 fichiers
`.cs` (Systems, UI, Weapons, Entities, Core/Rules). Il ne peut donc pas emprunter le chemin Web
« standard » sans être réécrit.

## 2. État vérifié (juillet 2026)

| Élément | Constat |
|---|---|
| Templates locaux `4.7.stable.mono/` | **Pas de template `web`** (android/ios/linux/macos/windows uniquement) |
| Doc officielle Godot | Export Web **non supporté** pour les projets C# en 4.x |
| PR officielle [#106125](https://github.com/godotengine/godot/pull/106125) (raulsntos) | **En draft / WIP, non mergée.** Vise 4.6+ au plus tôt, cible .NET 9/10 |
| Limitations connues de cette PR | Globalization désactivée (mode invariant forcé), certaines API BCL (crypto…) non fonctionnelles, **modifs manuelles du `.js` exporté requises** |
| Approche long terme discutée | Solution basée sur `libgodot` (encore en exploration) |

Conclusion : le support **arrive**, mais n'est **ni prêt ni présent** dans notre Godot 4.7 .NET actuel.

## 3. Options détaillées

### Option 1 — Attendre le support officiel .NET Web  ⭐ recommandé
- **Faisabilité** : garantie à terme, échéance **incertaine** (post-4.6, dépend de la stabilisation .NET 9/10 wasm).
- **Effort maintenant** : nul. Surveiller la PR #106125 / issue #70796 et les notes de version.
- **Risque** : limitations annoncées (i18n, crypto) — à revérifier au moment venu.
- **Action** : aucune modification ; ré-évaluer à chaque montée de version de Godot.

### Option 2 — Fork community `ComplexRobot/godot-dotnet-web-export`  (POC faisable aujourd'hui)
Éditeur Godot **custom** (binaire pré-buildé Windows) intégrant la PR raulsntos + patchs, avec
export Web C# expérimental. Maintenu (release ~juin 2026, 22 releases).
- **Faisabilité** : réelle mais **expérimentale / non officielle**.
- **Prérequis / effort** :
  - migrer le projet de **.NET 8 → .NET 9** (`<TargetFramework>net9.0</TargetFramework>`) ;
  - ajouter un `Program.cs` avec au moins une instruction top-level ;
  - installer le workload **`wasm-tools`** (`dotnet workload install wasm-tools`) ;
  - lancer l'`install.bat` du fork (configure templates + NuGet) ;
  - **ne PAS** utiliser ce fork pour le build Windows officiel (rester sur l'éditeur stable pour ça).
- **Limitations** : pas de GDExtension (OK, on n'en utilise pas), **globalization non fonctionnelle**,
  crypto désactivée, dépend d'un tiers.
- **Risque** : instabilité, divergence possible avec le futur support officiel, maintenance d'un
  environnement d'export à part. Convient à une **preuve de concept**, pas à une release durable.

### Option 3 — Portage GDScript (2ᵉ base de code)
- **Faisabilité** : technique garantie (GDScript exporte en Web nativement).
- **Effort** : **prohibitif** — réécrire ~160 fichiers C# (armes, ennemis, systèmes, UI, méta) en
  GDScript, puis maintenir **deux** implémentations en parallèle.
- **Verdict** : disproportionné pour l'objectif « ne pas remplacer l'existant ». **Écarté.**

## 4. Impacts spécifiques à Chimera Protocol (si Web tenté via option 2)

- **Localisation EN/FR/ES** : les traductions passent par `TranslationServer` (natif moteur, a priori
  OK), mais `Loc.T(key, args)` s'appuie sur `string.Format` (culture .NET). En **mode invariant**,
  le formatage reste fonctionnel mais séparateurs/nombres peuvent différer — **à valider**.
- **Sauvegarde** (`user://save.json`, `System.Text.Json`) : le stockage web passe par IndexedDB
  (persistant navigateur) — comportement à vérifier, pas de crypto requise chez nous (favorable).
- **Poids de téléchargement** : runtime Mono-wasm + `.pck` (640 PNG + audio OGG) = plusieurs
  dizaines/centaines de Mo → temps de chargement navigateur non négligeable ; envisager de la
  compression / le retrait d'assets inutiles pour le web.
- **Performance** : cible 200–300 entités, I-frames 0.45 s critiques. WebGL2 + wasm est **plus lent**
  que le natif → **playtest obligatoire** (nuées = pire cas).
- **Audio** : OGG, supporté nativement côté web (favorable).
- **Bandeau de mise à jour** : sans objet sur le web (page toujours à jour) ; masquer le check si
  plateforme = web pour éviter un faux positif.
- **itch.io** : upload « HTML5 » jouable dans le navigateur (option « This file will be played in the
  browser ») ; cohabite avec l'upload Windows existant sans le remplacer.

## 5. Recommandation

1. **Court terme** : ne rien changer au pipeline Windows. Conserver cette branche `feat/web-export`
   comme espace de veille/POC.
2. **Veille** : suivre PR [#106125](https://github.com/godotengine/godot/pull/106125) et issue
   [#70796](https://github.com/godotengine/godot/issues/70796) ; ré-évaluer à chaque release Godot
   (le jour où un template `web` apparaît dans la variante .NET, l'export devient trivial).
3. **Optionnel** : monter une **preuve de concept** isolée avec le fork ComplexRobot (option 2) sur
   cette branche pour mesurer poids, perfs et i18n **sans toucher** au projet principal — décision à
   valider avec l'utilisateur avant d'y investir (migration .NET 9 + environnement d'export séparé).

## Sources
- [Exporting for the Web — Godot Engine docs](https://docs.godotengine.org/en/stable/tutorials/export/exporting_for_web.html)
- [Issue #70796 — Readd web export for the C# (.NET) engine](https://github.com/godotengine/godot/issues/70796)
- [PR #106125 — [.NET] Add web export support (raulsntos)](https://github.com/godotengine/godot/pull/106125)
- [ComplexRobot/godot-dotnet-web-export (fork community)](https://github.com/ComplexRobot/godot-dotnet-web-export)
- [Godot Standard vs .NET Edition — export platform differences](https://knightli.com/en/2026/06/19/godot-standard-vs-dotnet-edition/)
- [Discussion #13076 — Godot 4.6 with .NET 10 & C# Web support](https://github.com/godotengine/godot-proposals/discussions/13076)
