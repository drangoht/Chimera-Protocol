using Godot;
using System.Collections.Generic;

public abstract partial class WeaponBase : Node2D
{
    [Export] public float Damage { get; set; } = 10f;
    [Export] public float Cooldown { get; set; } = 0.8f;
    [Export] public float ProjectileSpeed { get; set; } = 400f;

    /// <summary>Multiplicateur global de cadence de tir (power-up Surcadence). 1 = normal.
    /// Réinitialisé à chaque run par Player._Ready (statique → ne doit pas fuir entre runs).</summary>
    public static float FireRateMultiplier = 1f;

    /// <summary>
    /// Dégâts « de fiche » de l'arme, avant niveau et multiplicateurs — capturés UNE FOIS.
    ///
    /// Les armes de base tiennent ces valeurs dans <c>weapons.json</c> ; les FUSIONS les posent en dur
    /// dans leur propre <c>_Ready</c> (mécaniques trop spécifiques pour être décrites en JSON). Sans
    /// cette référence, <see cref="InventorySystem"/> ne pourrait pas leur appliquer le niveau ni le
    /// multiplicateur de dégâts du joueur sans les cumuler à chaque recalcul (le Noyau Thermique et
    /// les améliorations du Hub rappellent <c>RefreshWeaponDamages</c> plusieurs fois par run).
    /// </summary>
    public float BaseDamage { get; private set; }

    /// <summary>Cadence de fiche, avant réduction de recharge (Capaciteur, Hub). Voir <see cref="BaseDamage"/>.</summary>
    public float BaseCooldown { get; private set; }
    private bool _baseStatsCaptured;

    /// <summary>Mémorise les stats de fiche au premier appel ; sans effet ensuite (idempotent).</summary>
    public void CaptureBaseDamage()
    {
        if (_baseStatsCaptured) return;
        BaseDamage   = Damage;
        BaseCooldown = Cooldown;
        _baseStatsCaptured = true;
    }

    /// <summary>
    /// Facteur appliqué aux dégâts de fiche (niveau × multiplicateurs), 1 avant toute mise à
    /// l'échelle. Sert aux effets annexes chiffrés en dur dans une arme — brûlure, ralentissement —
    /// pour qu'ils suivent la progression au lieu de rester à leur valeur de départ.
    /// </summary>
    public float DamageScale => BaseDamage > 0f ? Damage / BaseDamage : 1f;

    private float _timer;

    public override void _Ready()
    {
        _timer = Cooldown;
    }

    public override void _Process(double delta)
    {
        _timer -= (float)delta * FireRateMultiplier;
        if (_timer <= 0f)
        {
            Attack();
            _timer = Cooldown;
        }
    }

    protected abstract void Attack();

    /// <summary>Les <paramref name="count"/> ennemis les plus proches (triés, distincts).
    /// Nom distinct des helpers privés historiques de certaines armes (évite CS0108).</summary>
    protected List<EnemyBase> AcquireNearestEnemies(int count)
    {
        var list = new List<(float dist, EnemyBase enemy)>();
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
            if (node is EnemyBase e)
                list.Add((GlobalPosition.DistanceSquaredTo(e.GlobalPosition), e));
        list.Sort((a, b) => a.dist.CompareTo(b.dist));

        var result = new List<EnemyBase>(count);
        for (int i = 0; i < list.Count && result.Count < count; i++)
            result.Add(list[i].enemy);
        return result;
    }
}
