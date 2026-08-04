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
