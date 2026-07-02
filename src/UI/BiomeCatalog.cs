using Godot;
using System.Collections.Generic;

/// <summary>Infos d'un biome pour l'écran de sélection de niveau (présentation).</summary>
public sealed class BiomeEntry
{
    public string Id          { get; }
    public string Name        { get; }
    public string Effect      { get; }
    public string Description { get; }
    public Color  Accent      { get; }
    public string PreviewPath { get; }   // tuile représentative (affichée en damier)

    public BiomeEntry(string id, string name, string effect, string description, Color accent, string preview)
    {
        Id = id; Name = name; Effect = effect; Description = description; Accent = accent; PreviewPath = preview;
    }
}

/// <summary>Catalogue des biomes pour l'écran de sélection de niveau (parallèle aux BiomeDef de GroundRenderer, lié par Id).</summary>
public static class BiomeCatalog
{
    private const string Tiles = "res://assets/sprites/tileset/";

    public static readonly IReadOnlyList<BiomeEntry> All = new List<BiomeEntry>
    {
        new("sanctuaire", "Sanctuaire Rouillé", "Terrain neutre",
            "L'arène d'origine, bleu-acier. Aucun bonus ni malus — le bon endroit pour apprendre la boucle.",
            new(0.30f, 0.85f, 0.95f), Tiles + "tile_floor_crack.png"),

        new("aether", "Friche d'Aether", "+20% d'XP",
            "Ruines saturées d'Aether corrompu. L'XP gagnée est augmentée de 20% : montée en puissance accélérée.",
            new(0.62f, 0.40f, 1.0f), Tiles + "biomes/aether/floor_03.png"),

        new("givre", "Givre Cryogénique", "Ennemis -18% lents",
            "Le gel engourdit tout. Les ennemis sont 18% plus lents : du répit pour respirer, kiter et viser.",
            new(0.62f, 0.88f, 0.95f), Tiles + "biomes/givre/floor_03.png"),

        new("fournaise", "Fournaise", "Ennemis +18% rapides",
            "Chaleur infernale qui surexcite la Rouille. Les ennemis se déplacent 18% plus vite — pour les téméraires.",
            new(1.0f, 0.50f, 0.18f), Tiles + "biomes/fournaise/floor_03.png"),

        new("neon", "Secteur Néon", "Overclock : ennemis +10% rapides, +15% XP",
            "Un secteur de données overclocké, nappé de néons. Les ennemis vont 10% plus vite, mais l'XP est augmentée de 15% : risque contre récompense.",
            new(0.95f, 0.30f, 0.85f), Tiles + "biomes/neon/floor_02.png"),
    };
}
