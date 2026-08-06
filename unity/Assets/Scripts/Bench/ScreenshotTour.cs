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
        // Le gel est volontairement ABSENT. Sa traînée sème des éclats de 22 px qui recouvrent
        // exactement ce qu'on vient regarder ; deux états sur la même image ne se jugent ni l'un ni
        // l'autre.
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

            // Pas d'orbe d'XP : 28 morts d'un coup font monter de niveau, et l'écran de montée —
            // modal et bloquant — recouvrait justement le combat qu'on venait photographier.
        }
    }

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
