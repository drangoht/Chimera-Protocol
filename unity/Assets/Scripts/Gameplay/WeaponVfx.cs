using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Traces visibles des armes — arcs, chaînes, anneaux, faisceaux (Lot 5 ter).
///
/// <para><b>Pourquoi cette classe existe.</b> Signalé en jouant : « je ne vois pas les autres armes,
/// ni leurs projectiles ». Le relevé en scène réelle a montré qu'elles fonctionnaient toutes — elles
/// tiraient, touchaient et tuaient — mais que <b>huit sur douze n'affichaient rien</b> : arcs,
/// chaînes, auras, drones, faisceaux et zones étaient de la pure logique. Une arme qui tue sans
/// laisser de trace ne se lit pas comme « effet manquant », elle se lit comme <b>« la carte n'a rien
/// fait »</b> — et c'est le pire retour possible sur une progression.</para>
///
/// <para><b>Placeholder assumé.</b> Les formes sont dessinées avec des points de sprite recyclés :
/// pas de shader, pas de matériau à référencer, donc rien qui puisse être supprimé du build ou
/// dépendre du pipeline de rendu. Les VFX d'origine (dessin procédural, shaders, particules)
/// restent à porter ; ce qui compte ici est que <b>chaque arme se voie</b>.</para>
///
/// <para>Le recyclage n'est pas un raffinement : une aura, une chaîne et un cône dessinés à chaque
/// tir de chaque arme représentent des centaines de points par seconde. Les créer et les détruire
/// produirait un ramassage de miettes permanent, à 200-300 entités déjà à l'écran.</para>
/// </summary>
public static class WeaponVfx
{
    /// <summary>Durée de vie d'une trace, en secondes. Assez courte pour rester lisible en nuée.</summary>
    public const float DefaultLife = 0.18f;

    private static readonly Stack<VfxDot> _pool = new();
    private static Transform? _root;

    /// <summary>Points actuellement affichés — observable par les bancs et le diagnostic.</summary>
    public static int ActiveDots { get; private set; }

    /// <summary>
    /// Points créés depuis le début, cumulés. C'est cette valeur — et non le compte instantané — qui
    /// permet de vérifier qu'une arme <b>a laissé une trace</b> : les traces s'effacent en 0,2 s, donc
    /// un relevé ponctuel raterait presque toujours l'événement.
    /// </summary>
    public static int DotsCreated { get; private set; }

    /// <summary>Un point unique.</summary>
    public static void Dot(Vector2 position, Color color, float size = 10f, float life = DefaultLife)
    {
        var dot = Rent();
        if (dot != null) dot.Show(position, color, size, life);
    }

    /// <summary>Segment entre deux points — chaîne d'éclairs, faisceau, lien de drone.</summary>
    public static void Line(Vector2 from, Vector2 to, Color color, float size = 8f,
                            float life = DefaultLife, float spacing = 18f)
    {
        float distance = Vector2.Distance(from, to);
        int steps = Mathf.Clamp(Mathf.RoundToInt(distance / Mathf.Max(1f, spacing)), 1, 40);

        for (int i = 0; i <= steps; i++)
            Dot(Vector2.Lerp(from, to, i / (float)steps), color, size, life);
    }

    /// <summary>
    /// Arc centré sur <paramref name="direction"/>. Un arc de 360° dessine un anneau complet — c'est
    /// ainsi que les auras et les champs se rendent, sans code séparé.
    /// </summary>
    public static void Arc(Vector2 center, Vector2 direction, float halfAngleDeg, float radius,
                           Color color, float size = 9f, float life = DefaultLife)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        int steps = Mathf.Clamp(Mathf.RoundToInt(halfAngleDeg / 6f), 3, 60);

        for (int i = -steps; i <= steps; i++)
        {
            float a = (baseAngle + halfAngleDeg * i / steps) * Mathf.Deg2Rad;
            Dot(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, size, life);
        }
    }

    /// <summary>Anneau complet — impulsion, aura, singularité.</summary>
    public static void Ring(Vector2 center, float radius, Color color,
                            float size = 9f, float life = DefaultLife)
        => Arc(center, Vector2.right, 180f, radius, color, size, life);

    /// <summary>
    /// Cône plein : deux bords et un arc extérieur. C'est la forme qui rend lisible <b>ce que l'arme
    /// couvre</b> — un simple arc extérieur ne dirait pas d'où part l'attaque.
    /// </summary>
    public static void Cone(Vector2 origin, Vector2 direction, float halfAngleDeg, float radius,
                            Color color, float life = DefaultLife)
    {
        if (direction.sqrMagnitude < 0.0001f) direction = Vector2.right;

        float baseAngle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        foreach (float edge in new[] { -halfAngleDeg, halfAngleDeg })
        {
            float a = (baseAngle + edge) * Mathf.Deg2Rad;
            Line(origin, origin + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, 8f, life, 22f);
        }

        Arc(origin, direction, halfAngleDeg, radius, color, 9f, life);
    }

    // ─── Recyclage ────────────────────────────────────────────────────────────

    private static VfxDot? Rent()
    {
        // Plafond dur : en nuée, plusieurs armes peuvent demander des centaines de points dans la
        // même frame. Mieux vaut une trace incomplète qu'une chute de cadence.
        if (ActiveDots >= 900) return null;

        VfxDot dot;
        if (_pool.Count > 0)
        {
            dot = _pool.Pop();
        }
        else
        {
            EnsureRoot();
            var go = new GameObject("VfxDot", typeof(SpriteRenderer), typeof(VfxDot));
            go.transform.SetParent(_root, false);
            dot = go.GetComponent<VfxDot>();
        }

        ActiveDots++;
        DotsCreated++;
        return dot;
    }

    internal static void Return(VfxDot dot)
    {
        ActiveDots--;
        _pool.Push(dot);
    }

    private static void EnsureRoot()
    {
        if (_root != null) return;

        var go = new GameObject("[WeaponVfx]");
        _root = go.transform;
    }

    /// <summary>
    /// Oublie le vivier au changement de scène. Sans cela, la pile garde des références sur des
    /// objets détruits, et le premier tir de la run suivante les réutilise — donc n'affiche rien.
    /// </summary>
    public static void Reset()
    {
        _pool.Clear();
        _root = null;
        ActiveDots = 0;
    }
}

/// <summary>Un point de trace : s'affiche, s'estompe, retourne au vivier.</summary>
public sealed class VfxDot : MonoBehaviour
{
    private SpriteRenderer? _renderer;
    private float _life;
    private float _left;
    private Color _color;

    private SpriteRenderer Renderer => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();

    internal void Show(Vector2 position, Color color, float size, float life)
    {
        transform.position = position;
        transform.localScale = new Vector3(size, size, 1f);

        _color = color;
        _life = Mathf.Max(0.01f, life);
        _left = _life;

        var r = Renderer;
        r.sprite = UiPrimitives.White;
        r.color = color;

        // Sous les entités : une trace ne doit jamais masquer un ennemi ni le joueur.
        r.sortingOrder = 8;

        gameObject.SetActive(true);
    }

    private void Update()
    {
        _left -= Time.deltaTime;

        if (_left <= 0f)
        {
            gameObject.SetActive(false);
            WeaponVfx.Return(this);
            return;
        }

        // Estompe : sans elle, les traces s'éteignent d'un coup et l'écran clignote.
        var c = _color;
        c.a = Mathf.Clamp01(_left / _life);
        Renderer.color = c;
    }
}
