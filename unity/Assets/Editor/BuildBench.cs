using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Build du prototype de banc, pilotable en ligne de commande (Lot 1).
/// La scène est construite PAR CODE : écrire du YAML <c>.unity</c> à la main est fragile, et une
/// scène de banc n'a aucune raison d'être un asset versionné qu'on édite.
///
/// <para>Usage :
/// <c>Unity.exe -batchmode -quit -projectPath unity -executeMethod BuildBench.Windows64Mono
/// -logFile &lt;log&gt;</c></para>
/// </summary>
public static class BuildBench
{
    private const string SceneAssetPath = "Assets/Scenes/BenchProto.unity";
    private const string OutDirMono     = "Build/bench-mono";
    private const string OutDirIl2cpp   = "Build/bench-il2cpp";

    [MenuItem("Chimera/Build banc (Mono)")]
    public static void Windows64Mono() => Build(ScriptingImplementation.Mono2x, OutDirMono);

    [MenuItem("Chimera/Build banc (IL2CPP)")]
    public static void Windows64Il2cpp() => Build(ScriptingImplementation.IL2CPP, OutDirIl2cpp);

    private static void Build(ScriptingImplementation backend, string outDir)
    {
        string scene = EnsureScene();

        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, backend);
        PlayerSettings.companyName    = "drangoht";
        PlayerSettings.productName    = "ChimeraProtocolBench";
        // Sans ça, un build headless peut rester bloqué sur l'écran de démarrage.
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.runInBackground   = true;

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string fullOutDir  = Path.Combine(projectRoot, outDir);
        Directory.CreateDirectory(fullOutDir);

        var options = new BuildPlayerOptions
        {
            scenes           = new[] { scene },
            locationPathName = Path.Combine(fullOutDir, "bench.exe"),
            target           = BuildTarget.StandaloneWindows64,
            options          = BuildOptions.None,
        };

        Debug.Log($"[BUILD] backend={backend} sortie={options.locationPathName}");
        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary s = report.summary;

        Debug.Log($"[BUILD] resultat={s.result} duree={s.totalTime} taille={s.totalSize} octets " +
                  $"erreurs={s.totalErrors}");

        if (s.result != BuildResult.Succeeded)
        {
            // Le code de sortie est ce que lit le script appelant : ne jamais réussir en silence.
            EditorApplication.Exit(1);
        }
    }

    /// <summary>Crée (ou recrée) la scène de banc : un seul GameObject portant <see cref="BenchProto"/>.</summary>
    private static string EnsureScene()
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject("BenchProto");
        go.AddComponent<BenchProto>();
        EditorSceneManager.MoveGameObjectToScene(go, scene);
        EditorSceneManager.SaveScene(scene, SceneAssetPath);

        Debug.Log("[BUILD] scene de banc generee : " + SceneAssetPath);
        return SceneAssetPath;
    }
}
