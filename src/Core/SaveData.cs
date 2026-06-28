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
}
