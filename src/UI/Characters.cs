using Godot;
using System.Collections.Generic;

/// <summary>Définition d'un personnage jouable : identité + stats de départ + arme + teinte.</summary>
public sealed class CharacterDef
{
    public string Id               { get; }
    public string Name             { get; }
    public string Tag              { get; }
    public string Description      { get; }
    public float  MaxHp            { get; }
    public float  Speed            { get; }
    public string StartingWeaponId { get; }
    /// <summary>Couleur d'aura (PointLight2D) du personnage.</summary>
    public Color  Tint             { get; }
    /// <summary>SpriteFrames dédié du personnage (chemin res://).</summary>
    public string FramesPath       { get; }

    public CharacterDef(string id, string name, string tag, string description,
                        float maxHp, float speed, string startingWeaponId, Color tint, string framesPath)
    {
        Id = id; Name = name; Tag = tag; Description = description;
        MaxHp = maxHp; Speed = speed; StartingWeaponId = startingWeaponId; Tint = tint; FramesPath = framesPath;
    }
}

/// <summary>
/// Registre statique des personnages jouables (source unique, à la manière de Codex).
/// Chaque perso pose ses stats de base AVANT les bonus méta (qui s'ajoutent par-dessus).
/// </summary>
public static class Characters
{
    private static readonly Color Cyan   = new(0.6f,  1f,    0.95f);
    private static readonly Color Orange = new(1f,    0.78f, 0.55f);
    private static readonly Color Green  = new(0.7f,  1f,    0.7f);

    public static readonly IReadOnlyList<CharacterDef> All = new List<CharacterDef>
    {
        new("chimera", "Chimera", "Cyborg — équilibré",
            "Le prototype polyvalent. Stats équilibrées et le fiable Canon à Impulsions. "
            + "Le bon point de départ pour apprendre la boucle.",
            100f, 200f, "impulse_cannon", Cyan,
            "res://assets/sprites/player/player_frames.tres"),

        new("titan", "Titan-Gardien", "Robot lourd — tank",
            "Châssis de combat blindé : beaucoup plus de PV mais plus lent. Démarre avec l'Essaim de "
            + "Drones pour encaisser et broyer au contact. Pour jouer en force.",
            150f, 160f, "drone_swarm", Orange,
            "res://assets/sprites/player/titan/titan_frames.tres"),

        new("vagabond", "Vagabond", "Humain — mobilité",
            "Survivant de chair et d'os : peu de PV mais très rapide. Démarre avec la Lame Plasma "
            + "pour frapper et se dégager. Récompense le kite agressif et l'esquive.",
            75f, 245f, "plasma_blade", Green,
            "res://assets/sprites/player/vagabond/vagabond_frames.tres"),
    };

    public static CharacterDef Get(string id)
    {
        foreach (var c in All) if (c.Id == id) return c;
        return All[0];
    }
}
