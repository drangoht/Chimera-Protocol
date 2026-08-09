using System.IO;
using System.Linq;
using Xunit;

namespace ChimeraProtocol.Tests;

/// <summary>
/// Verrous du son de tir.
///
/// <para><b>Ce que ces tests auraient trouvé.</b> Le portage Unity n'avait repris que deux des seize
/// <c>PlaySfx</c> d'armes du jeu publié : quatorze armes, dont la Bobine Tesla, tiraient sans le
/// moindre bruit. Aucun test ne pouvait le voir — elles blessaient, montaient de niveau et
/// s'affichaient correctement — et aucune capture non plus, une image étant muette. Il aura fallu
/// jouer. La table <see cref="WeaponSfx"/> existe pour rendre cet oubli impossible, et ces tests
/// pour qu'elle reste exhaustive quand une arme sera ajoutée.</para>
/// </summary>
public class WeaponSfxTests
{
    /// <summary>Identifiants de toutes les armes du jeu, lus dans les vraies données.</summary>
    private static string[] AllWeaponIds()
    {
        var json = File.ReadAllText(Path.Combine(TestPaths.RepoRoot, "data", "weapons.json"));
        var (weapons, fusions) = WeaponTable.Parse(json);

        return weapons.Keys.Concat(fusions.Keys).Distinct().ToArray();
    }

    [Fact]
    public void EveryWeapon_IsCovered_EitherBySoundOrByExplicitSilence()
    {
        var uncovered = AllWeaponIds().Where(id => !WeaponSfx.Covers(id)).ToArray();

        Assert.True(uncovered.Length == 0,
            "Armes sans entrée dans WeaponSfx — elles seraient MUETTES sans que rien ne le signale :\n  "
            + string.Join("\n  ", uncovered));
    }

    [Fact]
    public void SilentWeapons_AreExactlyTheContinuousOnes()
    {
        // Verrou de décision, pas de mécanique : ces trois armes frappent en continu (essaim en
        // orbite, voile de givre à 0,35 s de recharge) et un son par tick s'entendrait comme un
        // bourdonnement permanent. Toute autre arme muette est un oubli.
        Assert.Equal(new[] { "drone_swarm", "orbital_swarm", "frost_veil" }, WeaponSfx.Silent);

        foreach (var id in WeaponSfx.Silent)
            Assert.Null(WeaponSfx.For(id));
    }

    [Fact]
    public void EverySoundUsed_HasAWavFile()
    {
        var sfxDir = Path.Combine(TestPaths.RepoRoot, "assets", "audio", "sfx");

        var missing = WeaponSfx.AllSfxIds
            .Distinct()
            .Where(id => !File.Exists(Path.Combine(sfxDir, id + ".wav")))
            .ToArray();

        Assert.True(missing.Length == 0,
            "Sons d'arme sans fichier .wav :\n  " + string.Join("\n  ", missing));
    }

    /// <summary>
    /// Le son doit être <b>importé côté Unity</b>, et pas seulement présent dans les sources : ces
    /// deux dossiers sont distincts, et un fichier absent de <c>Resources/</c> se charge en
    /// <c>null</c> à l'exécution — l'arme redevient muette exactement comme avant le correctif.
    /// </summary>
    [Fact]
    public void EverySoundUsed_IsPresentInUnityResources()
    {
        var resources = Path.Combine(TestPaths.RepoRoot, "unity", "Assets", "Resources", "Audio", "sfx");

        var missing = WeaponSfx.AllSfxIds
            .Distinct()
            .Where(id => !File.Exists(Path.Combine(resources, id + ".wav")))
            .ToArray();

        Assert.True(missing.Length == 0,
            "Sons d'arme absents de unity/Assets/Resources/Audio/sfx :\n  " + string.Join("\n  ", missing));
    }

    /// <summary>
    /// Les armes de la même famille partagent leur son. Ce test ne fige pas la table entière — il
    /// vérifie les correspondances reprises du jeu publié qui ne sont pas devinables : la Bobine
    /// Tesla emprunte la décharge du Champ de Surcharge, la Lance Cryogénique le rail, le Flux de
    /// Braise la lame.
    /// </summary>
    [Theory]
    [InlineData("tesla_coil",       "sfx_weapon_overload_pulse")]
    [InlineData("cryo_lance",       "sfx_weapon_rail_shoot")]
    [InlineData("pyre_stream",      "sfx_weapon_plasma_swing")]
    [InlineData("singularity",      "sfx_weapon_overload_pulse")]
    [InlineData("overload_aegis",   "sfx_weapon_overload_pulse")]
    [InlineData("rail_overcharged", "sfx_weapon_rail_shoot")]
    public void KnownWeapons_KeepTheirPublishedSound(string weaponId, string expected)
        => Assert.Equal(expected, WeaponSfx.For(weaponId));
}
