using System.Text.RegularExpressions;
using Xunit;

namespace ChimeraProtocol.Tests;

/// <summary>
/// Garde-fou d'assets : un id de SFX inventé ne casse rien à la compilation et ne se voit qu'en
/// jouant l'écran concerné (« SFX introuvable : sfx_ui_click », sélecteur de cran de saturation).
/// Ce test relit les sources et vérifie que chaque littéral passé à PlaySfx/PreloadSfx a bien un
/// fichier dans assets/audio/sfx/.
/// </summary>
public class AudioAssetReferenceTests
{
    private static readonly Regex SfxCall =
        new(@"\b(?:PlaySfx|PreloadSfx)\(\s*""(?<id>[^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void EverySfxIdReferencedInCode_HasAWavFile()
    {
        var root  = RepoRoot();
        var sfxDir = Path.Combine(root, "assets", "audio", "sfx");
        Assert.True(Directory.Exists(sfxDir), $"Dossier des SFX introuvable : {sfxDir}");

        var missing = new List<string>();

        // Les DEUX moteurs sont balayés. Le port Unity charge ses effets par le même identifiant,
        // depuis Resources/Audio/sfx : n'inspecter que src/ laissait la moitié du jeu hors du
        // garde-fou, précisément la moitié la plus récente.
        var sources = Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs",
                                               SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(root, "unity", "Assets", "Scripts"), "*.cs",
                                             SearchOption.AllDirectories));

        foreach (var file in sources)
        {
            var lines = File.ReadAllLines(file);
            for (int i = 0; i < lines.Length; i++)
            {
                foreach (Match m in SfxCall.Matches(lines[i]))
                {
                    var id = m.Groups["id"].Value;
                    if (!File.Exists(Path.Combine(sfxDir, id + ".wav")))
                        missing.Add($"{Path.GetRelativePath(root, file)}:{i + 1} → {id}");
                }
            }
        }

        Assert.True(missing.Count == 0,
            "SFX référencés dans le code sans fichier .wav :\n  " + string.Join("\n  ", missing));
    }

    /// <summary>Racine du dépôt, repérée en remontant jusqu'au .csproj du jeu.</summary>
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ChimeraProtocol.csproj")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!.FullName;
    }
}
