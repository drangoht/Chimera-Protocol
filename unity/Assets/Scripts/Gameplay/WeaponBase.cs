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

    /// <summary>
    /// Dégâts par seconde <b>théoriques</b> de cette arme — sa contribution à l'indice de puissance.
    /// </summary>
    /// <remarks>
    /// <para>Théoriques, et c'est voulu : l'indice mesure le <b>build</b>, pas la réussite au tir.
    /// Une arme qui ne trouve pas de cible n'en est pas moins puissante, et confondre les deux ferait
    /// baisser l'indice dans une arène vide — on ne saurait plus si la courbe décrit la progression du
    /// joueur ou la densité du moment.</para>
    /// <para>Les dégâts réellement infligés sont mesurés à part, colonne <c>dps</c>.</para>
    /// </remarks>
    public float PowerContribution => EffectiveCooldown > 0.001f ? EffectiveDamage / EffectiveCooldown : 0f;

    /// <summary>Règle l'arme à un niveau donné.</summary>
    public virtual void Configure(int level)
    {
        CaptureSheetDamage();   // avant toute modification, sinon la fiche est déjà perdue
        Level = Mathf.Max(1, level);
    }

    /// <summary>
    /// Applique les <b>mécaniques</b> du palier de niveau : nombre de projectiles, vitesse,
    /// perforation, éventail. Sans effet par défaut — chaque famille d'arme en lit ce qui la
    /// concerne.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Ce point d'entrée n'existait pas</b>, et le manque était entièrement silencieux :
    /// <c>WeaponTable</c> lisait bien <c>projectileCount</c>, <c>projectileSpeed</c> et
    /// <c>piercing</c> depuis <c>weapons.json</c>, mais seuls les dégâts et la recharge étaient
    /// reportés sur l'arme. La Salve Éclatée restait donc à deux projectiles au niveau 20, l'Essaim
    /// Traqueur à deux missiles, et la Lance Vectorielle ne gagnait jamais son éventail — la moitié
    /// de la progression de ces armes n'existait pas, alors que leurs dégâts montaient bien, ce qui
    /// rendait le défaut invisible aux relevés comme aux tests.
    /// </remarks>
    public virtual void ApplyLevelStats(WeaponTable.WeaponLevelStats stats) { }

    /// <summary>
    /// Identifiant de cette arme, déduit de son type. Résolu une seule fois : la table est un
    /// dictionnaire, mais ce chemin est parcouru à chaque tir de chaque arme équipée.
    /// </summary>
    public string WeaponId => _weaponId ??= WeaponRegistry.IdOf(GetType()) ?? "";

    private string? _weaponId;

    /// <summary>
    /// Son joué à chaque tir réussi, ou <c>null</c> pour les armes muettes à dessein
    /// (<see cref="WeaponSfx.Silent"/>).
    ///
    /// <para><b>Il se joue ici et nulle part ailleurs.</b> Sous Godot, chaque arme appelait
    /// <c>PlaySfx</c> depuis sa propre méthode de tir ; le portage n'en a repris que deux sur seize,
    /// et les quatorze autres — dont la Bobine Tesla — sont restées muettes pendant toute la
    /// migration sans qu'aucun test ni aucune capture ne puisse le voir. Un point unique, alimenté
    /// par une table exhaustive, rend l'oubli impossible : une arme nouvelle est bruyante ou
    /// explicitement muette, jamais muette par distraction.</para>
    /// </summary>
    protected virtual string? FireSfx => WeaponSfx.For(WeaponId);

    protected virtual void Awake() => CaptureSheetDamage();

    protected virtual void Update()
    {
        TicksRun++;

        if (Player.Instance == null || Player.Instance.IsDead) return;

        _cooldownLeft -= Time.deltaTime;
        if (_cooldownLeft > 0f) return;

        if (!TryFire()) return;

        // APRÈS le tir, et seulement s'il a eu lieu : une arme qui ne trouve pas de cible renvoie
        // faux et ne consomme pas sa recharge — lui faire produire un son la ferait « tirer » à vide
        // à l'oreille, en boucle, tout le temps où l'arène est vide.
        var sfx = FireSfx;
        if (sfx != null) AudioSystem.PlaySfx(sfx);

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
