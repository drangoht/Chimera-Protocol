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
        GameObject corePrefab   = BuildAetherCorePrefab();

        // Projectiles des armes à munition : sans eux, trois familles d'armes épuisent leur recharge
        // et ne tirent rien — silencieusement.
        BuildMissilePrefab();
        BuildGlaivePrefab();

        // Le tir ennemi : le boss et les archétypes à distance le chargent par son chemin Godot.
        BuildEnemyBulletPrefab();

        var champions = BuildChampionPrefabs();
        GameObject miniBossPrefab = BuildMiniBossPrefab();

        BuildScene(enemyPrefab, bulletPrefab, orbPrefab, corePrefab, champions, miniBossPrefab);
        BuildIntro();
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

    /// <summary>Dossier des sprites d'armes et de projectiles.</summary>
    private const string WeaponSprites = "Assets/Art/sprites/weapons";

    /// <summary>
    /// Projectile du joueur. Son sprite est <c>weapon_bullet_impulse</c> — celui du jeu d'origine.
    /// </summary>
    /// <remarks>
    /// ⚠ Les trois gabarits de projectile prenaient « le premier sprite trouvé » du dossier, qui se
    /// trouve être <c>enemy_bullet_sentinel_01</c> : <b>les tirs du joueur portaient l'apparence des
    /// tirs ennemis</b>, tous les trois le même sprite, et seule leur teinte les distinguait. Le
    /// défaut était latent depuis le début du portage et il est devenu grave le jour où les ennemis
    /// se sont mis à tirer pour de bon — deux camps, un seul projectile à l'écran.
    ///
    /// <para>Même famille que l'orbe d'XP déguisée en Noyau d'Aether : un chargement « premier
    /// trouvé » est juste le jour où on l'écrit et faux au deuxième asset du dossier.</para>
    /// </remarks>
    private static GameObject BuildBulletPrefab()
    {
        var go = new GameObject("Bullet", typeof(SpriteRenderer), typeof(Bullet));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = LoadSpriteNamed(WeaponSprites, "weapon_bullet_impulse");
        sr.color = new Color(0.267f, 1f, 0.933f);
        sr.sortingOrder = 20;
        return SaveAsPrefab(go, "Bullet");
    }

    /// <summary>
    /// Missile à tête chercheuse. <b>Aucun sprite ici</b> : il se dessine lui-même.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Ces deux projectiles construisent leur silhouette à l'exécution
    /// (<see cref="MissileSprite"/>, <see cref="GlaiveSprite"/>) parce qu'ils <b>tournent</b> : un
    /// sprite du dossier porterait un ombrage cuit, dont la lumière tournerait avec l'objet. Leur
    /// poser un sprite ici ne servirait donc à rien — sinon à laisser une référence <b>inerte et
    /// trompeuse à la lecture</b>, exactement ce qui a fait attribuer une barre droite au missile et
    /// une texture de test UV au glaive.</para>
    /// </remarks>
    private static GameObject BuildMissilePrefab()
    {
        var go = new GameObject("Missile", typeof(SpriteRenderer), typeof(SeekerMissile));
        go.GetComponent<SpriteRenderer>().sortingOrder = 20;
        return SaveAsPrefab(go, "Missile");
    }

    /// <summary>Glaive lancé. Aucun sprite ici non plus — voir <see cref="BuildMissilePrefab"/>.</summary>
    private static GameObject BuildGlaivePrefab()
    {
        var go = new GameObject("Glaive", typeof(SpriteRenderer), typeof(GlaiveProjectile));
        go.GetComponent<SpriteRenderer>().sortingOrder = 20;
        return SaveAsPrefab(go, "Glaive");
    }

    /// <summary>
    /// Projectile <b>ennemi</b>, avec son propre sprite (<c>enemy_bullet_sentinel_01</c>).
    /// </summary>
    /// <remarks>
    /// Il n'existait pas : le portage n'avait aucun tir ennemi, et ce sprite dormait dans le dépôt
    /// sans que rien ne le charge. Un gabarit plutôt qu'une forme construite à l'exécution, parce
    /// que seul <c>Resources/</c> est atteignable en jeu — un sprite n'y arrive qu'en étant
    /// référencé par un prefab qui s'y trouve.
    /// </remarks>
    private static GameObject BuildEnemyBulletPrefab()
    {
        var go = new GameObject("EnemyBullet", typeof(SpriteRenderer), typeof(EnemyBullet));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = LoadSpriteNamed(WeaponSprites, "enemy_bullet_sentinel_01");
        sr.sortingOrder = 20;
        return SaveAsPrefab(go, "EnemyBullet");
    }

    /// <summary>
    /// Prefabs des champions qui ont une <b>classe dédiée</b> : les trois mid-boss de biome et le
    /// boss de fin. Chacun porte sa mécanique propre — le boss ne se réduit pas à un champion plus
    /// gros, et un champion de biome demande le réflexe inverse de l'incarnation finale du sien.
    /// </summary>
    private static EnemySpawner.NamedPrefab[] BuildChampionPrefabs()
    {
        var molten = SaveAsPrefab(NewChampion("MoltenColossus", typeof(MoltenColossus)), "MoltenColossus");
        var cryo   = SaveAsPrefab(NewChampion("CryoSentinel",   typeof(CryoSentinel)),   "CryoSentinel");
        var neon   = SaveAsPrefab(NewChampion("NeonWarden",     typeof(NeonWarden)),     "NeonWarden");

        // Le boss est rendu bien plus grand que tout le reste : faune 32 px · mini-boss globaux 64 ·
        // champions de biome 72 · boss 154.
        var bossGo = NewChampion("RustedCore", typeof(RustedCore));
        bossGo.transform.localScale = new Vector3(2.4f, 2.4f, 1f);
        var boss = SaveAsPrefab(bossGo, "RustedCore");

        return new[]
        {
            new EnemySpawner.NamedPrefab { Id = "molten_colossus", Prefab = molten },
            new EnemySpawner.NamedPrefab { Id = "cryo_sentinel",   Prefab = cryo   },
            new EnemySpawner.NamedPrefab { Id = "neon_warden",     Prefab = neon   },
            new EnemySpawner.NamedPrefab { Id = EnemySpawner.BossId, Prefab = boss },
        };
    }

    /// <summary>
    /// Repli des champions <b>sans</b> classe dédiée (les trois mini-boss globaux, non encore
    /// portés). Ils gardent ainsi leurs PV, leur taille et leur rayon de contact de champion, à
    /// défaut de leur mécanique propre — ce qui vaut mieux que de les voir passer pour de la faune.
    /// </summary>
    private static GameObject BuildMiniBossPrefab()
        => SaveAsPrefab(NewChampion("MiniBoss", typeof(MiniBoss)), "MiniBoss");

    private static GameObject NewChampion(string name, System.Type type)
    {
        var go = new GameObject(name, typeof(SpriteRenderer), typeof(FrameAnimator), type);
        go.GetComponent<SpriteRenderer>().sortingOrder = 12;   // au-dessus de la faune
        go.transform.localScale = new Vector3(1.5f, 1.5f, 1f); // champions de biome : 72 px
        return go;
    }

    /// <summary>
    /// Gabarit du Noyau d'Aether. Il porte son PROPRE sprite (<c>pickup_noyau_*</c>) et non le
    /// premier venu du dossier : un Noyau qui ressemble à une orbe d'XP perd la seule chose qui le
    /// distingue — il ne s'aspire pas, il faut aller le chercher.
    /// </summary>
    private static GameObject BuildAetherCorePrefab()
    {
        var go = new GameObject("AetherCore", typeof(SpriteRenderer), typeof(AetherCore));

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = LoadSpriteNamed("Assets/Art/sprites/pickups", "pickup_noyau_idle_01");
        sr.color = new Color(0.667f, 0.267f, 1f);   // #AA44FF, la couleur d'Aether
        sr.sortingOrder = 6;                        // au-dessus des orbes d'XP (5)

        return SaveAsPrefab(go, "AetherCore");
    }

    /// <summary>
    /// Gabarit de l'orbe d'XP. Son sprite est <b>nommé</b>, comme celui du Noyau.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Il prenait « le premier sprite trouvé » du dossier — qui se trouve être
    /// <c>pickup_noyau_idle_01</c>. Tous les orbes d'XP portaient donc l'apparence d'un <b>Noyau
    /// d'Aether</b>, et les trois <c>pickup_xporb_*</c> ne servaient à personne. Signalé en jouant
    /// le 2026-08-09 : « je ne vois pas le nombre de noyaux évoluer… ça incrémente de 1 après en
    /// avoir ramassé un certain nombre ». Le compteur était juste depuis le début — c'est ce que le
    /// joueur croyait ramasser qui ne l'était pas.</para>
    ///
    /// <para>Le Noyau est le <b>seul</b> objet du jeu qui demande de traverser volontairement le
    /// danger : il ne s'aspire pas, il faut aller le chercher. Le confondre avec l'orbe qui vient
    /// tout seul supprime cette décision — et fait passer un compteur exact pour un compteur cassé.</para>
    /// </remarks>
    private static GameObject BuildXpOrbPrefab()
    {
        var go = new GameObject("XpOrb", typeof(SpriteRenderer), typeof(XpOrb));
        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = LoadSpriteNamed("Assets/Art/sprites/pickups", "pickup_xporb_idle_01");
        sr.sortingOrder = 5;
        return SaveAsPrefab(go, "XpOrb");
    }

    /// <summary>
    /// Premier sprite trouvé sous un dossier — suffisant pour un cœur de run. Le choix précis des
    /// visuels appartient aux lots suivants ; ce qui compte ici est que la chaîne d'assets tienne.
    /// </summary>
    /// <summary>Sprite d'un nom précis sous un dossier, avec repli sur le premier trouvé.</summary>
    private static Sprite? LoadSpriteNamed(string folder, string name)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Sprite", new[] { folder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        Debug.LogWarning($"[SCENE] sprite '{name}' introuvable sous {folder} — repli sur le premier.");
        return LoadSprite(folder);
    }

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

    /// <summary>
    /// Scène de la cinématique d'ouverture — <b>première scène du build</b>, donc celle qui se
    /// charge au lancement.
    /// </summary>
    private static void BuildIntro()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // ⚠ L'AudioListener n'est pas un détail de caméra : sans lui, la piste d'intro se charge,
        // joue, et le jeu s'ouvre en silence — sans la moindre erreur.
        var camGo = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 360f;   // 720 px de haut, la résolution logique du jeu d'origine
        cam.backgroundColor = new Color(0.06f, 0.06f, 0.11f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.tag = "MainCamera";

        // ⚠ La caméra RECULE en Z. Laissée à l'origine, elle se retrouve dans le même plan que les
        // sprites de la cinématique : ceux-ci tombent derrière son plan de coupe et l'écran reste
        // noir — avec le texte, qui vit dans le canevas, parfaitement visible par-dessus. Le
        // symptôme se lit alors comme « les sprites ne se chargent pas », alors qu'ils sont là.
        camGo.transform.position = new Vector3(0f, 0f, -10f);

        var introGo = new GameObject("Intro", typeof(IntroScreen), typeof(MusicDirector));

        // Sans EventSystem, un clic ne parvient à personne — et l'intro se passe aussi au clic.
        var eventSystem = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        foreach (var go in new[] { camGo, introGo, eventSystem })
            EditorSceneManager.MoveGameObjectToScene(go, scene);

        EditorSceneManager.SaveScene(scene, GameScenes.PathOf(GameScenes.Intro));
        Debug.Log("[SCENE] intro ecrite : " + GameScenes.PathOf(GameScenes.Intro));
    }

    /// <summary>Scène du menu principal.</summary>
    private static void BuildMainMenu()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("MainCamera", typeof(Camera), typeof(AudioListener));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 540f;
        cam.backgroundColor = new Color(0.102f, 0.102f, 0.180f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.tag = "MainCamera";

        var menuGo = new GameObject("MainMenu", typeof(MainMenuScreen), typeof(MusicDirector));

        // ⚠ Les outils vivent sur LEUR PROPRE objet, jamais sur celui du menu : tous deux survivent
        // au changement de scène (DontDestroyOnLoad), et posés sur le menu ils le feraient survivre
        // avec eux — le menu resterait alors affiché PAR-DESSUS la partie.
        var toolsGo = new GameObject("[Outils]", typeof(SceneDiagnostic), typeof(ScreenshotTour));

        // Un EventSystem est INDISPENSABLE : sans lui, aucun bouton ne reçoit de clic ni de focus,
        // et le menu paraît simplement inerte — sans la moindre erreur.
        var eventSystem = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        foreach (var go in new[] { camGo, menuGo, toolsGo, eventSystem })
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

    private static void BuildScene(GameObject enemyPrefab, GameObject bulletPrefab, GameObject orbPrefab,
                                   GameObject corePrefab,
                                   EnemySpawner.NamedPrefab[] champions, GameObject miniBossPrefab)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Caméra orthographique en unités = pixels (PPU 1) : une demi-hauteur de 540 donne
        // exactement 1080 pixels de haut, comme la fenêtre de référence du jeu.
        //
        // ⚠ L'AudioListener n'est PAS un détail de caméra : sans lui, Unity ne restitue AUCUN son —
        // les clips se chargent, les AudioSource jouent, les compteurs montent, et le jeu reste
        // totalement muet. Aucune erreur n'est levée.
        var camGo = new GameObject("MainCamera",
            typeof(Camera), typeof(AudioListener), typeof(RunCamera));
        var cam = camGo.GetComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = 540f;   // RunCamera l'ajuste à la hauteur d'écran (1 px = 1 unité)
        cam.backgroundColor = new Color(0.102f, 0.102f, 0.180f);
        cam.clearFlags = CameraClearFlags.SolidColor;
        camGo.transform.position = new Vector3(0f, 0f, -10f);
        camGo.tag = "MainCamera";

        var systems = new GameObject("[Systems]",
            typeof(XpSystem), typeof(GameManager), typeof(InventorySystem),
            typeof(HUD), typeof(RunHud), typeof(MusicDirector));

        var playerGo = new GameObject("Player",
            typeof(SpriteRenderer), typeof(FrameAnimator), typeof(Player), typeof(ImpulseCannon));
        playerGo.GetComponent<SpriteRenderer>().sortingOrder = 15;

        var cannon = playerGo.GetComponent<ImpulseCannon>();
        cannon.BaseDamage = 10f;
        cannon.BaseCooldown = 0.5f;
        cannon.Range = 400f;
        cannon.BulletPrefab = bulletPrefab;

        // Le sol et les limites de l'arène : sans eux, la zone de jeu est un vide plat où rien ne
        // donne l'échelle ni le sens du déplacement.
        var arenaGo = new GameObject("Arene", typeof(ArenaRenderer));

        var spawnerGo = new GameObject("EnemySpawner", typeof(EnemySpawner));
        var spawner = spawnerGo.GetComponent<EnemySpawner>();
        spawner.EnemyPrefab = enemyPrefab;
        spawner.XpOrbPrefab = orbPrefab;
        spawner.ChampionPrefabs = champions;
        spawner.MiniBossPrefab = miniBossPrefab;

        // Les Noyaux d'Aether ont leur propre cadence — 45 s, indépendante des vagues : c'est une
        // monnaie de méta-progression, pas une récompense de combat.
        var coreSpawnerGo = new GameObject("AetherCoreSpawner", typeof(AetherCoreSpawner));
        coreSpawnerGo.GetComponent<AetherCoreSpawner>().CorePrefab = corePrefab;

        var bootGo = new GameObject("RunBootstrap", typeof(RunBootstrap));

        // Sans EventSystem, les ecrans modaux ne recoivent ni clic ni focus manette.
        var eventSystem = new GameObject("EventSystem",
            typeof(UnityEngine.EventSystems.EventSystem),
            typeof(UnityEngine.EventSystems.StandaloneInputModule));

        foreach (var go in new[] { camGo, systems, arenaGo, playerGo, spawnerGo, coreSpawnerGo,
                                   bootGo, eventSystem })
            EditorSceneManager.MoveGameObjectToScene(go, scene);

        EditorSceneManager.SaveScene(scene, GameScenes.PathOf(GameScenes.Game));
        Debug.Log("[SCENE] scene de jeu ecrite : " + GameScenes.PathOf(GameScenes.Game));
    }
}
