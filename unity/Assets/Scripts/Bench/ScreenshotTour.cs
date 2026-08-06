using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Capture chaque écran du jeu, en conditions réelles (<c>--screenshots</c>).
///
/// <para><b>Pourquoi cet outil existe.</b> Toutes les vérifications d'interface écrites jusqu'ici
/// répondent à « l'écran s'ouvre-t-il ? » et « la ligne existe-t-elle ? » — aucune ne répond à
/// « <b>à quoi ça ressemble ?</b> ». Un cadre étiré cent fois trop grand, une teinte appliquée deux
/// fois, un texte illisible : rien de tout cela ne fait échouer un test, et tout cela se voit en une
/// seconde sur une image. Juger une interface sans la regarder ne marche pas.</para>
///
/// <para>⚠ Il tourne <b>avec un rendu</b> : lancé en <c>-batchmode -nographics</c>, il produirait des
/// images noires. C'est le seul outil du dépôt qui exige une vraie fenêtre.</para>
/// </summary>
public sealed class ScreenshotTour : MonoBehaviour
{
    private string _dir = "screenshots";
    private int _index;

    private void Start()
    {
        bool wanted = false;

        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "--screenshots") wanted = true;
            else if (arg.StartsWith("--screenshots=", System.StringComparison.Ordinal))
            {
                wanted = true;
                _dir = arg.Substring("--screenshots=".Length);
            }
        }

        if (!wanted) { Destroy(this); return; }

        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        Directory.CreateDirectory(_dir);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Le menu se construit dans le Start de sa propre scène : lui laisser une frame.
        yield return new WaitForSecondsRealtime(1.5f);
        yield return Shot("menu");

        // Les écrans sont montés ici plutôt que pilotés par des clics : on capture l'habillage, pas
        // la navigation — et un clic simulé dépend d'un EventSystem et d'une position de souris.
        var host = new GameObject("ShotHost");

        var hub = host.AddComponent<HubScreen>();
        yield return null;
        hub.Show();
        yield return Shot("hub");
        hub.Hide();

        var levels = host.AddComponent<LevelSelectScreen>();
        yield return null;
        levels.Show();
        yield return Shot("niveaux");
        levels.Hide();

        var options = host.AddComponent<OptionsScreen>();
        yield return null;
        options.Show();
        yield return Shot("options");
        options.Hide();

        var codex = host.AddComponent<CodexScreen>();
        yield return null;
        codex.Show();
        yield return Shot("codex");
        codex.Hide();

        var challenges = host.AddComponent<ChallengeScreen>();
        yield return null;
        challenges.Show();
        yield return Shot("defis");
        challenges.Hide();

        Destroy(host);

        // Puis la run elle-même : HUD, arène, ennemis — le seul écran qu'on voit vraiment longtemps.
        SceneRoot.ChangeScene(GameScenes.Game);
        yield return new WaitForSecondsRealtime(6f);
        yield return Shot("run");

        // Puis le COMBAT. Sans cette nuée posée à la main, la tournée photographie une première
        // minute de run : le joueur y est seul au centre d'une arène vide, et aucune arme de zone,
        // de mêlée ou de chaîne n'a de cible à portée. Les captures montraient donc « rien ne
        // s'affiche » d'un arsenal qui s'affichait très bien — l'image ne mentait pas, elle
        // répondait à une autre question que celle qu'on lui posait.
        // Trois clichés, chacun juste après avoir posé sa nuée. Un seul ne suffirait pas : les effets
        // d'arme durent 0,2 s, donc une capture unique tombe presque toujours entre deux tirs — même
        // piège que le compteur qui monte sans qu'aucun son ne sorte.
        //
        // Ils s'enchaînent VITE, et c'est délibéré : les éliminations remplissent les jauges, et la
        // montée de niveau comme l'Assimilation ouvrent une modale qui recouvre exactement ce qu'on
        // photographie. Une fenêtre courte reste devant cette progression.
        SpawnSwarmAroundPlayer(24);

        for (int i = 2; i <= 4; i++)
        {
            yield return new WaitForSecondsRealtime(0.35f);
            yield return Shot($"run-{i}");
            SpawnSwarmAroundPlayer(10);
        }

        yield return ShootStatusEffects();
        yield return ShootChimera();

        var levelUp = FindFirstObjectByType<LevelUpScreen>();
        if (levelUp != null)
        {
            levelUp.Present(LevelUpPool.BuildOverload());
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("montee-de-niveau");
        }

        Debug.Log($"[SHOTS] {_index} captures ecrites dans {Path.GetFullPath(_dir)}");
        Application.Quit(0);
    }

    /// <summary>
    /// Photographie le <b>gel</b> et la <b>brûlure</b> — les deux états portés par les ennemis.
    /// </summary>
    /// <remarks>
    /// <para>Ils ne se capturent pas par hasard : il faut l'arme qui les inflige, une cible dans son
    /// cône <i>et</i> une cible qui y survive, alors qu'un ennemi de base meurt en une seconde. Un
    /// premier essai a produit exactement ce à quoi il fallait s'attendre — des images de combat où
    /// aucun ennemi ne brûle. Ici, les états sont posés <b>à la main</b> sur une nuée endurcie : on
    /// photographie l'apparence, pas la chaîne qui la déclenche (le banc s'en charge).</para>
    ///
    /// <para>C'est cette image qui répond à la seule question qui compte pour un effet d'état :
    /// <b>tient-il dans la silhouette de sa victime</b>, ou la recouvre-t-il ?</para>
    /// </remarks>
    private IEnumerator ShootStatusEffects()
    {
        SpawnSwarmAroundPlayer(14);

        // ⚠ Réappliqué à CHAQUE image, et pas une fois pour toutes : le spawner continue de peupler
        // l'arène pendant la pose, et la première version photographiait surtout des arrivants
        // intacts — quatre ennemis sans la moindre flamme au premier plan, ce qui se lit « l'effet
        // ne marche pas ».
        //
        // Le gel est volontairement ABSENT de cette image. Sa traînée sème des éclats de 22 px qui
        // recouvrent exactement ce qu'on vient regarder ; deux états sur la même silhouette ne se
        // jugent ni l'un ni l'autre. Il a sa propre pose, juste après (ShootFrost).
        // ⚠ La coroutine tourne PENDANT les deux clichés, pas seulement avant : le spawner ajoute des
        // ennemis en permanence, et une application qui s'arrête à la pose laisse au premier plan
        // des arrivants intacts — ce que la capture précédente montrait, et qui se lit « l'effet ne
        // marche pas » alors qu'il marche sur les autres.
        StartCoroutine(KeepBurning(4f));
        yield return new WaitForSecondsRealtime(1.4f);

        // Le relevé accompagne l'image : une flamme jugée « trop grosse » sur une capture ne dit pas
        // si c'est le dosage qui est mauvais ou la MESURE du corps qui a échoué et rendu son repli.
        int total = 0, withFx = 0, flaming = 0;
        string first = "aucun";

        foreach (var enemy in EnemyBase.Active.ToArray())
        {
            if (enemy == null) continue;
            total++;

            var fx = enemy.GetComponent<EnemyStatusFx>();
            if (fx == null) continue;

            withFx++;
            if (fx.FlamesVisible) flaming++;

            if (first == "aucun")
                first = $"corps {fx.BodyWidthPx:F0} px " +
                        $"({(fx.BodyMeasured ? "mesure" : "REPLI")}), flammes {fx.FlameSpanPx:F0} px";
        }

        // ⚠ Compter AVANT de regarder l'image : « les ennemis du fond n'ont pas de flammes » peut
        // vouloir dire que l'effet est invisible, ou simplement que ces ennemis-là ne brûlent pas.
        // Sans ce relevé, on doserait un effet qui n'a jamais été appliqué.
        Debug.Log($"[SHOTS] brulure : {flaming}/{withFx} en flammes sur {total} ennemis — {first}");

        // Deux clichés espacés : les langues de feu montent et les bouffées dérivent, un seul
        // instantané ne dirait rien de leur course.
        yield return Shot("etats-brulure");

        yield return new WaitForSecondsRealtime(0.8f);
        yield return Shot("etats-brulure-2");

        yield return ShootFrost();
    }

    /// <summary>
    /// Photographie le <b>gel</b>, séparément de la brûlure.
    /// </summary>
    /// <remarks>
    /// <para>Les deux états ne se jugent pas sur la même image : la traînée de givre sème des éclats
    /// qui recouvrent exactement les langues de feu qu'on vient regarder. Une capture par état, donc,
    /// et pour le gel il en faut <b>deux</b> — les cristaux scintillent et la vapeur retombe, un seul
    /// instantané ne dirait rien de leur vie.</para>
    ///
    /// <para>Le ralentissement lui-même, lui, <b>ne se photographie pas</b> : une image ne montre ni
    /// une vitesse ni une cadence d'animation. Le relevé joint dit ce que le cliché tait.</para>
    /// </remarks>
    private IEnumerator ShootFrost()
    {
        // ⚠ Purge d'abord : les cibles de la pose précédente brûlent encore (12 s de poison), et
        // deux états portés par la même silhouette ne se jugent ni l'un ni l'autre. Elles sont
        // détruites et non tuées — une mort déclencherait gerbes, orbes et compteurs, qui n'ont rien
        // à faire dans le cliché suivant.
        foreach (var enemy in EnemyBase.Active.ToArray())
            if (enemy != null) Destroy(enemy.gameObject);

        yield return null;

        SpawnSwarmAroundPlayer(14);

        // Même précaution que pour la brûlure : réappliqué à CHAQUE image, sans quoi la pose
        // photographie surtout les arrivants intacts que le spawner ajoute pendant ce temps.
        StartCoroutine(KeepFrozen(4f));
        yield return new WaitForSecondsRealtime(1.4f);

        // ⚠ Le relevé compte, il ne cite pas. Une première version décrivait le PREMIER ennemi venu
        // et annonçait « corps 48 px (REPLI) » — vrai pour lui seul, et impossible à distinguer d'un
        // échec de mesure généralisé. Ce qu'il faut savoir d'un effet dimensionné sur le corps, c'est
        // combien de corps ont pu être mesurés, et sur quelle plage de tailles il s'applique.
        int total = 0, withFx = 0, frozen = 0, measured = 0;
        float minBody = float.MaxValue, maxBody = 0f, maxSpan = 0f, level = 0f, cadence = 1f;

        foreach (var enemy in EnemyBase.Active.ToArray())
        {
            if (enemy == null) continue;
            total++;

            var fx = enemy.GetComponent<EnemyStatusFx>();
            if (fx == null) continue;

            withFx++;
            if (fx.ShardsVisible) frozen++;
            if (fx.BodyMeasured) measured++;

            minBody = Mathf.Min(minBody, fx.BodyWidthPx);
            maxBody = Mathf.Max(maxBody, fx.BodyWidthPx);
            maxSpan = Mathf.Max(maxSpan, fx.ShardSpanPx);
            level = fx.FrostLevel;
            cadence = fx.CadenceScale;
        }

        Debug.Log($"[SHOTS] gel : {frozen}/{withFx} givres sur {total} ennemis, " +
                  $"{measured}/{withFx} corps mesures ({minBody:F0}-{maxBody:F0} px) — " +
                  $"givre {maxSpan:F0} px au plus large, intensite {level:F2}, cadence x{cadence:F2}");

        yield return Shot("etats-gel");

        yield return new WaitForSecondsRealtime(0.8f);
        yield return Shot("etats-gel-2");

        yield return ShootFusions();
    }

    /// <summary>
    /// Photographie les <b>fusions à effet permanent</b> et la Ruche de Tourelles.
    /// </summary>
    /// <remarks>
    /// <para>Aucune ne peut apparaître dans une tournée ordinaire : une fusion se gagne en montant
    /// deux armes au niveau requis, une greffe demande de remplir une jauge entière. Elles sont donc
    /// <b>accordées à la main</b> — on photographie une allure, pas la chaîne qui y mène (le banc
    /// s'en charge).</para>
    ///
    /// <para>Ces trois-là ont en commun d'avoir été signalées <b>invisibles ou illisibles</b> en
    /// jouant : le voile de givre réduit à un cercle, la lame de fusion à un croissant tournoyant,
    /// les tourelles à rien du tout. C'est exactement ce qu'une image tranche et qu'aucun test ne
    /// voit.</para>
    /// </remarks>
    private IEnumerator ShootFusions()
    {
        foreach (var enemy in EnemyBase.Active.ToArray())
            if (enemy != null) Destroy(enemy.gameObject);

        yield return null;

        var inv = InventorySystem.Instance;
        var player = Player.Instance;
        if (inv == null || player == null) yield break;

        // ⚠ Une fusion ne s'ACQUIERT pas : elle se FORGE depuis son arme source montée au niveau
        // requis. Un premier essai appelait `AcquireOrLevelUp("frost_veil")`, qui ne connaît pas cet
        // identifiant et ne fait donc rien — le relevé disait « armes impulse_cannon » et la capture
        // montrait une run ordinaire. Sans le relevé, on aurait jugé des effets jamais équipés.
        foreach (var (source, passive, fusion) in new[]
                 {
                     ("cryo_lance",     "reinforced_plating", "frost_veil"),
                     ("plasma_blade",   "thermal_core",       "fusion_blade"),
                     ("impulse_cannon", "capacitor",          "rail_overcharged"),
                 })
        {
            // Le passif requis compte autant que le niveau d'arme : sans lui, `CanFuse` refuse en
            // silence et rend 0 — la fusion ne se forge pas, et rien ne le dit.
            for (int i = 0; i < 5; i++) inv.AcquireOrLevelUp(source);
            inv.AddOrUpgradePassive(passive);
            inv.ApplyFusion(fusion);
        }

        // Et la ruche, par la porte de derrière : la greffe de fusion ne s'obtient pas autrement.
        var manager = player.GetComponent<GraftManager>();
        var hive = Assimilation.Config.Fusions.Find(f => f.HasEffect("turrets"));
        if (manager != null && hive != null) manager.Apply(hive);

        // Et l'esquive, sans quoi sa jauge de recharge reste masquée — or c'est précisément elle
        // qu'on vient photographier. Une capture d'un HUD dont la moitié des lignes ne peut pas
        // s'afficher ne prouve rien de ces lignes-là.
        var dashGraft = Assimilation.Config.Grafts.Find(g => g.HasEffect("dash"));
        if (manager != null && dashGraft != null) manager.Apply(dashGraft);

        SpawnSwarmAroundPlayer(16);
        yield return new WaitForSecondsRealtime(1.6f);

        // ⚠ Le relevé dit si les AURAS EXISTENT. Sans lui, « je ne vois pas le nuage » ne distingue
        // pas « il est trop pâle » de « il n'a jamais été créé » — deux causes opposées, et la
        // première seule se corrige en montant l'opacité.
        int turrets = manager != null ? manager.Hive.Count : 0;
        var veil = FindFirstObjectByType<FrostVeil>();
        var blade = FindFirstObjectByType<FusionBlade>();

        Debug.Log($"[SHOTS] fusions : {turrets} tourelle(s), armes " +
                  string.Join(", ", inv.WeaponLevels.Keys) +
                  $" — nuage de givre {Describe(veil != null ? veil.Cloud : null)}" +
                  $", champ irradiant {Describe(blade != null ? blade.Field : null)}");

        yield return Shot("fusions");

        yield return new WaitForSecondsRealtime(0.9f);
        yield return Shot("fusions-2");
    }

    private static string Describe(AuraCloud? cloud)
        => cloud == null ? "ABSENT" : $"{cloud.PuffCount} bouffees sur {cloud.RadiusPx:F0} px";

    /// <summary>
    /// Photographie les quatre changements signalés en jouant : le souffle du Flux de Braise
    /// <b>sans son contour</b>, la couronne de la Colonne Solaire, la Singularité qui <b>tord le
    /// décor</b>, et les greffes portées <b>sur le corps</b> du joueur.
    /// </summary>
    /// <remarks>
    /// <para>⚠ L'arsenal est <b>vidé</b> avant chaque cliché. Les passes précédentes ont accumulé
    /// trois fusions sur le porteur : photographier une arme au milieu d'elles ne dirait rien de
    /// cette arme-là — c'est la version « armes » du piège de cumul des effets additifs.</para>
    ///
    /// <para>⚠ Chaque cliché est accompagné d'un <b>relevé compté</b>. « Je ne vois pas la fumée » ne
    /// distingue pas <i>trop pâle</i> de <i>jamais émise</i>, et « le décor ne bouge pas » ne
    /// distingue pas <i>trop faible</i> de <i>aucun pilier enregistré</i>.</para>
    /// </remarks>
    private IEnumerator ShootChimera()
    {
        var inv = InventorySystem.Instance;
        var player = Player.Instance;
        if (inv == null || player == null) yield break;

        // ─── Le souffle, sans contour ─────────────────────────────────────────
        inv.ResetForRun();
        for (int i = 0; i < 5; i++) inv.AcquireOrLevelUp("pyre_stream");

        SpawnSwarmAroundPlayer(14);
        yield return Play(1.2f);

        var pyre = FindFirstObjectByType<PyreStream>();
        Debug.Log($"[SHOTS] braise : {(pyre != null ? pyre.LastConeHits : -1)} cible(s) au dernier " +
                  $"souffle, {Vfx.ActiveEffects} effets a l'image");

        yield return Shot("braise");

        // ─── L'éruption solaire ───────────────────────────────────────────────
        // ⚠ Elle se FORGE : le passif requis compte autant que le niveau d'arme, et sans lui
        // `ApplyFusion` refuse en silence — on photographierait le Flux de Braise en croyant tenir
        // la Colonne Solaire.
        inv.AddOrUpgradePassive("thermal_core");
        inv.ApplyFusion("solar_column");

        SpawnSwarmAroundPlayer(14);
        yield return Play(1.0f);

        var solar = FindFirstObjectByType<SolarColumn>();
        var corona = solar != null ? solar.GetComponentInChildren<AuraCloud>() : null;

        Debug.Log($"[SHOTS] colonne solaire : {(solar != null ? "equipee" : "ABSENTE")}, " +
                  $"{(solar != null ? solar.LastFlareHits : -1)} cible(s) touchee(s), " +
                  $"couronne {Describe(corona)}");

        yield return Shot("colonne-solaire");
        yield return Play(0.5f);
        yield return Shot("colonne-solaire-2");

        // ─── La lame boomerang ────────────────────────────────────────────────
        // Elle empruntait le sprite d'un tir de sentinelle ennemie : c'est une image, et une seule,
        // qui dit si elle a désormais la forme de son icône.
        inv.ResetForRun();
        for (int i = 0; i < 5; i++) inv.AcquireOrLevelUp("glaive");

        SpawnSwarmAroundPlayer(12);
        yield return Play(0.9f);

        var lame = FindFirstObjectByType<GlaiveProjectile>();
        var lameSprite = lame != null ? lame.GetComponent<SpriteRenderer>()?.sprite : null;

        Debug.Log($"[SHOTS] lame boomerang : {(lame != null ? "en vol" : "AUCUNE")}, " +
                  $"sprite {(lameSprite == GlaiveSprite.Get() ? "croix dediee" : lameSprite?.name ?? "ABSENT")}, " +
                  $"rotation {(lame != null ? lame.transform.eulerAngles.z : -1f):F0} deg");

        yield return Shot("lame-boomerang");
        yield return Play(0.35f);
        yield return Shot("lame-boomerang-2");

        // ─── La singularité, posée SUR le décor ───────────────────────────────
        // Sans pilier dans le champ, l'image ne montre que le vortex : c'est précisément la capture
        // qui ne répond pas à la question posée. Le joueur est donc déplacé contre un obstacle, et
        // la nuée avec lui — le puits naît sur l'ennemi le plus proche.
        inv.ResetForRun();
        for (int i = 0; i < 5; i++) inv.AcquireOrLevelUp("singularity");

        if (ArenaObstacles.Count > 0)
            player.transform.position = ArenaObstacles.Centers[0] + new Vector2(70f, 0f);

        SpawnSwarmAroundPlayer(12);
        yield return Play(1.4f);

        Debug.Log($"[SHOTS] singularite : {ArenaObstacles.Count} obstacle(s) dans l'arene, " +
                  $"{SpaceDistortion.Registered} deformable(s), " +
                  $"{SpaceDistortion.LastBentCount} plie(s) par le champ");

        yield return Shot("singularite");
        yield return Play(0.4f);
        yield return Shot("singularite-2");

        // ─── Le corps de la chimère ───────────────────────────────────────────
        // Les appendices se posent depuis la liste des greffes portées : on la fournit directement,
        // les jauges n'ayant rien à voir avec ce qu'on photographie ici.
        //
        // ⚠ La scène est VIDÉE d'abord — armes, effets de greffe et faune. Le premier essai a
        // photographié le porteur au milieu du nuage d'une fusion précédente et sous une montée de
        // niveau en train de s'ouvrir : on ne voyait ni la carapace ni l'œil. Et vider la faune n'est
        // pas cosmétique — sans elle, plus d'éliminations, donc plus d'XP, donc plus de modale qui
        // s'invite entre le dernier écartement et la capture.
        inv.ResetForRun();
        player.GetComponent<GraftManager>()?.ClearAll();

        foreach (var enemy in EnemyBase.Active.ToArray())
            if (enemy != null) Destroy(enemy.gameObject);

        var body = player.GetComponent<ChimeraBody>() ?? player.gameObject.AddComponent<ChimeraBody>();
        yield return null;

        // ⚠ ZOOM. Le joueur mesure 32 px sur une image de 1280 : à l'échelle du jeu, un appendice de
        // 12 px occupe une dizaine de pixels à l'écran, et la capture ne peut répondre à aucune des
        // questions qu'on lui pose (tient-il sur la silhouette ? le reconnaît-on ?). C'est le pendant
        // du relevé compté : une image doit être cadrée sur ce qu'on lui demande de montrer.
        var camera = Camera.main;
        float restSize = camera != null ? camera.orthographicSize : 0f;
        if (camera != null) camera.orthographicSize = restSize * ChimeraZoom;

        foreach (var (name, grafts) in new[]
                 {
                     ("chimere-carapace", new[] { "grafted_carapace", "aiming_eye" }),
                     ("chimere-rodeur",   new[] { "stalker_wave", "erratic_servos", "swarm_symbiote" }),
                     ("chimere-fusions",  new[] { "fusion_charge_blindee", "fusion_ruche_tourelles" }),
                 })
        {
            body.Build(grafts);
            yield return Play(0.7f);

            Debug.Log($"[SHOTS] {name} : {body.PartCount} appendice(s), " +
                      $"corps {body.BodyPx:F0} px ({(body.BodyMeasured ? "mesure" : "REPLI")}), " +
                      $"zoom x{1f / ChimeraZoom:F0}");

            yield return Shot(name);
        }

        if (camera != null) camera.orthographicSize = restSize;
    }

    /// <summary>Facteur appliqué à la taille de la caméra pour les portraits de chimère.</summary>
    private const float ChimeraZoom = 0.2f;

    /// <summary>
    /// Laisse le jeu tourner <paramref name="seconds"/> secondes en <b>écartant la montée de
    /// niveau</b> à chaque image.
    /// </summary>
    /// <remarks>
    /// ⚠ Sans cela, la tournée ne photographie plus rien passé la 12ᵉ minute de run : les
    /// éliminations font monter le joueur de niveau, la modale s'ouvre, met le jeu à
    /// <c>timeScale = 0</c> — donc les armes cessent de tirer — et recouvre l'écran. Les images
    /// montrent alors trois cartes, et le relevé qui les accompagne des compteurs à zéro qu'on
    /// lirait « l'effet ne marche pas ». C'est arrivé sur cette passe même.
    /// </remarks>
    private IEnumerator Play(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            var levelUp = FindFirstObjectByType<LevelUpScreen>();
            if (levelUp != null && ModalQueue.Current == ModalKind.LevelUp) levelUp.DismissForBench();

            yield return null;
        }
    }

    /// <summary>Maintient tout le monde gelé — et en vie — pendant la durée d'une pose.</summary>
    private IEnumerator KeepFrozen(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            foreach (var enemy in EnemyBase.Active.ToArray())
            {
                if (enemy == null) continue;

                enemy.ApplyScaling(4000f, 0f);

                // La force du Voile de Givre : c'est le gel le plus fort du jeu, donc la borne haute
                // de l'effet — celle qu'il faut regarder pour juger s'il déborde.
                enemy.ApplySlow(0.55f, 1.5f);
            }

            yield return null;
        }
    }

    /// <summary>Maintient tout le monde en flammes — et en vie — pendant la durée d'une pose.</summary>
    private IEnumerator KeepBurning(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            foreach (var enemy in EnemyBase.Active.ToArray())
            {
                if (enemy == null) continue;

                // Beaucoup de PV et un poison très lent : la cible doit encore être là au
                // déclenchement de l'obturateur, sinon la capture ne montre qu'une arène vide.
                enemy.ApplyScaling(4000f, 0f);
                enemy.ApplyBurn(0.5f, 12f);
            }

            yield return null;
        }
    }

    /// <summary>
    /// Pose des ennemis en couronne autour du joueur, à portée des armes de mêlée comme des armes de
    /// zone. Ils sont instanciés depuis le prefab du spawner, donc scalés et armés comme les autres.
    /// </summary>
    private void SpawnSwarmAroundPlayer(int count)
    {
        var spawner = FindFirstObjectByType<EnemySpawner>();
        var player = Player.Instance;
        if (spawner == null || spawner.EnemyPrefab == null || player == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            float radius = 70f + (i % 4) * 55f;
            Vector2 at = (Vector2)player.transform.position
                       + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var go = Instantiate(spawner.EnemyPrefab, at, Quaternion.identity);
            go.SetActive(true);

            // ⚠ Le gabarit seul ne porte AUCUN sprite : c'est le spawner qui pose le jeu d'animations
            // depuis la définition, et sa méthode est privée. Sans ce câblage, la nuée posée à la
            // main se déplace, frappe, meurt — et reste totalement invisible. Le relevé du gel l'a
            // révélé (4 corps mesurés sur 18) : on photographiait des effets d'état portés par des
            // ennemis qui n'existaient pas à l'image, et l'on jugeait leur taille là-dessus.
            var enemy = go.GetComponent<EnemyBase>();
            var frames = SpriteFramesLibrary.ForEnemy(SwarmSpriteId, "");
            if (enemy != null && frames != null) enemy.SetSpriteFrames(frames);

            // Pas d'orbe d'XP : 28 morts d'un coup font monter de niveau, et l'écran de montée —
            // modal et bloquant — recouvrait justement le combat qu'on venait photographier.
        }
    }

    /// <summary>
    /// Faune de base employée pour les nuées posées à la main. C'est le plus petit corps du jeu
    /// (32 px) : la borne la plus sévère pour un effet d'état, qui doit tenir dessus <b>comme</b> sur
    /// un champion de 72.
    /// </summary>
    private const string SwarmSpriteId = "rust_swarm";

    private IEnumerator Shot(string name)
    {
        string path = Path.Combine(_dir, $"{_index:00}-{name}.png");

        // Une capture n'est écrite qu'à la fin de la frame suivante : attendre explicitement évite
        // de photographier l'écran d'AVANT.
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.6f);

        Debug.Log($"[SHOTS] {path}");
        _index++;
    }
}
