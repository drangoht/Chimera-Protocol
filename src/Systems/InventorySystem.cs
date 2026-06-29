using Godot;
using System.Collections.Generic;
using System.Text.Json;

/// <summary>
/// Singleton AutoLoad gérant l'inventaire d'armes et de passifs du joueur pendant un run.
/// Charge weapons.json et levelup_config.json au démarrage.
/// </summary>
public partial class InventorySystem : Node
{
    public static InventorySystem Instance { get; private set; } = null!;

    // id → niveau actuel (0 = non possédé)
    public Dictionary<string, int> WeaponLevels  { get; } = new();
    public Dictionary<string, int> PassiveLevels { get; } = new();
    // id fusion → true si appliquée
    public HashSet<string> AppliedFusions { get; } = new();

    /// <summary>Nombre d'armes actuellement équipées (fusions incluses, qui occupent un slot).</summary>
    public int EquippedWeaponCount => WeaponLevels.Count;
    /// <summary>Nombre maximum d'armes équipées simultanément.</summary>
    public const int MaxEquippedWeapons = 5;

    // Données JSON chargées
    public JsonDocument? WeaponsData { get; private set; }

    // Références aux nœuds d'armes actifs dans la scène du joueur
    private readonly Dictionary<string, Node> _weaponNodes = new();

    // Scènes d'armes pré-chargées
    private static readonly Dictionary<string, string> WeaponScenePaths = new()
    {
        { "impulse_cannon",  "res://scenes/weapons/ImpulseCannon.tscn"  },
        { "plasma_blade",    "res://scenes/weapons/PlasmaBlade.tscn"    },
        { "drone_swarm",     "res://scenes/weapons/DroneSwarm.tscn"     },
        { "overload_field",  "res://scenes/weapons/OverloadField.tscn"  },
        { "tesla_coil",      "res://scenes/weapons/TeslaCoil.tscn"      },
        { "scatter_volley",  "res://scenes/weapons/ScatterVolley.tscn"  },
        { "fusion_blade",    "res://scenes/weapons/FusionBlade.tscn"    },
        { "rail_overcharged","res://scenes/weapons/RailOvercharged.tscn"},
        { "orbital_swarm",   "res://scenes/weapons/OrbitalSwarm.tscn"   },
        { "overload_aegis",  "res://scenes/weapons/OverloadAegis.tscn"  },
    };

    public override void _Ready()
    {
        Instance = this;
        LoadWeaponsJson();
    }

    private void LoadWeaponsJson()
    {
        using var file = Godot.FileAccess.Open("res://data/weapons.json", Godot.FileAccess.ModeFlags.Read);
        if (file == null)
        {
            GD.PrintErr("[InventorySystem] Impossible de lire data/weapons.json");
            return;
        }
        string json = file.GetAsText();
        WeaponsData = JsonDocument.Parse(json);
    }

    // -------------------------------------------------------------------------
    // Armes
    // -------------------------------------------------------------------------

    /// <summary>
    /// Si l'arme n'est pas possédée, l'instancie et l'ajoute au joueur.
    /// Si déjà possédée, monte le niveau et met à jour les stats.
    /// </summary>
    public void AddOrUpgradeWeapon(string weaponId)
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        int currentLevel = WeaponLevels.GetValueOrDefault(weaponId, 0);
        int maxLevel = GetWeaponMaxLevel(weaponId);

        if (currentLevel >= maxLevel)
        {
            GD.PrintErr($"[InventorySystem] Arme {weaponId} déjà au niveau max.");
            return;
        }

        int newLevel = currentLevel + 1;
        WeaponLevels[weaponId] = newLevel;

        if (currentLevel == 0)
        {
            // Première acquisition : instancier la scène
            InstantiateWeapon(weaponId, newLevel, player);
        }
        else
        {
            // Upgrade : mettre à jour les stats de l'arme existante
            UpgradeWeaponNode(weaponId, newLevel);
        }

