using System.Text.Json;

/// <summary>
/// Conversion d'une sauvegarde <b>Godot</b> vers le format Unity — logique pure et testable (Lot 6).
///
/// <para><b>Le seul point du portage dont l'échec est irréversible pour le joueur.</b>
/// <c>user://</c> (Godot, <c>%APPDATA%\Godot\app_userdata\…</c>) et
/// <c>Application.persistentDataPath</c> (Unity, <c>%USERPROFILE%\AppData\LocalLow\…</c>) sont deux
/// dossiers différents : sans migration, une mise à jour par l'app itch fait disparaître Échos,
/// améliorations, défis, perks, records, complétions et arsenal découvert. Le joueur n'a aucun
/// recours, et rien ne le prévient (§9.3, risque R5).</para>
///
/// <para><b>La conversion est ici, l'accès disque est ailleurs.</b> Séparer les deux est ce qui
/// permet de la vérifier sur une <b>vraie sauvegarde de joueur</b> (figée dans
/// <c>tests/fixtures/</c>) plutôt que sur un échantillon fabriqué — et une migration validée sur un
/// échantillon inventé ne prouve rien de ce qui compte.</para>
/// </summary>
public static class SaveMigration
{
    /// <summary>Options d'(dé)sérialisation — <b>camelCase, comme Godot l'écrivait</b>.</summary>
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    /// <summary>
    /// Relit un <c>save.json</c> écrit par Godot. Le DTO étant partagé par les deux moteurs, il n'y a
    /// rien à convertir — mais un fichier abîmé ne doit <b>jamais</b> faire perdre la partie : on rend
    /// une sauvegarde vide plutôt que de lever.
    /// </summary>
    public static SaveData ReadSave(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SaveData();

        try
        {
            return JsonSerializer.Deserialize<SaveData>(json, JsonOptions) ?? new SaveData();
        }
        catch (JsonException)
        {
            return new SaveData();
        }
    }

    /// <summary>Sérialise une sauvegarde au format lisible par les deux moteurs.</summary>
    public static string WriteSave(SaveData data) => JsonSerializer.Serialize(data, JsonOptions);

    /// <summary>Relit des préférences déjà au format Unity.</summary>
    public static SettingsData ReadSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new SettingsData();

        try
        {
            return JsonSerializer.Deserialize<SettingsData>(json, JsonOptions) ?? new SettingsData();
        }
        catch (JsonException)
        {
            return new SettingsData();
        }
    }

    /// <summary>Sérialise les préférences.</summary>
    public static string WriteSettings(SettingsData data) => JsonSerializer.Serialize(data, JsonOptions);

    /// <summary>
    /// Convertit un <c>settings.cfg</c> de Godot. Chaque champ absent garde sa valeur par défaut : une
    /// sauvegarde antérieure à un réglage ne doit pas empêcher les autres de survivre.
    /// </summary>
    public static SettingsData FromLegacySettings(string? configText)
    {
        var doc = GodotConfig.Parse(configText);
        var d = new SettingsData();

        d.MasterVolume = doc.GetFloat("audio", "master", d.MasterVolume);
        d.MusicVolume  = doc.GetFloat("audio", "music",  d.MusicVolume);
        d.SfxVolume    = doc.GetFloat("audio", "sfx",    d.SfxVolume);

        d.Language    = doc.GetString("display", "language", d.Language);
        d.DisplayMode = doc.GetInt("display", "mode", d.DisplayMode);
        d.Width       = doc.GetInt("display", "width", d.Width);
        d.Height      = doc.GetInt("display", "height", d.Height);
        d.Vsync       = doc.GetBool("display", "vsync", d.Vsync);
        d.MaxFps      = doc.GetInt("display", "max_fps", d.MaxFps);
        d.ShowFps     = doc.GetBool("display", "show_fps", d.ShowFps);

        d.Difficulty     = doc.GetInt("gameplay", "difficulty", d.Difficulty);
        d.SaveVersion    = doc.GetInt("gameplay", "save_version", d.SaveVersion);
        d.ShakeIntensity = doc.GetFloat("gameplay", "shake_intensity", d.ShakeIntensity);
        d.ReduceFlashes  = doc.GetBool("gameplay", "reduce_flashes", d.ReduceFlashes);
        d.Rumble         = doc.GetFloat("gameplay", "rumble", d.Rumble);

        d.SaturationByLevel =
            GodotConfig.ParsePairTable(doc.GetStringArray("gameplay", "saturation_by_level"));
        d.SaturationBeatenByLevel =
            GodotConfig.ParsePairTable(doc.GetStringArray("gameplay", "saturation_beaten_by_level"));

        d.Completions = GodotConfig.ParsePairTable(doc.GetStringArray("progress", "completions"));

        // Les records vivent dans une section OUVERTE : un biome par clé. On ne peut donc pas les
        // lire par une liste connue d'avance — c'est la section elle-même qui fait foi.
        foreach (string biome in doc.Keys("highscores"))
            d.HighScores[biome] = doc.GetInt("highscores", biome);

        d.DiscoveredWeapons.AddRange(doc.GetStringArray("discovered", "weapons"));
        d.DiscoveredGrafts.AddRange(doc.GetStringArray("discovered", "grafts"));

        d.VersionStamp = doc.GetBool("interface", "version_stamp", d.VersionStamp);
        d.Discord      = doc.GetBool("interface", "discord", d.Discord);

        return d;
    }

    /// <summary>
    /// La migration a-t-elle vraiment ramené quelque chose ? Sert à ne journaliser (et à ne
    /// n'annoncer au joueur) une reprise que lorsqu'il y avait effectivement une partie à reprendre.
    /// </summary>
    public static bool CarriesProgress(SaveData save, SettingsData settings)
        => save.Meta.TotalEchoesEarned > 0
        || save.Meta.Upgrades.Count > 0
        || settings.Completions.Count > 0
        || settings.HighScores.Count > 0;
}
