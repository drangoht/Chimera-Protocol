using UnityEngine;

/// <summary>
/// Démarre la run dans un ordre <b>explicite</b> (docs/UNITY_MIGRATION_PLAN.md §4.6).
///
/// <para>Sous Godot, l'ordre d'initialisation est celui des AutoLoads déclarés dans
/// <c>project.godot</c> : il se lit, et il est garanti. Unity ne garantit rien entre
/// <c>MonoBehaviour</c>s. Plutôt que de disperser des réglages d'ordre d'exécution que personne ne
/// pense à consulter, un seul composant orchestre le démarrage — et il tourne en <c>Start</c>, donc
/// après tous les <c>Awake</c>, ce qui garantit que les singletons existent.</para>
/// </summary>
[DefaultExecutionOrder(100)]
public sealed class RunBootstrap : MonoBehaviour
{
    [Tooltip("Graine de reproductibilité. Zéro = aléatoire, comme une partie normale.")]
    public ulong Seed;

    private void Start()
    {
        if (Seed != 0UL) Gd.Seed(Seed);
        else             Gd.Randomize();

        GameManager.Instance?.StartRun();
    }
}
