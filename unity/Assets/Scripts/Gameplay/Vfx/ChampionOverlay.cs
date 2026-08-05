using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Marque <b>permanente</b> portée par un champion : arc de bouclier, halo de charge, périmètre de
/// gel. Contrairement aux effets de <see cref="Vfx"/>, qui s'estompent, celle-ci vit aussi longtemps
/// que la mécanique qu'elle annonce.
///
/// <para><b>Un objet séparé, jamais un dessin sur le champion.</b> C'est la règle héritée de Godot :
/// le clignotement de dégât écrit dans la teinte de l'entité, et tout ce qui serait dessiné par le
/// champion lui-même serait saturé au blanc à chaque coup encaissé — c'est-à-dire précisément quand
/// le joueur regarde.</para>
///
/// <para><b>Pourquoi ce n'est pas une finition.</b> Le Gardien Néon n'absorbe les dégâts que dans le
/// secteur couvert par son bouclier ; sa réponse est de tourner autour de lui. Sans arc affiché, le
/// joueur n'a aucun moyen de savoir où frapper : la mécanique n'existe pas, elle rend seulement les
/// dégâts irréguliers sans raison visible.</para>
/// </summary>
public sealed class ChampionOverlay : MonoBehaviour
{
    private const int Segments = 24;
    private static readonly Vector3[] Buffer = new Vector3[Segments + 1];

    private LineRenderer? _halo;
    private LineRenderer? _core;

    /// <summary>Rayon de l'arc, en pixels.</summary>
    public float Radius = 46f;

    /// <summary>Demi-ouverture, en degrés. 180 dessine un anneau complet.</summary>
    public float HalfArcDeg = 70f;

    /// <summary>Direction couverte, en degrés.</summary>
    public float AngleDeg;

    public Color Tint = new(0.4f, 1f, 0.9f);

    /// <summary>Crée l'overlay sur un objet enfant du champion.</summary>
    internal static ChampionOverlay Attach(Transform parent, Color tint, float radius, float halfArcDeg)
    {
        var go = new GameObject("ChampionOverlay", typeof(ChampionOverlay));
        go.transform.SetParent(parent, false);

        var overlay = go.GetComponent<ChampionOverlay>();
        overlay.Tint = tint;
        overlay.Radius = radius;
        overlay.HalfArcDeg = halfArcDeg;
        overlay.BuildLines();

        return overlay;
    }

    private void BuildLines()
    {
        _halo = MakeLine("Halo", VfxPrimitives.AdditiveBeam, 9f, 0.35f, VfxPrimitives.OrderOver);
        _core = MakeLine("Core", VfxPrimitives.AdditiveFlat, 3f, 0.95f, VfxPrimitives.OrderOver + 1);
    }

    private LineRenderer MakeLine(string name, Material material, float width, float alpha, int order)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(transform, false);

        var lr = go.GetComponent<LineRenderer>();

        // Espace MONDE et non local : le champion peut porter une échelle (1,5 pour les champions de
        // biome, 2,4 pour le boss), et un arc en espace local serait mis à cette échelle — donc
        // afficherait une portée fausse.
        lr.useWorldSpace = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = material;
        lr.sortingOrder = order;
        lr.startWidth = lr.endWidth = width;

        var c = new Color(Tint.r, Tint.g, Tint.b, alpha);
        lr.startColor = lr.endColor = c;

        return lr;
    }

    private void LateUpdate()
    {
        if (_halo == null || _core == null) return;

        Vector2 center = transform.position;
        float baseAngle = AngleDeg * Mathf.Deg2Rad;
        float half = HalfArcDeg * Mathf.Deg2Rad;

        for (int i = 0; i <= Segments; i++)
        {
            float a = baseAngle + Mathf.Lerp(-half, half, i / (float)Segments);
            Buffer[i] = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * Radius;
        }

        Apply(_halo);
        Apply(_core);
    }

    private static void Apply(LineRenderer lr)
    {
        lr.positionCount = Segments + 1;
        lr.SetPositions(Buffer);
    }
}
