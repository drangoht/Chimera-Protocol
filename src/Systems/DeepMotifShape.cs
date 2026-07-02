using Godot;

/// <summary>
/// Glyphe procédural (anneau hexagonal + rayons + noyau) représentant une structure lointaine
/// entrevue « sous l'arène » à travers les tuiles vitrées. Purement dessiné (<see cref="_Draw"/>),
/// aucun asset externe — évite tout risque d'import manquant (cf. BUG-301). Teinté/positionné par
/// <see cref="BiomeAtmosphere"/>, qui gère aussi son décalage de parallaxe.
/// </summary>
internal sealed partial class DeepMotifShape : Node2D
{
    private static readonly Color LineStrong = new(1f, 1f, 1f, 0.6f);
    private static readonly Color LineSoft   = new(1f, 1f, 1f, 0.3f);

    public override void _Draw()
    {
        const float rOuter = 46f;
        const float rInner = 25f;

        DrawHexRing(rOuter, LineStrong, 2.5f);
        DrawHexRing(rInner, LineSoft, 1.5f);
        for (int i = 0; i < 6; i += 2)
        {
            float a = Mathf.Tau * i / 6f;
            var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            DrawLine(dir * rInner, dir * rOuter, LineSoft, 1.5f, true);
        }
        DrawCircle(Vector2.Zero, 4f, LineStrong);
    }

    private void DrawHexRing(float r, Color c, float width)
    {
        var pts = new Vector2[7];
        for (int i = 0; i <= 6; i++)
        {
            float a = Mathf.Tau * i / 6f;
            pts[i] = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        DrawPolyline(pts, c, width, true);
    }
}
