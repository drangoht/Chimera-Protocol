using UnityEngine;

/// <summary>
/// Socle des armes — port de <c>WeaponBase</c> (Lot 2).
///
/// <para><b>Un piège de Godot disparaît ici, un autre le remplace.</b> Sous Godot,
/// <c>base._Ready()</c> devait impérativement être appelé <b>en dernier</b> dans les 19 armes, sans
/// quoi l'initialisation écrasait les réglages de la sous-classe. Unity n'a pas de chaîne d'appels
/// à la base : c'est l'ordre <c>Awake</c>/<c>OnEnable</c>/<c>Start</c> qui décide, et il n'est pas
/// garanti entre objets. La parade retenue est de n'avoir <b>aucune</b> initialisation implicite —
/// les sous-classes règlent leurs champs dans l'inspecteur ou dans <see cref="Configure"/>.</para>
/// </summary>
public abstract class WeaponBase : MonoBehaviour
{
    [Header("Réglages de base")]
    public float BaseDamage = 10f;

    [Tooltip("Secondes entre deux tirs, avant réduction de recharge.")]
    public float BaseCooldown = 1f;

    public float Range = 400f;

    /// <summary>Niveau courant (1 à 5 dans les données, extrapolé au-delà).</summary>
    public int Level { get; private set; } = 1;

    /// <summary>
    /// Dégâts de <b>fiche</b>, capturés une seule fois avant toute modification.
    /// </summary>
    /// <remarks>
    /// Indispensable pour les fusions : leur valeur d'origine est posée par leur propre classe, et
    /// le recalcul (niveau × multiplicateur) doit toujours repartir d'elle. Repartir de la valeur
    /// <i>courante</i> cumulerait les multiplicateurs à chaque achat de passif, jusqu'à des dégâts
    /// absurdes — le miroir exact du bug de 1.21.0, dans l'autre sens.
    /// </remarks>
    public float SheetDamage { get; private set; }

    private bool _sheetCaptured;

    /// <summary>Fige les dégâts de fiche. Idempotent : les appels suivants sont sans effet.</summary>
    public void CaptureSheetDamage()
    {
        if (_sheetCaptured) return;
        SheetDamage = BaseDamage;
        _sheetCaptured = true;
    }

    private float _cooldownLeft;

    /// <summary>Tirs effectués — statistique de run, et point d'observation pour les bancs.</summary>
    public int ShotsFired { get; private set; }

    /// <summary>Appels à <c>Update</c> — sert à distinguer « n'a pas tiré » de « ne tourne pas ».</summary>
    public int TicksRun { get; private set; }

    /// <summary>
    /// Recharge effective, bornée par <see cref="StatCaps"/>. Le plancher est ce qui a empêché,
    /// côté Godot, qu'un passif porte toutes les armes à la cadence maximale.
    /// </summary>
    protected float EffectiveCooldown
    {
        get
        {
            var stats = Player.Instance?.Stats;
            float reduction = stats != null
                ? Mathf.Min(stats.CooldownReduction, StatCaps.MaxCooldownReduction)
                : 0f;
            return Mathf.Max(StatCaps.MinCooldown, BaseCooldown * (1f - reduction));
        }
    }

    /// <summary>Dégâts effectifs, multiplicateur global du joueur appliqué.</summary>
    protected float EffectiveDamage
    {
        get
        {
            float mult = Player.Instance?.Stats.DamageMultiplier ?? 1f;
            return BaseDamage * mult;
        }
    }

    /// <summary>Règle l'arme à un niveau donné.</summary>
    public virtual void Configure(int level)
    {
        CaptureSheetDamage();   // avant toute modification, sinon la fiche est déjà perdue
        Level = Mathf.Max(1, level);
    }

    protected virtual void Awake() => CaptureSheetDamage();

    protected virtual void Update()
    {
        TicksRun++;

        if (Player.Instance == null || Player.Instance.IsDead) return;

        _cooldownLeft -= Time.deltaTime;
        if (_cooldownLeft > 0f) return;

        if (!TryFire()) return;

        ShotsFired++;
        _cooldownLeft = EffectiveCooldown;
    }

    /// <summary>Tente de tirer. Renvoie faux si rien n'était à portée (la recharge ne repart pas).</summary>
    protected abstract bool TryFire();

    /// <summary>
    /// Ennemi vivant le plus proche, dans la portée. Renvoie <c>null</c> s'il n'y en a aucun —
    /// c'est ce qui empêche l'arme de consommer sa recharge dans le vide.
    /// </summary>
    protected EnemyBase? FindNearestEnemy()
    {
        EnemyBase? best = null;
        float bestSqr = Range * Range;
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
