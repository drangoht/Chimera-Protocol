using Godot;

/// <summary>
/// AutoLoad singleton — coordinateur central de la run.
/// Tient la référence au joueur, émet le signal EnemyKilled,
/// et applique les bonus meta au début de chaque run.
/// </summary>
public partial class GameManager : Node
{
    public static GameManager Instance { get; private set; } = null!;

    public Player? PlayerInstance { get; private set; }

    /// <summary>
    /// Id de l'arme de départ sélectionnée dans le Hub.
    /// Valeur par défaut : Canon à Impulsions.
    /// Lue par InventorySystem.InitStartingWeapon() au démarrage de la run.
    /// </summary>
    public string StartingWeaponId { get; set; } = "impulse_cannon";

    /// <summary>
    /// Id du personnage sélectionné dans le Hub (registre <see cref="Characters"/>).
    /// Détermine les stats de base, la teinte et l'arme de départ par défaut.
    /// </summary>
    public string SelectedCharacterId { get; set; } = "chimera";

    /// <summary>
    /// Biome choisi dans l'écran de sélection de niveau (null = aléatoire).
    /// Lu par GroundRenderer au début de la run.
    /// </summary>
    public string? SelectedBiomeId { get; set; } = null;

    // ── Modificateurs de biome (posés par GroundRenderer au début de chaque run) ──
    /// <summary>Multiplicateur de vitesse appliqué à tous les ennemis (effet de biome).</summary>
    public float BiomeEnemySpeedMult { get; set; } = 1f;
    /// <summary>Multiplicateur d'XP gagnée (effet de biome).</summary>
    public float BiomeXpMult         { get; set; } = 1f;
    /// <summary>Couleur d'accent du biome courant (pour thématiser le HUD).</summary>
    public Color BiomeAccent         { get; set; } = new(0.30f, 0.85f, 0.95f);
    /// <summary>Nom et effet du biome courant (affichés dans le HUD).</summary>
    public string BiomeName          { get; set; } = "";
    public string BiomeEffect        { get; set; } = "";
    /// <summary>Id du biome effectivement joué (résolu par GroundRenderer, même si tiré au sort). Sert au badge de complétion.</summary>
    public string CurrentBiomeId     { get; set; } = "";

    /// <summary>Émis depuis EnemyBase.Die() — permet à RunStatsTracker de compter les kills sans couplage direct.</summary>
    [Signal] public delegate void EnemyKilledEventHandler();

    public override void _Ready()
    {
        Instance = this;
        EnsureGamepadUiBindings();
        InputRemap.EnsureExtraActions(); // action « dash » (greffe Servos Erratiques)
    }

    /// <summary>
    /// La map d'entrées par défaut de Godot 4.7 lie bien les directions UI à la manette
    /// (d-pad + stick) mais PAS la validation/l'annulation : `ui_accept`/`ui_cancel` n'ont que
    /// le clavier. Résultat : on navigue dans les menus à la manette mais le bouton A « ne fait
    /// rien ». On ajoute ici les boutons manette manquants (sans toucher aux bindings clavier).
    /// </summary>
    private static void EnsureGamepadUiBindings()
    {
        AddJoypadButton("ui_accept",      JoyButton.A);  // valider
        AddJoypadButton("ui_cancel",      JoyButton.B);  // annuler / retour
        AddJoypadButton("ui_focus_next",  JoyButton.RightShoulder);
        AddJoypadButton("ui_focus_prev",  JoyButton.LeftShoulder);

        // Action « pause » (ouvre/ferme le menu de pause en jeu) : Échap au clavier + Start manette.
        // Pas de section [input] dans project.godot → on crée l'action au boot.
        if (!InputMap.HasAction("pause")) InputMap.AddAction("pause");
        AddKey("pause", Key.Escape);
        AddJoypadButton("pause", JoyButton.Start);
    }

