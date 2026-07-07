using Godot;
using System.Collections.Generic;

/// <summary>
/// Applique les effets des greffes équipées côté joueur (SRP : la décision chiffrée est dans
/// GraftTable/AssimilationSystem, l'application est ici). Enfant du Player. Gère les modificateurs
/// de stats (avec retrait propre), les comportements (mini-essaims orbitants, tourelle, thorns, onde)
/// et la teinte additive cumulée sur le SelfModulate du joueur. Le dash est délégué au Player
/// (il lit l'entrée et déplace le corps). Voir docs/DESIGN_ASSIMILATION.md §14.
/// </summary>
public partial class GraftManager : Node2D
{
    private Player _player = null!;

    // Greffes actives (id → def).
    private readonly Dictionary<string, GraftTable.GraftDef> _active = new();

    // Deltas de stats effectivement appliqués (pour un retrait exact malgré les hardcaps).
    private struct StatDelta { public float MaxHp; public float DamageReduction; }
    private readonly Dictionary<string, StatDelta> _statDeltas = new();

    // ── Mini-essaims orbitants (swarm_symbiote) ──
    private readonly List<Node2D> _orbiters = new();
    private readonly Dictionary<EnemyBase, float> _orbiterRehit = new();
    private float _orbitAngle;
    private int   _swarmCount;
    private float _swarmRadius, _swarmAngularDeg, _swarmDamage, _swarmRehit, _swarmLifesteal;
    private bool  _swarmActive;

    // ── Tourelle (aiming_eye) ──
    private bool  _turretActive;
    private float _turretTimer, _turretCd, _turretCdFloor, _turretDamage, _turretRange, _turretSpeed;
    private bool  _turretCdr, _turretPierce;

    // ── Thorns (grafted_carapace) ──
    private bool  _thornsActive;
    private float _thornsTimer, _thornsRehit, _thornsDamage, _thornsRadius;

    // ── Onde de choc (stalker_wave) ──
    private bool  _shockActive;
    private float _shockTimer, _shockCd, _shockDamage, _shockRadius, _shockKnockback;
    private bool  _shockCdr;

    // ── Tourelles (fusion Ruche de Tourelles) ──
    private bool  _turretsActive;
    private readonly List<Node2D> _turrets = new();
    private readonly List<Line2D> _turretLines = new(); // liens d'ancrage joueur↔tourelle (lisibilité)
    private readonly List<float>  _turretsTimer = new();
    private readonly Dictionary<EnemyBase, float> _turretsContactRehit = new();
    private int   _turretsCount;
    private float _turretsAnchorR, _turretsFollow, _turretsCd, _turretsCdFloor, _turretsDamage;
    private float _turretsRange, _turretsSpeed, _turretsLifesteal, _turretsContactDmg, _turretsContactRehitSec;
    private bool  _turretsCdr, _turretsPierce;

    // ── Props de silhouette (Phase B) : éléments visuels attachés au joueur qui matérialisent
    // la chimère (carapace, servos, œil, résonateur, proue de charge, cœur de ruche). Procéduraux
    // et ombrés pseudo-3D (lumière haut-gauche, cf. ART_BRIEF_PSEUDO3D §1-2), ancrés en espace
    // local du joueur, miroir selon le facing. Le swarm/les tourelles servent déjà de silhouette. ──
    private readonly List<GraftProp> _props = new();
    private float _propBob; // phase d'oscillation partagée (respiration des props)

    private static PackedScene? _bulletScene;
    private static PackedScene? _shockwaveScene;

    public void Init(Player player) => _player = player;

    // -------------------------------------------------------------------------
    // Équipement / retrait
    // -------------------------------------------------------------------------

    public void Equip(GraftTable.GraftDef def)
    {
        if (def == null || _active.ContainsKey(def.Id)) return;
        _active[def.Id] = def;
        ApplyStatMods(def);
        RebuildBehaviors();
        RecomputeTint();
    }

    public void Unequip(string graftId)
    {
        if (!_active.ContainsKey(graftId)) return;
        ReverseStatMods(graftId);
        _active.Remove(graftId);
        RebuildBehaviors();
        RecomputeTint();
    }

    private void ApplyStatMods(GraftTable.GraftDef def)
    {
        var stats = _player.Stats;
        var d = new StatDelta();

        float hpAdd = (float)def.Stat("maxHpAdd");
        if (hpAdd != 0f) { stats.MaxHp += hpAdd; d.MaxHp = hpAdd; } // +PV max sans heal

        float drAdd = (float)def.Stat("damageReductionAdd");
        if (drAdd != 0f)
        {
            float before = stats.DamageReduction;
            stats.DamageReduction = StatCaps.CapDamageReduction(before + drAdd);
            d.DamageReduction = stats.DamageReduction - before; // delta réellement appliqué (post-cap)
        }

        _statDeltas[def.Id] = d;
        RecomputeSpeedMult();
    }

