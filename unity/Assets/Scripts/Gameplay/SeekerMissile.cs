using UnityEngine;

/// <summary>
/// Missile à tête chercheuse — projectile <b>guidé</b> qui corrige sa trajectoire vers sa cible.
///
/// <para>Le guidage est borné par <see cref="TurnRateDeg"/> : un missile qui tournerait
/// instantanément toucherait toujours, ce qui supprimerait toute notion de placement. La cible est
/// reverrouillée si elle meurt avant l'impact — sinon la moitié d'une salve se perdrait dès qu'une
/// autre arme tue en premier.</para>
/// </summary>
public sealed class SeekerMissile : MonoBehaviour
{
    public float Speed = 300f;
    public float HitRadius = 14f;

    [Tooltip("Vitesse de correction de trajectoire, en degrés par seconde.")]
    public float TurnRateDeg = 220f;

    public float Lifetime = 3.5f;

    private Vector2 _dir = Vector2.right;
    private float _damage;
    private float _timeLeft;
    private EnemyBase? _target;

    /// <summary>Arme le missile. À appeler juste après l'instanciation.</summary>
    public void Launch(Vector2 direction, float damage, EnemyBase? target)
    {
        _dir = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector2.right;
        _damage = damage;
        _target = target;
        _timeLeft = Lifetime;

        Dress();
        FaceHeading();
    }

    /// <summary>
    /// Pose la silhouette du missile — celle de sa carte, et non le sprite du gabarit.
    /// </summary>
    /// <remarks>
    /// ⚠ Même correctif que pour la Lame Boomerang, et pour la même raison : le gabarit servait une
    /// primitive d'emprunt (<c>weapon_bullet_rail</c>, une barre droite teintée en or), si bien que
    /// l'Essaim Traqueur <b>tirait des traits</b> indiscernables du Canon à Impulsions. Le gabarit
    /// garde donc une référence inerte — ce qui fait foi est ici.
    /// </remarks>
    private void Dress()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr == null) return;

        sr.sprite = MissileSprite.Get();
        sr.color = Color.white;   // la teinte vit dans le sprite : un multiplicateur l'assombrirait

        if (_halo != null) return;

        // Halo violet : un missile de 28 px se perd dans une nuée dès qu'il passe devant un ennemi
        // clair, et c'est le projectile qu'il faut suivre puisqu'il vire. Un disque radial est
        // invariant par rotation : il peut rester enfant d'un objet qui s'oriente.
        _halo = new GameObject("Halo", typeof(SpriteRenderer));
        _halo.transform.SetParent(transform, false);

        var hr = _halo.GetComponent<SpriteRenderer>();
        hr.sprite = VfxPrimitives.Glow;
        hr.sharedMaterial = VfxPrimitives.Additive;
        hr.color = new Color(MissileSprite.Body.r, MissileSprite.Body.g, MissileSprite.Body.b, 0.45f);
        hr.sortingOrder = sr.sortingOrder - 1;

        // Le sprite de lueur mesure 64 unités : l'échelle est le rapport du diamètre voulu à 64.
        float diameter = HitRadius * 2.4f;
        _halo.transform.localScale = Vector3.one * (diameter / 64f);

        AttachTrail();
    }

    /// <summary>
    /// Traînée violette derrière le missile — <b>la signature de sa vignette</b>.
    /// </summary>
    /// <remarks>
    /// L'icône de l'arme montre un missile suivi d'un chapelet de points incurvé, et c'est cette
    /// courbe qui la rend reconnaissable entre toutes. Elle sert aussi le jeu : un projectile qui
    /// <b>vire</b> se suit à sa trace bien mieux qu'à sa silhouette de 28 px, surtout dans une nuée.
    /// </remarks>
    private void AttachTrail()
    {
        var trail = gameObject.AddComponent<TrailRenderer>();

        trail.time = 0.18f;              // plus long que celui d'une balle : la trajectoire COURBE
        trail.startWidth = 5f;
        trail.endWidth = 0f;
        trail.numCapVertices = 2;
        trail.alignment = LineAlignment.View;
        trail.textureMode = LineTextureMode.Stretch;
        trail.sharedMaterial = VfxPrimitives.AdditiveBeam;
        trail.sortingOrder = 18;
        trail.startColor = new Color(MissileSprite.Body.r, MissileSprite.Body.g, MissileSprite.Body.b, 0.65f);
        trail.endColor = new Color(MissileSprite.Body.r, MissileSprite.Body.g, MissileSprite.Body.b, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    private GameObject? _halo;

    /// <summary>
    /// Oriente le missile dans le sens de son vol.
    /// </summary>
    /// <remarks>
    /// ⚠ Le portage ne tournait <b>jamais</b> le transform : il ne faisait qu'avancer le long de
    /// <c>_dir</c>. Tant que le projectile était une barre symétrique, cela ne se voyait pas. Avec
    /// une ogive, un missile qui part vers la gauche volerait <b>à reculons</b> — le genre de défaut
    /// qu'un sprite corrige et révèle du même geste.
    /// </remarks>
    private void FaceHeading()
        => transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(_dir.y, _dir.x) * Mathf.Rad2Deg);

    private void Update()
    {
        float dt = Time.deltaTime;

        _timeLeft -= dt;
        if (_timeLeft <= 0f) { Destroy(gameObject); return; }

        // Reverrouillage : sans lui, une salve se perd dès qu'une autre arme tue la cible.
        if (_target == null || _target.IsDead) _target = FindNearest();

        if (_target != null)
        {
            Vector2 desired = ((Vector2)_target.transform.position - (Vector2)transform.position).normalized;
            float maxTurn = TurnRateDeg * Mathf.Deg2Rad * dt;

            float angle = Vector2.SignedAngle(_dir, desired) * Mathf.Deg2Rad;
            float clamped = Mathf.Clamp(angle, -maxTurn, maxTurn);

            float c = Mathf.Cos(clamped), s = Mathf.Sin(clamped);
            _dir = new Vector2(_dir.x * c - _dir.y * s, _dir.x * s + _dir.y * c).normalized;
        }

        transform.position += (Vector3)(_dir * Speed * dt);
        FaceHeading();   // le guidage change le cap : la silhouette doit le suivre

        Vector2 me = transform.position;
        float sqr = HitRadius * HitRadius;

        foreach (var e in EnemyBase.Active)
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - me).sqrMagnitude > sqr) continue;

            e.TakeDamage(_damage);
            Destroy(gameObject);
            return;
        }
    }

    private EnemyBase? FindNearest()
    {
        EnemyBase? best = null;
        float bestSqr = float.MaxValue;
        Vector2 me = transform.position;

        foreach (var e in EnemyBase.Active)
        {
            if (e == null || e.IsDead) continue;
            float sqr = ((Vector2)e.transform.position - me).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = e; }
        }
        return best;
    }
}
