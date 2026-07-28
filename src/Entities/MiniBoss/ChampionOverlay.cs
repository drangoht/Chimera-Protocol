using Godot;

/// <summary>Ce que dessine un <see cref="ChampionOverlay"/>.</summary>
public enum ChampionOverlayKind
{
    /// <summary>Bouclier orbital du Gardien Néon : arc épais + nœuds aux extrémités.</summary>
    OrbitalShield,
    /// <summary>Cône de gel de la Sentinelle Cryo : secteur plein + bords marqués.</summary>
    FrostCone,
}

/// <summary>
/// Calque de rendu d'un champion, dessiné <b>hors de son arbre</b>.
///
/// Pourquoi ne pas dessiner dans le <c>_Draw</c> du champion lui-même : <see cref="EnemyBase.HitFlash"/>
/// anime <c>Modulate</c> depuis <c>(5,5,5,1)</c> à chaque coup encaissé, et <c>Modulate</c> se propage
/// à tout le sous-arbre — <i>y compris</i> au <c>_Draw</c> du nœud. Multipliées par 5, toutes les
/// composantes saturent : le bouclier magenta et le cône bleu sortent <b>blancs</b>. Mesuré en jeu :
/// pixels de l'arc à (142,142,145), soit un gris neutre. Et comme le joueur tire en continu, l'état
/// « flashé » est l'état normal — la couleur disparaissait exactement au moment où elle sert.
///
/// Le calque se parente donc à la <b>racine</b> (comme <see cref="BossHazard"/>) et suit son
/// propriétaire à chaque frame. Il échappe au flash, le corps le garde. Conséquence attendue : il est
/// justiciable de <c>SceneCleanup.ClearWorldVfx</c> en sortie de run, et se libère de lui-même dès
/// que son propriétaire disparaît.
/// </summary>
public partial class ChampionOverlay : Node2D
{
    private ChampionOverlayKind _kind;
    private EnemyBase? _owner;

    // ── Paramètres poussés par le champion à chaque frame ─────────────────────
    /// <summary>Angle central (radians) de l'arc ou de l'axe du cône.</summary>
    public float Angle { get; set; }
    /// <summary>Demi-ouverture (radians).</summary>
    public float HalfSpan { get; set; }
    /// <summary>Rayon de l'arc / portée du cône (px).</summary>
    public float Radius { get; set; }
    /// <summary>Intensité 0-1 d'un effet ponctuel (absorption d'un tir, tir du cône).</summary>
    public float Flash { get; set; }

    /// <summary>Crée le calque, le parente à la racine et le lie à <paramref name="owner"/>.</summary>
    public static ChampionOverlay Attach(EnemyBase owner, ChampionOverlayKind kind, int zIndex = 2)
    {
        var overlay = new ChampionOverlay { _kind = kind, _owner = owner, ZIndex = zIndex };
        owner.GetTree().Root.CallDeferred(Node.MethodName.AddChild, overlay);
        overlay.SetDeferred("global_position", owner.GlobalPosition);
        return overlay;
    }

    public override void _Process(double delta)
    {
        if (_owner == null || !IsInstanceValid(_owner)) { QueueFree(); return; }
        GlobalPosition = _owner.GlobalPosition;
        QueueRedraw();
    }

    /// <summary>Le champion masque/affiche le calque via <c>Visible</c> (le cône n'existe qu'à la visée).</summary>
    public override void _Draw()
    {
        if (_kind == ChampionOverlayKind.OrbitalShield) DrawShield();
        else                                            DrawCone();
    }

    private void DrawShield()
    {
        float a0   = Angle - HalfSpan;
        float span = HalfSpan * 2f;

        var body = new Color(1f, 0.24f, 0.82f, 0.80f + 0.20f * Flash);
        DrawArc(Vector2.Zero, Radius, a0, a0 + span, 40, body, 3.5f + 2f * Flash, true);

        var inner = new Color(1f, 0.62f, 0.94f, 0.45f + 0.35f * Flash);
        DrawArc(Vector2.Zero, Radius - 3f, a0, a0 + span, 40, inner, 1.5f, true);

        // Nœuds cyan aux deux extrémités : ce sont EUX que le joueur suit pour situer la brèche.
        var node = new Color(0.55f, 1f, 0.98f, 0.95f);
        DrawCircle(Vector2.FromAngle(a0) * Radius, 3f, node);
        DrawCircle(Vector2.FromAngle(a0 + span) * Radius, 3f, node);
    }

    private void DrawCone()
    {
        float alpha = Flash > 0f
            ? 0.45f * Flash
            : 0.18f + 0.18f * Mathf.Abs(Mathf.Sin(Time.GetTicksMsec() / 90f));

        var color = new Color(0.55f, 0.88f, 1f, alpha);
        float a0 = Angle - HalfSpan, a1 = Angle + HalfSpan;

        // Secteur plein + deux bords marqués : le joueur doit lire la LIMITE exacte de l'axe, c'est
        // elle qui lui dit de quel côté sortir (même parti pris que BossHazard).
        var pts = new System.Collections.Generic.List<Vector2> { Vector2.Zero };
        const int steps = 12;
        for (int i = 0; i <= steps; i++)
            pts.Add(Vector2.FromAngle(a0 + (a1 - a0) * i / steps) * Radius);
        DrawColoredPolygon(pts.ToArray(), color);

        var edge = new Color(0.75f, 0.95f, 1f, Mathf.Min(1f, alpha * 2.2f));
        DrawLine(Vector2.Zero, Vector2.FromAngle(a0) * Radius, edge, 2f);
        DrawLine(Vector2.Zero, Vector2.FromAngle(a1) * Radius, edge, 2f);
    }
}
