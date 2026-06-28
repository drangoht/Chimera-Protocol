using Godot;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// AutoLoad singleton — responsabilité unique : lire/écrire user://save.json.
/// Ne connaît pas la logique meta, seulement le JSON brut.
/// </summary>
public partial class SaveManager : Node
{
    public static SaveManager Instance { get; private set; } = null!;

    private const string SavePath = "user://save.json";

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented         = true,
        PropertyNamingPolicy  = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>Sérialise <paramref name="data"/> en JSON et l'écrit dans user://save.json.</summary>
    public void Save(SaveData data)
    {
        string json = JsonSerializer.Serialize(data, _jsonOptions);
        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Write);
        if (file == null)
        {
            GD.PrintErr($"[SaveManager] Impossible d'ouvrir {SavePath} en écriture.");
            return;
        }
        file.StoreString(json);
        GD.Print($"[SaveManager] Sauvegarde écrite ({json.Length} octets).");
    }

    /// <summary>Désérialise user://save.json. Retourne un <see cref="SaveData"/> vide si le fichier est absent ou invalide.</summary>
    public SaveData Load()
    {
        if (!Godot.FileAccess.FileExists(SavePath))
        {
            GD.Print("[SaveManager] Aucune sauvegarde trouvée — retourne SaveData vide.");
            return new SaveData();
        }

        using var file = Godot.FileAccess.Open(SavePath, Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr($"[SaveManager] Impossible de lire {SavePath}.");
            return new SaveData();
        }

        string json = file.GetAsText();
        try
        {
            var data = JsonSerializer.Deserialize<SaveData>(json, _jsonOptions);
            GD.Print("[SaveManager] Sauvegarde chargée.");
            return data ?? new SaveData();
        }
        catch (JsonException ex)
        {
            GD.PrintErr($"[SaveManager] JSON invalide : {ex.Message} — retourne SaveData vide.");
            return new SaveData();
        }
    }
}

// ---------------------------------------------------------------------------
// DTOs de sauvegarde
// ---------------------------------------------------------------------------

public class SaveData
{
    public MetaSaveData Meta { get; set; } = new();
}

public class MetaSaveData
{
    public int CurrentEchoes     { get; set; } = 0;
    public int TotalEchoesEarned { get; set; } = 0;
    public int TotalEchoesSpent  { get; set; } = 0;
    public System.Collections.Generic.Dictionary<string, int> Upgrades { get; set; } = new();
}
