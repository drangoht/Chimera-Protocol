using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Méta-progression : Échos d'Aether et améliorations permanentes (Lot 6).
///
/// <para><b>C'est la boucle de rétention du jeu</b> : une run rapporte des Échos, les Échos achètent
/// des améliorations au Hub, et les améliorations se sentent à la run suivante. Chacun des trois
/// maillons est inutile sans les deux autres — d'où leur portage en un seul bloc.</para>
///
/// <para><b>Statique, comme <see cref="GameSettings"/></b> : ces données sont lues depuis le menu
/// principal, avant qu'aucune scène de jeu n'existe. Les règles (lecture du fichier, prix,
/// remboursement, bonus) vivent dans <see cref="MetaUpgradeTable"/> — ici il n'y a que l'état et sa
/// persistance.</para>
/// </summary>
public static class MetaProgression
{
    private static MetaUpgradeTable.Document? _catalog;
    private static SaveData? _save;

    /// <summary>Catalogue des améliorations, chargé une fois.</summary>
    public static MetaUpgradeTable.Document Catalog
    {
        get
        {
            if (_catalog != null) return _catalog;

            _catalog = MetaUpgradeTable.Parse(DataFiles.Load("meta_upgrades.json"));
            Debug.Log($"[MetaProgression] {_catalog.Upgrades.Count} ameliorations chargees.");
            return _catalog;
        }
    }

    /// <summary>
    /// Sauvegarde en mémoire. <b>Propriétaire unique de <c>save.json</c></b> : tout autre système qui
    /// tiendrait sa propre copie écraserait les Échos en écrivant la sienne.
    /// </summary>
    public static SaveData Save
    {
        get
        {
            if (_save != null) return _save;

            // Passe par GameSettings : c'est lui qui déclenche, au tout premier accès, la reprise
            // d'une installation Godot. La lire ici garantit qu'elle a eu lieu avant toute écriture.
            _ = GameSettings.Current;

            _save = UserData.LoadSave();
            return _save;
        }
    }

    /// <summary>Échos disponibles.</summary>
    public static int CurrentEchoes => Save.Meta.CurrentEchoes;

    /// <summary>Améliorations dans leur ordre d'affichage.</summary>
    public static IReadOnlyList<MetaUpgradeDefinition> All => Catalog.Ordered;

    /// <summary>Niveau acheté pour une amélioration (0 si aucune).</summary>
    public static int LevelOf(string upgradeId)
        => Save.Meta.Upgrades.TryGetValue(upgradeId, out int level) ? level : 0;

    /// <summary>Prix du prochain niveau, ou <c>-1</c> si l'amélioration est au maximum.</summary>
    public static int NextCost(string upgradeId)
        => Catalog.Upgrades.TryGetValue(upgradeId, out var def)
            ? MetaUpgradeTable.NextCost(def, LevelOf(upgradeId))
            : -1;

    /// <summary>L'achat est-il possible ici et maintenant ?</summary>
    public static bool CanPurchase(string upgradeId)
    {
        int cost = NextCost(upgradeId);
        return cost >= 0 && CurrentEchoes >= cost;
    }

    /// <summary>Achète le prochain niveau. Renvoie faux si l'achat est refusé.</summary>
    public static bool TryPurchase(string upgradeId)
    {
        if (!CanPurchase(upgradeId)) return false;

        int cost = NextCost(upgradeId);
        var meta = Save.Meta;

        meta.CurrentEchoes -= cost;
        meta.TotalEchoesSpent += cost;
        meta.Upgrades[upgradeId] = LevelOf(upgradeId) + 1;

        Persist();
        Debug.Log($"[MetaProgression] {upgradeId} -> niveau {meta.Upgrades[upgradeId]} " +
                  $"({cost} Echos, reste {meta.CurrentEchoes}).");
        return true;
    }

    /// <summary>Crédite les Échos gagnés en fin de run.</summary>
    public static void AddEchoes(int amount)
    {
        if (amount <= 0) return;

        Save.Meta.CurrentEchoes += amount;
        Save.Meta.TotalEchoesEarned += amount;
        Persist();
    }

    /// <summary>Comptabilise une run terminée — base des défis « cumulés ».</summary>
    public static void RegisterRun(int kills)
    {
        Save.Meta.LifetimeRuns++;
        Save.Meta.LifetimeKills += Math.Max(0, kills);
        Persist();
    }

    /// <summary>
    /// Applique les bonus permanents aux statistiques, <b>avant</b> le début d'une run.
    ///
    /// <para>Les plafonds sont posés ici, à l'écriture, et non dans la table : c'est le total —
    /// méta plus passifs de run — qui doit rester borné, sinon le Hub rouvrirait le power-creep que
    /// <see cref="StatCaps"/> referme.</para>
    /// </summary>
    public static void ApplyTo(PlayerStats stats)
    {
        var b = MetaUpgradeTable.BonusesFor(LevelOf);

        stats.MaxHp += b.MaxHp;
        stats.CurrentHp = stats.MaxHp;

        stats.DamageMultiplier += b.DamageMultiplier;

        stats.Speed = StatCaps.CapSpeed(stats.Speed + b.Speed);
        stats.BaseSpeed = stats.Speed;

        stats.CooldownReduction = StatCaps.CapCooldownReduction(stats.CooldownReduction + b.CooldownReduction);
        stats.DamageReduction   = StatCaps.CapDamageReduction(stats.DamageReduction + b.DamageReduction);

        stats.HpRegenPerSecond += b.HpRegenPerSecond;
    }

    /// <summary>
    /// Rend tous les Échos dépensés et efface les améliorations. Renvoie le montant remboursé.
    /// </summary>
    public static int ResetUpgrades()
    {
        int refund = 0;
        foreach (var def in Catalog.Ordered)
            refund += MetaUpgradeTable.SpentUpTo(def, LevelOf(def.Id));

        if (refund == 0 && Save.Meta.Upgrades.Count == 0) return 0;

        Save.Meta.Upgrades.Clear();
        Save.Meta.CurrentEchoes += refund;
        Save.Meta.TotalEchoesSpent = Math.Max(0, Save.Meta.TotalEchoesSpent - refund);

        Persist();
        return refund;
    }

    /// <summary>
    /// Remet toute la méta-progression à zéro — Échos, améliorations, défis, compteurs.
    /// <b>Irréversible.</b> Sert au banc, qui doit repartir d'un état connu à chaque campagne, et à
    /// l'option de remise à zéro du joueur.
    /// </summary>
    public static void HardReset()
    {
        _save = new SaveData();
        Persist();
    }

    /// <summary>Écrit la sauvegarde. Point d'écriture unique.</summary>
    public static void Persist() => UserData.SaveGame(Save);

    /// <summary>Oublie l'état chargé — réservé aux bancs.</summary>
    public static void Reset()
    {
        _save = null;
        _catalog = null;
    }
}
