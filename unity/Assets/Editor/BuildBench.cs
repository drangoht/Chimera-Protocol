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
    private const string OutDirMono   = "Build/bench-mono";
    private const string OutDirIl2cpp = "Build/bench-il2cpp";
    private const string OutDirSmoke       = "Build/platform-smoke";
    private const string OutDirSmokeIl2cpp = "Build/platform-smoke-il2cpp";

    [MenuItem("Chimera/Build banc (Mono)")]
    public static void Windows64Mono()
        => Build<BenchProto>(ScriptingImplementation.Mono2x, OutDirMono, "BenchProto");

    [MenuItem("Chimera/Build banc (IL2CPP)")]
    public static void Windows64Il2cpp()
        => Build<BenchProto>(ScriptingImplementation.IL2CPP, OutDirIl2cpp, "BenchProto");

    /// <summary>
    /// Build de la vérification à l'exécution de la couche d'adaptation. Elle ne peut pas tourner
    /// dans la suite xUnit : elle dépend du cycle de vie Unity (Update/LateUpdate, timeScale,
    /// destruction d'objets), c'est-à-dire précisément de ce que les tests purs ne couvrent pas.
    /// </summary>
    [MenuItem("Chimera/Build verification plateforme (Mono)")]
    public static void Windows64PlatformSmoke()
        => Build<PlatformSmokeTest>(ScriptingImplementation.Mono2x, OutDirSmoke, "PlatformSmoke");

    /// <summary>
    /// Même vérification, compilée en IL2CPP. C'est la seule façon de savoir si la sérialisation
    /// par réflexion de System.Text.Json survit à l'AOT : elle échoue à l'exécution uniquement,
    /// jamais dans l'éditeur ni à la compilation (§5.2, R7).
    /// </summary>
    [MenuItem("Chimera/Build verification plateforme (IL2CPP)")]
    public static void Windows64PlatformSmokeIl2cpp()
        => Build<PlatformSmokeTest>(ScriptingImplementation.IL2CPP, OutDirSmokeIl2cpp, "PlatformSmoke");

    private static void Build<T>(ScriptingImplementation backend, string outDir, string sceneName)
        where T : MonoBehaviour
    {
        string scene = EnsureScene<T>(sceneName);

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

    /// <summary>Crée (ou recrée) une scène ne contenant qu'un GameObject portant le composant demandé.</summary>
    private static string EnsureScene<T>(string sceneName) where T : MonoBehaviour
    {
        Directory.CreateDirectory(Path.Combine(Application.dataPath, "Scenes"));
        string path = $"Assets/Scenes/{sceneName}.unity";

        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        var go = new GameObject(sceneName);
        var component = go.AddComponent<T>();

        // Injection d'un vrai jeu d'animations : hors Resources/, un asset n'existe à l'exécution
        // que s'il est référencé par une scène. Ce lien fait partie de ce qu'on vérifie.
        if (component is PlatformSmokeTest smoke)
        {
            smoke.TestFrames = AssetDatabase.LoadAssetAtPath<SpriteFramesAsset>(
                "Assets/Art/spriteframes/aether_golem.asset");
            if (smoke.TestFrames == null)
                Debug.LogWarning("[BUILD] SpriteFrames de test introuvable — lancer " +
                                 "BuildSpriteFrames.Run d'abord.");
        }

        EditorSceneManager.MoveGameObjectToScene(go, scene);
        EditorSceneManager.SaveScene(scene, path);

        Debug.Log("[BUILD] scene generee : " + path);
        return path;
    }
}
