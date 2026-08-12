using UnityEngine;

/// <summary>
/// Aimant — au contact du joueur, attire vers lui <b>toutes</b> les orbes d'XP et tous les Noyaux
/// posés dans l'arène (port de <c>MagnetPickup</c>).
///
/// <para><b>Ce système n'avait jamais été porté.</b> Ni l'objet, ni son spawner, ni son effet —
/// alors que <c>bonus_magnet</c> (« +1 apparition de l'Aimant par run et par niveau », 770 Échos
/// cumulés) est resté achetable au Hub depuis la 2.0.0. Le joueur pouvait donc payer pour étendre un
/// objet qui n'apparaissait <b>jamais</b>. Signalé en jouant le 2026-08-12, quatrième occurrence du
/// défaut favori de ce projet : <i>déclaré n'est pas consommé</i>.</para>
///
/// <para><b>Lui ne s'aimante pas</b>, et c'est ce qui le distingue de tout le reste : il faut marcher
/// dessus. Un objet qui vient au joueur ne peut pas récompenser le fait d'aller le chercher, et
/// l'Aimant est précisément la récompense d'un détour — il paie en une fois toutes les orbes que la
/// run a semées ailleurs. C'est le rôle que le Noyau d'Aether tenait avant de devenir magnétique le
/// même jour.</para>
///
/// <para>Détection par distance, sans <c>Collider2D</c> : cohérent avec le reste du portage.</para>
/// </summary>
public sealed class MagnetPickup : MonoBehaviour
{
    /// <summary>
    /// Distance de ramassage, en pixels. Plus large que celle d'une orbe (20 px) : l'objet est rare,
    /// le manquer d'un pixel après avoir traversé l'arène pour lui serait une punition absurde.
    /// </summary>
    public const float PickupRadius = 28f;

    /// <summary>Période de la pulsation lumineuse, en secondes — le tween du jeu d'origine.</summary>
    private const float PulsePeriod = 0.8f;

    /// <summary>Aimants ramassés depuis le début de la run — observable pour les vérifications.</summary>
    public static int CollectedCount { get; private set; }

    /// <summary>Remise à zéro entre deux runs du même processus.</summary>
    public static void ResetCounters() => CollectedCount = 0;

    private SpriteRenderer? _renderer;
    private SpriteRenderer? _halo;
    private bool _collected;

    private void Awake() => Dress();

    /// <summary>
    /// Pose la silhouette et le halo. Fait <b>par code</b> : le sprite est dessiné à l'exécution,
    /// donc aucun gabarit ne peut le référencer (même chaîne que <see cref="GlaiveProjectile"/>).
    /// </summary>
    private void Dress()
    {
        _renderer = GetComponent<SpriteRenderer>();
        if (_renderer == null) return;

        _renderer.sprite = MagnetSprite.Get();
        _renderer.color = Color.white;      // la teinte vit dans le sprite
        _renderer.sortingOrder = 7;         // au-dessus des Noyaux (6) et des orbes (5)

        // Halo cyan magnétique : le portage du PointLight2D du jeu publié. Sans lui, l'objet le plus
        // rare de la run est un petit gris de 28 px posé dans une arène de 1920 — on passe à côté
        // sans le voir, ce qui revient exactement à ne pas l'avoir fait apparaître.
        var halo = new GameObject("Halo", typeof(SpriteRenderer));
        halo.transform.SetParent(transform, false);

        _halo = halo.GetComponent<SpriteRenderer>();
        _halo.sprite = VfxPrimitives.Glow;
        _halo.sharedMaterial = VfxPrimitives.Additive;
        _halo.color = new Color(0.267f, 0.667f, 1f, 0.55f);
        _halo.sortingOrder = _renderer.sortingOrder - 1;

        // Le sprite de lueur mesure 64 unités : l'échelle est le rapport du diamètre voulu à 64.
        halo.transform.localScale = Vector3.one * (PickupRadius * 3.2f / 64f);
    }

    private void Update()
    {
        if (_collected) return;

        // La pulsation, comme celle du Noyau : c'est elle qui fait repérer l'objet de loin. Sous
        // Godot c'était l'énergie d'une PointLight2D qui montait et descendait en boucle.
        if (_halo != null)
        {
            float k = 0.45f + 0.55f * (0.5f + 0.5f * Mathf.Sin(Time.time / PulsePeriod * Mathf.PI * 2f));
            var c = _halo.color;
            _halo.color = new Color(c.r, c.g, c.b, k * 0.7f);
        }

        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        if (Vector2.Distance(transform.position, player.transform.position) <= PickupRadius) Collect();
    }

    private void Collect()
    {
        if (_collected) return;
        _collected = true;

        CollectedCount++;

        // Son d'événement de run, comme le Noyau — pas le grain d'XP.
        AudioSystem.PlaySfx("sfx_core_collect");

        int orbs = AttractEverything();

        // Tracé : l'effet est instantané et se lit comme une convergence à l'écran, pas comme un
        // compteur. Sans cette ligne, « l'Aimant a-t-il vraiment TOUT pris ? » ne se vérifie qu'en
        // arpentant une arène plus large que l'écran.
        Debug.Log($"[Aimant] ramasse a t = {GameManager.Instance?.RunTime ?? 0f:F0} s — " +
                  $"{orbs} ramassages rappeles (total {CollectedCount}).");

        Destroy(gameObject);
    }

    /// <summary>
    /// Force l'attraction de tout ce qui traîne au sol, et rend le nombre d'objets rappelés.
    /// </summary>
    /// <remarks>
    /// <para>Les Noyaux d'Aether sont inclus depuis le 2026-08-12, jour où ils sont devenus
    /// magnétiques : les exclure ferait de l'Aimant un objet qui aspire « presque tout », et le seul
    /// ramassage qu'il laisserait au sol serait justement le plus précieux.</para>
    ///
    /// <para>⚠ Le drapeau est posé une fois pour toutes sur les objets <b>présents</b> — ce qui tombe
    /// après reste ordinaire. L'Aimant vide l'arène à l'instant où on le prend ; il ne rend pas le
    /// reste de la run automatique.</para>
    /// </remarks>
    private static int AttractEverything()
    {
        int count = 0;

        foreach (var orb in XpOrb.Active)
        {
            if (orb == null) continue;
            orb.ForceMagnet = true;
            count++;
        }

        foreach (var core in AetherCore.Active)
        {
            if (core == null) continue;
            core.ForceMagnet = true;
            count++;
        }

        return count;
    }
}
