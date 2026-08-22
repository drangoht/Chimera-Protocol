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

    /// <summary>
    /// Meilleure <b>durée de survie</b>, par biome <i>et par cran de saturation</i> — clé
    /// <see cref="SurvivalKey"/>.
    ///
    /// <para><b>Pourquoi une seconde table plutôt qu'une clé enrichie dans <see cref="HighScores"/>.</b>
    /// Celle-ci porte les records déjà gagnés par les joueurs, sous une clé qui est le seul identifiant
    /// de biome ; en changer la forme les effacerait tous au premier lancement. Les deux coexistent
    /// donc : <see cref="HighScores"/> reste le meilleur temps du biome, tous crans confondus — c'est
    /// ce qu'affiche la carte du niveau — et celle-ci ajoute le détail.</para>
    ///
    /// <para><b>Pourquoi le détail est nécessaire.</b> Depuis que la run se juge sur le temps tenu
    /// (GDD §38), un record qui mélange les crans est trompeur : tenir quinze minutes au cran 0 et les
    /// tenir au cran V ne sont pas la même performance, et le premier rendrait le second invisible à
    /// jamais. Un record qu'on ne peut plus battre cesse d'être un objectif.</para>
    /// </summary>
    public Dictionary<string, int> SurvivalRecords { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Clé d'un record de survie. Le séparateur <c>#</c> n'apparaît dans aucun identifiant de biome :
    /// deux niveaux ne peuvent pas se retrouver sur la même ligne par accident.
    /// </summary>
    public static string SurvivalKey(string biomeId, int saturation)
        => $"{biomeId}#{saturation}";

    /// <summary>
    /// Personnage choisi pour la prochaine run.
    /// </summary>
    /// <remarks>
    /// <para>⚠ La valeur par défaut n'est pas <c>""</c> mais l'identifiant de la Chimère, et ce n'est
    /// pas cosmétique : une sauvegarde écrite avant l'existence de ce champ le relit vide, et
    /// <c>Characters.Get</c> replierait alors sur le défaut de toute façon — mais l'écran de
    /// sélection, lui, afficherait « aucun personnage » et le joueur croirait avoir perdu son choix.
    /// Le défaut se déclare ici, une fois, plutôt que d'être deviné à chaque lecture.</para>
    /// </remarks>
    public string CharacterId { get; set; } = Characters.DefaultId;

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
        SurvivalRecords.Clear();
        DiscoveredWeapons.Clear();
        DiscoveredGrafts.Clear();
        SaturationByLevel.Clear();
        SaturationBeatenByLevel.Clear();
    }
}
