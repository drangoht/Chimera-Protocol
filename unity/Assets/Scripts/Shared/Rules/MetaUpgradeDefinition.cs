using System.Collections.Generic;

/// <summary>Définition d'une amélioration meta (chargée depuis meta_upgrades.json).</summary>
public sealed class MetaUpgradeDefinition
{
    public string Id          { get; set; } = "";
    public string Name        { get; set; } = "";
    public string Description { get; set; } = "";
    public int    MaxLevel    { get; set; } = 1;
    public string StatTarget  { get; set; } = "";
    public List<int>    CostPerLevel   { get; set; } = new();
    public List<double> EffectPerLevel { get; set; } = new();
}