    private void ReverseStatMods(string graftId)
    {
        var stats = _player.Stats;
        if (_statDeltas.TryGetValue(graftId, out var d))
        {
            if (d.MaxHp != 0f)
            {
                stats.MaxHp = Mathf.Max(1f, stats.MaxHp - d.MaxHp);
                if (stats.CurrentHp > stats.MaxHp) stats.CurrentHp = stats.MaxHp;
                _player.EmitSignal(Player.SignalName.HpChanged, stats.CurrentHp, stats.MaxHp);
            }
            if (d.DamageReduction != 0f)
                stats.DamageReduction = Mathf.Max(0f, stats.DamageReduction - d.DamageReduction);
            _statDeltas.Remove(graftId);
        }
        RecomputeSpeedMult();
    }

    /// <summary>Recalcule le multiplicateur de vitesse (produit des speedMult actifs) — ne touche jamais MaxSpeed.</summary>
    private void RecomputeSpeedMult()
    {
        float mult = 1f;
        foreach (var def in _active.Values)
        {
            float m = (float)def.Stat("speedMult", 1.0);
            if (m > 0f) mult *= m;
        }
        _player.GraftSpeedMultiplier = mult;
    }

    // -------------------------------------------------------------------------
    // Reconstruction des comportements (idempotent)
    // -------------------------------------------------------------------------

    private void RebuildBehaviors()
    {
        // Purge des mini-essaims existants.
        foreach (var o in _orbiters) if (IsInstanceValid(o)) o.QueueFree();
        _orbiters.Clear();
        _orbiterRehit.Clear();
        // Purge des tourelles existantes (les liens d'ancrage sont enfants → libérés avec).
        foreach (var t in _turrets) if (IsInstanceValid(t)) t.QueueFree();
        _turrets.Clear();
        _turretLines.Clear();
        _turretsTimer.Clear();
        _turretsContactRehit.Clear();
        // Purge des props de silhouette (reconstruits ci-dessous).
        foreach (var p in _props) if (IsInstanceValid(p.Node)) p.Node.QueueFree();
        _props.Clear();

        _swarmActive = _turretActive = _thornsActive = _shockActive = _turretsActive = false;
        bool dash = false;

        foreach (var def in _active.Values)
        {
            if (def.HasEffect("orbitingAllies")) SetupSwarm(def);
            if (def.HasEffect("dash"))   { SetupDash(def);   dash = true; }
            if (def.HasEffect("charge")) { SetupCharge(def); dash = true; } // fusion : le dash devient une charge
            if (def.HasEffect("autoTurret")) SetupTurret(def);
            if (def.HasEffect("thorns")) SetupThorns(def);
            if (def.HasEffect("shockwave")) SetupShockwave(def);
            if (def.HasEffect("turrets")) SetupTurrets(def);
            BuildPropFor(def); // silhouette-chimère (Phase B)
        }

        if (!dash) _player.DisableDash();
    }

    private void SetupSwarm(GraftTable.GraftDef def)
    {
        _swarmActive       = true;
        _swarmCount        = Mathf.Max(1, (int)def.Effect("orbitingAllies", "count", 3));
        _swarmRadius       = (float)def.Effect("orbitingAllies", "orbitRadiusPx", 44);
        _swarmAngularDeg   = (float)def.Effect("orbitingAllies", "angularSpeedDegPerSec", 150);
        _swarmDamage       = (float)def.Effect("orbitingAllies", "contactDamage", 5);
        _swarmRehit        = (float)def.Effect("orbitingAllies", "rehitIntervalSec", 0.5);
        _swarmLifesteal    = (float)def.Effect("orbitingAllies", "lifestealFraction", 0.04);
        bool scales        = def.Effect("orbitingAllies", "scalesWithDamageMultiplier", 1) != 0;
        if (scales) _swarmDamage *= _player.Stats.DamageMultiplier;

        var col = TintColor(def);
        for (int i = 0; i < _swarmCount; i++)
        {
            var o = new Node2D { ZIndex = 4 };
            var poly = new Polygon2D
            {
                Polygon = new Vector2[] { new(6, 0), new(0, -6), new(-6, 0), new(0, 6) },
                Color   = col,
            };
            o.AddChild(poly);
            AddChild(o);
            _orbiters.Add(o);
        }
    }

    private void SetupDash(GraftTable.GraftDef def)
    {
        _player.EnableDash(
            distance: (float)def.Effect("dash", "distancePx", 180),
            duration: (float)def.Effect("dash", "durationSec", 0.18),
            cooldown: (float)def.Effect("dash", "cooldownSec", 3.5),
            cooldownFloor: (float)def.Effect("dash", "cooldownFloorSec", 1.5),
            iframes: (float)def.Effect("dash", "iframesSec", 0.25),
            affectedByCdr: def.Effect("dash", "affectedByCooldownReduction", 1) != 0);
    }

