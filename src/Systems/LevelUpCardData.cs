/// <summary>DTO d'une carte de level-up (arme/passif/fusion/xp_bonus), produit par LevelUpSystem.</summary>
public sealed class LevelUpCardData
{
    public string Id          { get; }
    public string DisplayName { get; }
    public string Description { get; }
    public string Rarity      { get; }
    public string CardType    { get; }   // "weapon" | "passive" | "fusion" | "xp_bonus"

    public LevelUpCardData(string id, string displayName, string description, string rarity, string cardType)
    {
        Id          = id;
        DisplayName = displayName;
        Description = description;
        Rarity      = rarity;
        CardType    = cardType;
    }

    public Godot.Collections.Dictionary ToGodotDict()
    {
        var d = new Godot.Collections.Dictionary();
        d["id"]           = Id;
        d["display_name"] = DisplayName;
        d["description"]  = Description;
        d["rarity"]       = Rarity;
        d["card_type"]    = CardType;
        return d;
    }
}
