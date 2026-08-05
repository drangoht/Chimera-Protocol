using UnityEngine;

/// <summary>
/// Projectile — port de <c>Bullet</c>. Touche par <b>distance</b>, comme tout le reste du jeu :
/// aucun collider, donc aucun coût de physique pour les centaines de projectiles simultanés.
/// </summary>
public sealed class Bullet : MonoBehaviour
{
    [Tooltip("Rayon de collision avec un ennemi.")]
    public float HitRadius = 12f;

    /// <summary>
    /// Le projectile traverse ses cibles au lieu de s'arrêter à la première.
    /// </summary>
    /// <remarks>
    /// Un projectile perforant garde la liste de ce qu'il a déjà touché : sans elle, il infligerait
    /// ses dégâts <b>à chaque frame</b> tant qu'il chevauche un ennemi, ce qui en ferait de très
    /// loin l'arme la plus forte du jeu.
    /// </remarks>
    public bool Piercing;

    /// <summary>
    /// Niveau de l'arme qui a tiré (1-8). Il ne change <b>rien</b> aux dégâts — ceux-ci arrivent déjà
    /// calculés — mais calibre la brillance du projectile et la violence de son impact. Sans lui, un
    /// tir de niveau 20 cogne à l'écran exactement comme un tir de niveau 1, et la progression ne se
    /// lit plus que dans les chiffres.
    /// </summary>
    public int Power = 1;

    /// <summary>Teinte du projectile — reprise pour son impact, sinon la gerbe jure avec le tir.</summary>
    private Color _tint = new(0.267f, 1f, 0.933f);

    private readonly System.Collections.Generic.HashSet<EnemyBase> _pierced = new();
    private Vector2 _velocity;
    private float   _damage;
    private float   _rangeLeft;

    /// <summary>
    /// Pose la lueur et la traînée. Appelé depuis <see cref="Launch"/> et <b>surtout pas</b> depuis
    /// <c>Awake</c> : l'arme affecte <see cref="Power"/> après l'instanciation, donc un
    /// <c>Awake</c> lirait toujours 1. Le code d'origine avait précisément ce défaut — sous Godot,
    /// <c>_Ready()</c> part dès l'<c>AddChild</c>, avant l'affectation de <c>Power</c>, et la
    /// brillance des projectiles n'a jamais suivi le niveau de l'arme.
    /// </summary>
    private void AttachVisuals()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) _tint = sr.color;

        AttachGlow();
        AttachTrail();
    }

    private void AttachGlow()
    {
        int p = Mathf.Clamp(Power, 1, 8);

        var go = new GameObject("Glow", typeof(SpriteRenderer));
        go.transform.SetParent(transform, false);

        // ⚠ Valeurs CALIBRÉES SUR CAPTURE, et non transposées de Godot. Les nombres d'origine
        // (185 px de diamètre à niveau 8, alpha 0,7) donnaient à l'écran d'énormes flaques cyan qui
        // noyaient l'arène : une arme à tir rapide envoie un FLUX de projectiles, et en mélange
        // additif leurs halos se cumulent jusqu'à saturer. Ce qui reste lisible seul devient un
        // aplat à dix exemplaires — c'est le cumul qu'il faut regarder, jamais le sprite isolé.
        float diameter = 14f + p * 2.5f;
        go.transform.localScale = Vector3.one * (diameter / 64f);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = VfxPrimitives.Glow;
        sr.sharedMaterial = VfxPrimitives.Additive;
        sr.color = new Color(_tint.r, _tint.g, _tint.b, Mathf.Clamp01(0.12f + p * 0.02f));
        sr.sortingOrder = 19;   // juste sous le projectile lui-même
    }

    private void AttachTrail()
    {
        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.time = 0.08f;
        trail.startWidth = 7f;
        trail.endWidth = 0f;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.sharedMaterial = VfxPrimitives.AdditiveBeam;
        trail.sortingOrder = 18;
        trail.startColor = new Color(_tint.r, _tint.g, _tint.b, 0.6f);
        trail.endColor = new Color(_tint.r, _tint.g, _tint.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>Arme le projectile. À appeler juste après l'instanciation.</summary>
    public void Launch(Vector2 velocity, float damage, float range)
    {
        _velocity  = velocity;
        _damage    = damage;
        _rangeLeft = range;

        AttachVisuals();
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        Vector2 step = _velocity * dt;

        transform.position += (Vector3)step;

        _rangeLeft -= step.magnitude;
        if (_rangeLeft <= 0f) { Destroy(gameObject); return; }

        Vector2 me = transform.position;
        float sqr = HitRadius * HitRadius;

        // Copie de sécurité : un projectile perforant poursuit sa course après avoir touché, donc la
        // boucle survit à une mise à mort — laquelle retire de EnemyBase.Active pendant l'énumération.
        foreach (var e in EnemyBase.Active.ToArray())
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - me).sqrMagnitude > sqr) continue;

            if (Piercing)
            {
                if (!_pierced.Add(e)) continue;   // déjà traversé : pas de second coup
                e.TakeDamage(_damage);
                Vfx.Impact(e.transform.position, Power, _tint);
                continue;                          // le projectile poursuit sa course
            }

            e.TakeDamage(_damage);
            Vfx.Impact(me, Power, _tint);
            Destroy(gameObject);
            return;
        }
    }
}
