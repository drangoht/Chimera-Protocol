using UnityEngine;

/// <summary>
/// Lueur additive éphémère — le portage du <c>PointLight2D</c> en mélange additif de Godot.
///
/// <para>Godot posait une <i>vraie</i> lumière 2D ; ici c'est un disque à dégradé, additionné à
/// l'image. Le résultat visuel est le même parce que ces lumières n'éclairaient rien : aucun sprite
/// du jeu n'était en matériau éclairé, elles ne servaient qu'à <b>ajouter de la clarté</b> au point
/// d'impact. Reproduire le vrai éclairage (Light2D d'URP) imposerait de rendre tous les sprites du
/// jeu « éclairés », donc de gérer une lumière globale — beaucoup de risque pour zéro différence
/// à l'écran.</para>
/// </summary>
public sealed class VfxGlow : MonoBehaviour
{
    private SpriteRenderer? _renderer;
    private Color _color;
    private float _life;
    private float _left;

    private Vector2 _drift;
    private float _scale;
    private float _growth = 1f;
    private bool _animated;

    private SpriteRenderer Renderer
        => _renderer != null ? _renderer : _renderer = GetComponent<SpriteRenderer>();

    /// <summary>Allume la lueur. <paramref name="radiusPx"/> est un rayon, pas un diamètre.</summary>
    /// <param name="intensity">
    /// L'« énergie » de Godot. Au-delà de 1 elle ne fait plus monter l'alpha (borné), mais les
    /// lueurs superposées continuent de s'additionner — c'est ainsi qu'un impact fort blanchit.
    /// </param>
    /// <param name="drift">
    /// Dérive, en pixels par seconde. Une lueur d'impact reste où elle est frappée ; une <b>bouffée
    /// de fumée</b> doit s'élever et quitter le corps qui l'a produite, sans quoi elle se lit comme
    /// une tache posée sur l'ennemi.
    /// </param>
    /// <param name="growth">
    /// Facteur d'expansion sur la durée de vie. Ce qui distingue de la fumée d'un simple point qui
    /// s'éteint, c'est qu'elle <b>s'étale en se diluant</b>.
    /// </param>
    internal void Show(Vector2 position, Color color, float radiusPx, float intensity, float life, int order,
                       Vector2 drift = default, float growth = 1f)
    {
        transform.position = position;

        // Le sprite mesure 64 unités (1 px = 1 unité) : l'échelle est donc directement le rapport
        // du diamètre voulu à cette taille de référence.
        float scale = radiusPx * 2f / 64f;
        transform.localScale = new Vector3(scale, scale, 1f);

        _drift = drift;
        _scale = scale;
        _growth = growth;

        // Le cas courant — un flash immobile de taille fixe — ne paie rien : sans ce drapeau, deux
        // cent vingt lueurs réécriraient position et échelle à chaque image pour ne rien changer.
        _animated = drift.sqrMagnitude > 0.0001f || !Mathf.Approximately(growth, 1f);

        _color = new Color(color.r, color.g, color.b, Mathf.Clamp01(color.a * intensity));
        _life = Mathf.Max(0.01f, life);
        _left = _life;

        var r = Renderer;
        r.sprite = VfxPrimitives.Glow;
        r.sharedMaterial = VfxPrimitives.Additive;
        r.color = _color;
        r.sortingOrder = order;

        gameObject.SetActive(true);
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

        // Décroissance douce plutôt que linéaire : sous Godot le fondu des lumières était en
        // EaseOut, et c'est ce qui fait qu'un flash « claque » puis s'attarde au lieu de s'éteindre
        // comme un interrupteur.
        float t = 1f - _left / _life;
        var c = _color;
        c.a = _color.a * Mathf.Pow(1f - t, 1.6f);
        Renderer.color = c;

        if (!_animated) return;

        transform.position += (Vector3)(_drift * Time.deltaTime);

        float s = _scale * Mathf.Lerp(1f, _growth, t);
        transform.localScale = new Vector3(s, s, 1f);
    }
}
