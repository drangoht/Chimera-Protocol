using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Noyau d'Aether — la monnaie de méta-progression (port d'<c>AetherCore</c>).
///
/// <para><b>Il s'aimante depuis le 2026-08-12, et c'est un renversement assumé.</b> Le Noyau était
/// jusque-là le seul objet du jeu à se ramasser <i>à la main</i> : il apparaît là où la nuée se
/// trouve, et aller le chercher ou y renoncer était la décision qu'il apportait à la boucle. Le
/// joueur a tranché autrement après l'avoir joué (« les orbes d'Aether devraient également être
/// attirées par le joueur comme les orbes d'XP »), et la mesure lui donne raison : en fin de partie,
/// un Noyau posé sous trois cents ennemis n'est pas un arbitrage, c'est une monnaie perdue.</para>
///
/// <para><b>Ce qui reste du parti pris d'origine.</b> Le Noyau garde son rayon d'aimantation
/// <i>propre</i>, distinct de celui des orbes uniquement par ce que la méta-progression y ajoute
/// (<c>core_magnetism</c> : 100 px de base, jusqu'à 150 — cf. <see cref="PickupMagnet.AttractRadius"/>).
/// Il faut donc toujours <b>s'en approcher</b> ; ce n'est plus le dernier pixel qui se paie, c'est
/// l'aller-retour. Il conserve aussi sa pulsation et son propre son : ramasser un Noyau reste un
/// événement de run, pas un grain d'XP parmi des centaines.</para>
/// </summary>
public sealed class AetherCore : MonoBehaviour
{
    /// <summary>
    /// Noyaux posés dans l'arène. Tenue comme celle des orbes d'XP, et pour la même raison :
    /// l'Aimant doit pouvoir les rappeler tous d'un coup sans balayer la scène entière.
    /// </summary>
    public static readonly List<AetherCore> Active = new();

    /// <summary>Attraction forcée depuis toute l'arène (ramassage d'un Aimant).</summary>
    public bool ForceMagnet { get; set; }

    /// <summary>
    /// Rayon de <b>ramassage effectif</b>, en pixels — <c>meta_upgrades.json</c>, <c>collectionRadiusPx</c>.
    /// C'est le contact des deux corps, la même valeur que <see cref="PickupMagnet.PickupRadius"/>.
    /// </summary>
    public const float BaseRadius = 20f;

    /// <summary>Bonus de rayon accordé par chaque niveau de <c>core_magnetism</c>.</summary>
    private static readonly float[] RadiusPerLevel = { 15f, 15f, 20f };

    /// <summary>Période de la pulsation lumineuse, en secondes.</summary>
    private const float PulsePeriod = 1f;

    private float _radius = BaseRadius;
    private SpriteRenderer? _renderer;
    private bool _collected;

    /// <summary>Rayon de ramassage effectif après méta-progression — observable pour les vérifications.</summary>
    public float Radius => _radius;

    /// <summary>
    /// Rayon à partir duquel le Noyau se met à suivre le joueur. Le bonus de <c>core_magnetism</c>
    /// s'y reporte tel quel : c'est là qu'il se ressent désormais.
    /// </summary>
    public float AttractRadius => PickupMagnet.AttractRadius(_radius - BaseRadius);

    /// <summary>Rayon de ramassage pour un niveau d'amélioration donné. Logique pure et testable.</summary>
    public static float RadiusForLevel(int level)
    {
        float radius = BaseRadius;
        for (int i = 0; i < level && i < RadiusPerLevel.Length; i++) radius += RadiusPerLevel[i];
        return radius;
    }

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    private void Start()
    {
        _radius = RadiusForLevel(MetaProgression.LevelOf("core_magnetism"));
        _renderer = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_collected) return;

        // Pulsation : sous Godot c'est une PointLight2D dont l'énergie oscille. Le portage n'a pas
        // d'éclairage 2D, mais le Noyau DOIT battre — c'est ce qui le distingue d'un décor au sol,
        // dans une arène où passent des centaines d'orbes. La luminosité du sprite tient ce rôle.
        if (_renderer != null)
        {
            float k = 0.8f + 0.6f * (0.5f + 0.5f * Mathf.Sin(Time.time / PulsePeriod * Mathf.PI * 2f));
            _renderer.color = new Color(k, k, k, 1f);
        }

        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        Vector2 me = transform.position;
        Vector2 target = player.transform.position;
        float dist = Vector2.Distance(me, target);

        if (dist <= _radius) { Collect(); return; }

        if (!ForceMagnet && dist > AttractRadius) return;

        // Même règle que les orbes d'XP : la vitesse se mesure contre celle du PORTEUR, jamais dans
        // l'absolu. Un Noyau qu'un joueur rapide sème derrière lui serait pire que pas de Noyau du
        // tout — il aurait fait l'aller-retour pour rien.
        float speed = PickupMagnet.SpeedAgainst(player.CurrentSpeed, ForceMagnet);
        transform.position = me + (target - me) / Mathf.Max(dist, 0.001f) * speed * Time.deltaTime;
    }

    private void Collect()
    {
        // Garde-fou : deux ramassages dans la même frame compteraient deux fois. Le cas se produit
        // quand un Colosse meurt sur un Noyau déjà posé.
        if (_collected) return;
        _collected = true;

        // Son plus marqué que celui des orbes : ramasser un Noyau est un événement de run, pas un
        // grain d'XP parmi des centaines.
        AudioSystem.PlaySfx("sfx_core_collect");

        GameManager.Instance?.RegisterCoreCollected();
        Destroy(gameObject);
    }
}
