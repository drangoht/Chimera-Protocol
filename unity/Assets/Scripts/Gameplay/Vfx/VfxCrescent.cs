using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Croissant d'énergie du coup de Lame Plasma — portage de <c>PlasmaArcFlash</c>.
///
/// <para>C'est le seul effet du jeu qui <b>s'anime</b> au lieu de s'estomper : le croissant gonfle
/// et une tranche lumineuse balaie l'arc, ce qui donne le sens du coup. Sous Godot, la note de la
/// classe dit pourquoi il existe — l'ancien nuage de particules carrées se lisait « rectangle
/// clignotant ». Le remplacer ici par un arc statique reproduirait ce défaut.</para>
///
/// <para>D'où un composant dédié plutôt qu'un appel à <see cref="VfxTrace"/> : une trace ordinaire
/// est figée à l'affichage, et ré-émettre un arc à chaque frame allouerait 24 points × 60 fois par
/// seconde et par coup.</para>
/// </summary>
public sealed class VfxCrescent : MonoBehaviour
{
    private const int Segments = 24;
    private const float Duration = 0.22f;

    private static readonly Color Cyan = new(0.267f, 1f, 0.933f);
    private static readonly Color White = new(0.85f, 1f, 1f);

    private static readonly Vector3[] Buffer = new Vector3[Segments + 1];

    private LineRenderer? _band;
    private LineRenderer? _outerHalo;
    private LineRenderer? _outerCore;
    private LineRenderer? _inner;
    private LineRenderer? _sweep;

    private float _radius;
    private float _halfAngleRad;
    private float _left;

    /// <summary>Construit les cinq traits. Appelé une seule fois, à la création de l'objet.</summary>
    internal void Build()
    {
        _band = MakeLine("Band", VfxPrimitives.AdditiveBeam);
        _outerHalo = MakeLine("OuterHalo", VfxPrimitives.AdditiveBeam);
        _outerCore = MakeLine("OuterCore", VfxPrimitives.AdditiveFlat);
        _inner = MakeLine("Inner", VfxPrimitives.AdditiveBeam);
        _sweep = MakeLine("Sweep", VfxPrimitives.AdditiveFlat);
    }

    private LineRenderer MakeLine(string name, Material material)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(transform, false);

        var lr = go.GetComponent<LineRenderer>();

        // Espace LOCAL, contrairement aux traces ordinaires : la rotation de l'objet porte la
        // direction du coup, exactement comme la rotation du Node2D sous Godot.
        lr.useWorldSpace = false;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 2;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = material;
        lr.sortingOrder = VfxPrimitives.OrderOver;

        return lr;
    }

    /// <summary>Déclenche le coup, centré sur <paramref name="center"/> et dirigé par <paramref name="dir"/>.</summary>
    internal void Show(Vector2 center, Vector2 dir, float halfAngleDeg, float radiusPx)
    {
        if (dir.sqrMagnitude < 0.0001f) dir = Vector2.right;

        transform.position = center;
        transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);

        _radius = radiusPx;
        _halfAngleRad = halfAngleDeg * Mathf.Deg2Rad;
        _left = Duration;

        gameObject.SetActive(true);
        Redraw(0f);
    }

    private void Update()
    {
        _left -= Time.deltaTime;

        if (_left <= 0f)
        {
            gameObject.SetActive(false);
            Vfx.Recycle(this);
            return;
        }

        Redraw(1f - _left / Duration);
    }

    private void Redraw(float t)
    {
        if (_band == null || _outerHalo == null || _outerCore == null || _inner == null || _sweep == null)
            return;

        float grow = 1f + 0.18f * t;              // le croissant gonfle en s'effaçant
        float r1 = _radius * grow;                // rayon extérieur
        float r0 = _radius * 0.5f * grow;         // rayon intérieur
        float rMid = (r0 + r1) * 0.5f;
        float band = r1 - r0;
        float a = 1f - t;                         // fondu sortant

        Arc(_band, rMid, band, new Color(Cyan.r, Cyan.g, Cyan.b, 0.45f * a));
        Arc(_outerHalo, r1, 10f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f * a));
        Arc(_outerCore, r1, 4f, new Color(White.r, White.g, White.b, a));
        Arc(_inner, r0, 3f, new Color(Cyan.r, Cyan.g, Cyan.b, 0.5f * a));

        // Tranche lumineuse qui balaie l'arc : c'est elle qui fait lire un COUP et non une aura.
        float sweep = -_halfAngleRad + 2f * _halfAngleRad * t;
        var sdir = new Vector2(Mathf.Cos(sweep), Mathf.Sin(sweep));

        _sweep.positionCount = 2;
        _sweep.SetPosition(0, sdir * r0);
        _sweep.SetPosition(1, sdir * (r1 + band * 0.3f));
        _sweep.startWidth = _sweep.endWidth = 4f;
        var sc = new Color(White.r, White.g, White.b, 0.9f * a);
        _sweep.startColor = _sweep.endColor = sc;
    }

    private void Arc(LineRenderer lr, float radius, float width, Color color)
    {
        for (int i = 0; i <= Segments; i++)
        {
            float angle = Mathf.Lerp(-_halfAngleRad, _halfAngleRad, i / (float)Segments);
            Buffer[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        lr.positionCount = Segments + 1;
        lr.SetPositions(Buffer);
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = color;
    }
}
