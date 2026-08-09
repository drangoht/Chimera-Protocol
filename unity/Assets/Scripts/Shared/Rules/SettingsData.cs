using System;
using System.Collections.Generic;

/// <summary>
/// Préférences et progression <b>hors méta</b> : ce que Godot rangeait dans <c>user://settings.cfg</c>
/// (Lot 6).
///
/// <para>Le format change — Godot écrivait un <c>ConfigFile</c>, Unity écrit du JSON — mais le
/// <b>contenu</b> est identique, clé pour clé. C'est ce qui permet à <see cref="SaveMigration"/> de
/// convertir une sauvegarde de joueur sans rien interpréter.</para>
///
/// <para>⚠ Ce fichier ne contient pas que des préférences : il porte les <b>records</b>, les
/// <b>complétions</b>, l'<b>arsenal découvert</b> et les <b>crans de saturation par biome</b>. Le
/// perdre, c'est effacer la progression d'un joueur aussi sûrement que perdre <c>save.json</c>.</para>
/// </summary>
public sealed class SettingsData
{
    /// <summary>Version du schéma. 2 = tables par biome (1.25.0).</summary>
    public int SaveVersion { get; set; } = 2;

    // ─── Audio ────────────────────────────────────────────────────────────────
    public float MasterVolume { get; set; } = 1f;
    public float MusicVolume  { get; set; } = 0.8f;
    public float SfxVolume    { get; set; } = 0.9f;

    // ─── Affichage ────────────────────────────────────────────────────────────
    public string Language { get; set; } = "fr";
    public int  DisplayMode { get; set; } = 2;
    public int  Width       { get; set; } = 1280;
    public int  Height      { get; set; } = 720;
    public bool Vsync       { get; set; } = true;
    public int  MaxFps      { get; set; }
    public bool ShowFps     { get; set; }

    // ─── Jeu ──────────────────────────────────────────────────────────────────
    /// <summary>Réglage d'assistance historique : 0 facile, 1 normal, 2 difficile.</summary>
    public int Difficulty { get; set; } = 1;

    public float ShakeIntensity { get; set; } = 1f;
    public bool  ReduceFlashes  { get; set; }
    public float Rumble         { get; set; } = 1f;

    /// <summary>Cran de saturation choisi, <b>par biome</b> (1.25.0 : le cran se règle par niveau).</summary>
    public Dictionary<string, int> SaturationByLevel { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Cran le plus haut <b>battu</b> par biome — c'est lui qui débloque le suivant.</summary>
    public Dictionary<string, int> SaturationBeatenByLevel { get; set; } = new(StringComparer.Ordinal);

    // ─── Progression ──────────────────────────────────────────────────────────
    public Dictionary<string, int> Completions { get; set; } = new(StringComparer.Ordinal);
    public Dictionary<string, int> HighScores  { get; set; } = new(StringComparer.Ordinal);

    public List<string> DiscoveredWeapons { get; set; } = new();
    public List<string> DiscoveredGrafts  { get; set; } = new();

    // ─── Interface ────────────────────────────────────────────────────────────
    public bool VersionStamp { get; set; } = true;
    public bool Discord      { get; set; } = true;

    /// <summary>
    /// Efface toute la <b>progression</b> portée par ce fichier, et rien d'autre.
    ///
    /// <para><b>Les préférences survivent</b> — volume, langue, plein écran, touches remappées. Un
    /// joueur qui demande à repartir de zéro veut retrouver un jeu vierge, pas un jeu qui lui reparle
    /// dans une langue qu'il ne lit pas et qui lui redemande de régler son écran. La frontière est
    /// donc « ce qui se gagne » contre « ce qui se règle ».</para>
    ///
    /// <para>⚠ Les <b>crans de saturation</b> tombent avec le reste, y compris le cran choisi. Un cran
    /// se débloque en battant le précédent : le laisser survivre donnerait un cran V ouvert à un
    /// joueur dont le compteur de victoires vient d'être remis à zéro — l'échelle mentirait sur ce
    /// qu'il a réellement gravi.</para>
    ///
    /// <para>Ne touche pas à <c>save.json</c> (Échos, améliorations, défis) : c'est un autre fichier,
    /// remis à zéro par <c>MetaProgression.HardReset</c>. Les deux vont ensemble — l'un sans l'autre
    /// laisse un joueur « neuf » avec 70 000 Échos, ou un arbre acheté sans aucune arme découverte.</para>
    /// </summary>
    public void ResetProgress()
    {
        Completions.Clear();
        HighScores.Clear();
        DiscoveredWeapons.Clear();
        DiscoveredGrafts.Clear();
        SaturationByLevel.Clear();
        SaturationBeatenByLevel.Clear();
    }
}
