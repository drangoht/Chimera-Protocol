# BRIEF DA — POLISH VISUEL "NEXT-LEVEL" — CHIMERA PROTOCOL
## Auteur : directeur-artistique — 2026-06-24

> Document de référence pour les agents `developpeur` et `graphiste` sur la phase de polish
> visuel post-MVP. Toutes les décisions de ce brief sont actées dans `docs/GDD.md` §12.
> Références permanentes : `docs/GDD.md` §3 §10 §12, `docs/STYLE_GUIDE.md`,
> `docs/ARENA_DA_BRIEF.md`, `CLAUDE.md` (décisions implémentation).
>
> PRINCIPE NON NÉGOCIABLE (rappel GDD §10) : "Lisibilité prioritaire sur la densité — le joueur
> doit toujours distinguer son personnage, les ennemis et les projectiles dans le chaos visuel
> d'une run avancée." Tout effet qui viole ce principe est refusé, indépendamment de son
> esthétique.

---

## 0. Diagnostic de l'état actuel et vision cible

### Ce qui fonctionne déjà

L'état actuel (2026-06-23) pose de bonnes bases :
- Opposition "matière morte / énergie vivante" lisible via la palette (GDD §12, STYLE_GUIDE §0)
- Bloom WorldEnvironment actif — les éléments Aether brillent correctement
- PointLight2D sur joueur (cyan #44FFEE), balles (bleu plasma / rouge-orange), geysers
- Fond assombri à 42% + overlay dark blue-noir ZIndex=-7 — entités plus lisibles qu'avant
- Death burst, impact burst, trail XP, particules ambiantes Aether aux bords

### Ce qui manque pour "impressionner"

Les jeux de référence cités (Neon Abyss, Synthetik, Geometry Wars, Hades, Returnal) ont
en commun : **le sol réagit**, **chaque mort est un événement**, **le joueur sent les impacts**.

Trois axes à traiter :
1. **Sol mort** : la grille de tiles fixes n'a aucune vie propre. Elle doit suggérer une
   technologie ancienne qui respire encore — sans concurrencer les entités dynamiques.
2. **Impacts sans poids** : tuer un ennemi ordinaire est visuellemement silencieux. Tuer
   le Colosse devrait faire trembler l'écran et exploser la zone.
3. **Joueur sans présence physique** : le joueur se déplace comme un curseur. Un trail
   de mouvement et une réaction de l'environnement à sa présence le rendent "incarné".

### Palette étendue pour le polish (sans modifier la palette de base STYLE_GUIDE §1)

Les couleurs suivantes s'ajoutent exclusivement aux shaders et VFX temporaires — jamais
sur des sprites permanents :

| Usage | Hex | Notes |
|---|---|---|
| Grille holographique sol | `#00A0BB` | Déjà autorisé STYLE_GUIDE §7.3, opacité 0.06 max |
| Shockwave ring Colosse | `#44FFEE` → transparent | Même que PointLight2D joueur |
| Vignette écran | `#000005` | Noir quasi-pur, pas de teinte chromatique sur la vignette |
| Chromatic aberration | décalage RGB | Pas une couleur : décalage des canaux ±4 px |
| Trail joueur | `#44FFEE` 15% opacité | Même cyan que PointLight2D joueur, très transparent |

---

## 1. Vision arène redesignée

### Concept visuel

L'arène reste "Le Sanctuaire en Ruines" (lore GDD §3 : architecture magique pré-Convergence
envahie par la Rouille Vivante). Ce qui change : **le Sanctuaire n'est pas complètement mort**.
L'Aether qui imprègne ses fondations filtre à travers les fissures du sol sous forme de
circuits lumineux quasi-imperceptibles — l'infrastructure de la pré-Convergence, visible
en palimpseste sous les ruines.

Ce concept justifie visuellement le shader de grille holographique (§2.1) sans rupture
de lore : ce ne sont pas des néons décoratifs, ce sont des vestiges du réseau Aether
qui animait ce lieu avant la Convergence.

### Ce qu'on supprime du fouillis actuel

- Aucun sprite supprimé — les changements sont appliqués via modulation et shader
- La distribution des tiles est déjà optimisée (72/18/5/4/1% — GDD §10 Phase 4) : NE PAS
  la modifier. C'est le bon équilibre.
- Le `Polygon2D` dark overlay ZIndex=-7 `Color(0,0.01,0.06,0.38)` est conservé tel quel —
  c'est la base du shader holographique.

### Ce qu'on ajoute

1. Shader holographique sur le dark overlay (§2.1) — technologie ancienne qui respire
2. Vignette écran (§2.2) — concentre l'attention sur la zone de combat
3. Bloom renforcé (§2.3) — les éléments Aether explosent visuellement
4. Screen shake calibré (§3.1) — le monde réagit aux événements importants
5. Trail joueur (§3.4) — le joueur laisse une trace de son passage
6. Shockwave Colosse (§3.3) — mort mémorable de l'ennemi le plus difficile

