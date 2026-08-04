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

    [Tooltip("Arme de départ, déjà posée sur le joueur dans la scène.")]
    public string StartingWeaponId = "impulse_cannon";

    private void Start()
    {
        if (Seed != 0UL) Gd.Seed(Seed);
        else             Gd.Randomize();

        // Le vivier de traces garde des références sur des objets détruits avec la scène précédente :
        // sans cette remise à zéro, les premiers tirs de la run les réutiliseraient — donc
        // n'afficheraient rien.
        WeaponVfx.Reset();

        GameManager.Instance?.StartRun();
        ApplyCommandLine();
        WireInventory();
    }

    /// <summary>
    /// Options de ligne de commande. <c>--run-duration=&lt;secondes&gt;</c> raccourcit le temps
    /// imparti : sans elle, <b>vérifier l'arrivée du boss coûte treize minutes de jeu réel</b>, ce qui
    /// revient en pratique à ne jamais la vérifier.
    /// </summary>
    private void ApplyCommandLine()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (!arg.StartsWith("--run-duration=", System.StringComparison.Ordinal)) continue;

            if (int.TryParse(arg.Substring("--run-duration=".Length), out int seconds) && seconds > 0)
            {
                GameManager.Instance?.OverrideRunDuration(seconds);
                Debug.Log($"[RunBootstrap] temps imparti force a {seconds}s.");
            }
        }
    }

    /// <summary>
    /// Relie l'inventaire au porteur, et lui <b>déclare l'arme de départ</b>.
    ///
    /// <para>Sans cette déclaration, l'arme posée dans la scène existe et tire, mais l'inventaire
    /// l'ignore : le choix de niveau la propose alors comme « nouvelle arme », en crée une seconde
    /// par-dessus, et la première ne monte plus jamais de niveau. Un défaut entièrement muet — deux
    /// canons superposés se voient à peine.</para>
    /// </summary>
    private void WireInventory()
    {
        var inv = InventorySystem.Instance;
        var player = Player.Instance;
        if (inv == null || player == null) return;

        inv.Mount = player.transform;

        var starting = player.GetComponentInChildren<WeaponBase>();
        if (starting != null && StartingWeaponId.Length > 0)
        {
            WeaponRegistry.InjectProjectilePrefabs(starting);
            inv.Register(StartingWeaponId, starting);
        }
    }
}
