using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Génère les prefabs de jeu et la scène de run (Lot 2).
///
/// <para><b>Pourquoi générer plutôt que d'authorer à la main.</b> Ces objets sont la traduction
/// directe de <c>.tscn</c> Godot existants. Les reconstruire à la souris serait long, non
/// reproductible, et impossible à relire en diff — alors qu'un script dit <b>exactement</b> ce que
/// contient chaque prefab et se rejoue à l'identique. Quand l'UI et les VFX arriveront (lots 3 à 5),
/// l'édition manuelle reprendra ses droits ; pour un cœur de run, le code est plus sûr.</para>
///
/// <para>Usage : <c>-executeMethod BuildGameScene.Run</c></para>
/// </summary>
public static class BuildGameScene
{
    private const string PrefabDir = "Assets/Resources/Prefabs/entities";

    [MenuItem("Chimera/Construire la scene de jeu")]
    public static void Run()
    {
        string root = Directory.GetParent(Application.dataPath)!.FullName;
        Directory.CreateDirectory(Path.Combine(root, PrefabDir));
        Directory.CreateDirectory(Path.Combine(root, "Assets/Scenes"));

        GameObject enemyPrefab  = BuildEnemyPrefab();
        GameObject bulletPrefab = BuildBulletPrefab();
        GameObject orbPrefab    = BuildXpOrbPrefab();

        BuildScene(enemyPrefab, bulletPrefab, orbPrefab);
        BuildMainMenu();
        RegisterScenes();

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[SCENE] prefabs et scene de jeu generes.");
    }

    // ─── Prefabs ──────────────────────────────────────────────────────────────

    private static GameObject BuildEnemyPrefab()
    {
        var go = new GameObject("Enemy", typeof(SpriteRenderer), typeof(FrameAnimator), typeof(EnemyBase));

        var enemy = go.GetComponent<EnemyBase>();
        enemy.MaxHp = 20f;
        enemy.Speed = 120f;
        enemy.Damage = 5f;
        enemy.XpValue = 1;

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sortingOrder = 10;

        return SaveAsPrefab(go, "Enemy");
    }

    private static GameObject BuildBulletPrefab()
    {
        var go = new GameObject("Bullet", typeof(SpriteRenderer), typeof(Bullet));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = LoadSprite("Assets/Art/sprites/weapons");
        sr.color = new Color(0.267f, 1f, 0.933f);
        sr.sortingOrder = 20;
        return SaveAsPrefab(go, "Bullet");
    }

    private static GameObject BuildXpOrbPrefab()
    {
        var go = new GameObject("XpOrb", typeof(SpriteRenderer), typeof(XpOrb));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = LoadSprite("Assets/Art/sprites/pickups");
        sr.sortingOrder = 5;
        return SaveAsPrefab(go, "XpOrb");
    }

    /// <summary>
    /// Premier sprite trouvé sous un dossier — suffisant pour un cœur de run. Le choix précis des
    /// visuels appartient aux lots suivants ; ce qui compte ici est que la chaîne d'assets tienne.
    /// </summary>
    private static Sprite? LoadSprite(string folder)
    {
        string[] guids = AssetDatabase.FindAssets("t:Sprite", new[] { folder });
        if (guids.Length == 0) return null;

        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static GameObject SaveAsPrefab(GameObject instance, string name)
    {
        string path = $"{PrefabDir}/{name}.prefab";
        var prefab = PrefabUtility.SaveAsPrefabAsset(instance, path);
        Object.DestroyImmediate(instance);
        return prefab;
    }

    /// <summary>Scène du menu principal — point d'entrée du jeu.</summary>
    private static void BuildMainMenu()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("MainCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 540f;
        cam.backgroundColor = new Color(0.102f, 0.102f, 0.180f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.tag = "MainCamera";

        var menuGo = new GameObject("MainMenu", typeof(MainMenuScreen));

        // Un EventSystem est INDISPENSABLE : sans lui, aucun bouton ne reçoit de clic ni de focus,
        // et le menu paraît simplement inerte — sans la moindre erreur.
        var eventSystem = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        foreach (var go in new[] { camGo, menuGo, eventSystem })
            EditorSceneManager.MoveGameObjectToScene(go, scene);

        EditorSceneManager.SaveScene(scene, GameScenes.PathOf(GameScenes.MainMenu));
        Debug.Log("[SCENE] menu principal ecrit : " + GameScenes.PathOf(GameScenes.MainMenu));
    }

    /// <summary>
    /// Déclare les scènes dans les réglages de build, dans l'ordre de <see cref="GameScenes.All"/>.
    /// Une scène absente d'ici ne peut pas être chargée à l'exécution : le symptôme est un écran
    /// noir sans message.
    /// </summary>
    private static void RegisterScenes()
    {
        var list = new EditorBuildSettingsScene[GameScenes.All.Length];
        for (int i = 0; i < GameScenes.All.Length; i++)
            list[i] = new EditorBuildSettingsScene(GameScenes.PathOf(GameScenes.All[i]), true);

        EditorBuildSettings.scenes = list;
        Debug.Log($"[SCENE] {list.Length} scenes declarees au build.");
    }

    // ─── Scène ────────────────────────────────────────────────────────────────

    private static void BuildScene(GameObject enemyPrefab, GameObject bulletPrefab, GameObject orbPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Caméra orthographique en unités = pixels (PPU 1) : une demi-hauteur de 540 donne
        // exactement 1080 pixels de haut, comme la fenêtre de référence du jeu.
        var camGo = new GameObject("MainCamera", typeof(Camera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 540f;
        cam.backgroundColor = new Color(0.102f, 0.102f, 0.180f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.tag = "MainCamera";

        var systems = new GameObject("[Systems]",
            typeof(XpSystem), typeof(GameManager), typeof(InventorySystem),
            typeof(HUD), typeof(RunHud));

        var playerGo = new GameObject("Player", typeof(SpriteRenderer), typeof(Player), typeof(ImpulseCannon));
        playerGo.GetComponent<SpriteRenderer>().sortingOrder = 15;

        var cannon = playerGo.GetComponent<ImpulseCannon>();
        cannon.BaseDamage = 10f;
        cannon.BaseCooldown = 0.5f;
        cannon.Range = 400f;
        cannon.BulletPrefab = bulletPrefab;

        var spawnerGo = new GameObject("EnemySpawner", typeof(EnemySpawner));
        var spawner = spawnerGo.GetComponent<EnemySpawner>();
        spawner.EnemyPrefab = enemyPrefab;
        spawner.XpOrbPrefab = orbPrefab;

        var bootGo = new GameObject("RunBootstrap", typeof(RunBootstrap));

        // Sans EventSystem, les ecrans modaux ne recoivent ni clic ni focus manette.
        var eventSystem = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        foreach (var go in new[] { camGo, systems, playerGo, spawnerGo, bootGo, eventSystem })
            EditorSceneManager.MoveGameObjectToScene(go, scene);

        EditorSceneManager.SaveScene(scene, GameScenes.PathOf(GameScenes.Game));
        Debug.Log("[SCENE] scene de jeu ecrite : " + GameScenes.PathOf(GameScenes.Game));
    }
}