---

## 2. Shaders à implémenter

### 2.1 Shader grille holographique sol (P0)

**Priorité : P0 — impact visuel très élevé, coût GPU minimal, zero sprite requis.**

**Où :** `ShaderMaterial` appliqué au `Polygon2D` dark overlay existant dans `GroundRenderer.cs`
(ZIndex=-7, `Color(0,0.01,0.06,0.38)`). Le shader s'applique par-dessus la couleur de base.

**Effet visuel attendu :** grille de circuits holographiques subliminaux — comme Tron mais à
6% d'opacité. Le joueur ne la "voit" pas consciemment mais elle renforce la lecture "technologie
ancienne" de l'arène. La grille se déplace très lentement (1-2 px/s) pour signifier que
le Sanctuaire n'est pas complètement mort.

**Paramètres clés :**
- Lignes de grille espacées de 32 px (cohérent avec la grille des tiles)
- Épaisseur de ligne : 1 px
- Couleur ligne : `#00A0BB` (Aether abyssal — déjà autorisé STYLE_GUIDE §7.3)
- Opacité max lignes : 0.06 (invisible en combat dense, subliminal)
- Animation : translation lente `TIME * 0.5` sur les deux axes (diagonale)
- Crosshatch secondaire à 45° en opacité 0.025 (moiré très léger, évoque les circuits)

**Code GLSL complet pour `developpeur` :**

```glsl
shader_type canvas_item;

uniform float grid_size : hint_range(16.0, 64.0) = 32.0;
uniform float line_width : hint_range(0.5, 2.0) = 1.0;
uniform vec4 line_color = vec4(0.0, 0.627, 0.733, 1.0); // #00A0BB
uniform float opacity : hint_range(0.0, 0.15) = 0.06;
uniform float scroll_speed : hint_range(0.0, 5.0) = 0.5;

void fragment() {
    vec4 base = texture(TEXTURE, UV) * COLOR;

    // Coordonnees monde (evite les artefacts de UV stretch)
    vec2 world_px = UV / TEXTURE_PIXEL_SIZE;
    vec2 scrolled = world_px + vec2(TIME * scroll_speed);

    // Grille orthogonale
    vec2 grid = mod(scrolled, grid_size);
    float line_h = step(grid_size - line_width, grid.x);
    float line_v = step(grid_size - line_width, grid.y);
    float grid_mask = max(line_h, line_v);

    // Grille diagonale secondaire (45 degres), opacite moitie
    vec2 diag = mod(scrolled * 0.707 + scrolled.yx * 0.707, grid_size);
    float diag_mask = max(
        step(grid_size - line_width * 0.8, diag.x),
        step(grid_size - line_width * 0.8, diag.y)
    ) * 0.4;

    float total_mask = clamp(grid_mask + diag_mask, 0.0, 1.0);

    vec4 grid_contrib = line_color * total_mask * opacity;
    COLOR = base + grid_contrib;
}
```

**Instruction `developpeur` :** dans `GroundRenderer.cs`, méthode `BuildFloor()`, après la
création du `Polygon2D` dark overlay (`_darkOverlay`), ajouter :

```csharp
var shaderMat = new ShaderMaterial();
shaderMat.Shader = GD.Load<Shader>("res://assets/shaders/floor_grid.gdshader");
_darkOverlay.Material = shaderMat;
```

Créer le fichier `assets/shaders/floor_grid.gdshader` avec le code GLSL ci-dessus.
Le `Polygon2D` a déjà `TextureMode = Tile` — vérifier que `UV` dans le shader
correspond bien aux coordonnées locales (ajuster `TEXTURE_PIXEL_SIZE` si nécessaire
en utilisant `FRAGCOORD / vec2(1920.0, 1216.0)` à la place de `UV` si l'UV tile
génère des artefacts).

---

### 2.2 Vignette écran (P0 — sorti de post-MVP)

**Priorité : P0 — améliore la lisibilité combat, pas uniquement cosmétique.**

**Justification de la promotion post-MVP → P0 :** la vignette réduit la luminosité des
bords d'écran où spawn la majorité des ennemis — le joueur détecte instinctivement les
nouveaux venus sans chercher dans les coins. Hades et Neon Abyss l'utilisent tous les deux
pour cette raison fonctionnelle, pas seulement esthétique.

**Où :** `ColorRect` plein écran (1280×720), ZIndex=90 (sous FadeOverlay ZIndex=100),
`ShaderMaterial`, `mouse_filter=2` (Ignore), ajouté à `Game.tscn` et `MainMenu.tscn`.

