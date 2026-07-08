using System.Collections.Generic;

/// <summary>Racine de la sauvegarde (user://save.json) — DTO sérialisé par SaveManager.</summary>
public class SaveData
{
    public MetaSaveData Meta { get; set; } = new();
}

/// <summary>Progression meta persistée : Échos d'Aether et niveaux d'améliorations.</summary>
public class MetaSaveData
{
    public int CurrentEchoes     { get; set; } = 0;
    public int TotalEchoesEarned { get; set; } = 0;
    public int TotalEchoesSpent  { get; set; } = 0;
    public Dictionary<string, int> Upgrades { get; set; } = new();

    // ── Défis / Succès (ChallengeSystem) ───────────────────────────────────────
    /// <summary>Ids des défis déjà accomplis (récompense déjà versée). Empêche le double-octroi.</summary>
    public List<string> UnlockedChallenges { get; set; } = new();
    /// <summary>Ids des perks de départ débloqués par un défi (équipables au Hub — lot 3).</summary>
    public List<string> UnlockedPerks { get; set; } = new();
    /// <summary>Ids des cosmétiques / titres débloqués par un défi (sélectionnables — lot 4).</summary>
    public List<string> UnlockedCosmetics { get; set; } = new();

    // ── Compteurs cumulés (pour les défis à condition "lifetime_*") ─────────────
    public long LifetimeKills { get; set; } = 0;
    public int  LifetimeRuns  { get; set; } = 0;
}
