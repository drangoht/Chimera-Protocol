using UnityEngine;

/// <summary>
/// Champ de Surcharge — archétype <b>aura pulsée</b> (Lot 3).
///
/// <para>Frappe périodiquement <b>tout</b> ce qui se trouve dans un rayon autour du joueur, avec un
/// recul. Contrairement à une aura continue, les dégâts sont <b>discrets</b> : une impulsion toutes
/// les <c>Cooldown</c> secondes.</para>
///
/// <para>⚠ La distinction discret/continu n'est pas cosmétique dans ce jeu : le cran de saturation VI
/// applique un plancher de dégâts en pourcentage des PV max qui ne doit <b>jamais</b> toucher un
/// dégât continu — appliqué à chaque tick, il tuerait en quelques frames. Une arme pulsée comme
/// celle-ci relève bien du chemin discret.</para>
/// </summary>
public class OverloadField : WeaponBase
{
    [Header("Champ")]
    public float Radius = 100f;

    [Tooltip("Distance de recul appliquée aux ennemis touchés.")]
    public float Knockback = 40f;

    /// <summary>Ennemis touchés par la dernière impulsion — observable pour les tests et le HUD.</summary>
    public int LastPulseHits { get; private set; }

    protected override void Awake()
    {
        BaseDamage = 8f;
        BaseCooldown = 2.5f;
        Range = Radius;

        // base.Awake() EN DERNIER : c'est lui qui fige la valeur de fiche, et il doit donc
        // voir celles posées ci-dessus. Même exigence d'ordre que le `base._Ready()` de Godot,
        // pour une raison différente — ici c'est la capture, là-bas l'initialisation.
        base.Awake();
    }

    protected override bool TryFire()
    {
        Vector2 center = transform.position;
        float sqr = Radius * Radius;
        float damage = EffectiveDamage;

        LastPulseHits = 0;

        // Copie de sécurité : TakeDamage peut tuer, donc retirer des éléments de la liste pendant
        // qu'on la parcourt.
        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;

            Vector2 offset = (Vector2)e.transform.position - center;
            if (offset.sqrMagnitude > sqr) continue;

            float dist = offset.magnitude;
            Vector2 dir = dist > 0.01f ? offset / dist : Vector2.right;
            e.transform.position = (Vector2)e.transform.position + dir * Knockback;

            e.TakeDamage(damage);
            LastPulseHits++;
        }

        // L'anneau n'est dessiné que si l'impulsion part vraiment : TryFire est appelée à chaque
        // frame une fois la recharge prête, donc dessiner ici sans condition afficherait une aura
        // permanente qui ne dirait plus rien du rythme de l'arme.
        if (LastPulseHits > 0)
        {
            // Onde qui se DILATE jusqu'à la portée, et non anneau posé : l'arme repousse, et un
            // cercle immobile ne dit pas dans quel sens.
            Vfx.Shockwave(center, Radius, 0.22f, new Color(0.67f, 0.27f, 1f));
            Vfx.Glow(center, new Color(0.67f, 0.27f, 1f), Radius * 0.22f, 0.6f, 0.18f);
        }

        // Une impulsion dans le vide ne doit pas consommer la recharge : sinon l'arme se déclenche
        // sans effet pendant les creux et se retrouve en recharge quand la nuée revient.
        return LastPulseHits > 0;
    }
}
