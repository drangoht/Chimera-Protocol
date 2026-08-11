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
        // La graine du banc l'emporte sur celle de l'inspecteur : c'est elle qui rend une campagne
        // APPARIÉE possible — rejouer les mêmes graines après un changement annule le bruit de tirage
        // dans la différence, et quelques paires tranchent ce que trente runs libres laissent indécis.
        ulong seed = DebugHooks.Seed ?? Seed;

        if (seed != 0UL) Gd.Seed(seed);
        else Gd.Randomize();

        SeedTheSpawner();

        // ⚠ Passe par SceneRoot et non par Time.timeScale : c'est lui qui retient la vitesse NOMINALE,
        // celle qu'une sortie de pause ou un ralenti doit restaurer. Écrire Time.timeScale en direct
        // ferait annuler l'accélération au premier menu ouvert — silencieusement, la campagne
        // continuant simplement trois fois plus lentement que prévu.
        if (DebugHooks.TimeScale > 0f)
        {
            SceneRoot.TimeScale = DebugHooks.TimeScale;
            Debug.Log($"[RunBootstrap] temps accelere x{DebugHooks.TimeScale:0.##}.");
        }

        // Le vivier de traces garde des références sur des objets détruits avec la scène précédente :
        // sans cette remise à zéro, les premiers tirs de la run les réutiliseraient — donc
        // n'afficheraient rien.
        Vfx.Reset();
        ScreenShake.Reset();

        // Même raison, sur des compteurs : gel et brûlure sont cumulés dans des champs STATIQUES, que
        // rien ne remettait à zéro. Deux runs de suite dans le même processus additionnaient donc leurs
        // totaux, et le diagnostic de la seconde partait avec les chiffres de la première.
        EnemyBase.ResetStatusCounters();

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

            // Et le CORPS de la chimère avec lui, pour la même raison — plus une seconde : un perk
            // de départ équipe une greffe d'office quelques lignes plus bas, et l'événement ne se
            // rejoue pas. Un composant ajouté après elle laisserait le joueur sans appendices toute
            // la run, sans que rien ne le signale.
            if (Player.Instance.GetComponent<ChimeraBody>() == null)
                Player.Instance.gameObject.AddComponent<ChimeraBody>();
        }

        Assimilation.ResetForRun();

        // La musique du biome démarre avec la run : les trois pistes (calme, combat, boss) tournent
        // ensemble et seuls leurs volumes bougent.
        MusicDirector.Instance?.PlayBiome(RunConfig.BiomeId);

        // Le statut Discord suit ce que le joueur fait vraiment. Le nom du biome est TRADUIT : c'est
        // le libellé que voient ses contacts, pas un identifiant interne.
        DiscordPresence.Instance?.SetInRun(
            Loc.T($"BIOME_{RunConfig.BiomeId.ToUpperInvariant()}_NAME"), RunConfig.Saturation);

        ApplyCommandLine();

        // ⚠ APRÈS ApplyCommandLine, qui peut imposer un niveau d'amélioration via `--force-meta=` :
        // lues avant, les charges vaudraient toujours celles de la sauvegarde et le drapeau serait
        // sans effet — un banc ne pourrait alors pas mesurer le cran IV sans avoir acheté les filets.
        // Et surtout pas dans un `Start` du joueur : l'ordre entre deux `Start` n'est pas garanti.
        Player.Instance?.InitSafetyNets();

        WireInventory();

        // ⚠ APRÈS le câblage de l'inventaire, et pas avant : celui-ci déclare l'arme de départ en
        // prenant la première arme trouvée sur le joueur. Un perk qui aurait déjà ajouté le Glaive
        // ferait enregistrer CELUI-CI comme arme de départ — le canon deviendrait alors une arme
        // fantôme, qui tire sans jamais monter de niveau.
        ApplyStartingPerk();

        // La saturation a besoin du point de montage pour créer les armes.
        if (_saturateArsenal || DebugHooks.SaturateArsenal) SaturateArsenal();

        AttachBenchPilot();
    }

    /// <summary>
    /// Donne au tirage de spawn la graine de <b>cette</b> run — dérivée du flux global, donc
    /// reproductible sous <c>--seed</c> et différente à chaque partie sans lui.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Sans cet appel, le spawn ne suit rien.</b> <see cref="EnemySpawner"/> tient son propre
    /// générateur, amorcé à zéro : jusqu'au 2026-08-11 personne ne le réamorçait, si bien que la
    /// faune et les élites sortaient dans le <b>même ordre à chaque partie</b> — un roguelite dont
    /// la vague de la troisième minute est toujours la même — et que deux campagnes de banc « à
    /// graines différentes » comparaient deux fois la même séquence.
    ///
    /// <para><b>Pourquoi ici et pas dans le spawner lui-même</b> : c'est le démarrage de la run qui
    /// connaît la graine, et lui seul. Le banc, qui monte sa scène sans ce composant, garde donc la
    /// séquence de la graine zéro sur laquelle ses 265 vérifications sont calibrées — et peut en
    /// choisir une autre en appelant <see cref="EnemySpawner.SeedSpawns"/>, ce à quoi cette méthode
    /// sert. Un banc qui suivrait l'aléa du jeu ne mesurerait plus rien de comparable.</para>
    /// </remarks>
    private static void SeedTheSpawner()
    {
        var spawner = FindFirstObjectByType<EnemySpawner>();
        if (spawner == null) return;

        // Dérivée, jamais recopiée : deux flux amorcés avec la MÊME graine avancent en parallèle et
        // rendent les mêmes valeurs aux mêmes moments — une corrélation invisible et indésirable.
        ulong spawnSeed = (ulong)Gd.RandRange(1, int.MaxValue);
        spawner.SeedSpawns(spawnSeed);

        // ⚠ La trace n'est pas décorative. C'est le SEUL contrôle praticable de ce câblage : mesurer
        // la variété du spawn en run réelle ne tranche pas — le pas de temps variable produit à lui
        // seul des écarts du même ordre, et deux runs de graines différentes divergent de toute
        // façon. La ligne, elle, dit exactement ce qui a été posé.
        Debug.Log($"[RunBootstrap] spawn amorce sur {spawnSeed}.");
    }

    private bool _saturateArsenal;

    /// <summary>
    /// Installe le pilote automatique sous <c>--auto-play</c> : conduite du personnage et résolution
    /// des écrans de choix.
    /// </summary>
    /// <remarks>
    /// <para>Seule la <b>conduite</b> est posée ici. La résolution des écrans de choix
    /// (<c>BenchAutoPlay</c>) s'installe elle-même depuis l'assemblage de banc : elle lit l'interface,
    /// que le jeu ne peut pas référencer sans créer un cycle. Les deux vont pourtant ensemble — un bot
    /// qui se déplace mais reste bloqué au premier passage de niveau ne mesure rien, et un bot qui
    /// prend des cartes sans bouger meurt en vingt secondes.</para>
    ///
    /// <para>Sur son <b>propre objet</b>, comme tous les outils de banc : posé sur un écran, son
    /// maintien entre scènes le ferait survivre par-dessus la partie suivante.</para>
    /// </remarks>
    private static void AttachBenchPilot()
    {
        if (!DebugHooks.AutoPlay) return;

        var host = new GameObject("[BenchPilot]");
        host.AddComponent<BenchAutoPilot>();

        Debug.Log("[RunBootstrap] --auto-play : le personnage est pilote.");
    }

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
    ///   <item><c>--biome=&lt;id&gt;</c> joue un biome précis (portage du drapeau Godot du même nom).
    ///         Le décor, la faune et l'atmosphère en dépendent entièrement : sans elle, juger une
    ///         arène demande de passer par l'écran de sélection, donc de ne jamais la juger en banc.
    ///         <b>Non persisté</b> — comme <c>--force-meta</c>, un drapeau de banc n'écrit pas dans
    ///         la sauvegarde du joueur.</item>
    /// </list>
    /// </summary>
    private void ApplyCommandLine()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg == "--saturate-arsenal") { _saturateArsenal = true; continue; }

            // ⚠ `--biome=` n'est PAS traitée ici : le décor lit le biome depuis son propre `Start`,
            // et l'ordre entre deux `Start` n'est pas garanti. Elle est lue par `RunConfig` à
            // l'initialisation de son champ, donc avant que quoi que ce soit ne l'interroge.

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
