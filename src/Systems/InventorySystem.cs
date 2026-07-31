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
    /// <summary>
    /// id de carte de surcharge → nombre de prises. Sans plafond, par construction (OverloadCards).
    /// Sert à l'affichage du niveau sur la carte ; les effets, eux, sont appliqués à chaque prise.
    /// </summary>
    public Dictionary<string, int> OverloadLevels { get; } = new();
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
        { "glaive",          "res://scenes/weapons/Glaive.tscn"         },
        { "seeker_swarm",    "res://scenes/weapons/SeekerSwarm.tscn"    },
        { "cryo_lance",      "res://scenes/weapons/CryoLance.tscn"      },
        { "pyre_stream",     "res://scenes/weapons/PyreStream.tscn"     },
        { "vector_lance",    "res://scenes/weapons/VectorLance.tscn"    },
        { "singularity",     "res://scenes/weapons/Singularity.tscn"    },
        { "fusion_blade",    "res://scenes/weapons/FusionBlade.tscn"    },
        { "rail_overcharged","res://scenes/weapons/RailOvercharged.tscn"},
        { "orbital_swarm",   "res://scenes/weapons/OrbitalSwarm.tscn"   },
        { "overload_aegis",  "res://scenes/weapons/OverloadAegis.tscn"  },
        { "ionic_storm",     "res://scenes/weapons/IonicStorm.tscn"     },
        { "solar_column",    "res://scenes/weapons/SolarColumn.tscn"    },
        { "hornet_swarm",    "res://scenes/weapons/HornetSwarm.tscn"    },
        { "vector_beam",     "res://scenes/weapons/VectorBeam.tscn"     },
        { "frost_veil",      "res://scenes/weapons/FrostVeil.tscn"      },
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

        // Arsenal à découverte : marque l'arme (active ou fusion) comme découverte à la 1re acquisition.
        GameSettings.Instance?.Discover(weaponId);
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

        // Les FUSIONS ne sont pas dans la section "weapons" : leurs stats vivent dans leur classe C#.
        // Elles passaient donc à côté de tout ce pipeline — ni niveau, ni multiplicateur de dégâts.
        if (AppliedFusions.Contains(weaponId)) { ApplyFusionStats(weaponId, level, node); return; }

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

    /// <summary>
    /// Applique niveau + multiplicateur de dégâts à une FUSION. Sa valeur de fiche reste celle que sa
    /// classe pose dans <c>_Ready</c> (chaque fusion a une mécanique propre : rafale perforante, aura
    /// continue, essaim orbital — indescriptible dans le tableau de niveaux du JSON) ; on part donc de
    /// <see cref="WeaponBase.BaseDamage"/> et on lui applique la même progression que les armes.
    ///
    /// Sans cela, une fusion gardait 22 de dégâts à vie quand l'arme qu'elle remplace en atteignait
    /// ~112 au même stade (niveau extrapolé × Noyau Thermique × améliorations du Hub) : la carte de
    /// fusion, présentée comme l'évolution ultime, divisait le DPS du joueur par 3 à 6.
    ///
    /// <paramref name="level"/> = 1 correspond aux valeurs d'origine ; au-delà, +10 %/niveau comme
    /// pour une arme au-delà de ses niveaux définis (<see cref="WeaponLeveling"/>).
    /// </summary>
    private void ApplyFusionStats(string fusionId, int level, Node node)
    {
        if (node is not WeaponBase wb) return;

        wb.CaptureBaseDamage();

        var player = GameManager.Instance?.PlayerInstance;
        float dmgMult = player?.Stats.DamageMultiplier ?? 1f;

        wb.Damage = WeaponLeveling.ExtrapolatedDamage(wb.BaseDamage, level, FusionDefinedMax) * dmgMult;

        // Réduction de recharge (Capaciteur + Hub) : appliquée depuis la cadence de fiche, jamais
        // depuis la valeur courante — RefreshWeaponCooldowns repasse ici à chaque achat de passif et
        // cumulerait sinon les réductions jusqu'à une cadence nulle.
        wb.Cooldown = ApplyCooldownReduction(wb.BaseCooldown);
    }

    /// <summary>Niveau au-delà duquel les dégâts d'une fusion sont extrapolés (elle n'a qu'un palier
    /// de stats, posé par sa classe).</summary>
    private const int FusionDefinedMax = 1;

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

            case "glaive" when node is Glaive gl:
                if (lvlData.TryGetProperty("glaiveCount", out var gc)) gl.GlaiveCount = gc.GetInt32();
                if (lvlData.TryGetProperty("range",       out var gr)) gl.Range       = gr.GetSingle();
                gl.Damage *= dmgMult;
                break;

            case "seeker_swarm" when node is SeekerSwarm ss:
                if (lvlData.TryGetProperty("missileCount",   out var mc)) ss.MissileCount    = mc.GetInt32();
                if (lvlData.TryGetProperty("projectileSpeed",out var ms)) ss.ProjectileSpeed = ms.GetSingle();
                ss.Damage *= dmgMult;
                break;

            case "cryo_lance" when node is CryoLance cl:
                if (lvlData.TryGetProperty("range",        out var clr)) cl.Range        = clr.GetSingle();
                if (lvlData.TryGetProperty("slowMult",     out var clm)) cl.SlowMult     = clm.GetSingle();
                if (lvlData.TryGetProperty("slowDuration", out var cld)) cl.SlowDuration = cld.GetSingle();
                cl.Damage *= dmgMult;
                break;

            case "pyre_stream" when node is PyreStream pyr:
                if (lvlData.TryGetProperty("coneAngle",    out var pca)) pyr.ConeAngle    = pca.GetSingle();
                if (lvlData.TryGetProperty("range",        out var prg)) pyr.Range        = prg.GetSingle();
                if (lvlData.TryGetProperty("burnDps",      out var pbd)) pyr.BurnDps      = pbd.GetSingle();
                if (lvlData.TryGetProperty("burnDuration", out var pbt)) pyr.BurnDuration = pbt.GetSingle();
                pyr.Damage *= dmgMult;
                break;

            case "vector_lance" when node is VectorLance vl:
                if (lvlData.TryGetProperty("projectileCount", out var vpc)) vl.ProjectileCount = vpc.GetInt32();
                if (lvlData.TryGetProperty("piercing",        out var vpi)) vl.IsPiercing      = vpi.GetBoolean();
                if (lvlData.TryGetProperty("projectileSpeed", out var vps)) vl.ProjectileSpeed = vps.GetSingle();
                if (lvlData.TryGetProperty("spreadDegrees",   out var vsd)) vl.SpreadDegrees   = vsd.GetSingle();
                vl.Damage *= dmgMult;
                break;

            case "singularity" when node is Singularity sg:
                if (lvlData.TryGetProperty("radius",       out var sgr)) sg.Radius       = sgr.GetSingle();
                if (lvlData.TryGetProperty("pullSpeed",    out var sgp)) sg.PullSpeed    = sgp.GetSingle();
                if (lvlData.TryGetProperty("duration",     out var sgd)) sg.Duration     = sgd.GetSingle();
                if (lvlData.TryGetProperty("tickInterval", out var sgt)) sg.TickInterval = sgt.GetSingle();
                sg.Damage *= dmgMult;
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

    /// <summary>
    /// Applique une carte de <b>surcharge</b> (progression de fin de partie, cf. <see cref="OverloadCards"/>).
    /// Aucun plafond de niveau, et aucun amortissement <see cref="PassiveScaling"/> : ces cartes
    /// répondent à une menace non bornée, les brider les ramènerait au défaut qu'elles corrigent.
    /// Les hardcaps de <see cref="StatCaps"/> ne s'appliquent pas non plus — aucune des trois stats
    /// touchées (PV max, régénération, multiplicateur de dégâts) n'en a.
    /// </summary>
    public void ApplyOverloadCard(string cardId)
    {
        var card = OverloadCards.ById(cardId);
        if (card == null) { GD.PrintErr($"[InventorySystem] Carte de surcharge inconnue : {cardId}"); return; }

        var player = GameManager.Instance.PlayerInstance;
        if (player == null) return;
        var stats = player.Stats;

        int takes = OverloadLevels.GetValueOrDefault(cardId, 0) + 1;
        OverloadLevels[cardId] = takes;

        if (card == OverloadCards.Plating)
        {
            // Soigne d'autant : sinon la carte prise à 20 % de vie n'offre qu'une plus grande barre
            // tout aussi vide, au moment précis où le joueur la choisit pour survivre.
            stats.MaxHp += card.Delta;

            // ⚠ Le soin passe par HealFlat, jamais par une écriture directe de CurrentHp : c'est le
            // SEUL chemin qui applique « Hémorragie » (cran I de saturation) et qui compte le soin dans
            // PowerTelemetry. Cette carte EST le canal de soin dominant en fin de partie — 44 prises
            // contre 1 d'Auto-réparation sur la session relevée du 2026-07-29 —, si bien qu'en écrivant
            // CurrentHp à la main elle échappait entièrement au cran qui vise précisément ce canal. Le
            // joueur ne sentait donc « aucune difficulté » au cran I (retour du 2026-07-31), et la
            // mesure au banc (−7,1 pts de temps soutenable) sous-estimait le cran d'autant.
            // Le gain de PV MAX n'est pas réduit, lui : le cran réduit les soins reçus, pas la barre.
            player.HealFlat(card.Delta);

            // Émis même si HealFlat n'a rien soigné (PV déjà pleins) : le HUD doit apprendre le
            // nouveau MAXIMUM, sinon la barre reste dessinée sur l'ancien.
            player.EmitSignal(Player.SignalName.HpChanged, stats.CurrentHp, stats.MaxHp);
        }
        else if (card == OverloadCards.Regen)
        {
            stats.HpRegenPerSecond += card.Delta;
        }
        else if (card == OverloadCards.Damage)
        {
            stats.DamageMultiplier += card.Delta;
            RefreshWeaponDamages();
        }

        GD.Print($"[InventorySystem] Surcharge {cardId} ×{takes}");
    }

    private void ApplyPassiveDelta(string passiveId, int newLevel, Player player)
    {
        if (WeaponsData == null) return;
        var stats = player.Stats;

        foreach (var passive in WeaponsData.RootElement.GetProperty("passives").EnumerateArray())
        {
            if (passive.GetProperty("id").GetString() != passiveId) continue;

            // Au-delà des niveaux définis (>3), on repart du delta du dernier niveau défini mais en
            // RENDEMENTS DÉCROISSANTS (PassiveScaling) : l'additif pur faisait franchir 100 % de
            // réduction de recharge au Capaciteur dès son niveau 8 et montait le Noyau Thermique à
            // ×4,00 — la puissance du joueur faisait ×6,4 sur 12 minutes d'overtime. Les plafonds
            // (DR 0.40, vitesse 380, réduction de recharge 0.75, cooldown plancher) restent actifs.
            //
            // EXCEPTION : les PV max (cf. Undamped ci-dessous). L'amortissement ne vise que les stats
            // dont la croissance est réellement explosive ; l'appliquer aux PV plats a fermé la
            // fenêtre d'overtime, cf. GDD §31.6.
            int definedMax = passive.GetProperty("levels").GetArrayLength();
            int lookup     = Mathf.Min(newLevel, definedMax);
            float Damped(float definedDelta) => PassiveScaling.ExtrapolatedDelta(definedDelta, newLevel, definedMax);
            float Undamped(float definedDelta) => definedDelta;

            foreach (var lvlData in passive.GetProperty("levels").EnumerateArray())
            {
                if (lvlData.GetProperty("level").GetInt32() != lookup) continue;

                switch (passiveId)
                {
                    case "thermal_core":
                        if (lvlData.TryGetProperty("damageMultiplierBonus", out var dmb))
                            stats.DamageMultiplier += Damped(dmb.GetSingle());
                        RefreshWeaponDamages();
                        break;

                    case "reinforced_plating":
                        if (lvlData.TryGetProperty("maxHpBonus",     out var hpb))
                        {
                            // NON amorti : seul levier défensif non plafonné du joueur, et le seul
                            // qui puisse encore croître après la 11e minute (DR et vitesse sont à
                            // leur cap dès la 4e). Les PV plats croissent LINÉAIREMENT face à une
                            // menace quadratique — ils n'ont jamais participé au power-creep que
                            // PassiveScaling corrige. Les amortir plafonnait les PV à 451 et
                            // ramenait la survie en overtime à ~1 min contre 5-10 visées (GDD §31.6).
                            float hpGain = Undamped(hpb.GetSingle());
                            stats.MaxHp += hpGain;
                            // Même règle que la carte Blindage : le soin qui accompagne le gain passe
                            // par HealFlat, donc par « Hémorragie » et par la télémétrie. Les PV MAX,
                            // eux, ne sont pas touchés par le cran.
                            player.HealFlat(hpGain);
                        }
                        if (lvlData.TryGetProperty("damageReduction", out var dr))
                            stats.DamageReduction = StatCaps.CapDamageReduction(stats.DamageReduction + Damped(dr.GetSingle()));
                        break;

                    case "servo_motors":
                        if (lvlData.TryGetProperty("speedBonus", out var sb))
                            stats.Speed = StatCaps.CapSpeed(stats.Speed + Damped(sb.GetSingle()));
                        break;

                    case "capacitor":
                        if (lvlData.TryGetProperty("cooldownReduction", out var cr))
                            stats.CooldownReduction = StatCaps.CapCooldownReduction(stats.CooldownReduction + Damped(cr.GetSingle()));
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

            // La fusion HÉRITE du niveau de l'arme qu'elle remplace. Repartir de 1 effaçait tous les
            // niveaux investis — et comme l'arme de base disparaît du pool de cartes et que la fusion
            // n'y entrait pas, la perte était définitive : accepter la carte « évolution » divisait
            // durablement le DPS d'un build de fin de run (mesuré : 103 DPS tout fusionné contre 410
            // avec une arme montée conservée, à niveau de joueur égal).
            int inheritedLevel = Mathf.Max(1, WeaponLevels.GetValueOrDefault(replacesId, 1));

            // Retire l'arme de base
            if (_weaponNodes.TryGetValue(replacesId, out var oldNode))
            {
                oldNode.QueueFree();
                _weaponNodes.Remove(replacesId);
                WeaponLevels.Remove(replacesId);
            }

            // Instancie la fusion
            AppliedFusions.Add(fusionId);
            WeaponLevels[fusionId] = inheritedLevel;

            var player = GameManager.Instance.PlayerInstance;
            if (player != null)
                InstantiateWeapon(fusionId, inheritedLevel, player);

            GD.Print($"[InventorySystem] Fusion appliquée : {fusionId} (niveau hérité {inheritedLevel})");
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

        // Fusions : même plafond que les armes (elles montent désormais par cartes, cf. LevelUpSystem).
        foreach (var f in WeaponsData.RootElement.GetProperty("fusions").EnumerateArray())
            if (f.GetProperty("id").GetString() == weaponId)
                return f.TryGetProperty("maxLevel", out var m) ? m.GetInt32() : FusionMaxLevel;

        return 5;
    }

    /// <summary>Plafond de niveau d'une fusion, aligné sur celui des armes de base.</summary>
    public const int FusionMaxLevel = 20;

    /// <summary>
    /// Indice de puissance du loadout : somme des <c>dégâts / recharge</c> des armes équipées, en
    /// dégâts par seconde théoriques sur une cible unique. Multiplicateur de dégâts et réductions de
    /// recharge y sont déjà inclus (ils sont posés sur les nœuds d'arme par <c>ApplyWeaponStats</c>).
    ///
    /// Approximation assumée : elle ignore le nombre de projectiles, les perforations et les
    /// mécaniques continues. Elle ne sert donc PAS à comparer deux armes entre elles, mais à suivre
    /// **l'évolution d'un même build dans le temps** — ce que le DPS mesuré sur le terrain ne permet
    /// pas (il monte tout seul quand la population d'ennemis monte). Utilisée par
    /// <see cref="PowerTelemetry"/>, aucun effet sur le gameplay.
    /// </summary>
    public float PowerIndex()
    {
        float total = 0f;
        foreach (var node in _weaponNodes.Values)
            if (node is WeaponBase wb && wb.Cooldown > 0.001f)
                total += wb.Damage / wb.Cooldown;
        return total;
    }

    /// <summary>
    /// Vrai si toutes les stats que porte ce passif sont déjà à leur plafond dur : le monter d'un
    /// niveau de plus ne changerait strictement rien. <see cref="LevelUpSystem"/> le retire alors du
    /// pool — proposer une carte sans effet vole un choix au joueur, et c'était le cas du Capaciteur
    /// et des Servomoteurs sur toute la fin de run.
    ///
    /// Le Noyau Thermique (dégâts) et le Blindage (PV) n'ont pas de plafond dur : ils rapportent
    /// toujours quelque chose, de moins en moins (cf. <see cref="PassiveScaling"/>).
    /// </summary>
    public bool IsPassiveSaturated(string passiveId)
    {
        var stats = GameManager.Instance?.PlayerInstance?.Stats;
        if (stats == null) return false;

        return passiveId switch
        {
            "capacitor"    => stats.CooldownReduction >= StatCaps.MaxCooldownReduction - 0.0001f,
            "servo_motors" => stats.Speed             >= StatCaps.MaxSpeed - 0.0001f,
            _              => false,
        };
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
        OverloadLevels.Clear();
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
