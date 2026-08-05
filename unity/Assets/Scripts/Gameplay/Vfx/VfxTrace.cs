using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Trait lumineux à deux passes — le portage de la paire de <c>Line2D</c> de Godot (halo large
/// translucide + cœur fin sur-exposé).
///
/// <para><b>Pourquoi deux passes et pas un trait épais.</b> Un seul trait donne une barre de
/// couleur ; c'est le contraste entre un halo diffus et un cœur blanc franc qui fait lire
/// « énergie ». Tous les faisceaux, éclairs, arcs et anneaux du jeu sont construits ainsi sous
/// Godot, et c'est la différence la plus visible avec les chapelets de points qui les remplaçaient
/// jusqu'ici.</para>
///
/// <para>La même classe dessine <b>toutes</b> les formes : une polyligne suffit à décrire un
/// faisceau (2 points), un éclair dentelé (N points), un arc, un anneau fermé ou le contour d'un
/// cône. Il n'y a donc pas un composant par arme à maintenir.</para>
/// </summary>
public sealed class VfxTrace : MonoBehaviour
{
    private LineRenderer? _halo;
    private LineRenderer? _core;

    private Color _color;
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
        lr.useWorldSpace = true;

        // Le trait est un ruban qui doit faire face à la caméra : en vue de dessus orthographique,
        // toute autre orientation le rendrait invisible par la tranche.
        lr.alignment = LineAlignment.View;
        lr.textureMode = LineTextureMode.Stretch;
        lr.numCapVertices = 4;
        lr.numCornerVertices = 4;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = material;

        return lr;
    }

    /// <summary>
    /// Affiche la polyligne. <paramref name="points"/> peut être plus grand que
    /// <paramref name="count"/> : les traces réutilisent un tampon partagé pour ne rien allouer.
    /// </summary>
    internal void Show(Vector3[] points, int count, Color color,
                       float coreWidth, float haloWidth, float life, int order)
    {
        if (_halo == null || _core == null) return;

        count = Mathf.Clamp(count, 2, points.Length);

        Apply(_halo, points, count, haloWidth, order);
        Apply(_core, points, count, coreWidth, order + 1);

        _color = color;
        _life = Mathf.Max(0.01f, life);
        _left = _life;

        Tint(1f);
        gameObject.SetActive(true);
    }

    private static void Apply(LineRenderer lr, Vector3[] points, int count, float width, int order)
    {
        lr.positionCount = count;
        lr.SetPositions(points);
        lr.startWidth = width;
        lr.endWidth = width;
        lr.sortingOrder = order;
    }

    private void Tint(float k)
    {
        if (_halo == null || _core == null) return;

        // Le halo reste franchement translucide, le cœur presque opaque et tiré vers le blanc :
        // c'est ce déséquilibre qui produit la sur-exposition, pas une couleur plus vive.
        var halo = new Color(_color.r, _color.g, _color.b, _color.a * 0.45f * k);
        var core = new Color(
            Mathf.Lerp(_color.r, 1f, 0.55f),
            Mathf.Lerp(_color.g, 1f, 0.55f),
            Mathf.Lerp(_color.b, 1f, 0.55f),
            _color.a * k);

        _halo.startColor = halo;
        _halo.endColor = halo;
        _core.startColor = core;
        _core.endColor = core;
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

        Tint(_left / _life);
    }
}
