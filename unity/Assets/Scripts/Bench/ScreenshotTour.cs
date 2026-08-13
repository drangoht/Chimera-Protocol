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

        Showcase();
        StartCoroutine(Run());
    }

    /// <summary>
    /// Remplit une progression de <b>vitrine</b>, en mémoire seulement.
    /// </summary>
    /// <remarks>
    /// <para>Sans elle, une tournée lancée sur une installation neuve photographie un jeu <b>vide</b> :
    /// Codex masqué (il ne dévoile que ce qu'on a croisé), Hub sans Échos, aucune arme découverte,
    /// aucun défi. Les images seraient exactes et parfaitement inutilisables — elles montreraient
    /// l'absence de contenu là où la galerie doit en montrer.</para>
    ///
    /// <para>⚠ <b>Rien n'est écrit sur disque</b> : on modifie l'état chargé, jamais le fichier. La
    /// règle est la même que pour les drapeaux de banc — un outil ne laisse pas sa mise en scène dans
    /// la sauvegarde du joueur.</para>
    /// </remarks>
    private static void Showcase()
    {
        var settings = GameSettings.Current;

        var inventory = InventorySystem.Instance;
        if (inventory != null)
        {
            foreach (string id in inventory.AllWeaponIds)
                if (!settings.DiscoveredWeapons.Contains(id)) settings.DiscoveredWeapons.Add(id);
        }

        foreach (var graft in Assimilation.Config.Grafts)
            if (!settings.DiscoveredGrafts.Contains(graft.Id)) settings.DiscoveredGrafts.Add(graft.Id);

        // Une complétion par biome : les niveaux s'ouvrent, les badges s'affichent, et l'échelle de
        // saturation montre un cran gagné plutôt qu'une porte fermée.
        foreach (string biome in LevelThreat.Order)
        {
            settings.Completions[biome] = 1;
            settings.SaturationBeatenByLevel[biome] = 1;
            if (settings.HighScores.TryGetValue(biome, out int best) && best > 0) continue;
            settings.HighScores[biome] = 13 * 60 + 42;
        }

        // Des Échos en banque : le Hub photographié à zéro montre une boutique où rien n'est
        // achetable, c'est-à-dire l'inverse de ce qu'il est. La somme reste modeste — assez pour que
        // les premiers achats s'ouvrent, pas au point de donner l'arbre entier.
        if (MetaProgression.CurrentEchoes < 4000) MetaProgression.Save.Meta.CurrentEchoes = 4000;

        Debug.Log("[ScreenshotTour] progression de vitrine posee (en memoire, rien n'est ecrit).");
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

        // Second cliché, liste déroulée jusqu'en bas : les contrôles et la remise à zéro vivent
        // sous la ligne de flottaison, et une capture du haut de l'écran ne dit rien de ce qui s'y
        // trouve — c'est ainsi qu'une section entière peut manquer sans que personne ne le voie.
        foreach (var scroll in host.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true))
            scroll.verticalNormalizedPosition = 0f;

        yield return null;
        yield return Shot("options-bas");
        options.Hide();

        var codex = host.AddComponent<CodexScreen>();
        yield return null;
        codex.Show();
        yield return Shot("codex");

        // Les onglets du Codex sont quatre écrans, pas un : l'arsenal et la chimère montrent ce que
        // le bestiaire ne dit pas, et ce sont eux qui portent la promesse du jeu — les armes qu'on
        // construit, les greffes qu'on assimile.
        codex.SelectTab(CodexScreen.Tab.Arsenal);
        yield return null;
        yield return Shot("codex-arsenal");

        codex.SelectTab(CodexScreen.Tab.Chimera);
        yield return null;
        yield return Shot("codex-chimere");

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

        // ⚠ L'écran de PAUSE se photographie ICI, et pas plus loin : son corps est le résumé de la
        // partie en cours, et les mises en scène qui suivent vident l'inventaire (`ResetForRun`)
        // pour isoler une arme à la fois. Photographié après elles, il annonce « aucune arme » —
        // exact à cet instant, et parfaitement trompeur comme image de contrôle.
        //
        // Il manquait tout court à cette tournée, et ça s'est payé : une refonte de sa zone de
        // défilement a réduit le résumé à une colonne de 100 px, tronquée. Rien ne l'a signalé — le
        // banc vérifie que la pause s'ouvre, se ferme et rend la main, ce qu'elle faisait très bien
        // en étant illisible. **C'est le joueur qui l'a vu.** Un écran qu'aucune image ne montre est
        // un écran que personne ne relit.
        var pause = FindFirstObjectByType<PauseScreen>();
        if (pause != null)
        {
            pause.Open();
            yield return new WaitForSecondsRealtime(0.5f);
            yield return Shot("pause", keepModal: true);
            pause.Resume();
        }

        yield return ShootStatusEffects();
        yield return ShootChimera();

        var levelUp = FindFirstObjectByType<LevelUpScreen>();
        if (levelUp != null)
        {
            levelUp.Present(LevelUpPool.BuildOverload());
            yield return new WaitForSecondsRealtime(1f);
            yield return Shot("montee-de-niveau", keepModal: true);
        }

        yield return ShootRarityCards();
        yield return ShootFusionForged();

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

        yield return ShootOverloadField();
    }

    /// <summary>
    /// Photographie le <b>Champ de Surcharge au niveau 5</b> : son aura permanente, et l'impulsion
    /// qui la décharge.
    /// </summary>
    /// <remarks>
    /// <para>Signalé « trop discret » en jouant, pour deux raisons qu'une seule image ne sépare pas :
    /// l'arme <b>ne grandissait pas</b> (rayon figé à celui du niveau 1, faute de lire <c>radius</c>)
    /// et n'existait à l'écran que pendant les 0,22 s de son onde. Le relevé donne donc le
    /// <b>rayon effectif</b> : sans lui, une capture montrant un petit champ ne distingue pas « le
    /// niveau n'est pas appliqué » de « l'effet est trop pâle ».</para>
    ///
    /// <para>Deux clichés à des instants choisis : l'un pendant la <b>charge</b> — l'aura seule, ce
    /// que le joueur voit 90 % du temps — l'autre <b>juste après</b> une impulsion, avec ses ondes et
    /// ses arcs. Un seul cliché ne dirait rien du contraste entre les deux, qui est tout l'effet.</para>
    /// </remarks>
    private IEnumerator ShootOverloadField()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) yield break;

        inv.ResetForRun();
        for (int i = 0; i < 5; i++) inv.AcquireOrLevelUp("overload_field");

        SpawnSwarmAroundPlayer(18);
        yield return Play(1.4f);

        var field = FindFirstObjectByType<OverloadField>();

        Debug.Log($"[SHOTS] surcharge : rayon {(field != null ? field.Radius : -1f):F0} px " +
                  $"(niveau 5 attendu 200), recul {(field != null ? field.Knockback : -1f):F0} px, " +
                  $"{(field != null ? field.LastPulseHits : -1)} cible(s) a la derniere impulsion — " +
                  $"nuage {Describe(field != null ? field.Field : null)}");

        yield return Shot("surcharge-charge");

        // Assez pour qu'une impulsion soit partie (1,5 s de recharge au niveau 5), sans laisser ses
        // ondes s'éteindre : elles vivent 0,3 s.
        yield return Play(1.5f);
        yield return Shot("surcharge-impulsion");
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

        // ─── L'Aimant, et l'arène qui se vide ─────────────────────────────────
        // Ce système entier n'avait jamais été porté, et sa silhouette est dessinée à l'exécution :
        // aucun test ne peut dire si un fer à cheval se lit comme un fer à cheval. Le premier cliché
        // le montre posé au sol au milieu des orbes, le second l'arène après ramassage — c'est le
        // couple d'images qui répond, pas l'objet seul.
        inv.ResetForRun();

        var magnetSpawner = FindFirstObjectByType<MagnetSpawner>();
        if (magnetSpawner != null)
        {
            var orbPrefab = Spawner.Load("res://scenes/entities/XpOrb.tscn");
            Vector2 home = player.transform.position;

            if (orbPrefab != null)
                for (int i = 0; i < 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f;
                    float r = 200f + (i % 4) * 70f;
                    var go = Instantiate(orbPrefab,
                        home + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r, Quaternion.identity);
                    go.SetActive(true);
                }

            magnetSpawner.SpawnAt(home + new Vector2(90f, 0f));
            yield return Play(0.2f);

            int posees = FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length;
            var pickup = FindFirstObjectByType<MagnetPickup>();

            Debug.Log($"[SHOTS] aimant : {(pickup != null ? "au sol" : "ABSENT")}, " +
                      $"sprite {(pickup?.GetComponent<SpriteRenderer>()?.sprite == MagnetSprite.Get() ? "fer a cheval dedie" : "ABSENT")}, " +
                      $"{posees} orbe(s) semee(s)");

            yield return Shot("aimant");

            // Le joueur marche dessus, puis on laisse la convergence se jouer : c'est le geste, et
            // c'est lui qu'il faut voir — un aimant qui ne vide pas l'écran n'est pas un aimant.
            player.transform.position = home + new Vector2(90f, 0f);
            yield return Play(0.45f);

            int restantes = FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length;
            Debug.Log($"[SHOTS] aimant : {posees} orbes avant, {restantes} apres " +
                      $"({MagnetPickup.CollectedCount} aimant(s) ramasse(s))");

            yield return Shot("aimant-2");
        }

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

    /// <summary>
    /// Photographie une main qui contient les <b>trois raretés à la fois</b>, puis une carte de
    /// fusion seule.
    /// </summary>
    /// <remarks>
    /// <para>La capture précédente présente une main de <b>surcharge</b> : trois cartes de même
    /// nature, donc trois cadres identiques. C'est exactement l'image qui ne permet pas de juger la
    /// hiérarchie de rareté — celle qu'on croit avoir vérifiée en la regardant. Une aura ne se juge
    /// pas seule : ce qui compte est l'<b>écart</b> entre commune, rare et épique, et l'écart ne se
    /// voit que côte à côte.</para>
    ///
    /// <para>Les trois identifiants sont pris tels quels dans <c>rarityByCard</c>
    /// (<c>impulse_cannon</c> commune, <c>tesla_coil</c> rare, <c>singularity</c> épique) : la rareté
    /// affichée vient de l'inventaire, pas de la mise en scène, donc l'image montre bien ce que le
    /// jeu déciderait.</para>
    /// </remarks>
    private IEnumerator ShootRarityCards()
    {
        var levelUp = FindFirstObjectByType<LevelUpScreen>();
        if (levelUp == null) yield break;

        var hand = new[]
        {
            new LevelUpCard(LevelUpCardKind.NewWeapon, "impulse_cannon", 1),
            new LevelUpCard(LevelUpCardKind.NewWeapon, "tesla_coil", 1),
            new LevelUpCard(LevelUpCardKind.NewWeapon, "singularity", 1),
        };

        // ⚠ `Present` seul NE SUFFIT PAS quand l'écran est déjà ouvert : il passe par
        // `ModalQueue.Request`, qui ne rouvre pas une modale déjà en cours — donc `Show` n'est pas
        // rappelé, `BuildCards` non plus, et l'écran garde la main précédente à l'affichage. La
        // première version de cette capture a produit exactement cela : une image nommée
        // « cartes-raretes » montrant les trois cartes de SURCHARGE du cliché d'avant, toutes de même
        // rareté — c'est-à-dire l'image qu'on voulait justement remplacer. `Replace` reconstruit sur
        // place ; l'appeler après `Present` couvre les deux cas.
        levelUp.Present(hand);
        yield return null;
        levelUp.Replace(hand);

        // Assez long pour que l'arrivée en cascade soit finie ET que la respiration des auras soit
        // passée par un sommet : photographiée à 0,2 s, l'aura épique serait au creux de sa
        // pulsation, c'est-à-dire à l'opacité qu'on a justement choisi de ne PAS montrer.
        yield return new WaitForSecondsRealtime(1.2f);
        yield return Shot("cartes-raretes", keepModal: true);

        levelUp.Replace(new[] { new LevelUpCard(LevelUpCardKind.Fusion, "fusion_blade", 1) });

        yield return new WaitForSecondsRealtime(1.2f);
        yield return Shot("carte-fusion", keepModal: true);

        levelUp.DismissForBench();
        yield return new WaitForSecondsRealtime(0.3f);
    }

    /// <summary>
    /// Photographie la <b>forge d'une fusion</b> : l'annonce et la fanfare, ensemble.
    /// </summary>
    /// <remarks>
    /// <para>Les deux sont déclenchés <b>à la main</b> plutôt qu'en forgeant réellement une fusion :
    /// <c>ApplyFusion</c> exige l'arme source au niveau requis et son passif, conditions qu'une
    /// tournée de captures n'atteint jamais. On photographie ici l'apparence, pas la chaîne qui la
    /// déclenche — c'est le même parti pris que pour le gel et la brûlure.</para>
    ///
    /// <para>⚠ La fanfare déclenche un <b>ralenti</b>. Les attentes sont donc en temps réel
    /// (<c>WaitForSecondsRealtime</c>) : comptées en temps de jeu, elles dureraient cinq fois plus
    /// longtemps et la capture arriverait bien après l'extinction des ondes.</para>
    /// </remarks>
    private IEnumerator ShootFusionForged()
    {
        var banner = FindFirstObjectByType<FusionBanner>();
        var player = Player.Instance;

        if (banner == null || player == null) yield break;

        banner.Show("fusion_blade");
        FusionFanfare.Play(player.transform.position, player);

        // Au sommet de la première onde : l'éclat est parti, la deuxième onde n'est pas encore
        // dissipée, et le panneau vient d'atteindre sa taille finale.
        yield return new WaitForSecondsRealtime(0.35f);
        yield return Shot("fusion-forgee", keepModal: true);
    }

    /// <param name="keepModal">
    /// Vrai pour les rares clichés dont la modale EST le sujet (l'écran de montée de niveau).
    /// </param>
    private IEnumerator Shot(string name, bool keepModal = false)
    {
        string path = Path.Combine(_dir, $"{_index:00}-{name}.png");

        // ⚠ La modale est écartée ICI, au point unique par lequel passent toutes les captures.
        // Elle l'était auparavant pendant les phases de jeu seulement, et les clichés de combat —
        // pris entre deux attentes courtes — se faisaient recouvrir quand même : la galerie du
        // Secteur Néon, qui donne +15 % d'XP, ne montrait qu'un écran de choix de cartes.
        if (!keepModal)
        {
            var levelUp = FindFirstObjectByType<LevelUpScreen>();
            if (levelUp != null && levelUp.IsVisible) levelUp.DismissForBench();

            var assimilation = FindFirstObjectByType<AssimilationScreen>();
            if (assimilation != null && assimilation.IsVisible) assimilation.AcceptForBench();
        }

        // Une capture n'est écrite qu'à la fin de la frame suivante : attendre explicitement évite
        // de photographier l'écran d'AVANT.
        yield return new WaitForEndOfFrame();
        ScreenCapture.CaptureScreenshot(path);
        yield return new WaitForSecondsRealtime(0.6f);

        Debug.Log($"[SHOTS] {path}");
        _index++;
    }
}
