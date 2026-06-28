/// <summary>
/// Poids de tirage par rareté pour les cartes de level-up (logique pure, testable).
/// Commun 60 / Rare 30 / Épique 10 (cf. levelup_config.json).
/// </summary>
public static class RarityWeights
{
    public static float Weight(string rarity) => rarity switch
    {
        "common" => 60f,
        "rare"   => 30f,
        "epic"   => 10f,
        _        => 60f,
    };
}
