using Godot;

/// <summary>Nature d'une zone au sol laissée par une incarnation du boss (cf. GDD §29.2).</summary>
public enum BossHazardKind
{
    /// <summary>Flaque de magma (Fournaise) : dégâts par seconde tant que le joueur reste dedans.</summary>
    Magma,
    /// <summary>Plaque de givre (Givre) : ralentit le joueur, sans dégâts.</summary>
    Frost,
}

/// <summary>
/// Zone de danger persistante posée par le boss de fin — flaque de magma ou plaque de givre.
///
/// Entièrement construite en code (pas de `.tscn`) : ces zones naissent par paquets pendant un
/// combat déjà chargé, et une scène par flaque n'apporterait rien de configurable. La détection du
/// joueur passe par une distance au carré plutôt que par une `Area2D` — même choix que
/// <see cref="RustedCore"/> pour ses dégâts de contact : pas de couche de collision à accorder, et
/// une flaque ne peut pas rater le joueur parce qu'il n'a pas bougé (une `Area2D` ne signale
/// l'entrée que sur mouvement physique, cf. docs/PITFALLS.md §tests headless).
///
/// Se parente à la racine de l'arbre pour survivre au boss qui l'a créée, ce qui la rend
/// justiciable de <c>SceneCleanup.ClearWorldVfx</c> à la sortie de run (docs/PITFALLS.md).
/// </summary>
public partial class BossHazard : Node2D
{
    /// <summary>Dégâts par seconde d'une flaque de magma, avant réduction de dégâts du joueur.</summary>
    private const float MagmaDps = 9f;
    /// <summary>Ralentissement d'une plaque de givre (×0,55) et sa rémanence après la sortie.</summary>
    private const float FrostSlowMult = 0.55f;
    private const float FrostSlowLinger = 0.35f;

    private const float FadeInSec  = 0.25f;
    private const float FadeOutSec = 0.6f;

    private BossHazardKind _kind = BossHazardKind.Magma;
    private float _radius   = 42f;
    private float _lifetime = 6f;
    private float _armDelay;
    private float _age;
    private float _alpha;

    private PointLight2D? _light;

    /// <summary>La zone est-elle encore en phase de télégraphe (inerte) ?</summary>
    private bool Arming => _age < _armDelay;

    /// <summary>
    /// Crée une zone et la parente à la racine ; retourne l'instance (déjà en scène).
    /// <paramref name="armDelay"/> = temps de télégraphe pendant lequel la zone clignote sans rien
    /// appliquer : une flaque qui blesse à l'instant où elle apparaît se lit comme un coup gratuit.
    /// </summary>
    public static BossHazard Spawn(SceneTree tree, Vector2 position, BossHazardKind kind,
                                   float radius, float lifetime, float armDelay = 0f)
    {
        var hazard = new BossHazard
        {
            _kind     = kind,
            _radius   = radius,
            _lifetime = lifetime,
            _armDelay = armDelay,
            ZIndex    = -1,          // au sol : sous le joueur (Z 5) et sous les ennemis
        };
        tree.Root.CallDeferred(Node.MethodName.AddChild, hazard);
        hazard.SetDeferred("global_position", position);
        return hazard;
    }

    private Color BaseColor => _kind == BossHazardKind.Magma
        ? new Color(1f, 0.30f, 0.05f)     // rouge-magma franc, pas l'or du décor de la Fournaise
        : new Color(0.45f, 0.85f, 1f);

    public override void _Ready()
    {
        _light = new PointLight2D
        {
            Color        = BaseColor,
            Energy       = 0f,
            Texture      = Player.MakeRadialLightTexture(64),
            TextureScale = _radius / 20f,
            BlendMode    = PointLight2D.BlendModeEnum.Add,
        };
        AddChild(_light);
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        _age += dt;

        // Fondu d'apparition puis de disparition : une flaque qui « pop » se lit comme un bug, et
        // une flaque qui disparaît sèchement fait douter le joueur de l'endroit où il peut passer.
        float remaining = _lifetime - _age;
        _alpha = Mathf.Min(
            FadeInSec  <= 0f ? 1f : Mathf.Clamp(_age / FadeInSec, 0f, 1f),
            FadeOutSec <= 0f ? 1f : Mathf.Clamp(remaining / FadeOutSec, 0f, 1f));

        if (_light != null)
            _light.Energy = _alpha * (_kind == BossHazardKind.Magma ? 0.85f : 0.5f) * (Arming ? 0.2f : 1f);

        QueueRedraw();

        if (remaining <= 0f) { QueueFree(); return; }

        ApplyToPlayer(dt);
    }

    private void ApplyToPlayer(float dt)
    {
        // La zone n'agit qu'une fois armée ET installée : sinon une flaque encore transparente
        // blesserait le joueur avant d'être visible, ce qui se lit comme un coup non télégraphié.
        if (Arming || _alpha < 0.5f) return;

        var player = GameManager.Instance?.PlayerInstance;
        if (player == null || !IsInstanceValid(player)) return;
        if (GlobalPosition.DistanceSquaredTo(player.GlobalPosition) > _radius * _radius) return;

        if (_kind == BossHazardKind.Magma)
            player.TakeDamage(MagmaDps * dt * (1f - player.Stats.DamageReduction));
        else
            player.ApplyChill(FrostSlowMult, FrostSlowLinger);
    }

    public override void _Draw()
    {
        var c = BaseColor;
        if (Arming)
        {
            // Télégraphe : liseré seul, qui pulse. Pas de nappe pleine — le joueur doit lire
            // « ça va tomber ici » sans confondre avec une zone déjà active.
            float pulse = 0.35f + 0.45f * Mathf.Abs(Mathf.Sin(_age * 12f));
            DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 32,
                    new Color(c.R, c.G, c.B, pulse), 2f, true);
            return;
        }

        // Nappe pleine + liseré plus dense : le liseré marque la limite exacte de la zone, ce que
        // le dégradé seul ne fait pas dans le chaos (même parti pris que le Voile de Givre).
        // Le magma est plus opaque que le givre : sur le sol déjà orange de la Fournaise, une nappe
        // légère lisait comme une simple bulle de lumière (constaté au playtest du 2026-07-28).
        float fill = _kind == BossHazardKind.Magma ? 0.42f : 0.28f;
        DrawCircle(Vector2.Zero, _radius, new Color(c.R, c.G, c.B, fill * _alpha));
        DrawArc(Vector2.Zero, _radius, 0f, Mathf.Tau, 32,
                new Color(c.R, c.G, c.B, 0.75f * _alpha), 2.5f, true);
    }
}
