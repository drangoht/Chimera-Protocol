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
        Vfx.Reset();
        ScreenShake.Reset();

        // Le biome décide de la faune, du palier de menace et de l'incarnation du boss. Le poser
        // avant StartRun évite qu'une run hérite en silence du choix de la précédente.
        if (GameManager.Instance != null) GameManager.Instance.CurrentBiomeId = RunConfig.BiomeId;

        GameManager.Instance?.StartRun();

        // Les bonus permanents s'appliquent APRÈS StartRun — qui remet les statistiques à leur état
        // de début de partie et effacerait donc tout ce que le joueur a acheté au Hub.
        if (Player.Instance != null)
        {
            MetaProgression.ApplyTo(Player.Instance.Stats);

            // Le porteur des greffes doit exister avant la première élimination : une jauge peut se
            // remplir dans les premières secondes.
            if (Player.Instance.GetComponent<GraftManager>() == null)
                Player.Instance.gameObject.AddComponent<GraftManager>();
        }

        Assimilation.ResetForRun();

        // La musique du biome démarre avec la run : les trois pistes (calme, combat, boss) tournent
        // ensemble et seuls leurs volumes bougent.
        MusicDirector.Instance?.PlayBiome(RunConfig.BiomeId);

        ApplyCommandLine();
        WireInventory();

        // ⚠ APRÈS le câblage de l'inventaire, et pas avant : celui-ci déclare l'arme de départ en
        // prenant la première arme trouvée sur le joueur. Un perk qui aurait déjà ajouté le Glaive
        // ferait enregistrer CELUI-CI comme arme de départ — le canon deviendrait alors une arme
        // fantôme, qui tire sans jamais monter de niveau.
        ApplyStartingPerk();

        // La saturation a besoin du point de montage pour créer les armes.
        if (_saturateArsenal) SaturateArsenal();
    }

    private bool _saturateArsenal;

    /// <summary>
    /// Applique le perk de départ équipé au Hub. C'est ce qui referme la boucle des défis : sans lui,
    /// une récompense se débloque, s'équipe… et ne se voit jamais en jeu.
    ///
    /// <para><b>Défensif</b> : seul un perk réellement débloqué s'applique — une sauvegarde éditée ne
    /// doit pas accorder un bonus qui n'a pas été gagné.</para>
    /// </summary>
    private void ApplyStartingPerk()
    {
        string perk = MetaProgression.EquippedPerk;
        if (perk.Length == 0 || !MetaProgression.HasPerk(perk)) return;

        switch (perk)
        {
            case "start_graft_swarm":
                Assimilation.GrantStartingGraft("swarm_symbiote");
                break;

            case "start_weapon_glaive":
                InventorySystem.Instance?.AcquireOrLevelUp("glaive");
                break;

            case "start_extra_slot":
                Assimilation.AddBonusSlots(1);
                break;

            default:
                Debug.LogWarning($"[RunBootstrap] perk de depart inconnu : '{perk}' — aucun effet.");
                break;
        }

        Debug.Log($"[RunBootstrap] perk de depart applique : {perk}.");
    }

    /// <summary>
    /// Donne au joueur un arsenal de fin de partie. <b>Indispensable dès qu'on raccourcit le temps
    /// imparti</b> : abréger le décompte n'abrège pas la construction du build, et le boss — calibré
    /// sur un TTK joué avec trois armes de niveau 20 — devient alors un mur de patience. Mesuré :
    /// 4,7 % de ses PV en 20 s avec un build de niveau 9, soit ~7 minutes de mise à mort.
    ///
    /// <para>⚠ Ce n'est pas une aide de jeu : c'est un <b>outil de mesure</b>, et il donne une borne
    /// haute — l'arsenal est saturé alors que le personnage n'a ni les PV ni les cartes de surcharge
    /// qu'une vraie run de treize minutes aurait accumulés.</para>
    /// </summary>
    private void SaturateArsenal()
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        foreach (string id in inv.AllWeaponIds)
        {
            if (!inv.Has(id) && inv.WeaponCount >= InventorySystem.MaxWeapons) continue;
            for (int level = 0; level < inv.WeaponMaxLevel(id); level++) inv.AcquireOrLevelUp(id);
        }

        foreach (string id in inv.AllPassiveIds)
            for (int level = 0; level < 10; level++) inv.AddOrUpgradePassive(id);

        Debug.Log($"[RunBootstrap] arsenal sature : {inv.WeaponCount} armes.");
    }

    /// <summary>
    /// Options de ligne de commande.
    ///
    /// <list type="bullet">
    ///   <item><c>--run-duration=&lt;secondes&gt;</c> raccourcit le temps imparti : sans elle,
    ///         <b>vérifier l'arrivée du boss coûte treize minutes de jeu réel</b>, ce qui revient en
    ///         pratique à ne jamais la vérifier.</item>
    ///   <item><c>--saturate-arsenal</c> donne un arsenal de fin de partie. <b>Les deux vont
    ///         ensemble</b> : raccourcir le décompte sans donner le build correspondant fait affronter
    ///         le boss avec les moyens de la première minute.</item>
    ///   <item><c>--force-weapon=&lt;id&gt;[:&lt;niveau&gt;]</c> donne une arme précise, à un niveau
    ///         précis. Sans elle, <b>vérifier une arme rare revient à jouer jusqu'à la tirer</b> —
    ///         c'est-à-dire à ne pas la vérifier. Répétable.</item>
    ///   <item><c>--force-meta=&lt;id&gt;:&lt;niveau&gt;</c> impose une amélioration du Hub pour la
    ///         run en cours, <b>sans toucher à la sauvegarde</b>. C'est le seul moyen d'observer
    ///         Renouveler et Passer sans avoir d'abord dépensé des Échos.</item>
    /// </list>
    /// </summary>
    private void ApplyCommandLine()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "--saturate-arsenal") { _saturateArsenal = true; continue; }

            if (arg.StartsWith("--force-weapon=", System.StringComparison.Ordinal))
            {
                ForceWeapon(arg.Substring("--force-weapon=".Length));
                continue;
            }

            if (arg.StartsWith("--force-meta=", System.StringComparison.Ordinal))
            {
                ForceMeta(arg.Substring("--force-meta=".Length));
                continue;
            }

            if (!arg.StartsWith("--run-duration=", System.StringComparison.Ordinal)) continue;

            if (int.TryParse(arg.Substring("--run-duration=".Length), out int seconds) && seconds > 0)
            {
                GameManager.Instance?.OverrideRunDuration(seconds);
                Debug.Log($"[RunBootstrap] temps imparti force a {seconds}s.");
            }
        }
    }

    /// <summary>Donne une arme au niveau demandé (« id » ou « id:niveau »).</summary>
    private static void ForceWeapon(string spec)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        string[] parts = spec.Split(':');
        string id = parts[0];
        int level = parts.Length > 1 && int.TryParse(parts[1], out int n) ? Mathf.Max(1, n) : 1;

        for (int i = 0; i < level; i++) inv.AcquireOrLevelUp(id);

        inv.WeaponLevels.TryGetValue(id, out int applied);
        Debug.Log($"[RunBootstrap] arme forcee : {id} niveau {applied}.");
    }

    /// <summary>Impose une amélioration du Hub pour la run — la sauvegarde n'est pas modifiée.</summary>
    private static void ForceMeta(string spec)
    {
        string[] parts = spec.Split(':');
        if (parts.Length < 2 || !int.TryParse(parts[1], out int level)) return;

        MetaProgression.OverrideUpgradeLevel(parts[0], Mathf.Max(0, level));
        Debug.Log($"[RunBootstrap] amelioration forcee : {parts[0]} niveau {level}.");
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