    /// <summary>Fusion Charge Blindée : le dash devient une charge (couloir de dégâts + knockback).</summary>
    private void SetupCharge(GraftTable.GraftDef def)
    {
        float dmg = (float)def.Effect("charge", "impactDamage", 45);
        if (def.Effect("charge", "scalesWithDamageMultiplier", 1) != 0)
            dmg *= _player.Stats.DamageMultiplier;

        _player.EnableDash(
            distance:      (float)def.Effect("charge", "distancePx", 240),
            duration:      (float)def.Effect("charge", "durationSec", 0.22),
            cooldown:      (float)def.Effect("charge", "cooldownSec", 4.0),
            cooldownFloor: (float)def.Effect("charge", "cooldownFloorSec", 1.8),
            iframes:       (float)def.Effect("charge", "iframesSec", 0.30),
            affectedByCdr: def.Effect("charge", "affectedByCooldownReduction", 1) != 0,
            chargeWidth:     (float)def.Effect("charge", "corridorWidthPx", 48),
            chargeDamage:    dmg,
            chargeKnockback: (float)def.Effect("charge", "knockbackPx", 90));
    }

    /// <summary>Fusion Ruche de Tourelles : les 4 essaims deviennent 4 tourelles en suivi lerp qui tirent.</summary>
    private void SetupTurrets(GraftTable.GraftDef def)
    {
        _turretsActive          = true;
        _turretsCount           = Mathf.Max(1, (int)def.Effect("turrets", "count", 4));
        _turretsAnchorR         = (float)def.Effect("turrets", "anchorRadiusPx", 90);
        _turretsFollow          = (float)def.Effect("turrets", "followSpeedPx", 120);
        _turretsCd              = (float)def.Effect("turrets", "cooldownSec", 1.0);
        _turretsCdFloor         = (float)def.Effect("turrets", "cooldownFloorSec", 0.15);
        _turretsDamage          = (float)def.Effect("turrets", "damage", 12);
        _turretsRange           = (float)def.Effect("turrets", "targetRangePx", 380);
        _turretsSpeed           = (float)def.Effect("turrets", "projectileSpeed", 300);
        _turretsPierce          = def.Effect("turrets", "pierceCount", 1) >= 1;
        _turretsCdr             = def.Effect("turrets", "affectedByCooldownReduction", 1) != 0;
        _turretsLifesteal       = (float)def.Effect("turrets", "lifestealFraction", 0.04);
        _turretsContactDmg      = (float)def.Effect("turrets", "contactDamage", 8);
        _turretsContactRehitSec = (float)def.Effect("turrets", "contactRehitIntervalSec", 0.6);
        if (def.Effect("turrets", "scalesWithDamageMultiplier", 1) != 0)
        {
            _turretsDamage     *= _player.Stats.DamageMultiplier;
            _turretsContactDmg *= _player.Stats.DamageMultiplier;
        }

        // Lisibilité (playtest 2026-07-07, BUG-F01) : les tourelles doivent trancher nettement sur
        // les ennemis de rouille (orange) et passer AU-DESSUS d'eux. Corps cyan de la palette UI
        // (≠ teinte de greffe), Z=6 (ennemis ~0, joueur 5), plus grosses, contour sombre + cœur clair,
        // et un lien d'ancrage fin vers le joueur pour rattacher visuellement les 4 tourelles.
        var body    = new Color(0.27f, 1f, 0.93f);     // cyan #44FFEE
        var outline = new Color(0f, 0.12f, 0.11f, 0.7f);
        var core    = new Color(0.85f, 1f, 0.98f);
        for (int i = 0; i < _turretsCount; i++)
        {
            var t = new Node2D { ZIndex = 6 };
            // Lien d'ancrage joueur↔tourelle (derrière le corps), mis à jour dans UpdateTurrets.
            var link = new Line2D
            {
                Width        = 2f,
                DefaultColor = new Color(body.R, body.G, body.B, 0.22f),
                ZIndex       = -1,
                Points       = new Vector2[] { Vector2.Zero, Vector2.Zero },
            };
            t.AddChild(link);
            _turretLines.Add(link);
            // Contour sombre (halo de contraste), corps cyan, puis cœur clair — arrow ~22 px.
            t.AddChild(new Polygon2D { Polygon = TurretShape(1.35f), Color = outline });
            t.AddChild(new Polygon2D { Polygon = TurretShape(1.0f),  Color = body });
            t.AddChild(new Polygon2D { Polygon = TurretShape(0.42f), Color = core });
            AddChild(t);
            t.GlobalPosition = _player.GlobalPosition;
            _turrets.Add(t);
            _turretsTimer.Add(EffectiveCd(_turretsCd, _turretsCdFloor, _turretsCdr));
        }
    }

    /// <summary>Silhouette de tourelle (flèche pointant +x, ~22 px de long à scale 1) mise à l'échelle.</summary>
    private static Vector2[] TurretShape(float s) => new Vector2[]
    {
        new(11 * s, 0), new(5 * s, -8 * s), new(-7 * s, -7 * s), new(-7 * s, 7 * s), new(5 * s, 8 * s),
    };

    private void SetupTurret(GraftTable.GraftDef def)
    {
        _turretActive  = true;
        _turretCd      = (float)def.Effect("autoTurret", "cooldownSec", 1.4);
        _turretCdFloor = (float)def.Effect("autoTurret", "cooldownFloorSec", 0.15);
        _turretDamage  = (float)def.Effect("autoTurret", "damage", 18);
        _turretRange   = (float)def.Effect("autoTurret", "targetRangePx", 420);
        _turretSpeed   = (float)def.Effect("autoTurret", "projectileSpeed", 320);
        _turretPierce  = def.Effect("autoTurret", "pierceCount", 1) >= 1;
        _turretCdr     = def.Effect("autoTurret", "affectedByCooldownReduction", 1) != 0;
        if (def.Effect("autoTurret", "scalesWithDamageMultiplier", 1) != 0)
            _turretDamage *= _player.Stats.DamageMultiplier;
        _turretTimer = EffectiveCd(_turretCd, _turretCdFloor, _turretCdr);
    }

