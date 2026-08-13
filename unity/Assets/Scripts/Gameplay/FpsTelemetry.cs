using UnityEngine;

/// <summary>
/// Relève la cadence d'affichage dans le journal, avec la charge qui l'explique.
///
/// <para><b>Pourquoi un relevé écrit plutôt que le compteur du HUD.</b> Le compteur affiche un
/// nombre qu'il faut lire à l'œil, sur une image, au bon moment — donc jamais pendant le pic qui
/// nous intéresse. Ici chaque fenêtre laisse une ligne : on peut relire la mesure après coup,
/// la comparer entre deux plateformes, et surtout constater les creux qu'un coup d'œil manque.</para>
///
/// <para><b>Et pourquoi c'est indispensable en web.</b> C'est la plateforme dont la cadence est le
/// premier risque — un survivor tient 200 à 300 entités, et un navigateur n'a ni fils d'exécution ni
/// compilation optimisée — et c'est aussi la seule où l'on ne peut <b>rien</b> mesurer de
/// l'extérieur : tant que le canevas tourne, il monopolise le fil principal et aucune extension ne
/// parvient plus à y injecter le moindre script. Le seul instrument possible est donc dans le jeu,
/// et sa sortie doit passer par la console du navigateur — le seul canal qui en ressorte.</para>
///
/// <para>⚠ Actif uniquement sous <c>--show-fps</c> (<c>?show-fps</c> dans une adresse), et
/// <b>n'écrit rien</b> dans la sauvegarde : une mesure ne laisse pas sa mise en scène derrière elle.</para>
/// </summary>
public sealed class FpsTelemetry : MonoBehaviour
{
    /// <summary>Durée d'une fenêtre de relevé, en secondes.</summary>
    /// <remarks>
    /// Cinq secondes : assez long pour qu'une moyenne veuille dire quelque chose, assez court pour
    /// qu'une nuée qui arrive se voie sur la ligne suivante plutôt que d'être noyée dans la précédente.
    /// </remarks>
    private const float WindowSeconds = 5f;

    private readonly FrameStats _stats = new();
    private float _elapsed;
    private float _total;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!DebugHooks.ShowFps) return;

        var host = new GameObject("[FpsTelemetry]");
        host.AddComponent<FpsTelemetry>();
        DontDestroyOnLoad(host);

        Debug.Log("[Fps] --show-fps : releve toutes les 5 s (moyenne, pire image, part sous 30).");
    }

    private void Update()
    {
        // Temps NON mis à l'échelle : une pause ou un ralenti de fusion changent le temps du jeu, pas
        // le temps qu'une image met à s'afficher. Mesurer l'autre reviendrait à confondre la vitesse
        // de la simulation avec la fluidité — et `--timescale` fausserait tout relevé de banc.
        float dt = Time.unscaledDeltaTime;

        _stats.Add(dt);
        _elapsed += dt;
        _total += dt;

        if (_elapsed < WindowSeconds) return;

        // La population est ce qui explique la cadence : une moyenne de 40 ne dit rien si l'on ignore
        // s'il y avait 12 ennemis ou 280 à l'écran.
        Debug.Log($"[Fps] t={_total:F0}s {_stats.Format()} ennemis={EnemyBase.Active.Count}");

        _stats.Reset();
        _elapsed = 0f;
    }
}
