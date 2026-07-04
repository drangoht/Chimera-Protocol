using Godot;

/// <summary>
/// Halo pulsant dessiné derrière un ennemi d'élite (feedback visuel type Risk of Rain 2 : la menace
/// se lit à la couleur et à la taille, sans nameplate). Ajouté en enfant par EnemyBase.ApplyElite,
/// se contente de dessiner un disque coloré translucide qui « respire ». Aucune logique de jeu.
/// </summary>
public partial class EliteAura : Node2D
{
    private Color _color = new(1f, 1f, 1f, 0.35f);
    private float _radius = 18f;
    private float _t = 0f;

    public void Configure(Color color, float radius)
    {
        _color  = color;
        _radius = radius;
        ZIndex  = -1;   // derrière le sprite de l'ennemi
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        _t += (float)delta;
        QueueRedraw();
    }

    public override void _Draw()
    {
        // Respiration : rayon et alpha oscillent doucement.
        float pulse = 0.8f + 0.2f * Mathf.Sin(_t * 4.5f);
        var c = _color;
        c.A *= pulse;
        // Anneau doux : disque plein translucide + liseré plus vif.
        DrawCircle(Vector2.Zero, _radius * pulse, c);
        var ring = _color;
        ring.A = Mathf.Min(1f, _color.A * 2.2f) * pulse;
        DrawArc(Vector2.Zero, _radius * pulse, 0f, Mathf.Tau, 24, ring, 2f, true);
    }
}
