using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Joueur — port du cœur de <c>Player</c> (Lot 2, docs/UNITY_MIGRATION_PLAN.md).
///
/// <para><b>Mouvement par transform, sans physique dynamique</b> (§4.4, point P1 tranché) : sous
/// Godot, <c>EnemyBase.CollisionMask = 2</c> — les ennemis ne collisionnent ni entre eux ni avec le
/// joueur, seulement avec les obstacles statiques. Reproduire ici un <c>Rigidbody2D</c> ajouterait
/// une physique que le jeu d'origine n'a jamais eue.</para>
///
/// <para><b>Les i-frames sont critiques</b> : 0,45 s, valeur que le projet documente comme telle
/// pour la survie en nuée. Un joueur touché par 30 ennemis dans la même frame ne doit encaisser
/// qu'un seul coup.</para>
/// </summary>
public sealed class Player : MonoBehaviour
{
    /// <summary>Fenêtre d'invulnérabilité après un coup. Constante de gameplay, pas un réglage.</summary>
    public const float InvulnWindow = 0.45f;

    /// <summary>Rayon du corps, utilisé pour repousser les ennemis qui le chevauchent.</summary>
    private const float PlayerBodyRadius = 13f;

    public static Player? Instance { get; private set; }

    public PlayerStats Stats { get; } = new();

    /// <summary>Multiplicateur de vitesse temporaire (effets, ralentissements).</summary>
    public float SpeedMultiplier { get; set; } = 1f;

    /// <summary>Direction de visée : souris ou stick droit.</summary>
    public Vector2 AimDirection { get; private set; } = Vector2.right;

    /// <summary>Vrai quand le sprite regarde à gauche — lu par les accessoires de silhouette.</summary>
    public bool FacingLeft { get; private set; }

    /// <summary>Vitesse courante, en unités par seconde.</summary>
    public Vector2 Velocity { get; private set; }

    /// <summary>
    /// Direction imposée de l'extérieur, court-circuitant le clavier. Sert au <b>banc</b>
    /// (<c>--auto-play</c>) : le pilote automatique doit traverser exactement le même chemin de
    /// mouvement qu'un joueur humain, sinon la mesure porte sur autre chose que le jeu.
    /// </summary>
    public Vector2? ExternalMoveOverride { get; set; }

    /// <summary>Émis à chaque changement de PV : <c>(courant, max)</c>.</summary>
    public event Action<float, float>? HealthChanged;

    /// <summary>Émis quand les PV atteignent zéro.</summary>
    public event Action? Died;

    private float _invulnTimer;
    private bool  _dead;

    private void Awake()
    {
        Instance = this;
        Stats.ResetForRun();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (_dead) return;

        float dt = Time.deltaTime;
        if (_invulnTimer > 0f) _invulnTimer -= dt;

        UpdateMovement(dt);
        UpdateRegen(dt);
        PushEnemiesAside();
    }

    // ─── Déplacement ──────────────────────────────────────────────────────────

    private void UpdateMovement(float dt)
    {
        // Les touches passent par InputRemap : elles sont rebindables depuis les Options, et le
        // libellé affiché au joueur doit venir de la même source (cf. InputRemap).
        Vector2 input = ExternalMoveOverride ?? InputRemap.MoveVector();

        // La vitesse est plafonnée par StatCaps — la même source que côté Godot.
        float speed = Mathf.Min(Stats.Speed * SpeedMultiplier, StatCaps.MaxSpeed);
        Velocity = input * speed;

        Vector3 next = transform.position + (Vector3)(Velocity * dt);
        next.x = Mathf.Clamp(next.x, -Arena.HalfWidth, Arena.HalfWidth);
        next.y = Mathf.Clamp(next.y, -Arena.HalfHeight, Arena.HalfHeight);
        transform.position = next;

        if (Mathf.Abs(input.x) > 0.01f) FacingLeft = input.x < 0f;
        if (Velocity.sqrMagnitude > 1f) AimDirection = Velocity.normalized;
    }

    /// <summary>
    /// Repousse les ennemis qui chevauchent le corps, sans les bloquer — port fidèle de
    /// <c>PushEnemiesAside</c>. La séparation reste <b>sous</b> le rayon de contact de l'ennemi,
    /// pour que les dégâts continuent de s'appliquer : c'est ce qui donne la sensation de
    /// « labourer la foule » plutôt que de la pousser devant soi.
    /// </summary>
    private void PushEnemiesAside()
    {
        Vector2 me = transform.position;

        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy == null) continue;

