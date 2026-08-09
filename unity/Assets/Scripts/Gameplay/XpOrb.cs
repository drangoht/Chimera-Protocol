using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Orbe d'expérience — port de <c>XpOrb</c> (Lot 2).
///
/// <para><b>Pourquoi l'orbe existe, plutôt que de créditer l'XP à la mort.</b> C'est une boucle de
/// gameplay à part entière : l'orbe oblige le joueur à <b>aller chercher</b> sa récompense, donc à
/// entrer dans la zone qu'il vient de nettoyer. Sauter cette étape rendrait le jeu plus sûr et
/// changerait son rythme — exactement le genre de dérive silencieuse que le périmètre « parité
/// stricte » interdit.</para>
///
/// <para>Détection par distance, sans <c>Collider2D</c> : cohérent avec le reste du portage, et
/// nécessaire quand des centaines d'orbes coexistent en fin de partie.</para>
/// </summary>
public sealed class XpOrb : MonoBehaviour
{
    /// <summary>Distance à laquelle l'orbe se met à suivre le joueur.</summary>
    private const float MagnetRadius = 100f;

    /// <summary>Vitesse d'attraction, en unités par seconde.</summary>
    private const float MagnetSpeed = 300f;

    /// <summary>Distance de ramassage effectif.</summary>
    private const float PickupRadius = 16f;

    /// <summary>Multiplicateur de vitesse quand un aimant force l'attraction de toute l'arène.</summary>
    private const float ForcedMagnetBoost = 2.5f;

    /// <summary>Échelles visuelles par palier (T1 → T4).</summary>
    private static readonly float[] TierScales = { 1.0f, 1.25f, 1.5f, 1.75f };

    /// <summary>Couleurs par palier : vert, cyan, violet, or.</summary>
    private static readonly Color[] TierColors =
    {
        new(0.35f, 0.95f, 0.45f),
        new(0.27f, 1.00f, 0.93f),
        new(0.67f, 0.27f, 1.00f),
        new(1.00f, 0.80f, 0.27f),
    };

    /// <summary>Valeur en XP.</summary>
    public int Value = 2;

    /// <summary>Palier visuel, de 0 (T1) à 3 (T4).</summary>
    public int OrbTier;

    /// <summary>Attraction forcée depuis toute l'arène (ramassage d'un aimant).</summary>
    public bool ForceMagnet { get; set; }

    private bool _collected;

    /// <summary>Applique valeur et palier, et met le visuel en accord.</summary>
    public void Configure(int value, int tier)
    {
        Value = value;
        OrbTier = Mathf.Clamp(tier, 0, TierScales.Length - 1);
        ApplyTierVisuals();
    }

    /// <summary>
    /// Orbes présents dans l'arène. Tenue comme celle des ennemis, et pour la même raison : le
    /// pilote de banc les relit périodiquement, et un balayage de la scène entière à chaque
    /// réévaluation coûterait plus cher que tout le reste du banc réuni.
    /// </summary>
    public static readonly List<XpOrb> Active = new();

    private void OnEnable() => Active.Add(this);

    private void OnDisable() => Active.Remove(this);

    private void Start() => ApplyTierVisuals();

    private void ApplyTierVisuals()
    {
        int t = Mathf.Clamp(OrbTier, 0, TierScales.Length - 1);
        transform.localScale = Vector3.one * TierScales[t];

        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = TierColors[t];
    }

    private void Update()
    {
        if (_collected) return;

        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        Vector2 me = transform.position;
        Vector2 target = player.transform.position;
        float dist = Vector2.Distance(me, target);

        if (dist < PickupRadius) { Collect(); return; }

        if (!ForceMagnet && dist > MagnetRadius) return;

        float speed = ForceMagnet ? MagnetSpeed * ForcedMagnetBoost : MagnetSpeed;
        Vector2 dir = (target - me) / Mathf.Max(dist, 0.001f);
        transform.position = me + dir * speed * Time.deltaTime;
    }

    private void Collect()
    {
        // Garde-fou : deux ramassages dans la même frame doubleraient l'XP. Le cas se produit
        // réellement quand plusieurs orbes convergent au même endroit en fin de partie.
        if (_collected) return;
        _collected = true;

        AudioSystem.PlaySfx("sfx_xp_collect", pitchVariation: 0.12f);
        XpSystem.Instance?.AddXp(Value);
        Destroy(gameObject);
    }
}
