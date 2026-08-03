using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ennemi de base — port de <c>EnemyBase</c> (Lot 2, docs/UNITY_MIGRATION_PLAN.md).
///
/// <para><b>Deux propriétés du jeu d'origine gouvernent tout ce fichier</b>, établies en lisant le
/// code Godot (§4.4) :</para>
/// <list type="number">
///   <item>Les <b>dégâts de contact se calculent par distance</b>, jamais par collision physique —
///         d'où l'absence totale de <c>Collider2D</c> ici ;</item>
///   <item>les ennemis <b>ne collisionnent ni entre eux ni avec le joueur</b> (<c>mask = 2</c>,
///         obstacles statiques uniquement). Il n'y a donc aucune physique à n corps à reproduire,
///         ce qui rend 300 entités bon marché (0,168 ms/pas mesuré au Lot 1).</item>
/// </list>
///
/// <para>La liste <see cref="Active"/> remplace le groupe <c>enemies</c> de l'arbre Godot
/// (<c>GetNodesInGroup</c>) : même service, sans parcours d'arbre à chaque frame.</para>
/// </summary>
public class EnemyBase : MonoBehaviour
{
    /// <summary>
    /// Tous les ennemis vivants — remplace le groupe <c>enemies</c>. Maintenue à l'activation et à
    /// la destruction, jamais reconstruite : elle est parcourue à chaque frame par le joueur et par
    /// les armes.
    /// </summary>
    public static readonly List<EnemyBase> Active = new();

    [Header("Statistiques")]
    public float MaxHp = 20f;
    public float Speed = 120f;
    public float Damage = 5f;
    public int   XpValue = 1;

    /// <summary>Rayon des dégâts de contact. Surchargeable — les champions frappent plus large.</summary>
    protected virtual float ContactRadius => 24f;

    /// <summary>Rayon utilisé par le joueur pour repousser l'ennemi hors de son corps.</summary>
    public float PushRadius => ContactRadius;

    /// <summary>Émis à la mort, avec la valeur d'XP à créditer.</summary>
    public event Action<int>? Died;

    private float _currentHp;
    private bool  _isDead;
    private FrameAnimator? _animator;

    /// <summary>PV courants — lus par l'UI et les armes.</summary>
    public float CurrentHp => _currentHp;

    /// <summary>L'ennemi est-il déjà mort ? Une arme ne doit pas le frapper deux fois.</summary>
    public bool IsDead => _isDead;

    protected virtual void Awake()
    {
        _currentHp = MaxHp;
        _animator = GetComponentInChildren<FrameAnimator>();
    }

    protected virtual void OnEnable() => Active.Add(this);

    protected virtual void OnDisable() => Active.Remove(this);

    protected virtual void Update()
    {
        if (_isDead) return;

        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        float dt = Time.deltaTime;
        UpdateMovement(player, dt);
        HandleContactDamage(player, dt);
    }

    /// <summary>Poursuite directe. Les sous-classes changent ce comportement (kite, erratique…).</summary>
    protected virtual void UpdateMovement(Player player, float dt)
    {
        Vector2 to = (Vector2)player.transform.position - (Vector2)transform.position;
        float dist = to.magnitude;
        if (dist < 0.001f) return;

        Vector2 dir = to / dist;
        transform.position += (Vector3)(dir * Speed * dt);

        if (_animator != null)
        {
            _animator.FlipX = dir.x < 0f;
            _animator.Play("move");
        }
    }

    /// <summary>
    /// Dégâts de contact, par <b>distance</b> et non par collision. Le joueur porte des i-frames de
    /// 0,45 s, ce qui borne naturellement les dégâts d'une nuée : appeler ceci à chaque frame pour
    /// 300 ennemis reste correct.
    /// </summary>
    protected virtual void HandleContactDamage(Player player, float dt)
    {
        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist < ContactRadius) DealDiscreteDamage(player, Damage);
    }

    /// <summary>
    /// Chemin <b>unique</b> des coups discrets. Le projet a introduit cette centralisation côté
    /// Godot parce que huit appelants recopiaient la réduction de dégâts, et parce qu'un plancher
    /// exprimé en pourcentage des PV max ne doit <b>jamais</b> toucher un dégât continu — appliqué
    /// à chaque tick, il tuerait en quelques frames.
    /// </summary>
    protected void DealDiscreteDamage(Player player, float amount) => player.TakeDamage(amount);

    /// <summary>Applique le scaling de vague — voir <c>EnemyScaling</c> (logique pure partagée).</summary>
    public void ApplyScaling(float scaledMaxHp, float scaledDamage)
    {
        MaxHp = scaledMaxHp;
        Damage = scaledDamage;
        _currentHp = MaxHp;
    }

    /// <summary>Installe le jeu d'animations, comme <c>SetSpriteFrames</c> sous Godot.</summary>
    public void SetSpriteFrames(SpriteFramesAsset frames)
    {
        _animator ??= GetComponentInChildren<FrameAnimator>();
        if (_animator == null) return;

        _animator.SetSpriteFrames(frames);
        _animator.Play("idle");
    }

    /// <summary>Encaisse des dégâts. Sans effet si l'ennemi est déjà mort.</summary>
    public virtual void TakeDamage(float amount)
    {
        if (_isDead || amount <= 0f) return;

        _currentHp -= amount;
        if (_currentHp <= 0f) Die();
    }

    /// <summary>Mort : crédite l'XP puis retire l'ennemi.</summary>
    protected virtual void Die()
    {
        if (_isDead) return;
        _isDead = true;

        Died?.Invoke(XpValue);
        XpSystem.Instance?.AddXp(XpValue);

        Destroy(gameObject);
    }
}