            float sep = Mathf.Max(PlayerBodyRadius, enemy.PushRadius - 6f);
            Vector2 offset = (Vector2)enemy.transform.position - me;
            float dist = offset.magnitude;
            if (dist >= sep) continue;

            Vector2 dir = dist > 0.01f
                ? offset / dist
                : (Velocity.sqrMagnitude > 1f ? Velocity.normalized : Vector2.right);

            enemy.transform.position = me + dir * sep;
        }
    }

    // ─── Régénération ─────────────────────────────────────────────────────────

    private void UpdateRegen(float dt)
    {
        if (Stats.RegenSuppressLeft > 0f)
        {
            // Suspension sous le feu : on coupe la SOURCE, la réserve déjà constituée continue
            // d'absorber (règle du cran de saturation, GDD §33.7).
            Stats.RegenSuppressLeft -= dt;
            return;
        }

        if (Stats.HpRegenPerSecond <= 0f) return;

        float tick = Stats.HpRegenPerSecond * dt;
        float missing = Stats.MaxHp - Stats.CurrentHp;
        float applied = Mathf.Min(tick, missing);

        if (applied > 0f) Heal(applied);

        // Le surplus qui serait perdu à PV pleins alimente la réserve anti-pic.
        float surplus = tick - applied;
        if (surplus > 0f)
        {
            float cap = RegenReserve.Capacity(Stats.HpRegenPerSecond, Stats.MaxHp);
            Stats.RegenReserveCharge = Mathf.Min(Stats.RegenReserveCharge + surplus, cap);
        }
    }

    // ─── Dégâts et soins ──────────────────────────────────────────────────────

    /// <summary>
    /// Encaisse un coup. Sans effet pendant les i-frames — c'est ce qui rend une nuée survivable.
    /// </summary>
    public void TakeDamage(float amount)
    {
        if (_dead || _invulnTimer > 0f || amount <= 0f) return;

        _invulnTimer = InvulnWindow;

        // La réduction est bornée par StatCaps : une seule source de vérité avec Godot.
        float dr = Mathf.Min(Stats.DamageReduction, StatCaps.MaxDamageReduction);
        float net = amount * (1f - dr);

        // La réserve de régénération absorbe en premier, après les i-frames.
        if (Stats.RegenReserveCharge > 0f)
        {
            float absorbed = Mathf.Min(Stats.RegenReserveCharge, net);
            Stats.RegenReserveCharge -= absorbed;
            net -= absorbed;
        }

        // Tout coup encaissé suspend la régénération, même entièrement absorbé.
        Stats.RegenSuppressLeft = RegenReserve.SuppressionSeconds;

        if (net <= 0f) { HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp); return; }

        Stats.CurrentHp = Mathf.Max(0f, Stats.CurrentHp - net);
        HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);

        if (Stats.CurrentHp <= 0f)
        {
            _dead = true;
            Died?.Invoke();
        }
    }

    /// <summary>
    /// Soigne d'un montant absolu. <b>Chemin unique</b> pour tout soin : le projet a déjà connu un
    /// bug majeur parce que des soins écrivaient <c>CurrentHp</c> en direct, échappant ainsi aux
    /// crans de saturation et à l'instrumentation. Rien ne doit contourner cette méthode.
    /// </summary>
    public void HealFlat(float amount)
    {
        if (_dead || amount <= 0f) return;
        Stats.CurrentHp = Mathf.Min(Stats.MaxHp, Stats.CurrentHp + amount);
        HealthChanged?.Invoke(Stats.CurrentHp, Stats.MaxHp);
    }

    /// <summary>Soigne d'une fraction des PV max.</summary>
    public void Heal(float amount) => HealFlat(amount);

    /// <summary>Le joueur est-il invulnérable en ce moment ?</summary>
    public bool IsInvulnerable => _invulnTimer > 0f;

    /// <summary>Le joueur est-il mort ?</summary>
    public bool IsDead => _dead;
}

/// <summary>Dimensions de l'arène — reprises de <c>Constants</c> côté Godot.</summary>
public static class Arena
{
    public const int Width  = 1920;
    public const int Height = 1216;

    public const float HalfWidth  = Width / 2f;
    public const float HalfHeight = Height / 2f;
}