**Paramètres :**
- Couleur de fond du ColorRect : `Color(0,0,0,0)` (transparent — le shader gère tout)
- Rayon intérieur transparent : 0.45 (45% depuis le centre)
- Rayon de transition : 0.30 (fondu sur 30% du rayon)
- Opacité max en bordure : 0.72
- Couleur vignette : `#000005` (noir quasi-pur, pas de teinte bleue qui modifierait
  la palette des ennemis en bordure)

**Code GLSL complet :**

```glsl
shader_type canvas_item;

uniform float inner_radius : hint_range(0.2, 0.8) = 0.45;
uniform float feather : hint_range(0.1, 0.5) = 0.30;
uniform float max_opacity : hint_range(0.0, 1.0) = 0.72;
uniform vec4 vignette_color = vec4(0.0, 0.0, 0.02, 1.0); // #000005

void fragment() {
    // Distance normalisee depuis le centre (0 = centre, 1 = coin)
    vec2 centered = UV - vec2(0.5);
    // Corriger l'aspect ratio (viewport 1280x720 = 16:9)
    centered.x *= 1.777; // 16/9
    float dist = length(centered) * 1.414; // normalise vers 1.0 au coin

    float vignette = smoothstep(inner_radius, inner_radius + feather, dist);
    vignette *= max_opacity;

    COLOR = vignette_color;
    COLOR.a = vignette;
}
```

**Instruction `developpeur` :** créer `assets/shaders/screen_vignette.gdshader`.
Dans `Game.tscn`, ajouter un `CanvasLayer` "PostFX" (layer=90) contenant un `ColorRect`
nommé "Vignette" (AnchorPreset FullRect). Assigner le `ShaderMaterial`.
Dans `MainMenu.tscn`, le shader de vignette est déjà partiellement implémenté
(smoothstep 0.35→0.75, opacité 0.65 — GDD §16 Phase 3c) : migrer vers ce shader
unifié. Conserver les paramètres MainMenu (opacité 0.65) distincts de Game.tscn (0.72).

---

### 2.3 Bloom renforcé (P0)

**Où :** `WorldEnvironment` dans `Game.tscn`, nœud existant.

**Paramètres actuels → cibles :**

| Paramètre | Actuel | Cible | Justification |
|---|---|---|---|
| `glow_enabled` | true | true | inchangé |
| `glow_hdr_threshold` | 0.6 | 0.6 | NE PAS modifier — protège les matières mortes |
| `glow_intensity` | 0.8 | 1.4 | +75% — les PointLight2D Aether explosent |
| `glow_strength` | 1.2 | 1.8 | +50% — étalement du halo plus dramatique |
| `glow_blend_mode` | Additive | Additive | inchangé |
| Niveaux actifs | 1+2 | 1+2+3 | Ajouter niveau 3 pour les halos plus larges |

**Attention :** vérifier en test que les balles ennemies (rouge-orange `Color(1,0.35,0.1)`)
ne deviennent pas trop proéminentes avec le bloom renforcé — elles ne doivent pas rivaliser
visuellement avec les projectiles joueur (cyan, plus clairs). Si nécessaire, réduire
l'énergie du PointLight2D EnemyBullet de 1.0 → 0.7 pour compenser.

---

### 2.4 Shader shockwave ring (P1)

**Où :** `ShaderMaterial` sur un `Sprite2D` 256×256 px blanc (`#FFFFFF`) instancié
à la mort du Colosse Greffé. Le sprite est ajouté à `GetTree().Root` via `CallDeferred`
(même pattern que `EnemyDeathBurst`), ZIndex=5 (au-dessus de tout sauf UI), auto-détruit
via `Timer 0.5s`.

**Effet visuel attendu :** anneau cyan lumineux qui se dilate depuis le centre de la mort
du Colosse (radius 0 → 128 px), s'éclaircit puis disparaît en 0.4 s. Référence visuelle :
explosion plasma de Returnal, mais pixel art (bords francs, pas anti-aliasé).

**Code GLSL complet :**

```glsl
shader_type canvas_item;

uniform float progress : hint_range(0.0, 1.0) = 0.0; // animé par code 0→1 en 0.4s
uniform float ring_width : hint_range(0.01, 0.15) = 0.06;
uniform vec4 ring_color = vec4(0.267, 1.0, 0.933, 1.0); // #44FFEE

void fragment() {
    vec2 centered = UV - vec2(0.5);
    float dist = length(centered) * 2.0; // 0 au centre, 1 au bord

    float outer = progress;
    float inner = max(0.0, progress - ring_width);

    float ring = step(inner, dist) * step(dist, outer);
    float fade = 1.0 - progress; // s'estompe en s'expandant

    COLOR = ring_color;
    COLOR.a = ring * fade * 0.85;
}
```

