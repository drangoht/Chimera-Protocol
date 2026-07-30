using Godot;
using System.Linq;

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

        // Banc de mesure : --timescale=<x> accélère la run pour atteindre le boss (13 min) en
        // quelques minutes réelles. Plafonné à 4 — au-delà, les deltas deviennent assez grands pour
        // que les projectiles traversent leurs cibles et toute mesure de DPS est fausse.
        if (DebugHooks.TimeScale > 0f)
        {
            Engine.TimeScale = Mathf.Clamp(DebugHooks.TimeScale, 0.25f, 4f);
            GD.Print($"[GameManager] --timescale : temps du jeu ×{Engine.TimeScale}");
        }

        // Banc multi-run : --seed=<n> fixe le RNG global. Deux campagnes lancées sur la même liste de
        // seeds comparent des runs APPARIÉES (mêmes vagues, mêmes tirages de cartes) — l'écart observé
        // est alors imputable au réglage testé, pas au tirage. C'est le seul moyen de sortir du bruit
        // sans multiplier les runs à l'infini : sans appariement, la variance inter-run atteint un
        // facteur 2,4 sur la survie (cf. docs/TEST_REPORT.md, 2026-07-29).
        if (DebugHooks.Seed.HasValue)
        {
            GD.Seed(DebugHooks.Seed.Value);
            GD.Print($"[GameManager] --seed : RNG global fixé à {DebugHooks.Seed.Value}");
        }
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
        // `--debug-enemy=<id>` emprunte le même chemin en changeant seulement la cible : c'est le
        // moyen de valider un mid-boss de biome sans jouer 8 min par niveau (cf. GDD §32).
        if (DebugHooks.BossDebug || !string.IsNullOrEmpty(DebugHooks.DebugEnemy))
            Callable.From(ApplyBossDebugHook).CallDeferred();

        // Hook --saturate-arsenal : indépendant des deux précédents (on veut pouvoir saturer sans
        // faire apparaître de champion). Même différé, pour les mêmes raisons.
        if (DebugHooks.SaturateArsenal)
            Callable.From(ApplySaturateArsenalHook).CallDeferred();

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

        // Perk de départ équipé (débloqué via les Défis, choisi au Hub) : greffe offerte / arme
        // supplémentaire / +1 emplacement de greffe. Différé pour que le GraftManager du joueur soit
        // prêt (comme le hook --force-graft). Sans effet si aucun perk équipé.
        if (!string.IsNullOrEmpty(MetaProgressionSystem.Instance?.Meta.EquippedPerk))
            Callable.From(ApplyStartingPerkHook).CallDeferred();
    }

    /// <summary>
    /// Applique le perk de départ équipé (MetaSaveData.EquippedPerk) au début de la run. Défensif :
    /// n'applique que si le perk est réellement débloqué (garde contre une sauvegarde éditée).
    /// </summary>
    private void ApplyStartingPerkHook()
    {
        var meta = MetaProgressionSystem.Instance;
        if (meta == null) return;
        string perk = meta.Meta.EquippedPerk;
        if (perk.Length == 0 || !meta.Meta.UnlockedPerks.Contains(perk)) return;

        switch (perk)
        {
            case "start_graft_swarm":
                AssimilationSystem.Instance?.GrantStartingGraft("swarm_symbiote");
                break;
            case "start_weapon_glaive":
                var inv = InventorySystem.Instance;
                if (inv != null && !inv.WeaponLevels.ContainsKey("glaive"))
                {
                    inv.AddOrUpgradeWeapon("glaive");
                    inv.RefreshWeaponDamages();
                }
                break;
            case "start_extra_slot":
                AssimilationSystem.Instance?.AddBonusSlots(1);
                break;
        }
        GD.Print($"[GameManager] Perk de départ appliqué : {perk}");
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
    /// Hook de debug « champion isolé ». Deux usages, volontairement distincts :
    ///
    /// <list type="bullet">
    /// <item><c>--debug-boss</c> — <b>mesurer</b>. Équipe un loadout de test représentatif d'une fin
    /// de run et spawne le boss final : c'est le protocole de mesure du TTK (GDD §20.6).</item>
    /// <item><c>--debug-enemy=&lt;id&gt;</c> — <b>observer</b>. Spawne le champion demandé en laissant
    /// le loadout de départ. Un loadout de fin de run tue un mid-boss en deux secondes et son aura
    /// (Voile de Givre) recouvre l'arène : aucun de ses patterns n'est alors observable, ni à l'œil
    /// ni en capture.</item>
    /// </list>
    ///
    /// Les deux se combinent (<c>--debug-enemy=X --debug-boss</c>) pour mesurer le TTK d'un mid-boss.
    /// </summary>
    private void ApplyBossDebugHook()
    {
        var player = PlayerInstance;
        var inv = InventorySystem.Instance;
        if (player == null || inv == null) return;

        if (DebugHooks.BossDebug)
            EquipDebugTestLoadout(inv);

        SpawnDebugChampion();
    }

    /// <summary>Point d'entrée du hook <c>--saturate-arsenal</c>.</summary>
    private void ApplySaturateArsenalHook()
    {
        var inv = InventorySystem.Instance;
        if (PlayerInstance == null || inv == null) return;
        SaturateArsenal(inv);
    }

    /// <summary>
    /// Monte tout l'arsenal à son plafond (<c>--saturate-arsenal</c>) pour que le pool de
    /// <see cref="LevelUpSystem"/> soit vide dès le premier level-up et que les cartes de
    /// <see cref="OverloadCards"/> soient proposées tout de suite. Cf. <see cref="DebugHooks.SaturateArsenal"/>
    /// pour la raison d'être : le banc n'atteint jamais la saturation par le jeu normal.
    /// </summary>
    private void SaturateArsenal(InventorySystem inv)
    {
        // Une première arme doit être équipée pour que les fusions puissent s'enchaîner ; le reste
        // est entièrement piloté par le pool lui-même (LevelUpSystem.DebugDrainPool), qui sait seul
        // quand il est vide.
        int consumed = LevelUpSystem.Instance?.DebugDrainPool() ?? 0;

        GD.Print($"[GameManager] --saturate-arsenal : {consumed} cartes consommées, pool vide. " +
                 $"Armes: {string.Join(", ", inv.WeaponLevels.Select(kv => $"{kv.Key} L{kv.Value}"))} · " +
                 $"Passifs: {string.Join(", ", inv.PassiveLevels.Select(kv => $"{kv.Key} L{kv.Value}"))}");

        // Provoque le level-up que le flag sert justement à rendre observable. Nécessaire : une fois
        // l'arsenal saturé, un bot immobile (--auto-play) tue tout à distance et ne ramasse plus un
        // seul orbe d'XP — le banc restait au niveau 0 sur 300 s et l'écran ne s'ouvrait jamais. En
        // session jouée, cela évite aussi d'attendre le prochain palier pour voir les cartes.
        XpSystem.Instance?.AddXp(XpSystem.Instance.XpToNextLevel);
    }

    /// <summary>Loadout de test du protocole de mesure (cf. <see cref="ApplyBossDebugHook"/>).</summary>
    private void EquipDebugTestLoadout(InventorySystem inv)
    {

        // 1) Loadout calé sur ce qu'un joueur a RÉELLEMENT au boss, mesuré sur des runs complètes
        // (banc --auto-play, 2026-07-28) : niveau 66-84, armes L6-13, passifs L3-13, et surtout
        // plusieurs FUSIONS. L'ancien loadout (5 armes L10, un seul passif à 3, aucune fusion,
        // joueur niveau 1) sous-estimait le DPS d'un facteur ~2 et ne testait aucune fusion — donc
        // aucune mesure de TTK n'était comparable à une vraie fin de run.

        // Passifs d'abord : les fusions les exigent en prérequis.
        // Niveaux volontairement modestes : dans une vraie run, les ~70 cartes se dispersent (armes
        // L3-13, passifs L3-13, cartes d'XP). Tout monter à fond fabriquerait un build PARFAIT à
        // ~2300 DPS, le haut de la fourchette et non la médiane (~600-900 DPS mesurés).
        var passives = new (string Id, int Level)[]
        {
            ("thermal_core", 5), ("capacitor", 4), ("reinforced_plating", 6), ("servo_motors", 6),
        };
        foreach (var (id, level) in passives)
            for (int i = 0; i < level; i++)
                inv.AddOrUpgradePassive(id);

        // 5 armes montées, choisies pour couvrir 4 archétypes fusionnables (tir direct, chaîne,
        // mêlée, aura de zone) + une arme de base conservée — le cas de figure le plus fréquent.
        const int DebugWeaponLevel = 7;
        string[] weapons = { "impulse_cannon", "tesla_coil", "plasma_blade", "cryo_lance", "glaive" };
        foreach (var w in weapons)
            for (int lvl = inv.WeaponLevels.GetValueOrDefault(w, 0); lvl < DebugWeaponLevel; lvl++)
                inv.AddOrUpgradeWeapon(w);

        // Fusions : elles héritent du niveau de l'arme remplacée (cf. InventorySystem.ApplyFusion),
        // le loadout reste donc à 5 armes.
        foreach (var f in new[] { "rail_overcharged", "ionic_storm", "fusion_blade", "frost_veil" })
            if (inv.CanFuse(f)) inv.ApplyFusion(f);

        inv.RefreshWeaponDamages();
    }

    /// <summary>
    /// Fait apparaître le champion visé, seul dans l'arène, avec le scaling de SA fenêtre de spawn.
    /// </summary>
    private void SpawnDebugChampion()
    {
        if (GetTree().GetFirstNodeInGroup(Constants.GroupEnemySpawner) is not EnemySpawner spawner)
        {
            GD.PrintErr("[GameManager] debug champion : EnemySpawner introuvable dans la scène.");
            return;
        }

        // --debug-enemy=<id> cible un autre champion ; sa fenêtre de spawn réelle sert de temps
        // de scaling, sans quoi un mid-boss (8 min) apparaîtrait avec les PV d'un t=13 min.
        string targetId = DebugHooks.DebugEnemy ?? "rusted_core";
        float  tMinutes = spawner.SpawnStartMinuteOf(targetId) ?? 13f;

        spawner.AmbientEnabled = false;     // isole le champion : pas d'ennemis/XP parasites
        spawner.DebugSpawnById(targetId, tMinutes);
        GD.Print($"[GameManager] debug champion : {targetId} spawné isolé (t={tMinutes:0.#} min, "
               + $"loadout de test {(DebugHooks.BossDebug ? "équipé" : "NON équipé")}).");
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
