using System;
using System.Collections.Generic;

/// <summary>
/// Son de tir de chaque arme — logique pure, donc testable et partagée par les deux moteurs.
///
/// <para><b>Pourquoi une table plutôt qu'un appel dans chaque arme.</b> Sous Godot, les seize
/// <c>PlaySfx</c> d'armes étaient écrits un par un, à l'intérieur de la méthode de tir de chaque
/// classe. Le portage en a repris <b>deux</b> — l'Impulsion et la Lance — et les quatorze autres sont
/// restées muettes, la Bobine Tesla comprise, sans que rien ne le signale : une arme sans son
/// fonctionne parfaitement, elle blesse, monte de niveau et s'affiche. C'est la même famille de
/// défaut que les données déclarées et jamais consommées, appliquée à l'audio.</para>
///
/// <para>Un appel dispersé dans vingt-et-une classes ne peut être ni vérifié ni comparé ; une table
/// se lit d'un coup d'œil, et un banc peut exiger qu'elle soit <b>exhaustive</b> — c'est-à-dire que
/// toute arme y figure, fût-ce pour déclarer qu'elle est muette <i>à dessein</i>.</para>
/// </summary>
public static class WeaponSfx
{
    /// <summary>
    /// Armes dont le tir ne produit aucun son, et pourquoi.
    ///
    /// <para>Ce ne sont pas des oublis : ces trois-là n'ont pas d'<b>événement</b> de tir. Un essaim
    /// de drones en orbite et un voile de givre frappent en continu, par ticks rapprochés (0,35 s
    /// pour le Voile) — un son par tick ne s'entendrait pas comme un tir mais comme un bourdonnement
    /// permanent, qui masquerait de surcroît les sons qui portent une information (dégât subi,
    /// mort d'un champion). Le jeu publié fait le même choix.</para>
    /// </summary>
    public static readonly IReadOnlyList<string> Silent = new[]
    {
        "drone_swarm",
        "orbital_swarm",
        "frost_veil",
    };

    /// <summary>
    /// Son joué à chaque tir, par identifiant d'arme. Valeurs reprises une à une du jeu publié.
    ///
    /// <para>Cinq effets seulement pour dix-huit armes, et c'est voulu : ils disent la
    /// <b>famille</b> du tir (impulsion, salve, lame, décharge, rail), pas l'arme. Une banque d'un son
    /// par arme se transformerait en bouillie dès trois armes équipées.</para>
    /// </summary>
    private static readonly Dictionary<string, string> _byWeapon = new(StringComparer.Ordinal)
    {
        // ── Armes de base ────────────────────────────────────────────────────
        ["impulse_cannon"] = "sfx_weapon_impulse_shoot",
        ["plasma_blade"]   = "sfx_weapon_plasma_swing",
        ["overload_field"] = "sfx_weapon_overload_pulse",
        ["tesla_coil"]     = "sfx_weapon_overload_pulse",
        // La salve a son propre son, plus sourd et plus court que l'impulsion, et atténué au mixage
        // (voir la table de mixage d'AudioSystem). Ce n'est pas une exception à la règle des familles
        // ci-dessus mais son application : la Volée Multiple tire en salve, et à recharge réduite elle
        // enchaîne plusieurs salves par seconde EN MÊME TEMPS que le Canon à Impulsions, avec lequel
        // elle partageait exactement le même claquement — deux armes sur le même transitoire, ça ne
        // s'entend plus comme deux tirs mais comme une saturation. Signalé en jouant : « trop présent,
        // limite saturé ».
        ["scatter_volley"] = "sfx_weapon_scatter_shoot",
        ["glaive"]         = "sfx_weapon_plasma_swing",
        ["seeker_swarm"]   = "sfx_weapon_impulse_shoot",
        ["cryo_lance"]     = "sfx_weapon_rail_shoot",
        ["pyre_stream"]    = "sfx_weapon_plasma_swing",
        ["vector_lance"]   = "sfx_weapon_impulse_shoot",
        ["singularity"]    = "sfx_weapon_overload_pulse",

        // ── Fusions ──────────────────────────────────────────────────────────
        ["rail_overcharged"] = "sfx_weapon_rail_shoot",
        ["ionic_storm"]      = "sfx_weapon_overload_pulse",
        ["solar_column"]     = "sfx_weapon_plasma_swing",
        ["hornet_swarm"]     = "sfx_weapon_impulse_shoot",
        ["vector_beam"]      = "sfx_weapon_impulse_shoot",

        // L'Égide hérite du Champ de Surcharge sous Godot (sa méthode d'attaque appelle celle de sa
        // base, son comprise) : l'héritage est ici rendu EXPLICITE, car le portage résout le son par
        // l'identifiant de l'arme et non par sa classe — une chaîne d'héritage ne s'y voit pas.
        ["overload_aegis"] = "sfx_weapon_overload_pulse",

        // La Lame à Fusion était muette sous Godot alors que la Lame de Plasma dont elle est
        // l'évolution claque à chaque coup : une arme qui devient plus forte et perd son bruit se lit
        // comme une arme cassée. Corrigé ici, la cadence d'une lame ne posant aucun risque de
        // saturation.
        ["fusion_blade"] = "sfx_weapon_plasma_swing",
    };

    /// <summary>
    /// Son de tir d'une arme, ou <c>null</c> si elle est muette à dessein.
    /// </summary>
    /// <remarks>
    /// ⚠ Un identifiant <b>inconnu</b> renvoie <c>null</c> lui aussi : une arme ajoutée sans entrée
    /// dans cette table serait donc silencieuse. C'est exactement le défaut que la table existe pour
    /// empêcher — d'où <see cref="Covers"/>, que le banc et les tests appliquent à l'arsenal complet.
    /// </remarks>
    public static string? For(string weaponId)
        => _byWeapon.TryGetValue(weaponId, out var sfx) ? sfx : null;

    /// <summary>
    /// L'arme est-elle <b>traitée</b> — c'est-à-dire dotée d'un son ou déclarée muette explicitement ?
    /// C'est le critère de la vérification d'exhaustivité, et il ne se confond pas avec « a un son ».
    /// </summary>
    public static bool Covers(string weaponId)
        => _byWeapon.ContainsKey(weaponId) || IsSilent(weaponId);

    /// <summary>L'arme est-elle muette à dessein ?</summary>
    public static bool IsSilent(string weaponId)
    {
        foreach (var id in Silent)
            if (string.Equals(id, weaponId, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>Tous les sons employés — sert à vérifier que chacun existe bien en asset.</summary>
    public static IEnumerable<string> AllSfxIds => _byWeapon.Values;
}