**Instruction `developpeur` :** créer `src/VFX/ShockwaveRing.cs` et
`scenes/vfx/vfx_shockwave_ring.tscn`. Dans `GraftedColossus.Die()`, après la ligne
`SpawnDeathBurst()`, appeler `SpawnShockwave()` (même pattern CallDeferred).

```csharp
// Dans ShockwaveRing.cs
[Export] public float Duration = 0.4f;
private ShaderMaterial _mat;

public override void _Ready() {
    _mat = (ShaderMaterial)((Sprite2D)GetChild(0)).Material;
    var tween = CreateTween();
    tween.TweenMethod(Callable.From<float>(p => _mat.SetShaderParameter("progress", p)),
        0.0f, 1.0f, Duration);
    tween.TweenCallback(Callable.From(QueueFree));
}
```

**Brief graphiste :** aucun sprite requis — le shader travaille sur un `Sprite2D` blanc
généré via `ImageTexture.CreateFromImage(Image.Create(4,4,false,Image.Format.Rgba8))`
(texture blanche 4×4 px, le shader scale visuellement). Alternativement : produire
`vfx_white_circle.png` — cercle blanc plein 64×64 px sur fond transparent.

---

### 2.5 Shader chromatic aberration sur fusion (P1)

**Où :** `ShaderMaterial` sur un `ColorRect` plein écran dans un `CanvasLayer` ZIndex=89
(juste sous la vignette ZIndex=90), nommé "ChromaticFX". Déclenché depuis
`FusionFlash.TriggerFlash()` en parallèle du flash blanc.

**Contexte de lisibilité :** la fusion déclenche une pause du gameplay (`GetTree().Paused=true`
via LevelUpScreen). Il n'y a aucun ennemi en mouvement pendant ce moment. Le risque de
confusion visuelle est donc nul — c'est le seul moment où un effet de distorsion peut
s'appliquer sans contrainte de lisibilité combat.

