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
    private const string OutDirRunSmoke    = "Build/run-smoke";
    private const string OutDirGame        = "Build/game";
    private const string OutDirWeb         = "Build/web";

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

    /// <summary>
    /// Build du <b>vrai jeu</b> : menu principal + scène de run, telles que déclarées dans
    /// <see cref="GameScenes.All"/>. C'est ce build qui sera publié — les autres ne servent qu'à
    /// vérifier.
    /// </summary>
    [MenuItem("Chimera/Build du jeu (Mono)")]
    public static void Windows64Game()
    {
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.companyName = "drangoht";
        PlayerSettings.productName = "Chimera Protocol";
        PlayerSettings.SplashScreen.show = false;
        PlayerSettings.runInBackground = true;

        ApplyGameIcon();
        StampGitSha();
        StreamingManifest.Write();

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string outDir = Path.Combine(projectRoot, OutDirGame);
        Directory.CreateDirectory(outDir);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = GameScenePaths(),
            locationPathName = Path.Combine(outDir, "ChimeraProtocol.exe"),
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[BUILD] jeu : resultat={summary.result} duree={summary.totalTime} " +
                  $"taille={summary.totalSize} scenes={GameScenes.All.Length}");

        if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);

        WriteBuildStamp(outDir);
    }

    /// <summary>
    /// Build <b>web</b> du jeu — la même chose, jouable dans un navigateur.
    /// </summary>
    /// <remarks>
    /// <para>Usage : <c>-executeMethod BuildBench.WebGame</c>. Sortie : <c>unity/Build/web/</c>,
    /// à pousser tel quel sur itch.io en cochant « This file will be played in the browser ».</para>
    ///
    /// <para><b>Les réglages sont posés ici, pas laissés à l'éditeur</b>, pour la même raison que
    /// l'icône : un réglage fait à la souris ne vaut que sur le poste où il a été fait et se perd au
    /// premier clone. Et trois d'entre eux ne se voient <b>jamais</b> à la compilation — ils
    /// produisent un jeu qui démarre puis se comporte mal, ce qui est le pire des symptômes.</para>
    /// </remarks>
    [MenuItem("Chimera/Build du jeu (Web)")]
    public static void WebGame()
    {
        PlayerSettings.companyName = "drangoht";
        PlayerSettings.productName = "Chimera Protocol";
        PlayerSettings.SplashScreen.show = false;

        StampGitSha();
        StreamingManifest.Write();
        PluginPlatforms.Apply();
        ApplyWebSettings();

        string projectRoot = Directory.GetParent(Application.dataPath)!.FullName;
        string outDir = Path.Combine(projectRoot, OutDirWeb);
        Directory.CreateDirectory(outDir);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = GameScenePaths(),
            // ⚠ En WebGL, c'est un DOSSIER et non un fichier : Unity y écrit index.html et Build/.
            locationPathName = outDir,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        });

        var summary = report.summary;
        Debug.Log($"[BUILD] jeu web : resultat={summary.result} duree={summary.totalTime} " +
                  $"taille={summary.totalSize} scenes={GameScenes.All.Length}");

        if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);

        WriteBuildStamp(outDir);
        StampWebCacheBuster(outDir);
    }

    /// <summary>
    /// Réglages du lecteur web. Chacun corrige un défaut qui ne se voit qu'à l'exécution, dans un
    /// navigateur, sur une machine qui n'est pas celle du développeur.
    /// </summary>
    private static void ApplyWebSettings()
    {
        var web = NamedBuildTarget.WebGL;

        // ─── Mémoire ─────────────────────────────────────────────────────────
        // Le projet livrait le défaut d'Unity : 32 Mo de tas initial. Un survivor tient 200 à 300
        // entités simultanées à l'écran ; le tas grandit alors par paliers pendant la nuée, c'est-à-
        // dire précisément au moment où le jeu ne peut pas se permettre une pause. Partir large ne
        // coûte rien au chargement et supprime les à-coups de croissance.
        PlayerSettings.WebGL.initialMemorySize = 512;
        PlayerSettings.WebGL.maximumMemorySize = 2048;

        // ─── Transport ───────────────────────────────────────────────────────
        // Brotli comprime nettement mieux que Gzip sur du WebAssembly, mais le navigateur ne sait le
        // décompresser que si le serveur annonce l'encodage. Le repli JS rend le build indépendant de
        // cette configuration : il fonctionne sur itch.io comme sur un hébergement quelconque.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
        PlayerSettings.WebGL.decompressionFallback = true;

        // Le .data pèse des dizaines de mégaoctets, dont l'essentiel est de la musique qui ne change
        // pas d'une session à l'autre. Sans ce cache, chaque partie le retélécharge en entier.
        PlayerSettings.WebGL.dataCaching = true;

        // ─── Stripping ───────────────────────────────────────────────────────
        // ⚠ WebGL est la SEULE plateforme dont le niveau par défaut est le plus agressif. Combiné à
        // la sérialisation par réflexion de la sauvegarde, cela produit un jeu qui démarre, joue, et
        // écrit une sauvegarde vide. Assets/link.xml protège les règles ; ce niveau protège le reste.
        PlayerSettings.SetManagedStrippingLevel(web, ManagedStrippingLevel.Low);

        // ─── Diagnostic ──────────────────────────────────────────────────────
        // Les exceptions explicitement levées gardent leur pile dans la console du navigateur. Sans
        // cela, une exception se manifeste par un jeu qui s'arrête — sans message. C'est le seul
        // moyen d'instruire un défaut qu'on ne reproduit pas hors du navigateur ; le coût en vitesse
        // se rediscutera une fois le portage mesuré.
        // ⚠⚠ CE RÉGLAGE SUPPRIME LES VÉRIFICATIONS DE BORNES ET DE NULLITÉ. Il ne garde que les
        // exceptions *explicitement levées* — un `array[trop_loin]` ou un déréférencement nul ne
        // lèvent alors plus rien de lisible : ils tapent dans la mémoire linéaire et le navigateur
        // rend « RuntimeError: memory access out of bounds » suivi de trois cents offsets wasm, sans
        // un seul nom de méthode. Le réglage censé rendre les défauts instruisibles est donc celui
        // qui rend le plus grave d'entre eux illisible.
        //
        // ▶ Pour instruire un crash au démarrage : passer temporairement à `FullWithStacktrace`,
        //   rebuilder, lire le nom de la méthode, puis revenir ici.
        PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;

        // ─── Page hôte ───────────────────────────────────────────────────────
        // Le jeu tourne en plein écran logique : c'est un survivor, pas une vignette dans une page.
        //
        // ⚠ Le gabarit par défaut d'Unity produit un jeu qui DÉMARRE sur téléphone et ne s'y joue
        // pas : le double-appui zoome la page, le glissement la fait défiler, le geste depuis le bord
        // revient en arrière, et la barre d'URL recouvre le bas de l'écran — donc le bouton
        // d'esquive. Rien de tout cela n'est visible depuis Unity, et rien ne lève d'erreur : ces
        // défauts vivent AVANT le moteur. Le gabarit du projet les désarme, et plafonne le rapport de
        // pixels, qui est le réglage de performance le plus rentable du portage mobile.
        PlayerSettings.WebGL.template = "PROJECT:ChimeraMobile";
        PlayerSettings.WebGL.powerPreference = WebGLPowerPreference.HighPerformance;

        Debug.Log($"[BUILD] reglages web : tas {PlayerSettings.WebGL.initialMemorySize} Mo, " +
                  $"{PlayerSettings.WebGL.compressionFormat} (repli " +
                  $"{PlayerSettings.WebGL.decompressionFallback}), stripping Low, " +
                  $"gabarit {PlayerSettings.WebGL.template}.");
    }

    /// <summary>Chemins des scènes du jeu, dans l'ordre — la première est celle qui se charge.</summary>
    private static string[] GameScenePaths()
    {
        var scenes = new string[GameScenes.All.Length];
        for (int i = 0; i < scenes.Length; i++) scenes[i] = GameScenes.PathOf(GameScenes.All[i]);

        return scenes;
    }

    /// <summary>
    /// Écrit, à côté du binaire, la carte d'identité de ce qui vient d'être construit.
    /// </summary>
    /// <remarks>
    /// <para><b>C'est le seul contrôle honnête de fraîcheur.</b> Les métadonnées Windows d'un
    /// exécutable Unity décrivent le <b>moteur</b> — « 6000.5.6f1 » — et non le jeu : les interroger
    /// pour savoir quelle version on s'apprête à publier ne dit rien. Et l'horodatage du fichier ne
    /// vaut pas mieux, le build étant incrémental : un binaire identique n'est pas réécrit.</para>
    ///
    /// <para>Ce fichier, lui, est produit <b>par le build</b> : il ne peut pas annoncer une version
    /// que le build n'a pas posée. Le script de release le lit avant de pousser — c'est le garde-fou
    /// qui manquait le jour où une release a expédié le binaire de la version précédente.</para>
    /// </remarks>
    /// <summary>
    /// Pose l'identité git du code qu'on s'apprête à construire, dans la ressource que le jeu lit
    /// pour afficher son tampon. Appelé <b>avant</b> le build, sans quoi le binaire embarquerait la
    /// valeur précédente.
    /// </summary>
    /// <remarks>
    /// <para><b>Pourquoi ceci n'appartient pas au script de release.</b> C'était le cas, et le
    /// fichier ne bougeait donc qu'au moment d'une publication — mais il <b>restait là ensuite</b>.
    /// Tout build local ultérieur affichait le SHA de la <i>dernière release</i> : pas « dev », pas
    /// un avertissement, le numéro d'un commit qui n'est pas celui du binaire. Constaté le
    /// 2026-08-10, un build fraîchement construit annonçant un commit vieux d'un jour, précisément
    /// pendant qu'on cherchait à savoir si le binaire testé contenait un correctif. Un garde-fou de
    /// fraîcheur qui se trompe est pire que pas de garde-fou : on lui fait confiance.</para>
    ///
    /// <para>Écrire l'identité <b>ici</b> la lie à l'acte qui produit le binaire — elle ne peut plus
    /// décrire autre chose que ce qui vient d'être construit.</para>
    ///
    /// <para>Le suffixe <c>+</c> signale un <b>arbre de travail modifié</b> : le binaire ne
    /// correspond alors à aucun commit, et c'est le cas le plus courant pendant une session de mise
    /// au point. Deux fichiers sont exclus de ce constat — <c>build_sha.txt</c> et
    /// <c>ProjectSettings.asset</c> — parce que la release les pose juste avant de construire et les
    /// commite juste après : ils sont en avance sur le commit courant par construction, pas par
    /// négligence, et sans cette exclusion toute release s'auto-déclarerait sale.</para>
    /// </remarks>
    private static void StampGitSha()
    {
        const string assetPath = "Assets/Resources/build_sha.txt";

        string sha = Git("rev-parse --short HEAD");

        if (sha.Length == 0)
        {
            // Pas de dépôt, ou pas de git dans le PATH : « dev » dit franchement qu'on ne sait pas,
            // là où un SHA périmé prétendrait savoir.
            sha = "dev";
        }
        else if (HasLocalChanges())
        {
            sha += "+";
        }

        string full = Path.Combine(Directory.GetParent(Application.dataPath)!.FullName, "Assets/Resources/build_sha.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);

        bool isNew = !File.Exists(full);
        File.WriteAllText(full, sha);

        // Le fichier est ignoré par git : sur un clone frais, il n'existe pas encore et la base
        // d'assets ne le connaît donc pas — un ImportAsset seul ne suffirait pas à l'y faire entrer.
        if (isNew) AssetDatabase.Refresh();

        // Sans réimport, le build embarquerait la version que la base d'assets a en mémoire.
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

        Debug.Log($"[BUILD] identite git : {sha}");
    }

    /// <summary>Le dépôt porte-t-il des modifications autres que celles que la release pose elle-même ?</summary>
    /// <remarks>
    /// <c>build_sha.txt</c> est de surcroît ignoré par git — il n'apparaîtra donc pas dans le
    /// <c>status</c> — mais il reste listé ici : l'exclusion doit rester vraie si quelqu'un décide un
    /// jour de le reversionner, et une liste qui décrit une intention vaut mieux qu'une liste qui
    /// s'appuie sur un effet de bord du <c>.gitignore</c>.
    /// </remarks>
    private static bool HasLocalChanges()
    {
        string[] posedByRelease =
        {
            "unity/Assets/Resources/build_sha.txt",
            "unity/ProjectSettings/ProjectSettings.asset",
        };

        foreach (string line in Git("status --porcelain").Split('\n'))
        {
            string entry = line.Trim();
            if (entry.Length == 0) continue;

            // « XY chemin » — le statut tient sur les deux premières colonnes.
            string path = entry.Length > 2 ? entry.Substring(2).Trim().Replace('\\', '/') : "";

            bool ignored = false;
            foreach (string posed in posedByRelease)
                if (path.EndsWith(posed, StringComparison.Ordinal)) { ignored = true; break; }

            if (!ignored) return true;
        }

        return false;
    }

    /// <summary>Exécute une commande git à la racine du dépôt. Chaîne vide si git est indisponible.</summary>
    private static string Git(string args)
    {
        try
        {
            string unityDir = Directory.GetParent(Application.dataPath)!.FullName;
            string repoRoot = Directory.GetParent(unityDir)!.FullName;

            var psi = new System.Diagnostics.ProcessStartInfo("git", args)
            {
                WorkingDirectory = repoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return "";

            string output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit(10000);

            return proc.ExitCode == 0 ? output.Trim() : "";
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[BUILD] git indisponible ({e.Message}) — le tampon dira « dev ».");
            return "";
        }
    }

    private static void WriteBuildStamp(string outDir)
    {
        var sha = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/build_sha.txt");
        string shaText = sha != null && sha.text.Trim().Length > 0 ? sha.text.Trim() : "dev";

        string json =
            "{\n" +
            $"  \"version\": \"{PlayerSettings.bundleVersion}\",\n" +
            $"  \"sha\": \"{shaText}\",\n" +
            $"  \"date\": \"{DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}\",\n" +
            $"  \"engine\": \"{Application.unityVersion}\"\n" +
            "}\n";

        File.WriteAllText(Path.Combine(outDir, "build_stamp.json"), json);
        Debug.Log($"[BUILD] tampon : v{PlayerSettings.bundleVersion}-{shaText}");
    }

    /// <summary>
    /// Remplace le jeton <c>__BUILD_ID__</c> de la page hôte par un identifiant <b>unique à ce
    /// build</b>, qui devient un paramètre d'URL sur chaque fichier téléchargé.
    /// </summary>
    /// <remarks>
    /// <para>⚠⚠ <b>Sans cela, le navigateur mélange deux builds — et le symptôme est un crash
    /// illisible.</b> Les fichiers de sortie d'Unity portent toujours le même nom
    /// (<c>web.wasm.unityweb</c>, <c>web.data.unityweb</c>) : rien dans l'URL ne distingue un build
    /// du suivant. Le navigateur sert donc ce qu'il a en cache, et peut associer le <c>.data</c>
    /// d'un build au <c>.wasm</c> d'un autre. Le jeu ne dit pas « version périmée » : il rend
    /// <c>RuntimeError: memory access out of bounds</c> et trois cents offsets wasm, au démarrage,
    /// sans un seul nom de méthode.</para>
    ///
    /// <para>C'est arrivé pendant le portage tactile, et il a fallu deux rebuilds pour comprendre :
    /// le seul indice était que les offsets restaient <b>identiques</b> après une recompilation qui
    /// avait pourtant changé la taille du binaire de 700 Ko. Un message d'erreur qui ne change pas
    /// alors que le binaire a changé ne vient pas du binaire qu'on croit exécuter.</para>
    ///
    /// <para>L'horodatage s'ajoute au SHA parce qu'un build local n'a rien de commité : deux builds
    /// d'affilée partagent le même SHA et doivent quand même se distinguer. Le paramètre invalide
    /// aussi le cache <c>IndexedDB</c> d'Unity (<c>dataCaching</c>), qui indexe par URL.</para>
    /// </remarks>
    private static void StampWebCacheBuster(string outDir)
    {
        string indexPath = Path.Combine(outDir, "index.html");
        if (!File.Exists(indexPath))
        {
            Debug.LogWarning("[BUILD] index.html introuvable : pas de garde-cache posee.");
            return;
        }

        var sha = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/build_sha.txt");
        string shaText = sha != null && sha.text.Trim().Length > 0 ? sha.text.Trim() : "dev";
        string buildId = $"{shaText}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        string html = File.ReadAllText(indexPath);
        if (!html.Contains("__BUILD_ID__"))
        {
            // Le gabarit a été modifié sans que ce jeton y survive : le dire fort, sans quoi le
            // défaut ne se manifestera qu'au prochain rebuild, sous la forme d'un crash au démarrage.
            Debug.LogWarning("[BUILD] __BUILD_ID__ absent du gabarit : le navigateur pourra melanger " +
                             "deux builds (voir StampWebCacheBuster).");
            return;
        }

        File.WriteAllText(indexPath, html.Replace("__BUILD_ID__", buildId));
        Debug.Log($"[BUILD] garde-cache : {buildId}");
    }

    /// <summary>
    /// Pose l'icône du jeu sur l'exécutable et sur la fenêtre.
    /// </summary>
    /// <remarks>
    /// <para>Le portage sortait un binaire à l'icône <b>Unity</b> : dans la barre des tâches, dans
    /// l'explorateur et sur le raccourci du bureau, le jeu ne portait pas son nom mais celui du
    /// moteur qui l'a construit. C'est la première image que voit un joueur, avant même le menu.</para>
    ///
    /// <para><b>Posée au build et non à la main dans l'éditeur</b> : un réglage d'éditeur ne vaut que
    /// sur le poste où il a été fait, et se perd au premier clone du dépôt. Ici l'icône est un asset
    /// versionné, appliqué par le script qui produit le binaire.</para>
    ///
    /// <para>⚠ Unity attend un <c>Texture2D</c> lisible : sans <c>isReadable</c>, l'appel est
    /// silencieusement ignoré et le binaire garde l'icône du moteur — sans erreur ni avertissement.
    /// D'où le réglage d'import forcé ici même, plutôt qu'un <c>.meta</c> qu'on croirait suffisant.</para>
    /// </remarks>
    private static void ApplyGameIcon()
    {
        const string path = "Assets/Art/branding/icon.png";

        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogWarning($"[BUILD] icone introuvable ({path}) — le binaire gardera celle d'Unity.");
            return;
        }

        if (!importer.isReadable || importer.textureType != TextureImporterType.GUI)
        {
            importer.textureType = TextureImporterType.GUI;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (icon == null)
        {
            Debug.LogWarning($"[BUILD] icone illisible ({path}).");
            return;
        }

        // La même image pour toutes les tailles : Unity la redimensionne. Le sprite d'origine fait
        // 256 px, soit la plus grande taille demandée par Windows — aucun agrandissement, donc aucun
        // flou sur les grandes icônes.
        var target = NamedBuildTarget.Standalone;
        int[] sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);

        var icons = new Texture2D[sizes.Length];
        for (int i = 0; i < icons.Length; i++) icons[i] = icon;

        PlayerSettings.SetIcons(target, icons, IconKind.Any);

        Debug.Log($"[BUILD] icone posee sur {icons.Length} taille(s).");
    }

    /// <summary>Critère de sortie du Lot 2 : le cœur de run, vérifié headless.</summary>
    [MenuItem("Chimera/Build verification coeur de run (Mono)")]
    public static void Windows64RunSmoke()
        => Build<RunSmokeTest>(ScriptingImplementation.Mono2x, OutDirRunSmoke, "RunSmoke");

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
