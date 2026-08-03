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
    /// <summary>
    /// Jeu d'animations réel, injecté au moment du build (voir <c>BuildBench</c>). Il ne peut pas
    /// être chargé ici : un asset hors <c>Resources/</c> n'existe pas à l'exécution s'il n'est
    /// référencé par aucune scène — c'est justement ce lien qu'on veut vérifier.
    /// </summary>
    public SpriteFramesAsset? TestFrames;

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
        RunJsonRoundTrip();
        RunSpawnerPathMapping();
        yield return RunFrameAnimator();

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

    /// <summary>
    /// Les chemins Godot doivent se traduire sans réécriture des 65 sites d'appel — c'est tout
    /// l'intérêt de garder les chaînes identiques entre les deux moteurs.
    /// </summary>
    private void RunSpawnerPathMapping()
    {
        Check("spawner : chemin de scene Godot traduit",
              Spawner.ToResourcePath("res://scenes/entities/XpOrb.tscn") == "Prefabs/entities/XpOrb",
              Spawner.ToResourcePath("res://scenes/entities/XpOrb.tscn"));

        Check("spawner : sous-dossier profond preserve",
              Spawner.ToResourcePath("res://scenes/entities/MiniBoss/CryoSentinel.tscn")
                  == "Prefabs/entities/MiniBoss/CryoSentinel");

        Check("spawner : prefab absent signale sans crasher",
              Spawner.Load("res://scenes/entities/NExistePas.tscn") == null);
    }

    /// <summary>
    /// Le lecteur d'animations doit boucler, signaler la fin d'une animation non bouclée (c'est ce
    /// qui fait disparaître un ennemi après sa mort) et tolérer une animation absente.
    /// </summary>
    private IEnumerator RunFrameAnimator()
    {
        if (TestFrames == null)
        {
            Check("animateur : jeu d'animations injecte", false, "TestFrames non assigne au build");
            yield break;
        }

        Check("animateur : jeu d'animations injecte", true,
              $"{TestFrames.Id}, {TestFrames.Animations.Length} animations");

        var go = new GameObject("anim", typeof(SpriteRenderer), typeof(FrameAnimator));
        var renderer = go.GetComponent<SpriteRenderer>();
        var anim = go.GetComponent<FrameAnimator>();
        anim.SetSpriteFrames(TestFrames);

        // Boucle : l'image doit changer, et la lecture continuer.
        anim.Play("idle");
        Sprite first = renderer.sprite;
        yield return new WaitForSeconds(0.6f);

        Check("animateur : image affichee", first != null);
        Check("animateur : animation bouclee continue", anim.IsPlaying);

        // Non bouclée : signal de fin, puis arrêt sur la dernière image.
        string finished = "";
        anim.AnimationFinished += n => finished = n;
        anim.Play("death");
        yield return new WaitForSeconds(2.0f);

        Check("animateur : fin d'animation non bouclee signalee", finished == "death", $"recu='{finished}'");
        Check("animateur : arret apres la derniere image", !anim.IsPlaying);

        // Animation absente : ne doit ni crasher ni interrompre ce qui tourne.
        anim.Play("idle");
        anim.Play("cette_animation_nexiste_pas");
        Check("animateur : animation absente toleree", anim.CurrentAnimation == "idle",
              $"courante='{anim.CurrentAnimation}'");

        Destroy(go);
    }

    /// <summary>Forme représentative de <c>SaveData</c> : collections, dictionnaire, imbrication.</summary>
    private sealed class FakeSaveData
    {
        public int Echoes { get; set; }
        public long LifetimeKills { get; set; }
        public List<string> UnlockedChallenges { get; set; } = new();
        public Dictionary<string, int> BiomeRanks { get; set; } = new();
        public NestedBlock Meta { get; set; } = new();

        public sealed class NestedBlock
        {
            public string EquippedPerk { get; set; } = "";
            public bool[] Flags { get; set; } = System.Array.Empty<bool>();
        }
    }

    /// <summary>
    /// Le seul point du portage signalé comme fragile en AOT : <c>SaveManager</c> sérialise par
    /// <b>réflexion</b> (<c>JsonSerializer.Serialize&lt;SaveData&gt;</c>). Sous IL2CPP, la
    /// réflexion sur génériques peut échouer <b>à l'exécution seulement</b> — jamais dans l'éditeur,
    /// jamais à la compilation. C'est exactement le mode de défaillance qu'on ne veut pas découvrir
    /// après publication, avec des sauvegardes de joueurs en jeu (§5.2, R7).
    /// </summary>
    private void RunJsonRoundTrip()
    {
        try
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase,
                WriteIndented = false,
            };

            var original = new FakeSaveData
            {
                Echoes = 70084,
                LifetimeKills = 1234567L,
                UnlockedChallenges = new List<string> { "first_boss", "no_hit" },
                BiomeRanks = new Dictionary<string, int> { { "sanctuaire", 6 }, { "neon", 3 } },
                Meta = new FakeSaveData.NestedBlock
                {
                    EquippedPerk = "start_extra_slot",
                    Flags = new[] { true, false, true },
                },
            };

            string json = System.Text.Json.JsonSerializer.Serialize(original, options);
            var back = System.Text.Json.JsonSerializer.Deserialize<FakeSaveData>(json, options);

            bool ok = back != null
                      && back.Echoes == 70084
                      && back.LifetimeKills == 1234567L
                      && back.UnlockedChallenges.Count == 2
                      && back.BiomeRanks.TryGetValue("sanctuaire", out int r) && r == 6
                      && back.Meta.EquippedPerk == "start_extra_slot"
                      && back.Meta.Flags.Length == 3 && back.Meta.Flags[2];

            Check("System.Text.Json : aller-retour complet (risque AOT)", ok,
                  ok ? $"{json.Length} octets" : "valeurs incorrectes au retour");

            // La convention camelCase doit survivre : c'est elle qui détermine si les sauvegardes
            // existantes des joueurs restent lisibles.
            Check("System.Text.Json : convention camelCase preservee",
                  json.Contains("\"echoes\"") && json.Contains("\"biomeRanks\""),
                  json.Substring(0, System.Math.Min(80, json.Length)));
        }
        catch (System.Exception e)
        {
            Check("System.Text.Json : aller-retour complet (risque AOT)", false,
                  e.GetType().Name + " : " + e.Message);
        }
    }
}
