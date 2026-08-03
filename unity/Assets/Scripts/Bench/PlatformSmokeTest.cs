using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Vérifie à l'<b>exécution</b> que la couche d'adaptation fonctionne réellement sous Unity
/// (Lot 1 — docs/UNITY_MIGRATION_PLAN.md §4).
///
/// <para><b>Pourquoi ce fichier existe alors que les noyaux sont déjà testés.</b> Les tests xUnit
/// couvrent la logique pure (<see cref="TweenTimeline"/>, <see cref="TimerWheel"/>,
/// <see cref="DeferredQueue"/>) — mais ils ne peuvent rien dire des adaptateurs, qui dépendent du
/// cycle de vie Unity : ordre <c>Update</c>/<c>LateUpdate</c>, <c>Time.timeScale</c>, destruction
/// d'objets. Or c'est <b>exactement là</b> que se logent les erreurs de portage. « Ça compile »
/// n'est pas « ça marche ».</para>
///
/// <para>S'exécute headless et sort avec un code non nul en cas d'échec, pour être utilisable
/// depuis un script de vérification.</para>
/// </summary>
public sealed class PlatformSmokeTest : MonoBehaviour
{
    private readonly List<string> _results = new();
    private int _failures;

    private void Check(string name, bool ok, string detail = "")
    {
        if (!ok) _failures++;
        _results.Add($"{(ok ? "  OK  " : " ECHEC")} {name}{(detail.Length > 0 ? " — " + detail : "")}");
    }

    private IEnumerator Start()
    {
        yield return RunTweenReachesExactFinalValue();
        yield return RunTweenDiesWithOwner();
        yield return RunUnscaledTweenSurvivesPause();
        yield return RunTimers();
        yield return RunDeferredOrdering();

        var sb = new StringBuilder();
        sb.AppendLine("=== VERIFICATION DE LA COUCHE D'ADAPTATION ===");
        foreach (string r in _results) sb.AppendLine(r);
        sb.AppendLine(_failures == 0
            ? $"TOUT PASSE ({_results.Count} verifications)"
            : $"{_failures} ECHEC(S) sur {_results.Count}");
        Debug.Log(sb.ToString());

        Application.Quit(_failures == 0 ? 0 : 1);
    }

    /// <summary>Le contrat central : la valeur finale doit être atteinte exactement.</summary>
    private IEnumerator RunTweenReachesExactFinalValue()
    {
        float value = -1f;
        bool finished = false;

        var t = GTween.Create(this);
        t.TweenFloat(v => value = v, 0f, 42f, 0.2f, TransType.Quad, EaseType.Out);
        t.Finished += () => finished = true;

        yield return new WaitForSeconds(0.5f);

        Check("interpolation : valeur finale exacte", Mathf.Approximately(value, 42f), $"valeur={value}");
        Check("interpolation : signal de fin emis", finished);
    }

    /// <summary>
    /// Une interpolation dont le propriétaire est détruit ne doit plus rien appliquer — sinon le
    /// setter touche un objet Unity détruit, ce qui lève une exception au pire moment.
    /// </summary>
    private IEnumerator RunTweenDiesWithOwner()
    {
        var victim = new GameObject("victime");
        int applications = 0;

        var t = GTween.Create(victim);
        t.TweenFloat(_ => applications++, 0f, 1f, 5f);

        yield return null;
        yield return null;
        int before = applications;

        Destroy(victim);
        yield return null;
        yield return null;
        yield return null;

        Check("interpolation : s'arrete avec son proprietaire",
              applications <= before + 1, $"avant={before} apres={applications}");
        Check("interpolation : marquee terminee", t.IsDone);
    }

    /// <summary>
    /// La pause du jeu passe par <c>timeScale = 0</c>. Une interpolation d'UI doit continuer, une
    /// interpolation de jeu doit se figer — sans quoi le menu de pause serait lui-même figé.
    /// </summary>
    private IEnumerator RunUnscaledTweenSurvivesPause()
    {
        float ui = 0f, game = 0f;

        GTween.Create(this, ignoreTimeScale: true).TweenFloat(v => ui = v, 0f, 1f, 0.3f);
        GTween.Create(this).TweenFloat(v => game = v, 0f, 1f, 0.3f);

        SceneRoot.Paused = true;
        yield return new WaitForSecondsRealtime(0.5f);
        SceneRoot.Paused = false;

        Check("interpolation UI : avance malgre la pause", ui > 0.9f, $"ui={ui:F3}");
        Check("interpolation jeu : figee par la pause", game < 0.05f, $"jeu={game:F3}");
    }

    private IEnumerator RunTimers()
    {
        int scaled = 0, unscaled = 0, repeated = 0, cancelled = 0;

        SceneRoot.CreateTimer(0.1, () => scaled++);
        SceneRoot.CreateUnscaledTimer(0.1, () => unscaled++);
        SceneRoot.CreateTimer(0.05, () => repeated++, repeat: true);
        int id = SceneRoot.CreateTimer(0.1, () => cancelled++);
        SceneRoot.CancelTimer(id);

        yield return new WaitForSeconds(0.4f);

        Check("minuterie : declenchee une fois", scaled == 1, $"n={scaled}");
        Check("minuterie temps reel : declenchee", unscaled == 1, $"n={unscaled}");
        Check("minuterie repetitive : plusieurs fois", repeated >= 3, $"n={repeated}");
        Check("minuterie annulee : jamais declenchee", cancelled == 0, $"n={cancelled}");
    }

    /// <summary>
    /// L'exécution différée doit avoir lieu à la fin de la frame, donc APRÈS le code qui l'a
    /// demandée — c'est tout l'intérêt de <c>CallDeferred</c>.
    /// </summary>
    private IEnumerator RunDeferredOrdering()
    {
        var order = new List<string>();

        SceneRoot.CallDeferred(() => order.Add("a"));
        SceneRoot.CallDeferred(() => order.Add("b"));
        order.Add("immediat");

        yield return null;

        bool ok = order.Count == 3 && order[0] == "immediat" && order[1] == "a" && order[2] == "b";
        Check("differe : execute en fin de frame, dans l'ordre", ok, string.Join(",", order));

        // Chaînage : une action différée qui en ajoute une autre doit être traitée dans le même
        // vidage, sinon on introduit une latence d'une frame.
        var chain = new List<string>();
        SceneRoot.CallDeferred(() =>
        {
            chain.Add("premier");
            SceneRoot.CallDeferred(() => chain.Add("second"));
        });

        yield return null;

        Check("differe : chainage resolu dans le meme vidage",
              chain.Count == 2 && chain[1] == "second", string.Join(",", chain));
    }
}
