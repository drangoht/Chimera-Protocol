using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Bobine Tesla — archétype <b>chaîne</b> (Lot 3).
///
/// <para>Frappe l'ennemi le plus proche, puis rebondit vers ses voisins. Chaque cible n'est touchée
/// <b>qu'une fois par décharge</b> : sans cet ensemble de cibles déjà frappées, la chaîne
/// rebondirait indéfiniment entre deux ennemis proches et infligerait des dégâts illimités en une
/// frame.</para>
///
/// <para>La portée de rebond est mesurée <b>depuis la cible précédente</b>, pas depuis le joueur :
/// c'est ce qui permet à la chaîne de progresser dans la nuée au lieu de rester collée au porteur.</para>
/// </summary>
public sealed class TeslaCoil : WeaponBase
{
    [Header("Chaîne")]
    [Tooltip("Rebonds après la cible initiale.")]
    public int ChainCount = 2;

    [Tooltip("Portée d'un rebond, mesurée depuis la cible précédente.")]
    public float ChainRange = 160f;

    private readonly HashSet<EnemyBase> _alreadyHit = new();

    /// <summary>Cibles touchées par la dernière décharge — observable pour les tests et le HUD.</summary>
    public int LastChainLength { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 14f;
        BaseCooldown = 1.20f;
        Range = 300f;

        // base.Awake() EN DERNIER : c'est lui qui fige la valeur de fiche, et il doit donc
        // voir celles posées ci-dessus. Même exigence d'ordre que le `base._Ready()` de Godot,
        // pour une raison différente — ici c'est la capture, là-bas l'initialisation.
        base.Awake();
    }

    protected override bool TryFire()
    {
        var first = FindNearestEnemy();
        if (first == null) return false;

        _alreadyHit.Clear();
        LastChainLength = 0;

        EnemyBase? current = first;
        float damage = EffectiveDamage;

        for (int i = 0; i <= ChainCount && current != null; i++)
        {
            _alreadyHit.Add(current);
            Vector2 from = current.transform.position;

            current.TakeDamage(damage);
            LastChainLength++;

            current = FindNearestUnhit(from);
        }

        return true;
    }

    /// <summary>Voisin le plus proche non encore touché, dans la portée de rebond.</summary>
    private EnemyBase? FindNearestUnhit(Vector2 from)
    {
        EnemyBase? best = null;
        float bestSqr = ChainRange * ChainRange;

        foreach (var e in EnemyBase.Active)
        {
            if (e == null || e.IsDead || _alreadyHit.Contains(e)) continue;

            float sqr = ((Vector2)e.transform.position - from).sqrMagnitude;
            if (sqr < bestSqr) { bestSqr = sqr; best = e; }
        }
        return best;
    }
}