        GD.Print($"[InventorySystem] Arme {weaponId} niveau {newLevel}");
    }

    private void InstantiateWeapon(string weaponId, int level, Player player)
    {
        if (!WeaponScenePaths.TryGetValue(weaponId, out string? path)) return;

        var scene = GD.Load<PackedScene>(path);
        if (scene == null)
        {
            GD.PrintErr($"[InventorySystem] Scène introuvable : {path}");
            return;
        }

        var node = scene.Instantiate<Node>();
        player.AddChild(node);
        _weaponNodes[weaponId] = node;

        ApplyWeaponStats(weaponId, level, node);
        TriggerWeaponEquipVfx(player, weaponId);
    }

    private void UpgradeWeaponNode(string weaponId, int level)
    {
        if (!_weaponNodes.TryGetValue(weaponId, out var node)) return;
        ApplyWeaponStats(weaponId, level, node);
        TriggerWeaponUpgradeVfx(weaponId, level);
    }

    private static void TriggerWeaponEquipVfx(Player player, string weaponId)
    {
        var tween = player.CreateTween();
        tween.TweenProperty(player, "modulate", Colors.White, 0.45f)
             .From(new Color(2.8f, 1.8f, 0.3f, 1f));
        HUD.Instance?.ShowWeaponEquipped(weaponId, Codex.DisplayName(weaponId));
    }

    private static void TriggerWeaponUpgradeVfx(string weaponId, int level)
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player != null)
        {
            var tween = player.CreateTween();
            tween.TweenProperty(player, "modulate", Colors.White, 0.35f)
                 .From(new Color(0.3f, 2.5f, 2.2f, 1f));
        }
        HUD.Instance?.ShowWeaponUpgraded(weaponId, Codex.DisplayName(weaponId), level);
    }

    private static void TriggerPassiveVfx(Player player, string passiveId, int level)
    {
        var tween = player.CreateTween();
        tween.TweenProperty(player, "modulate", Colors.White, 0.4f)
             .From(new Color(1.5f, 0.5f, 2.8f, 1f));
        string display = level == 1
            ? Codex.DisplayName(passiveId)
            : $"{Codex.DisplayName(passiveId)}  Niv.{level}";
        HUD.Instance?.ShowPassiveAcquired(passiveId, display);
    }

    private void ApplyWeaponStats(string weaponId, int level, Node node)
    {
        if (WeaponsData == null) return;

        // Cherche l'arme dans le JSON
        foreach (var weapon in WeaponsData.RootElement.GetProperty("weapons").EnumerateArray())
        {
            if (weapon.GetProperty("id").GetString() != weaponId) continue;

            var levels     = weapon.GetProperty("levels");
            int definedMax = levels.GetArrayLength();
            // Au-delà des niveaux définis (>5), on réutilise le dernier niveau défini et on
            // extrapole les dégâts (+10%/niveau) ; les mécaniques (projectiles, chaînes…) plafonnent.
            int lookup     = Mathf.Min(level, definedMax);

            foreach (var lvlData in levels.EnumerateArray())
            {
                if (lvlData.GetProperty("level").GetInt32() != lookup) continue;

                if (node is WeaponBase wb)
                {
                    if (lvlData.TryGetProperty("damage", out var d))
                        wb.Damage = WeaponLeveling.ExtrapolatedDamage(d.GetSingle(), level, definedMax);
                    if (lvlData.TryGetProperty("cooldown", out var c)) wb.Cooldown = ApplyCooldownReduction(c.GetSingle());
                }

                // Armes spécialisées
                ApplySpecializedStats(weaponId, lvlData, node, lookup);
                break;
            }
            break;
        }
    }

    private void ApplySpecializedStats(string weaponId, JsonElement lvlData, Node node, int level)
    {
        var player = GameManager.Instance.PlayerInstance;
        float dmgMult = player?.Stats.DamageMultiplier ?? 1f;

        switch (weaponId)
        {
            case "impulse_cannon" when node is ImpulseCannon ic:
                if (lvlData.TryGetProperty("projectileCount", out var pc)) ic.ProjectileCount = pc.GetInt32();
                if (lvlData.TryGetProperty("piercing",        out var pi)) ic.IsPiercing      = pi.GetBoolean();
                if (lvlData.TryGetProperty("projectileSpeed", out var ps)) ic.ProjectileSpeed  = ps.GetSingle();
                ic.Damage *= dmgMult;
                break;

            case "scatter_volley" when node is ScatterVolley sv:
                if (lvlData.TryGetProperty("projectileCount", out var spc)) sv.ProjectileCount = spc.GetInt32();
                if (lvlData.TryGetProperty("piercing",        out var spi)) sv.IsPiercing      = spi.GetBoolean();
                if (lvlData.TryGetProperty("projectileSpeed", out var sps)) sv.ProjectileSpeed = sps.GetSingle();
                sv.Damage *= dmgMult;
                break;

            case "plasma_blade" when node is PlasmaBlade pb:
                if (lvlData.TryGetProperty("arcAngleDegrees", out var arc)) pb.ArcAngleDeg = arc.GetSingle();
                if (lvlData.TryGetProperty("arcRadius",       out var rad)) pb.ArcRadius   = rad.GetSingle();
                pb.Damage *= dmgMult;
                break;

            case "drone_swarm" when node is DroneSwarm ds:
                if (lvlData.TryGetProperty("droneCount",         out var cnt))  ds.DroneCount       = cnt.GetInt32();
                if (lvlData.TryGetProperty("orbitSpeedDegPerSec",out var spd))  ds.OrbitSpeedDeg    = spd.GetSingle();
                if (lvlData.TryGetProperty("damageInterval",     out var di))   ds.DamageInterval   = di.GetSingle();
                ds.Damage *= dmgMult;
                break;

            case "overload_field" when node is OverloadField of:
                if (lvlData.TryGetProperty("radius",     out var r))  of.Radius    = r.GetSingle();
                if (lvlData.TryGetProperty("knockbackPx",out var kb)) of.Knockback = kb.GetSingle();
                of.Damage *= dmgMult;
                break;

            case "tesla_coil" when node is TeslaCoil tc:
                if (lvlData.TryGetProperty("chainCount", out var cc)) tc.ChainCount = cc.GetInt32();
                if (lvlData.TryGetProperty("chainRange", out var crg)) tc.ChainRange = crg.GetSingle();
                tc.Damage *= dmgMult;
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Passifs
    // -------------------------------------------------------------------------

    public void AddOrUpgradePassive(string passiveId)
    {
        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;

        int currentLevel = PassiveLevels.GetValueOrDefault(passiveId, 0);
        int maxLevel = GetPassiveMaxLevel(passiveId);

        if (currentLevel >= maxLevel)
        {
            GD.PrintErr($"[InventorySystem] Passif {passiveId} déjà au niveau max.");
            return;
        }

        int newLevel = currentLevel + 1;
        PassiveLevels[passiveId] = newLevel;

        ApplyPassiveDelta(passiveId, newLevel, player);
        TriggerPassiveVfx(player, passiveId, newLevel);
        GD.Print($"[InventorySystem] Passif {passiveId} niveau {newLevel}");
    }

    private void ApplyPassiveDelta(string passiveId, int newLevel, Player player)
    {
        if (WeaponsData == null) return;
        var stats = player.Stats;

        foreach (var passive in WeaponsData.RootElement.GetProperty("passives").EnumerateArray())
        {
            if (passive.GetProperty("id").GetString() != passiveId) continue;

            // Au-delà des niveaux définis (>3), on réapplique le delta du dernier niveau défini
            // à chaque montée (les plafonds — DR 0.40, vitesse 380, cooldown min — restent actifs).
            int definedMax = passive.GetProperty("levels").GetArrayLength();
            int lookup     = Mathf.Min(newLevel, definedMax);

            foreach (var lvlData in passive.GetProperty("levels").EnumerateArray())
            {
                if (lvlData.GetProperty("level").GetInt32() != lookup) continue;

                switch (passiveId)
                {
                    case "thermal_core":
                        if (lvlData.TryGetProperty("damageMultiplierBonus", out var dmb))
                            stats.DamageMultiplier += dmb.GetSingle();
                        RefreshWeaponDamages();
                        break;

                    case "reinforced_plating":
                        if (lvlData.TryGetProperty("maxHpBonus",     out var hpb))
                        {
                            stats.MaxHp    += hpb.GetSingle();
                            stats.CurrentHp = Mathf.Min(stats.CurrentHp + hpb.GetSingle(), stats.MaxHp);
                        }
                        if (lvlData.TryGetProperty("damageReduction", out var dr))
                            stats.DamageReduction = StatCaps.CapDamageReduction(stats.DamageReduction + dr.GetSingle());
                        break;

                    case "servo_motors":
                        if (lvlData.TryGetProperty("speedBonus", out var sb))
                            stats.Speed = StatCaps.CapSpeed(stats.Speed + sb.GetSingle());
                        break;

                    case "capacitor":
                        if (lvlData.TryGetProperty("cooldownReduction", out var cr))
                            stats.CooldownReduction = StatCaps.CapCooldownReduction(stats.CooldownReduction + cr.GetSingle());
                        // Recalcule les cooldowns des armes actives
                        RefreshWeaponCooldowns();
                        break;
                }
                break;
            }
            break;
        }
    }

    // -------------------------------------------------------------------------
    // Fusions
    // -------------------------------------------------------------------------

    public bool CanFuse(string fusionId)
    {
        if (WeaponsData == null) return false;

        foreach (var fusion in WeaponsData.RootElement.GetProperty("fusions").EnumerateArray())
        {
            if (fusion.GetProperty("id").GetString() != fusionId) continue;

            var req = fusion.GetProperty("requires");
            string reqWeapon   = req.GetProperty("weapon").GetString()!;
            int    reqWeaponLv = req.GetProperty("weaponLevel").GetInt32();
            string reqPassive  = req.GetProperty("passive").GetString()!;

            int weaponLevel   = WeaponLevels.GetValueOrDefault(reqWeapon,  0);
            int passiveLevel  = PassiveLevels.GetValueOrDefault(reqPassive, 0);

            return weaponLevel >= reqWeaponLv && passiveLevel >= 1;
        }
        return false;
    }

    public void ApplyFusion(string fusionId)
    {
        if (!CanFuse(fusionId)) return;
        if (AppliedFusions.Contains(fusionId)) return;

        if (WeaponsData == null) return;

        foreach (var fusion in WeaponsData.RootElement.GetProperty("fusions").EnumerateArray())
        {
            if (fusion.GetProperty("id").GetString() != fusionId) continue;

            string replacesId = fusion.GetProperty("replaces").GetString()!;

            // Retire l'arme de base
            if (_weaponNodes.TryGetValue(replacesId, out var oldNode))
            {
                oldNode.QueueFree();
                _weaponNodes.Remove(replacesId);
                WeaponLevels.Remove(replacesId);
            }

            // Instancie la fusion
            AppliedFusions.Add(fusionId);
            WeaponLevels[fusionId] = 1;

            var player = GameManager.Instance.PlayerInstance;
            if (player != null)
                InstantiateWeapon(fusionId, 1, player);

            GD.Print($"[InventorySystem] Fusion appliquée : {fusionId}");
            break;
        }
    }

    // -------------------------------------------------------------------------
    // Utilitaires
    // -------------------------------------------------------------------------

    public int GetWeaponMaxLevel(string weaponId)
    {
        if (WeaponsData == null) return 5;
        foreach (var w in WeaponsData.RootElement.GetProperty("weapons").EnumerateArray())
            if (w.GetProperty("id").GetString() == weaponId)
                return w.GetProperty("maxLevel").GetInt32();
        return 5;
    }

    public int GetPassiveMaxLevel(string passiveId)
    {
        if (WeaponsData == null) return 3;
        foreach (var p in WeaponsData.RootElement.GetProperty("passives").EnumerateArray())
            if (p.GetProperty("id").GetString() == passiveId)
                return p.GetProperty("maxLevel").GetInt32();
        return 3;
    }

    private float ApplyCooldownReduction(float baseCooldown)
    {
        var player = GameManager.Instance.PlayerInstance;
        float cr = player?.Stats.CooldownReduction ?? 0f;
        return StatCaps.EffectiveCooldown(baseCooldown, cr);
    }

    private void RefreshWeaponCooldowns()
    {
        foreach (var (weaponId, node) in _weaponNodes)
        {
            if (node is WeaponBase)
            {
                int level = WeaponLevels.GetValueOrDefault(weaponId, 1);
                ApplyWeaponStats(weaponId, level, node);
            }
        }
    }

    public void RefreshWeaponDamages()
    {
        // ApplyWeaponStats repart de la valeur brute JSON puis ApplySpecializedStats
        // multiplie par DamageMultiplier courant — pas de double-application.
        foreach (var (weaponId, node) in _weaponNodes)
        {
            if (node is WeaponBase)
            {
                int level = WeaponLevels.GetValueOrDefault(weaponId, 1);
                ApplyWeaponStats(weaponId, level, node);
            }
        }
    }

    /// <summary>Réinitialise l'inventaire entre deux runs.</summary>
    public void Reset()
    {
        foreach (var node in _weaponNodes.Values)
            if (IsInstanceValid(node)) node.QueueFree();
        _weaponNodes.Clear();
        WeaponLevels.Clear();
        PassiveLevels.Clear();
        AppliedFusions.Clear();
    }

    /// <summary>Niveau courant d'une arme (1 mini pour le calcul d'intensité VFX).</summary>
    public int GetWeaponLevel(string weaponId) => Mathf.Max(1, WeaponLevels.GetValueOrDefault(weaponId, 1));

    /// <summary>
    /// Puissance totale du build = somme des niveaux d'armes. Sert à l'intensité
    /// globale des VFX (aura joueur, screen shake, brillance). Min 1.
    /// </summary>
    public int TotalWeaponPower
    {
        get
        {
            int sum = 0;
            foreach (var lvl in WeaponLevels.Values) sum += lvl;
            return Mathf.Max(1, sum);
        }
    }

    /// <summary>
    /// Enregistre une arme déjà présente dans la scène (depuis Player.tscn) sans l'instancier.
    /// Permet à InventorySystem de connaître l'arme de départ au niveau 1.
    /// </summary>
    public void RegisterExistingWeapon(string weaponId, Node node)
    {
        WeaponLevels[weaponId] = 1;
        _weaponNodes[weaponId] = node;
        GD.Print($"[InventorySystem] Arme existante enregistrée : {weaponId} niveau 1");
    }
}
