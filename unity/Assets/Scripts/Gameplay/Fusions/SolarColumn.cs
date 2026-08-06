using UnityEngine;

/// <summary>
/// Colonne Solaire — évolution du <see cref="PyreStream"/> + Noyau Thermique.
///
/// <para><b>Ce n'est pas un souffle plus fort, c'est une éruption.</b> Le jet dirigé devient radial :
/// à chaque pulsation, tout ennemi dans le rayon subit les dégâts <b>et</b> une brûlure massive, et
/// une couronne de flammes jaillit dans toutes les directions.</para>
///
/// <para><b>Pourquoi cette classe ne dérive plus de son arme d'origine.</b> Le portage l'écrivait
/// <c>: PyreStream</c> et se contentait d'en relever quatre champs. La fusion héritait donc du cône
/// dirigé, et rien — ni à l'écran, ni dans les dégâts — ne la distinguait de l'arme qu'elle
/// remplace : le joueur payait la carte la plus rare du jeu pour un souffle un peu plus long. C'est
/// le signalement « la colonne solaire n'est pas visible », et il portait sur les deux à la fois.</para>
///
/// <para>⚠ <b>Ses données le disaient déjà.</b> <c>weapons.json</c> déclare
/// <c>"type": "radial_burn"</c> et <c>"radius": 155</c> pour cette fusion — deux clés qu'aucune ligne
/// de code ne lisait. Cinquième occurrence de « une donnée déclarée n'est pas une donnée
/// consommée », et la plus muette de toutes : l'arme tirait, blessait, brûlait, montait de niveau.
/// Rien n'était en panne ; c'était simplement une autre arme.</para>
/// </summary>
public sealed class SolarColumn : WeaponBase
{
    /// <summary>Rayon de l'éruption, en pixels — la valeur de <c>weapons.json</c>.</summary>
    public float Radius = 155f;

    [Header("Brûlure")]
    public float BurnDps = 18f;
    public float BurnDuration = 3.0f;

    /// <summary>Ouverture d'une langue de la couronne, en degrés.</summary>
    private const float ConeAngle = 70f;

    /// <summary>Ennemis touchés par la dernière pulsation — observable pour les tests et le HUD.</summary>
    public int LastFlareHits { get; private set; }

    private float _spin;

    private AuraCloud? _corona;

    protected override void Awake()
    {
        BaseDamage = 10f;
        BaseCooldown = 0.7f;

        // La portée sert de <b>garde</b> : sans ennemi dedans, l'arme ne consomme pas sa recharge.
        // Elle doit donc valoir le rayon qui blesse, et pas un chiffre plus large — sinon l'éruption
        // part sur des cibles qu'elle n'atteindra pas.
        Range = Radius;

        base.Awake();

        BuildCorona();
    }

    /// <summary>
    /// Couronne permanente autour du porteur — le portage de la lumière solaire pulsée du jeu publié
    /// (<c>PointLight2D</c> orange, énergie 0,35 ↔ 0,60 en boucle).
    /// </summary>
    /// <remarks>
    /// <para>Elle ne décore pas : c'est le <b>seul</b> signe permanent qu'on porte cette arme. Une
    /// fusion dont la trace n'existe qu'au moment du tir est indiscernable, entre deux pulsations, de
    /// l'arme dont elle est l'évolution.</para>
    ///
    /// <para>⚠ Un nuage de bouffées (<see cref="AuraCloud"/>) et non un anneau : une couronne solaire
    /// n'a pas de bord, et un cercle lui donnerait exactement la forme d'une portée d'arme. Réglé bas
    /// (0,15 par bouffée) — la Fournaise est un biome clair, et le jeu publié notait déjà qu'une aura
    /// additive y sature vite au point de masquer le joueur.</para>
    /// </remarks>
    private void BuildCorona()
    {
        var go = new GameObject("CouronneSolaire");
        go.transform.SetParent(transform, false);

        // ⚠ Rayon et opacité relevés sur capture. À 0,42 × le rayon et 0,15 par bouffée, la couronne
        // était un léger reflet sur le corps du joueur : elle ne disait ni qu'il portait une arme
        // solaire, ni jusqu'où celle-ci brûle. Elle couvre désormais les trois quarts du rayon qui
        // blesse — sans le dessiner, un nuage n'ayant pas de bord.
        _corona = go.AddComponent<AuraCloud>();
        _corona.Configure(Radius * 0.72f, new Color(1f, 0.62f, 0.24f), 0.20f, 12, seed: 20260806);
    }

    protected override bool TryFire()
    {
        if (FindNearestEnemy() == null) return false;

        Vector2 center = transform.position;
        float damage = EffectiveDamage;
        float sqr = Radius * Radius;

        // ⚠ La brûlure suit la mise à l'échelle de l'arme, comme dans le jeu publié. Laissée à sa
        // valeur de fiche, elle devient négligeable en fin de run alors qu'elle représente la moitié
        // de l'identité de la Colonne Solaire — c'est même l'essentiel de ses dégâts.
        float burn = BurnDps * (Player.Instance?.Stats.DamageMultiplier ?? 1f);

        LastFlareHits = 0;

        // La couronne tourne d'un tir à l'autre : six bras qui retombent toujours dans les six mêmes
        // directions se lisent comme un motif imprimé, pas comme une éruption.
        _spin += 0.41f;
        Vfx.SolarFlare(center, Radius, ConeAngle, _spin);

        AudioSystem.PlaySfx("sfx_weapon_plasma_swing");
        ScreenShake.Shake(3f, 0.1f);

        // Copie de sécurité : blesser peut tuer, et une mort retire de la liste vivante.
        var snapshot = EnemyBase.Active.ToArray();

        foreach (var e in snapshot)
        {
            if (e == null || e.IsDead) continue;
            if (((Vector2)e.transform.position - center).sqrMagnitude > sqr) continue;

            e.TakeDamage(damage);
            e.ApplyBurn(burn, BurnDuration);
            LastFlareHits++;
        }

        return true;
    }

    /// <summary>
    /// La couronne est un <b>enfant du porteur</b>, pas de l'arme : rien ne la détruirait quand le
    /// composant disparaît, et le joueur garderait un halo solaire sans arme solaire.
    /// </summary>
    /// <remarks>
    /// ⚠ On détruit l'objet de la couronne, jamais <c>gameObject</c> — celui de l'arme <i>est</i>
    /// celui du joueur.
    /// </remarks>
    private void OnDestroy()
    {
        if (_corona != null) Destroy(_corona.gameObject);
    }
}
