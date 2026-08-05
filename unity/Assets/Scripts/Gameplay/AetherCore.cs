using UnityEngine;

/// <summary>
/// Noyau d'Aether — la monnaie de méta-progression, ramassée <b>à la main</b> (port d'<c>AetherCore</c>).
///
/// <para><b>Il ne s'aspire pas, et c'est tout son intérêt.</b> Les orbes d'XP viennent au joueur dès
/// qu'il approche ; un Noyau exige d'aller le <i>chercher</i>, et il apparaît là où la nuée se
/// trouve. C'est le seul objet du jeu qui demande de traverser volontairement le danger, et cette
/// décision — y aller ou renoncer — est ce qu'il apporte à la boucle. Le rendre magnétique le
/// transformerait en orbe d'XP violette.</para>
///
/// <para>Le rayon de ramassage est la <b>seule</b> chose que la méta-progression élargit
/// (<c>core_magnetism</c> : 20 px de base, jusqu'à 70). L'amélioration rend la traversée moins
/// précise, jamais inutile.</para>
/// </summary>
public sealed class AetherCore : MonoBehaviour
{
    /// <summary>Rayon de ramassage de base, en pixels — <c>meta_upgrades.json</c>, <c>collectionRadiusPx</c>.</summary>
    public const float BaseRadius = 20f;

    /// <summary>Bonus de rayon accordé par chaque niveau de <c>core_magnetism</c>.</summary>
    private static readonly float[] RadiusPerLevel = { 15f, 15f, 20f };

    /// <summary>Période de la pulsation lumineuse, en secondes.</summary>
    private const float PulsePeriod = 1f;

    private float _radius = BaseRadius;
    private SpriteRenderer? _renderer;
    private bool _collected;

    /// <summary>Rayon effectif après méta-progression — observable pour les vérifications.</summary>
    public float Radius => _radius;

    /// <summary>Rayon de ramassage pour un niveau d'amélioration donné. Logique pure et testable.</summary>
    public static float RadiusForLevel(int level)
    {
        float radius = BaseRadius;
        for (int i = 0; i < level && i < RadiusPerLevel.Length; i++) radius += RadiusPerLevel[i];
        return radius;
    }

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

        if (Vector2.Distance(transform.position, player.transform.position) <= _radius) Collect();
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
