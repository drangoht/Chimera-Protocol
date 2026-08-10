using System.Text.RegularExpressions;
using Xunit;

namespace ChimeraProtocol.Tests;

/// <summary>
/// Garde-fou d'assets : un id de SFX inventé ne casse rien à la compilation et ne se voit qu'en
/// jouant l'écran concerné (« SFX introuvable : sfx_ui_click », sélecteur de cran de saturation).
/// Ce test relit les sources et vérifie que chaque littéral passé à PlaySfx/PreloadSfx a bien un
/// fichier dans <c>unity/Assets/Resources/Audio/sfx/</c> — là où <c>Resources.Load</c> ira le
/// chercher, et non dans un dossier de sources que le binaire n'embarque pas.
/// </summary>
public class AudioAssetReferenceTests
{
    private static readonly Regex SfxCall =
        new(@"\b(?:PlaySfx|PreloadSfx)\(\s*""(?<id>[^""]+)""", RegexOptions.Compiled);

    [Fact]
    public void EverySfxIdReferencedInCode_HasAWavFile()
    {
        var root = TestPaths.RepoRoot;
        var sfxDir = TestPaths.Sfx;
        Assert.True(Directory.Exists(sfxDir), $"Dossier des SFX introuvable : {sfxDir}");

        var missing = new List<string>();

        var sources = Directory.EnumerateFiles(Path.Combine(TestPaths.UnityAssets, "Scripts"), "*.cs",
                                               SearchOption.AllDirectories);

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
}
