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

        // Réinitialise les systèmes avant chaque run
        XpSystem.Instance?.Reset();
        InventorySystem.Instance?.Reset();
        LevelUpSystem.Instance?.Reset();

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

    /// <summary>Appelé par EnemyBase.Die() pour notifier la fin d'un ennemi.</summary>
    public void NotifyEnemyKilled()
    {
        EmitSignal(SignalName.EnemyKilled);
    }
}