    private static void AddJoypadButton(string action, JoyButton button)
    {
        if (!InputMap.HasAction(action)) return;
        // Ne pas dupliquer si le binding existe déjà (idempotent).
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventJoypadButton jb && jb.ButtonIndex == button) return;
        InputMap.ActionAddEvent(action, new InputEventJoypadButton { ButtonIndex = button });
    }

    private static void AddKey(string action, Key key)
    {
        if (!InputMap.HasAction(action)) return;
        foreach (var e in InputMap.ActionGetEvents(action))
            if (e is InputEventKey k && k.Keycode == key) return;
        InputMap.ActionAddEvent(action, new InputEventKey { Keycode = key });
    }

    public void RegisterPlayer(Player player)
    {
        PlayerInstance = player;

        // Applique le personnage sélectionné AVANT tout : pose les stats de base
        // (les bonus méta s'ajouteront par-dessus) et la teinte d'identité.
        var character = Characters.Get(SelectedCharacterId);
        player.Stats.MaxHp     = character.MaxHp;
        player.Stats.CurrentHp = character.MaxHp;
        player.Stats.Speed     = character.Speed;
        player.Stats.BaseSpeed = character.Speed;
        player.SetCharacterFrames(character.FramesPath);
        player.ApplyCharacterVisual(character.Tint);

        // Statut Discord : bascule en « en run » (personnage + biome courant).
        DiscordPresence.Instance?.SetInRun(character.Name, BiomeName);

        // Réinitialise les systèmes avant chaque run
        XpSystem.Instance?.Reset();
        InventorySystem.Instance?.Reset();
        LevelUpSystem.Instance?.Reset();
        AssimilationSystem.Instance?.Reset();
        ModalQueue.Reset();

        // Gère l'arme de départ hardcodée dans Player.tscn
        foreach (var child in player.GetChildren())
        {
            if (child is ImpulseCannon cannon)
            {
                if (StartingWeaponId == "impulse_cannon")
                {
                    InventorySystem.Instance?.RegisterExistingWeapon("impulse_cannon", cannon);
                }
                else
                {
                    // Arme alternative sélectionnée — retire le canon hardcodé et instancie la bonne arme
                    cannon.QueueFree();
                    InventorySystem.Instance?.AddOrUpgradeWeapon(StartingWeaponId);
                }
                break;
            }
        }

        // Applique les bonus meta permanents dès que le joueur s'enregistre
        MetaProgressionSystem.Instance?.ApplyMetaBonusesToStats(player.Stats);

        // L'arme de départ a été instanciée AVANT que le multiplicateur de dégâts méta ne soit
        // posé sur les stats — on ré-applique les stats des armes équipées pour qu'elles en
        // bénéficient (sinon Vagabond/Titan/Chimera démarrent sans le bonus de dégâts du Hub).
        InventorySystem.Instance?.RefreshWeaponDamages();

        // Hook de debug --debug-boss : loadout de test + spawn immédiat du boss final.
        // Différé pour laisser tous les _Ready de la scène passer (EnemySpawner doit avoir
        // chargé enemies.json, les armes doivent pouvoir s'instancier proprement).
        // Aucun effet sans le flag.
        if (DebugHooks.BossDebug)
            Callable.From(ApplyBossDebugHook).CallDeferred();

        // Hook --force-fusion=<id|all> : équipe d'office une (ou les deux) fusion(s) de greffes
        // pour valider leur ressenti/équilibrage sans grinder les jauges. Aucun effet sans le flag.
        if (!string.IsNullOrEmpty(DebugHooks.ForcedFusion))
            Callable.From(ApplyFusionDebugHook).CallDeferred();

        // Hook --force-graft=<id|all> : équipe d'office une (ou les 5) greffe(s) de base pour valider
        // visuellement les props de silhouette (Phase B). Aucun effet sans le flag.
        if (!string.IsNullOrEmpty(DebugHooks.ForcedGraft))
            Callable.From(ApplyGraftDebugHook).CallDeferred();

        // Hook --force-buff : 2e arme + 2 power-ups quasi-permanents pour valider la BuffBar HUD
        // (position sous le loadout, pas de chevauchement). Aucun effet sans le flag.
        if (DebugHooks.ForceBuff)
            Callable.From(ApplyBuffDebugHook).CallDeferred();
    }

    /// <summary>
    /// Hook --force-buff : équipe une 2e arme (scatter_volley) et applique Overclock + Berserk avec une
    /// durée quasi-infinie pour que la BuffBar reste visible en même temps que le loadout d'armes.
    /// N'est appelé que si <see cref="DebugHooks.ForceBuff"/> est vrai.
    /// </summary>
    private void ApplyBuffDebugHook()
    {
        var player = PlayerInstance;
        var inv = InventorySystem.Instance;
        if (player == null || inv == null) return;

        if (!inv.WeaponLevels.ContainsKey("scatter_volley"))
            inv.AddOrUpgradeWeapon("scatter_volley");

        const float LongDuration = 9999f;
        player.ApplyPowerUp(PowerUpType.Overclock, LongDuration);
        player.ApplyPowerUp(PowerUpType.Berserk,   LongDuration);
        GD.Print("[GameManager] --force-buff : 2e arme + Overclock/Berserk appliqués (BuffBar de test).");
    }

    /// <summary>
    /// Hook --force-graft : équipe la (ou les) greffe(s) demandée(s) via AssimilationSystem.DebugForceGraft.
    /// <c>all</c> équipe les 5 greffes de base. N'est appelé que si le flag est présent.
    /// </summary>
    private void ApplyGraftDebugHook()
    {
        var sys = AssimilationSystem.Instance;
        if (sys == null) return;
        sys.DebugForceGraft(DebugHooks.ForcedGraft!);
    }

    /// <summary>
    /// Hook --force-fusion : équipe la ou les fusions demandées via AssimilationSystem.DebugForceFusion.
    /// <c>all</c> équipe les deux fusions livrées. N'est appelé que si le flag est présent.
    /// </summary>
    private void ApplyFusionDebugHook()
    {
        var sys = AssimilationSystem.Instance;
        if (sys == null) return;
        string arg = DebugHooks.ForcedFusion!;
        if (arg == "all")
        {
            sys.DebugForceFusion("fusion_charge_blindee");
            sys.DebugForceFusion("fusion_ruche_tourelles");
            sys.DebugForceFusion("fusion_nova_rodeur");
        }
        else
        {
            sys.DebugForceFusion(arg);
        }
    }

    /// <summary>
    /// Hook --debug-boss : équipe un loadout de test représentatif (5 armes niv.10 +
    /// thermal_core ×1.45) et force le spawn immédiat du boss final à son PV réel (scaling t=13 min).
    /// N'est appelé que si <see cref="DebugHooks.BossDebug"/> est vrai.
    /// </summary>
    private void ApplyBossDebugHook()
    {
        var player = PlayerInstance;
        var inv = InventorySystem.Instance;
        if (player == null || inv == null) return;

        // 1) Loadout de référence du game-designer (cellule TTK ~28 s) : 5 armes au niveau 10.
        const int DebugWeaponLevel = 10;
        string[] weapons = { "impulse_cannon", "scatter_volley", "drone_swarm", "tesla_coil", "plasma_blade" };
        foreach (var w in weapons)
            for (int lvl = inv.WeaponLevels.GetValueOrDefault(w, 0); lvl < DebugWeaponLevel; lvl++)
                inv.AddOrUpgradeWeapon(w);

        // Noyau Thermique à ses 3 niveaux DÉFINIS (×1.45) — pas au max extrapolé (L20 = ×4.0,
        // non représentatif). Aligne le DPS de test sur l'hypothèse d'équilibrage du game-designer.
        const int DebugThermalLevel = 3;
        for (int i = 0; i < DebugThermalLevel; i++)
            inv.AddOrUpgradePassive("thermal_core");

        // 2) Spawn immédiat du boss à son PV réel (même scaling temporel qu'à t=13 min).
        var spawner = GetTree().GetFirstNodeInGroup(Constants.GroupEnemySpawner) as EnemySpawner;
        if (spawner != null)
        {
            spawner.AmbientEnabled = false; // isole le boss : pas d'ennemis/XP parasites
            spawner.DebugSpawnById("rusted_core", 13f);
            GD.Print("[GameManager] --debug-boss : loadout de test équipé + rusted_core spawné isolé (t=13 min).");
        }
        else
        {
            GD.PrintErr("[GameManager] --debug-boss : EnemySpawner introuvable dans la scène.");
        }
    }

    /// <summary>Appelé par EnemyBase.Die() (et les Die() surchargés) pour notifier la fin d'un ennemi.
    /// Route aussi le kill vers le système d'Assimilation (jauge de greffe via l'archétype/champion).
    /// <paramref name="enemy"/> est optionnel (rétro-compat) : sans lui, seul le signal EnemyKilled part.</summary>
    public void NotifyEnemyKilled(EnemyBase? enemy = null)
    {
        EmitSignal(SignalName.EnemyKilled);

        if (enemy != null)
            AssimilationSystem.Instance?.OnEnemyKilled(
                enemy.AssimArchetype, enemy.IsElite, enemy.AssimIsMiniBoss, enemy.AssimIsBoss);
    }
}