    private void SetupThorns(GraftTable.GraftDef def)
    {
        _thornsActive = true;
        _thornsRehit  = (float)def.Effect("thorns", "rehitIntervalSec", 0.6);
        _thornsDamage = (float)def.Effect("thorns", "damage", 18);
        _thornsRadius = (float)def.Effect("thorns", "radiusPx", 40);
        if (def.Effect("thorns", "scalesWithDamageMultiplier", 1) != 0)
            _thornsDamage *= _player.Stats.DamageMultiplier;
        _thornsTimer = _thornsRehit;
    }

    private void SetupShockwave(GraftTable.GraftDef def)
    {
        _shockActive    = true;
        _shockCd        = (float)def.Effect("shockwave", "cooldownSec", 4.0);
        _shockDamage    = (float)def.Effect("shockwave", "damage", 60);
        _shockRadius    = (float)def.Effect("shockwave", "radiusPx", 160);
        _shockKnockback = (float)def.Effect("shockwave", "knockbackPx", 60);
        _shockCdr       = def.Effect("shockwave", "affectedByCooldownReduction", 1) != 0;
        if (def.Effect("shockwave", "scalesWithDamageMultiplier", 1) != 0)
            _shockDamage *= _player.Stats.DamageMultiplier;
        _shockTimer = EffectiveCd(_shockCd, StatCaps.MinCooldown, _shockCdr);
    }

