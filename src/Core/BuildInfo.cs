using Godot;

/// <summary>
/// Informations de build exposées à l'UI (tampon de version bas-droite) et à Discord.
/// <see cref="GitSha"/> est AUTO-GÉNÉRÉ : ne pas éditer à la main — régénéré par
/// <c>tools/gen_build_info.ps1</c> (appelé par <c>tools/release_itch.ps1</c> avant l'export,
/// pour que le SHA corresponde au commit publié). La version vient de <c>project.godot</c>
/// (<c>application/config/version</c>), donc elle suit automatiquement le bump de release.
/// </summary>
public static class BuildInfo
{
    // <AUTOGEN:GITSHA> — remplacé par le SHA court courant lors de la génération.
    public const string GitSha = "b91865b";
    // </AUTOGEN:GITSHA>

    /// <summary>Version sémantique déclarée dans project.godot (ex. "1.10.0").</summary>
    public static string Version => (string)ProjectSettings.GetSetting("application/config/version", "");

    /// <summary>Libellé complet affiché en jeu : <c>v1.10.0-186397d</c>.</summary>
    public static string Label => $"v{Version}-{GitSha}";
}
