using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Anneau lumineux, éventuellement en expansion — portage de l'onde de choc (<c>ShockwaveRing</c>,
/// un shader sous Godot) et de tous les anneaux de zone : aura, champ de surcharge, singularité,
/// nova, esquive.
///
/// <para>Un cercle fermé plutôt qu'une polyligne ouverte : <c>LineRenderer.loop</c> raccorde le
/// dernier point au premier sans jointure visible, là où une polyligne laisse une <b>encoche</b> à
/// l'endroit de la fermeture — défaut immédiatement lisible sur une aura qui reste affichée en
/// permanence.</para>
/// </summary>
public sealed class VfxRing : MonoBehaviour
{
    private const int Segments = 40;
    private static readonly Vector3[] Buffer = new Vector3[Segments];

    private LineRenderer? _halo;
    private LineRenderer? _core;

    private Color _color;
    private float _fromRadius;
    private float _toRadius;
    private float _width;
    private float _life;
    private float _left;

    /// <summary>Construit les deux traits. Appelé une seule fois, à la création de l'objet.</summary>
    internal void Build()
    {
        _halo = MakeLine("Halo", VfxPrimitives.AdditiveBeam);
        _core = MakeLine("Core", VfxPrimitives.AdditiveFlat);
    }

    private LineRenderer MakeLine(string name, Material material)
    {
        var go = new GameObject(name, typeof(LineRenderer));
        go.transform.SetParent(transform, false);

        var lr = go.GetComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCornerVertices = 2;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = material;

        return lr;
    }

    /// <summary>
    /// Affiche l'anneau. Quand <paramref name="toRadius"/> diffère de <paramref name="fromRadius"/>,
    /// l'anneau se dilate sur toute sa durée — c'est ce qui distingue une <b>onde</b> qui part d'un
    /// point d'une <b>aura</b> qui marque une portée.
    /// </summary>
    internal void Show(Vector2 center, float fromRadius, float toRadius, Color color,
                       float width, float life, int order)
    {
        transform.position = center;

        _color = color;
        _fromRadius = fromRadius;
        _toRadius = toRadius;
        _width = width;
        _life = Mathf.Max(0.01f, life);
        _left = _life;

        if (_halo != null) _halo.sortingOrder = order;
        if (_core != null) _core.sortingOrder = order + 1;

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

        Redraw(1f - _left / _life);
    }

    private void Redraw(float t)
    {
        if (_halo == null || _core == null) return;

        float radius = Mathf.Lerp(_fromRadius, _toRadius, t);
        float a = 1f - t;

        for (int i = 0; i < Segments; i++)
        {
            float angle = i / (float)Segments * Mathf.PI * 2f;
            Buffer[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
        }

        Apply(_halo, _width * 2.2f, new Color(_color.r, _color.g, _color.b, _color.a * 0.4f * a));
        Apply(_core, _width, new Color(
            Mathf.Lerp(_color.r, 1f, 0.5f),
            Mathf.Lerp(_color.g, 1f, 0.5f),
            Mathf.Lerp(_color.b, 1f, 0.5f),
            _color.a * a));
    }

    private static void Apply(LineRenderer lr, float width, Color color)
    {
        lr.positionCount = Segments;
        lr.SetPositions(Buffer);
        lr.startWidth = lr.endWidth = width;
        lr.startColor = lr.endColor = color;
    }
}