    // -------------------------------------------------------------------------
    // Boucle (hors physique — AddChild sûr, gelé pendant une pause modale)
    // -------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        if (_swarmActive)   UpdateSwarm(dt);
        if (_turretActive)  UpdateTurret(dt);
        if (_thornsActive)  UpdateThorns(dt);
        if (_shockActive)   UpdateShockwave(dt);
        if (_turretsActive) UpdateTurrets(dt);
        if (_props.Count > 0) UpdateProps(dt);
    }

    private void UpdateSwarm(float dt)
    {
        _orbitAngle += Mathf.DegToRad(_swarmAngularDeg) * dt;
        var center = _player.GlobalPosition;

        // Positionnement.
        for (int i = 0; i < _orbiters.Count; i++)
        {
            if (!IsInstanceValid(_orbiters[i])) continue;
            float a = _orbitAngle + i * (Mathf.Tau / _orbiters.Count);
            _orbiters[i].GlobalPosition = center + Vector2.Right.Rotated(a) * _swarmRadius;
        }

        // Décroissance des cooldowns par ennemi + purge des invalides.
        var stale = new List<EnemyBase>();
        foreach (var kv in _orbiterRehit)
        {
            if (!IsInstanceValid(kv.Key)) { stale.Add(kv.Key); continue; }
        }
        foreach (var e in stale) _orbiterRehit.Remove(e);
        var keys = new List<EnemyBase>(_orbiterRehit.Keys);
        foreach (var e in keys) _orbiterRehit[e] -= dt;

        // Dégâts de contact des mini-essaims (check de distance, pas d'Area2D).
        const float hitR = 16f;
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase enemy || !IsInstanceValid(enemy)) continue;
            if (_orbiterRehit.GetValueOrDefault(enemy, 0f) > 0f) continue;

            bool touching = false;
            for (int i = 0; i < _orbiters.Count; i++)
            {
                if (!IsInstanceValid(_orbiters[i])) continue;
                if (_orbiters[i].GlobalPosition.DistanceTo(enemy.GlobalPosition) <= hitR) { touching = true; break; }
            }
            if (!touching) continue;

            enemy.TakeDamage(_swarmDamage);
            _orbiterRehit[enemy] = _swarmRehit;
            if (_swarmLifesteal > 0f) _player.HealFlat(_swarmDamage * _swarmLifesteal);
        }
    }

    private void UpdateTurret(float dt)
    {
        _turretTimer -= dt;
        if (_turretTimer > 0f) return;
        _turretTimer = EffectiveCd(_turretCd, _turretCdFloor, _turretCdr);

        var target = NearestEnemy(_turretRange);
        if (target == null) return;

        _bulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
        if (_bulletScene == null) return;
        var b = _bulletScene.Instantiate<Bullet>();
        b.Direction  = (target.GlobalPosition - _player.GlobalPosition).Normalized();
        b.Speed      = _turretSpeed;
        b.Damage     = _turretDamage;
        b.IsPiercing = _turretPierce;
        b.Power      = 2;
        GetTree().Root.AddChild(b);
        b.GlobalPosition = _player.GlobalPosition;
        AudioSystem.Instance?.PlaySfx("sfx_card_select");
    }

    private void UpdateThorns(float dt)
    {
        _thornsTimer -= dt;
        if (_thornsTimer > 0f) return;
        _thornsTimer = _thornsRehit;

        var center = _player.GlobalPosition;
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase enemy || !IsInstanceValid(enemy)) continue;
            if (center.DistanceTo(enemy.GlobalPosition) <= _thornsRadius)
                enemy.TakeDamage(_thornsDamage);
        }
    }

    private void UpdateShockwave(float dt)
    {
        _shockTimer -= dt;
        if (_shockTimer > 0f) return;
        _shockTimer = EffectiveCd(_shockCd, StatCaps.MinCooldown, _shockCdr);

        var center = _player.GlobalPosition;

        // VFX : réutilise l'anneau de choc (teinté magenta/rouille).
        _shockwaveScene ??= GD.Load<PackedScene>("res://scenes/vfx/vfx_shockwave_ring.tscn");
        if (_shockwaveScene != null)
        {
            var ring = _shockwaveScene.Instantiate<Node2D>();
            ring.Modulate = new Color(1.3f, 0.4f, 1.0f, 1f);
            GetTree().Root.AddChild(ring);
            ring.GlobalPosition = center;
        }
        ScreenShake.Instance?.Shake(4f, 0.15f);

        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase enemy || !IsInstanceValid(enemy)) continue;
            var off = enemy.GlobalPosition - center;
            if (off.Length() > _shockRadius) continue;
            enemy.TakeDamage(_shockDamage);
            var dir = off.LengthSquared() > 0.01f ? off.Normalized() : Vector2.Right;
            enemy.GlobalPosition += dir * _shockKnockback; // repousse
        }
    }

    private void UpdateTurrets(float dt)
    {
        var center = _player.GlobalPosition;

        // Décroissance des cooldowns de contact + purge des invalides.
        var stale = new List<EnemyBase>();
        foreach (var kv in _turretsContactRehit) if (!IsInstanceValid(kv.Key)) stale.Add(kv.Key);
        foreach (var e in stale) _turretsContactRehit.Remove(e);
        var keys = new List<EnemyBase>(_turretsContactRehit.Keys);
        foreach (var e in keys) _turretsContactRehit[e] -= dt;

        for (int i = 0; i < _turrets.Count; i++)
        {
            if (!IsInstanceValid(_turrets[i])) continue;

            // Ancre en anneau ; suivi lerp lent (les tourelles traînent derrière le joueur, §15.3).
            float a = i * (Mathf.Tau / _turrets.Count);
            var anchor = center + Vector2.Right.Rotated(a) * _turretsAnchorR;
            _turrets[i].GlobalPosition = _turrets[i].GlobalPosition.MoveToward(anchor, _turretsFollow * dt);

            // Lien d'ancrage vers le joueur (point 1 en coordonnées locales : le nœud tourelle peut
            // être pivoté vers sa cible, donc on contre-rotate pour que le lien vise toujours le joueur).
            if (i < _turretLines.Count && IsInstanceValid(_turretLines[i]))
                _turretLines[i].SetPointPosition(1,
                    (center - _turrets[i].GlobalPosition).Rotated(-_turrets[i].Rotation));

            // Tir sur l'ennemi le plus proche de la tourelle.
            _turretsTimer[i] -= dt;
            if (_turretsTimer[i] <= 0f)
            {
                var target = NearestEnemyTo(_turrets[i].GlobalPosition, _turretsRange);
                if (target != null)
                {
                    _turretsTimer[i] = EffectiveCd(_turretsCd, _turretsCdFloor, _turretsCdr);
                    _bulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
                    // Oriente la tourelle vers sa cible (le canon montre d'où part le tir → lisibilité).
                    var dir = (target.GlobalPosition - _turrets[i].GlobalPosition).Normalized();
                    _turrets[i].Rotation = dir.Angle();
                    if (_bulletScene != null)
                    {
                        var b = _bulletScene.Instantiate<Bullet>();
                        b.Direction  = dir;
                        b.Speed      = _turretsSpeed;
                        b.Damage     = _turretsDamage;
                        b.IsPiercing = _turretsPierce;
                        b.Power      = 2;
                        GetTree().Root.AddChild(b);
                        b.GlobalPosition = _turrets[i].GlobalPosition;
                        if (_turretsLifesteal > 0f) _player.HealFlat(_turretsDamage * _turretsLifesteal);
                    }
                }
                // Pas de cible : timer reste <= 0, on re-teste au prochain frame.
            }
        }

        // Contact résiduel (dissuade le hug d'une tourelle).
        if (_turretsContactDmg > 0f)
        {
            const float hitR = 16f;
            foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
            {
                if (node is not EnemyBase enemy || !IsInstanceValid(enemy)) continue;
                if (_turretsContactRehit.GetValueOrDefault(enemy, 0f) > 0f) continue;

                bool touching = false;
                for (int i = 0; i < _turrets.Count; i++)
                {
                    if (!IsInstanceValid(_turrets[i])) continue;
                    if (_turrets[i].GlobalPosition.DistanceTo(enemy.GlobalPosition) <= hitR) { touching = true; break; }
                }
                if (!touching) continue;

                enemy.TakeDamage(_turretsContactDmg);
                _turretsContactRehit[enemy] = _turretsContactRehitSec;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Props de silhouette (Phase B) — la chimère « pousse » sur le corps du joueur
    // -------------------------------------------------------------------------
    //
    // Chaque greffe/fusion sans nœud visuel propre (le swarm et les tourelles en ont déjà) reçoit
    // un « prop » : petit assemblage de Polygon2D ancré au corps du joueur, ombré pseudo-3D (lumière
    // haut-gauche, cf. ART_BRIEF_PSEUDO3D §1-2). Procédural (pas d'asset PNG), cohérent avec les
    // essaims/tourelles déjà procéduraux, et surtout indépendant du personnage : marche pour les
    // 4 corps jouables sans art par perso/frame (choix « props attachés », pas « couches par frame »).

    /// <summary>Un prop de silhouette ancré au joueur (espace local du GraftManager = espace joueur).</summary>
    private sealed class GraftProp
    {
        public Node2D Node = null!;
        public Node2D? Sub;                                   // sous-élément animé (pupille de l'œil, vents…)
        public Vector2 Anchor;                                // offset local depuis le centre du joueur
        public bool Mirror;                                   // miroir X selon le facing (props directionnels)
        public System.Action<GraftProp, float>? Update;       // animation par frame (bob/rotation/visée/pulse)
    }

    private void UpdateProps(float dt)
    {
        _propBob += dt;
        bool left = _player.FacingLeft;
        for (int i = 0; i < _props.Count; i++)
        {
            var p = _props[i];
            if (!IsInstanceValid(p.Node)) continue;
            float ax = p.Mirror && left ? -p.Anchor.X : p.Anchor.X;
            p.Node.Position = new Vector2(ax, p.Anchor.Y);
            if (p.Mirror) p.Node.Scale = new Vector2(left ? -1f : 1f, 1f);
            p.Update?.Invoke(p, dt);
        }
    }

    /// <summary>Construit le prop de silhouette d'une greffe/fusion (bespoke par id ; le swarm et les
    /// tourelles n'en ont pas — leurs nœuds servent déjà de silhouette).</summary>
    private void BuildPropFor(GraftTable.GraftDef def)
    {
        var b = BaseColorFromTint(def.Tint);
        switch (def.Id)
        {
            case "grafted_carapace":        BuildCarapaceProp(b); break;
            case "erratic_servos":          BuildServosProp(b);   break;
            case "aiming_eye":              BuildEyeProp(b);      break;
            case "stalker_wave":            BuildWaveProp(b);     break;
            case "fusion_charge_blindee":   BuildChargeProwProp(b); break;
            case "fusion_ruche_tourelles":  BuildHiveCoreProp(b);   break;
        }
    }

    private void AddProp(Node2D node, Vector2 anchor, bool mirror, int z,
                         Node2D? sub = null, System.Action<GraftProp, float>? update = null)
    {
        node.ZIndex = z;
        node.Position = anchor;
        AddChild(node);
        _props.Add(new GraftProp { Node = node, Sub = sub, Anchor = anchor, Mirror = mirror, Update = update });
    }

    // ── Carapace Greffée : pauldrons blindés + plastron sur le haut du corps ──
    private void BuildCarapaceProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var node = new Node2D();
        // Plastron (bande d'armure sur les épaules) : base + reflet haut + ombre basse.
        node.AddChild(P(new[] { V(-8, -1), V(8, -1), V(7, 4), V(-7, 4) }, b));
        node.AddChild(P(new[] { V(-8, -1), V(8, -1), V(8, 0), V(-8, 0) }, hi));
        node.AddChild(P(new[] { V(-7, 3), V(7, 3), V(7, 4), V(-7, 4) }, sh));
        // Pauldron gauche (tourné vers la lumière) : base éclaircie.
        node.AddChild(P(new[] { V(-11, -4), V(-4, -5), V(-3, 0), V(-10, 1) }, b));
        node.AddChild(P(new[] { V(-11, -4), V(-4, -5), V(-4, -4), V(-10, -2) }, hi));
        // Pauldron droit (à l'ombre) : base assombrie.
        node.AddChild(P(new[] { V(4, -5), V(11, -4), V(10, 1), V(3, 0) }, sh));
        node.AddChild(P(new[] { V(4, -5), V(11, -4), V(11, -3), V(4, -4) }, b));
        AddProp(node, new Vector2(0, 1), mirror: false, z: 1);
    }

    // ── Servos Erratiques : deux tuyères sur les flancs bas (débordent la silhouette pour rester
    //    lisibles), vents lumineux qui pulsent — et s'embrasent pendant le dash ──
    private void BuildServosProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var vent = new Color(Mathf.Min(b.R * 1.7f, 1f), Mathf.Min(b.G * 1.7f, 1f), Mathf.Min(b.B * 1.8f, 1f));
        var node = new Node2D();
        // Tuyère gauche + droite (biseaux dépassant hors du corps, x jusqu'à ±11), pointant bas-dehors.
        node.AddChild(P(new[] { V(-6, -2), V(-11, 2), V(-9, 8), V(-4, 4) }, b));
        node.AddChild(P(new[] { V(-6, -2), V(-11, 2), V(-9, 3), V(-5, 0) }, hi));
        node.AddChild(P(new[] { V(6, -2), V(11, 2), V(4, 4), V(9, 8) }, sh));
        node.AddChild(P(new[] { V(6, -2), V(11, 2), V(9, 3), V(5, 0) }, hi));
        // Sous-nœud des vents (tips lumineux, pulsent/s'embrasent dans Update).
        var vents = new Node2D();
        vents.AddChild(P(new[] { V(-10, 5), V(-7, 5), V(-8, 9), V(-11, 8) }, vent));
        vents.AddChild(P(new[] { V(7, 5), V(10, 5), V(11, 8), V(8, 9) }, vent));
        node.AddChild(vents);
        AddProp(node, new Vector2(0, 5), mirror: false, z: 1, sub: vents, update: (p, dt) =>
        {
            float pulse = _player.IsDashing ? 1f : 0.5f + 0.3f * Mathf.Sin(_propBob * 6f);
            if (p.Sub != null) p.Sub.Modulate = new Color(1f, 1f, 1f, pulse);
        });
    }

    // ── Œil de Visée : orbe flottant au-dessus de la tête, pupille qui suit l'ennemi le plus proche ──
    private void BuildEyeProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var sclera = new Color(0.92f, 0.94f, 0.98f);
        var node = new Node2D();
        // Sclère (octogone pâle) → iris teinté → pupille sombre (sous-nœud mobile).
        node.AddChild(P(Octagon(6f), sh));            // contour/ombre
        node.AddChild(P(Octagon(5f), sclera));        // blanc de l'œil
        node.AddChild(P(Octagon(3.2f), b));           // iris
        node.AddChild(P(Octagon(3.2f, topHalf: true), hi)); // reflet haut de l'iris
        var pupil = new Node2D();
        pupil.AddChild(P(Octagon(1.6f), new Color(0.05f, 0.05f, 0.09f)));
        node.AddChild(pupil);
        AddProp(node, new Vector2(0, -15), mirror: false, z: 2, sub: pupil, update: (p, dt) =>
        {
            p.Node.Position += new Vector2(0, Mathf.Sin(_propBob * 3f) * 1.3f); // flottaison
            var target = NearestEnemyTo(_player.GlobalPosition, 420f);
            Vector2 dir = target != null
                ? (target.GlobalPosition - _player.GlobalPosition).Normalized()
                : Vector2.Zero;
            if (p.Sub != null)
                p.Sub.Position = p.Sub.Position.Lerp(dir * 1.6f, 0.25f);
        });
    }

    // ── Onde du Rôdeur : couronne-résonateur qui tourne et enfle juste avant chaque onde ──
    private void BuildWaveProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var node = new Node2D();
        // 3 nœuds-diapasons sur un anneau (radius ~13), reliés par de fins segments.
        const float r = 13f;
        var ring = new Line2D { Width = 1.4f, DefaultColor = new Color(b.R, b.G, b.B, 0.35f), Closed = true };
        var pts = new Godot.Collections.Array<Vector2>();
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3f;
            var pos = Vector2.Right.Rotated(a) * r;
            pts.Add(pos);
            var nub = new Node2D { Position = pos, Rotation = a };
            nub.AddChild(P(new[] { V(3, 0), V(-2, -2), V(-2, 2) }, b));
            nub.AddChild(P(new[] { V(3, 0), V(-2, -2), V(0, -1) }, hi));
            node.AddChild(nub);
        }
        ring.Points = pts.ToArray();
        node.AddChild(ring);
        AddProp(node, new Vector2(0, 1), mirror: false, z: -1, update: (p, dt) =>
        {
            p.Node.Rotation += dt * 0.9f;
            float ratio = _shockCd > 0.01f ? Mathf.Clamp(1f - _shockTimer / _shockCd, 0f, 1f) : 0f;
            float s = 1f + 0.22f * ratio; // enfle en anticipation de l'onde
            p.Node.Scale = new Vector2(s, s);
        });
    }

    // ── Fusion Charge Blindée : proue blindée orientée vers le facing, s'illumine à la charge ──
    private void BuildChargeProwProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var node = new Node2D();
        // Coque épaisse (héritage carapace) + proue en biseau pointant +x.
        node.AddChild(P(new[] { V(-7, -3), V(4, -3), V(4, 4), V(-7, 4) }, b));
        node.AddChild(P(new[] { V(-7, -3), V(4, -3), V(4, -2), V(-7, -2) }, hi));
        node.AddChild(P(new[] { V(-7, 3), V(4, 3), V(4, 4), V(-7, 4) }, sh));
        // Proue (sous-nœud, s'illumine au dash).
        var prow = new Node2D();
        prow.AddChild(P(new[] { V(4, -3), V(11, 0), V(4, 3) }, hi));
        prow.AddChild(P(new[] { V(4, 0), V(11, 0), V(4, 3) }, sh));
        node.AddChild(prow);
        AddProp(node, new Vector2(2, 1), mirror: true, z: 1, sub: prow, update: (p, dt) =>
        {
            if (p.Sub != null)
                p.Sub.Modulate = _player.IsDashing ? new Color(1.8f, 1.7f, 1.4f) : Colors.White;
        });
    }

    // ── Fusion Ruche de Tourelles : petit cœur de ruche (grappe d'alvéoles) dans le dos ──
    private void BuildHiveCoreProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var node = new Node2D();
        // 4 alvéoles hexagonales serrées, teinte des tourelles (cyan).
        var cells = new[] { V(-3, -4), V(3, -4), V(-4, 1), V(3, 1) };
        foreach (var c in cells)
        {
            node.AddChild(P(Hexagon(3.2f, c), sh));
            node.AddChild(P(Hexagon(2.4f, c), b));
            node.AddChild(P(Hexagon(2.4f, c, topHalf: true), hi));
        }
        AddProp(node, new Vector2(0, -3), mirror: false, z: 1, update: (p, dt) =>
        {
            float g = 0.85f + 0.15f * Mathf.Sin(_propBob * 4f); // léger battement
            p.Node.Modulate = new Color(g, g, g);
        });
    }

    // ── Primitives géométriques ──
    private static Polygon2D P(Vector2[] pts, Color c) => new() { Polygon = pts, Color = c };
    private static Vector2 V(float x, float y) => new(x, y);

    private static Vector2[] Octagon(float r, bool topHalf = false)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.Pi / 8f + i * Mathf.Tau / 8f;
            var pt = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.9f); // légèrement aplati
            if (topHalf && pt.Y > 0f) pt.Y = 0f;
            list.Add(pt);
        }
        return list.ToArray();
    }

    private static Vector2[] Hexagon(float r, Vector2 center = default, bool topHalf = false)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Pi / 6f + i * Mathf.Tau / 6f;
            var pt = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            if (topHalf && pt.Y > center.Y) pt.Y = center.Y;
            list.Add(pt);
        }
        return list.ToArray();
    }

    // ── Ombrage pseudo-3D (lumière haut-gauche, ART_BRIEF_PSEUDO3D §2) répliqué en C# pour les
    //    props procéduraux (la lib PIL ne sert que les PNG pré-rendus). ──
    private enum Face { Highlight, Base, Shadow, Contact }

    private static Color Shade(Color b, Face face)
    {
        float h = b.H, s = b.S, v = b.V;
        switch (face)
        {
            case Face.Highlight: v = Mathf.Min(v * 1.35f, 1f); s *= 0.85f; break;
            case Face.Shadow:    v *= 0.55f; s = Mathf.Min(s * 1.10f, 1f); break;
            case Face.Contact:   v *= 0.35f; s = Mathf.Min(s * 1.15f, 1f); break;
        }
        var c = Color.FromHsv(h, s, v);
        return new Color(c.R, c.G, c.B, b.A);
    }

    /// <summary>Normalise une teinte-multiplicateur (canaux &gt; 1 possibles, ex. servos [0.6,0.85,1.3])
    /// en couleur de matière lisible (canal dominant ramené à ~0.85), en préservant la teinte.</summary>
    private static Color BaseColorFromTint(float[] tint)
    {
        float r = tint[0], g = tint[1], bl = tint[2];
        float max = Mathf.Max(r, Mathf.Max(g, bl));
        if (max <= 0.001f) return new Color(0.8f, 0.8f, 0.8f);
        float k = 0.85f / max;
        return new Color(r * k, g * k, bl * k);
    }

    // -------------------------------------------------------------------------
    // Rendu — teinte additive cumulée sur SelfModulate (pas Modulate, réservé HitFlash/blink)
    // -------------------------------------------------------------------------

    private void RecomputeTint()
    {
        Color acc = Colors.White;
        foreach (var def in _active.Values)
        {
            var t = TintColor(def);
            // Nudge léger vers la teinte de la greffe (cumulatif, borné).
            acc = new Color(
                Mathf.Lerp(acc.R, acc.R * t.R, 0.5f),
                Mathf.Lerp(acc.G, acc.G * t.G, 0.5f),
                Mathf.Lerp(acc.B, acc.B * t.B, 0.5f), 1f);
        }
        _player.SetGraftTint(acc);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static Color TintColor(GraftTable.GraftDef def)
        => new(def.Tint[0], def.Tint[1], def.Tint[2], 1f);

    private float EffectiveCd(float baseCd, float floor, bool cdr)
    {
        if (!cdr) return Mathf.Max(floor, baseCd);
        float reduced = baseCd * (1f - _player.Stats.CooldownReduction);
        return Mathf.Max(floor, reduced);
    }

    private EnemyBase? NearestEnemy(float maxRange) => NearestEnemyTo(_player.GlobalPosition, maxRange);

    private EnemyBase? NearestEnemyTo(Vector2 from, float maxRange)
    {
        EnemyBase? best = null;
        float bestSq = maxRange * maxRange;
        foreach (var node in GetTree().GetNodesInGroup(Constants.GroupEnemies))
        {
            if (node is not EnemyBase e || !IsInstanceValid(e)) continue;
            float dsq = from.DistanceSquaredTo(e.GlobalPosition);
            if (dsq <= bestSq) { bestSq = dsq; best = e; }
        }
        return best;
    }
}