**Paramètres :**
- Décalage canal R : `+4 px` vers la droite
- Décalage canal B : `-4 px` vers la gauche
- Canal G : inchangé (point d'ancrage)
- Durée : 0.30 s (synchronisé avec `FusionFlash` — montée 0.1s + descente 0.2s)
- Atténuation : suit la courbe du flash blanc (fort au début, fade vers 0)

**Code GLSL complet :**

```glsl
shader_type canvas_item;

uniform float strength : hint_range(0.0, 10.0) = 4.0; // animé 0→4→0 par code
uniform sampler2D SCREEN_TEXTURE : hint_screen_texture, filter_linear_mipmap;

void fragment() {
    vec2 offset = vec2(strength / 1280.0, 0.0); // en UV normalisé

    float r = texture(SCREEN_TEXTURE, SCREEN_UV + offset).r;
    float g = texture(SCREEN_TEXTURE, SCREEN_UV).g;
    float b = texture(SCREEN_TEXTURE, SCREEN_UV - offset).b;

    COLOR = vec4(r, g, b, 1.0);
}
```

**Note importante :** `hint_screen_texture` requiert que le `ColorRect` soit un enfant de
`CanvasLayer` avec `follow_viewport_enabled=true`. Le `ShaderMaterial` doit avoir
`blend_mode = Mix` (pas Add). La propriété `strength` est animée via Tween depuis
`FusionFlash.cs` : `0 → 4.0 en 0.1s → 0 en 0.2s`.

---

## 3. Effets d'explosion et de satisfaction

### 3.1 Screen shake (P0)

**Principe :** offset de la `Camera2D` via Tween, sans modifier `GlobalPosition` du joueur
ni les limites de la caméra. L'offset revient à `Vector2.Zero` à la fin du shake.

**Implémentation :** créer `src/Systems/ScreenShake.cs`, AutoLoad dans `project.godot`
(après `AudioSystem`).

```csharp
// ScreenShake.cs — AutoLoad singleton
public partial class ScreenShake : Node
{
    public static ScreenShake? Instance { get; private set; }

    private Camera2D? _camera;
    private Tween? _shakeTween;

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetCamera(Camera2D cam) => _camera = cam;

    public void Shake(float amplitude, float duration)
    {
        if (_camera is null) return;
        _shakeTween?.Kill();
        _shakeTween = CreateTween();
        _shakeTween.SetTrans(Tween.TransitionType.Sine);
        _shakeTween.SetEase(Tween.EaseType.Out);

        // Oscillation : N allers-retours pendant la duree
        int steps = Mathf.Max(2, (int)(duration / 0.04f));
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            float decay = 1.0f - t; // decroit lineairement
            var target = i % 2 == 0
                ? new Vector2(amplitude * decay, amplitude * 0.5f * decay)
                : new Vector2(-amplitude * decay, -amplitude * 0.4f * decay);
            _shakeTween.TweenProperty(_camera, "offset",
                target, duration / steps);
        }
        _shakeTween.TweenProperty(_camera, "offset",
            Vector2.Zero, 0.04f);
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
```

**Dans `Player._Ready()` :** appeler `ScreenShake.Instance?.SetCamera(_camera)` où
`_camera` est la référence à la `Camera2D` enfant du joueur.

**Tableau des amplitudes par événement :**

| Événement | Amplitude (px) | Durée (s) | Déclencheur |
|---|---|---|---|
| Mort ennemi ordinaire (Essaim, Drone) | 0 | 0 | Aucun — trop fréquent |
| Mort Sentinelle Corrompue | 3 | 0.12 | `CorruptedSentinel.Die()` |
| Mort Colosse Greffé | 12 | 0.35 | `GraftedColossus.OnAnimationFinished()` |
| Mort du joueur | 20 | 0.50 | `Player.HandleDeath()` |
| Level-up | 6 | 0.20 | `XpSystem` signal `LevelUp` |
| Fusion déclenchée | 8 | 0.25 | `FusionFlash.TriggerFlash()` |
| Geyser actif — dégât joueur | 2 | 0.08 | `AetherGeyser.OnBodyInZone()` |

**Règle de lisibilité :** ne jamais cumuler deux shakes simultanément (`.Kill()` avant
chaque nouveau déclenchement). Les essaims meurent par dizaines — un shake par
Essaim créerait une vibration permanente illisible.

---

### 3.2 Hit stop / freeze frame (P1)

**Principe :** `Engine.TimeScale` passe à `0.05` pendant 2 frames puis revient à `1.0`.
L'effet s'appelle "hitstop" dans la littérature game feel (Street Fighter, Hades).

**Budget :** uniquement sur 2 événements coûteux mais rares :
1. Mort du Colosse Greffé
2. Activation d'une fusion (avant le FusionFlash)

**Implémentation dans `ScreenShake.cs` (extension de la classe) :**

```csharp
public async void HitStop(float duration = 0.05f)
{
    Engine.TimeScale = 0.05f;
    await ToSignal(GetTree().CreateTimer(duration,
        processAlways: true, processInPhysics: false), "timeout");
    Engine.TimeScale = 1.0f;
}
```

**Important :** `processAlways: true` dans `CreateTimer` — le timer doit tourner même
si le jeu est "presque pausé" par le TimeScale réduit. Ne pas utiliser `await Task.Delay`
(ne respecte pas le TimeScale Godot).

**Attention fusion :** la fusion se déclenche depuis `LevelUpScreen.OnCardChosen()` pendant
que `GetTree().Paused = true`. Ne PAS appeler `HitStop` dans ce contexte — le timer ne
se déclencherait jamais (Paused bloque les signaux non-Always). Appeler `HitStop` uniquement
depuis `FusionFlash.TriggerFlash()` si `!GetTree().Paused`.

---

### 3.3 Explosion Colosse — séquence complète

Quand le Colosse meurt, la séquence suivante doit se déclencher dans cet ordre précis :

1. **Frame 7 de `death`** (flash violet libération Noyau — déjà implémenté) :
   - `SpawnAetherCore()` — déjà implémenté
   - `SpawnDeathBurst()` (particles colossus `#4A4A52`+`#AA44FF`) — déjà implémenté

2. **Simultanément (via `CallDeferred`) :**
   - `ScreenShake.Instance?.Shake(12f, 0.35f)`
   - `ScreenShake.Instance?.HitStop(0.05f)`
   - `SpawnShockwaveRing()` — nouveau (§2.4)

3. **Dans `GraftedColossus.OnAnimationFinished()` branch `death`, après `QueueFree()` :**
   - Aucune modification requise — les CallDeferred ci-dessus survivent au QueueFree
     car ils sont posés sur des AutoLoad (`ScreenShake`) ou des nœuds root.

**Résultat attendu :** freeze 2 frames → explosion de particules violettes → anneau
cyan qui se dilate → écran qui tremble en décroissance. Dure 0.5 s au total.
Mémorable, jamais vu auparavant dans cette run (Colosse = spawn 1 par 9 minutes).

---

### 3.4 Trail joueur (P1)

**Où :** `GPUParticles2D` enfant de `Player.tscn`, positionné à `Vector2.Zero` local
(centré sur le joueur).

**Paramètres :**

| Paramètre | Valeur |
|---|---|
| `amount` | 6 |
| `lifetime` | 0.10 s |
| `one_shot` | false |
| `emission_shape` | Point (émet depuis le centre du joueur) |
| `initial_velocity` | 0 (les particules restent sur place) |
| `gravity` | Vector2(0,0) |
| Couleur | `Color(0.267, 1.0, 0.933, 0.15)` → transparent (`#44FFEE` à 15%) |
| `scale` | 3.0 (carré 3×3 px) |
| Texture | `vfx_particle_trail_player.png` (3×3 px blanc, fourni par graphiste) |

**Activation dans `Player.cs` :**
```csharp
// Dans _PhysicsProcess(), apres MoveAndSlide()
bool isMoving = Velocity.LengthSquared() > 100f; // > ~10 px/s
if (_trailParticles.Emitting != isMoving)
    _trailParticles.Emitting = isMoving;
```

**Note DA :** le trail ne doit pas être visible à l'arrêt — il marque le déplacement,
pas la présence statique. L'opacité de 15% est délibérément faible : le joueur ne doit
pas ressembler à une comète, il doit avoir une légèreté de mouvement.

**Brief graphiste :** produire `vfx_particle_trail_player.png` — carré 3×3 px
`#FFFFFF` (blanc pur, la couleur est gérée par le `color_ramp` du `GPUParticles2D`).

---

### 3.5 Explosion mort Sentinelle — upgrade (P0)

La Sentinelle dispose déjà du death burst via `EnemyBase.SpawnDeathBurst()`.
Ajouter le screen shake 3 px (§3.1) depuis `CorruptedSentinel.Die()`, après l'appel
existant à `SpawnDeathBurst()`.

Aucun nouveau VFX requis pour la Sentinelle — le burst existant + shake léger
suffisent à marquer l'événement sans le sur-théâtraliser.

---

### 3.6 Feedback level-up — upgrade (P0)

Le level-up actuel n'a aucun feedback dans l'arène (seul l'écran de choix apparaît).
Ajouter dans le handler du signal `LevelUp` de `XpSystem` (dans `Player.cs` ou
`GameManager.cs`) :
1. `ScreenShake.Instance?.Shake(6f, 0.20f)`
2. Flash joueur de 0.15 s (couleur or `Color(1f, 0.8f, 0.267f, 1f)`) via le
   pattern existant `HitFlash` de `Player` — surcharger `HitFlash` pour accepter
   une couleur optionnelle, avec blanc comme défaut actuel.

Ce double feedback (shake + flash or sur le joueur) ancre le level-up dans l'espace
de jeu, pas uniquement dans l'UI.

---

## 4. Éclairage additionnel

### 4.1 Nouveaux PointLight2D à ajouter

Les éléments suivants n'ont pas encore de contribution lumineuse propre :

**Orbes XP (P1) :**
- `PointLight2D` sur `XpOrb.tscn`, couleur `#AAFF44` (jaune-vert XP), énergie 0.3,
  TextureScale 1.2. Avec 50-100 orbes à l'écran en fin de run, ne pas dépasser énergie 0.3.
- BlendMode Add. Texture : factory `Player.MakeRadialLightTexture(32)` (réutiliser la factory
  existante — textures cachées statiquement).

**Noyaux d'Aether (P1) :**
- `PointLight2D` sur `AetherCore.tscn`, couleur `#AA44FF` (violet Noyau), énergie 0.8,
  TextureScale 2.0. Plus intense que les orbes XP — le Noyau est l'objectif principal de
  la run, il doit attirer l'œil de loin.
- Animation de pulsation : `energy` animé 0.8 → 1.4 → 0.8 en 1.0 s boucle via
  `AnimationPlayer`. Même pattern que le pulse XP Orb existant mais sur `energy` pas `modulate`.

**Obstacles Piliers de Sanctuaire (P2) :**
- `PointLight2D` sur les fissures Aether des piliers. Couleur `#00A0BB`, énergie 0.15,
  TextureScale 0.8. Positionnement : offset Y=-16 px (vers la fissure visible).
- Priorité P2 — les piliers sont statiques et nombreux. Impact performance à surveiller.
  Désactiver si > 5 piliers actifs génèrent des baisses de FPS mesurables.

### 4.2 Lumières pulsantes existantes — ajustements

**Joueur (ajustement P0) :** énergie actuelle 0.55. Avec le bloom renforcé (§2.3),
réduire à 0.45 pour conserver le même rendu visuel perçu (le bloom amplifie la lumière).

**Balles joueur (ajustement P0) :** énergie actuelle 1.2. Réduire à 0.9 — trop intense
avec le bloom ×1.75. Le projectile doit briller, pas aveugler.

**Balles ennemies (ajustement P0) :** énergie actuelle 1.0. Conserver si les balles
ennemies restent lisibles comme "danger" avec le bloom renforcé. Sinon réduire à 0.75.

### 4.3 Réactivité lumineuse aux événements

**Geyser actif — déjà implémenté (0.4→1.8 energy)** : conserver tel quel. Avec le bloom
renforcé, l'intensité 1.8 peut paraître trop agressive — tester en run et ajuster à 1.4 si
le geyser "explose" visuellement de manière illisible.

**Flash joueur level-up (ajout P0 — cf §3.6) :** le flash or du joueur bénéfice du bloom
renforcé sans modification supplémentaire — la couleur `#FFCC44` est au-dessus du seuil
0.6 de `glow_hdr_threshold`.

---

## 5. Assets à créer ou modifier

### 5.1 Shaders GLSL (créer)

| Fichier | Section | Responsable |
|---|---|---|
| `assets/shaders/floor_grid.gdshader` | §2.1 | `developpeur` |
| `assets/shaders/screen_vignette.gdshader` | §2.2 | `developpeur` |
| `assets/shaders/shockwave_ring.gdshader` | §2.4 | `developpeur` |
| `assets/shaders/chromatic_aberration.gdshader` | §2.5 | `developpeur` |

### 5.2 Sprites requis pour les nouveaux VFX

**Graphiste — nouveaux fichiers :**

| Fichier | Taille | Couleur | Usage | Priorité |
|---|---|---|---|---|
| `vfx_particle_trail_player.png` | 3×3 px | `#FFFFFF` | Trail joueur (couleur gérée par GPUParticles2D) | P1 |
| `vfx_white_circle.png` | 64×64 px | Cercle blanc plein sur transparent | Base texture shockwave ring | P1 |
| `vfx_particle_xp_orb_light.png` | 32×32 px | Gradient radial `#AAFF44`→transparent | Texture PointLight2D XP Orb | P1 |

Note sur `vfx_white_circle.png` : si la factory `Player.MakeRadialLightTexture()` est
utilisée pour la texture du shockwave, ce sprite n'est pas requis. Le `developpeur` choisit
l'approche (factory code vs sprite préféré).

### 5.3 Tiles sol — ne pas modifier

Les tiles sol (`tile_floor_01` à `tile_floor_debris`) sont conservées telles quelles.
Le shader holographique (§2.1) s'applique par-dessus l'overlay existant — aucune
modification de texture n'est nécessaire. La palette des tiles reste intacte.

### 5.4 Ressources CC0 externes — ne rien intégrer sans validation

Les ressources gratuites sur internet (itch.io, opengameart.org) peuvent servir
d'inspiration pour les shaders, mais l'intégration directe de shaders tiers nécessite :
1. Validation de la licence (CC0, MIT, ou licence permissive explicite)
2. Adaptation à la palette exacte du STYLE_GUIDE (les shaders génériques utilisent des
   couleurs qui ne respecteront pas notre opposition "matière morte / énergie vivante")
3. Validation par le `directeur-artistique` avant commit

Les shaders fournis dans ce brief (§2.1 à §2.5) sont rédigés spécifiquement pour
Chimera Protocol — utiliser ceux-là en priorité.

---

## 6. Ordre d'implémentation recommandé

Classé par rapport impact visuel / effort d'implémentation. Les priorités P0 doivent
être implémentées en une seule session avant toute validation joueur.

### Session 1 — Fondations visuelles (impact fort, effort modéré)

**1. Bloom renforcé (30 min — `developpeur`)** :
Modifier le `WorldEnvironment` existant : `glow_intensity` 0.8→1.4, `glow_strength` 1.2→1.8,
activer niveau 3. Ajuster les énergies PointLight2D joueur/balles (§4.2).
Test immédiat : lancerune run et vérifier que les ennemis restent lisibles.

**2. Screen shake (2h — `developpeur`)** :
Créer `ScreenShake.cs` AutoLoad. Brancher sur `GraftedColossus.Die()`, `Player.HandleDeath()`,
`XpSystem` signal LevelUp, `FusionFlash.TriggerFlash()`, `CorruptedSentinel.Die()`.
Ajuster les amplitudes en test pour que le shake soit *senti* sans être inconfortable.

**3. Vignette écran (1h — `developpeur`)** :
Shader `screen_vignette.gdshader`, `CanvasLayer` ZIndex=90 dans `Game.tscn`.
Tester : combat dense avec 50+ ennemis. La vignette doit concentrer l'œil, pas obscurcir.

**4. Shader grille holographique sol (2h — `developpeur`)** :
Shader `floor_grid.gdshader`, assigné au `Polygon2D` dark overlay existant.
Vérifier que l'opacité 0.06 reste subliminal et ne rivalise pas avec les entités.

### Session 2 — Feedbacks d'impact (impact fort sur game feel, effort modéré)

**5. Flash or joueur sur level-up (1h — `developpeur`)** :
Modifier `Player.HitFlash()` pour accepter une couleur optionnelle. Brancher sur `LevelUp`.

**6. Shockwave ring Colosse (3h — `developpeur`)** :
Shader `shockwave_ring.gdshader`, scène `vfx_shockwave_ring.tscn`, intégration dans
`GraftedColossus.Die()`. Script `ShockwaveRing.cs`.

**7. Hit stop (1h — `developpeur`)** :
Méthode `HitStop()` dans `ScreenShake.cs`. Brancher sur mort Colosse uniquement
en premier test, puis sur fusion après validation.

### Session 3 — Présence et ambiance (impact moyen, effort faible)

**8. PointLight2D XP Orbs et Noyaux (1h30 — `developpeur`)** :
Ajouter les lumières sur `XpOrb.tscn` et `AetherCore.tscn`. Ajuster énergies en run.

**9. Trail joueur (1h — `developpeur` + 15 min `graphiste`)** :
`GPUParticles2D` dans `Player.tscn`. Sprite `vfx_particle_trail_player.png` (15 min graphiste).

**10. Chromatic aberration fusion (2h — `developpeur`)** :
Shader `chromatic_aberration.gdshader`, intégration dans `FusionFlash.cs`.
À faire après validation de la fusion visuelle existante.

### Session 4 — Détails finaux (impact faible, effort faible, ne commencer qu'après les sessions 1-3)

**11. Piliers PointLight2D (1h — `developpeur`)** :
Lumières P2 sur les fissures Aether des piliers.

**12. Outline glow ennemis (4h — `developpeur`, effort GPU à surveiller)** :
Shader `canvas_item` sur les `AnimatedSprite2D`. Test perf obligatoire à 200 ennemis.
Si FPS < 55 sur la machine de test, cette feature est annulée.

---

## 7. Notes de cohérence DA — Familles visuelles

### Règle d'application aux nouvelles lumières

L'ajout de PointLight2D sur les orbes XP (`#AAFF44`) et les Noyaux (`#AA44FF`) respecte
la hiérarchie visuelle "énergie vivante" (STYLE_GUIDE §0) :
- Cyan `#44FFEE` : joueur, projectiles alliés, Aether positif
- Jaune-vert `#AAFF44` : XP, récompense de combat
- Violet `#AA44FF` : Noyaux, monnaie meta (la plus précieuse — lumière plus intense justifiée)
- Rouge-orange `Color(1,0.35,0.1)` : projectiles ennemis (danger)

Ces couleurs forment une hiérarchie de lecture instantanée : le joueur identifie "ami/ennemi/
récompense" par la couleur de la lumière, pas uniquement par la forme du sprite.

### Shaders sur matière morte uniquement

Les shaders holographiques et de vignette s'appliquent à des éléments "sans vie"
(sol, bordure d'écran). Ils ne doivent JAMAIS s'appliquer directement sur des sprites
d'entités vivantes (joueur, ennemis) — ce serait brouiller la frontière "matière morte /
énergie vivante" qui est le principe directeur du projet (STYLE_GUIDE §0).

L'unique exception actée : le shader d'outline glow ennemis (P2, §6 session 4) utilise
la couleur ennemie (`#FF2200`) sur la silhouette, pas une couleur Aether. Il renforce
la lisibilité de la silhouette sans contaminer la famille visuelle des ennemis.

### Cohérence avec les trois familles futures (GDD §4, STYLE_GUIDE §9)

Les effets de ce brief sont génériques (screen shake, vignette, bloom) et s'appliqueront
sans modification aux futurs personnages Humain et Robot. Le trail joueur utilise la couleur
du PointLight2D joueur (`#44FFEE`) — cette valeur devra être exposée en paramètre configurable
par personnage lors de l'ajout des autres archétypes.

Le shockwave Colosse (`#44FFEE`) appartient à la palette Aether (la mort du Colosse libère
de l'Aether, cf. lore GDD §3 : "la Rouille Vivante a digéré des Noyaux pour l'animer").
Cohérence lore validée.

---

## Critères de validation (non négociables)

Avant de considérer une feature comme "livrée", le `game-tester` doit confirmer :

1. **Lisibilité 150 ms** (règle absolue STYLE_GUIDE §0) : en combat dense ≥50 ennemis,
   le joueur, les ennemis et les projectiles restent distincts du fond.
2. **FPS ≥ 55** en pic d'ennemis (200 actifs). Si un effet fait descendre le FPS sous
   ce seuil, il est désactivé ou rétrogradé P3.
3. **Shake non nauséeux** : amplitude et durée dans les plages du §3.1. Demander à un
   testeur de jouer 5 minutes avec les shakes actifs — si inconfort signalé, réduire
   l'amplitude de 30%.
4. **Grille holographique subliminal** : en montrant le jeu à un testeur naïf, il ne
   doit pas signaler "il y a une grille sur le sol" spontanément. Si c'est le cas,
   réduire l'opacité du shader (paramètre `opacity` dans le GLSL).

---

*Brief produit par `directeur-artistique` le 2026-06-24.*
*Toute modification à ce document requiert validation par `directeur-artistique`.*
*Références : `docs/GDD.md` §12, `docs/STYLE_GUIDE.md`, `docs/ARENA_DA_BRIEF.md`.*
