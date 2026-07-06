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
    private readonly List<float>  _turretsTimer = new();
    private readonly Dictionary<EnemyBase, float> _turretsContactRehit = new();
    private int   _turretsCount;
    private float _turretsAnchorR, _turretsFollow, _turretsCd, _turretsCdFloor, _turretsDamage;
    private float _turretsRange, _turretsSpeed, _turretsLifesteal, _turretsContactDmg, _turretsContactRehitSec;
    private bool  _turretsCdr, _turretsPierce;

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
        // Purge des tourelles existantes.
        foreach (var t in _turrets) if (IsInstanceValid(t)) t.QueueFree();
        _turrets.Clear();
        _turretsTimer.Clear();
        _turretsContactRehit.Clear();

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

        var col = TintColor(def);
        for (int i = 0; i < _turretsCount; i++)
        {
            var t = new Node2D { ZIndex = 4 };
            var poly = new Polygon2D
            {
                Polygon = new Vector2[] { new(7, 0), new(3, -6), new(-5, -5), new(-5, 5), new(3, 6) },
                Color   = col,
            };
            t.AddChild(poly);
            AddChild(t);
            t.GlobalPosition = _player.GlobalPosition;
            _turrets.Add(t);
            _turretsTimer.Add(EffectiveCd(_turretsCd, _turretsCdFloor, _turretsCdr));
        }
    }

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

            // Tir sur l'ennemi le plus proche de la tourelle.
            _turretsTimer[i] -= dt;
            if (_turretsTimer[i] <= 0f)
            {
                var target = NearestEnemyTo(_turrets[i].GlobalPosition, _turretsRange);
                if (target != null)
                {
                    _turretsTimer[i] = EffectiveCd(_turretsCd, _turretsCdFloor, _turretsCdr);
                    _bulletScene ??= GD.Load<PackedScene>("res://scenes/weapons/Bullet.tscn");
                    if (_bulletScene != null)
                    {
                        var b = _bulletScene.Instantiate<Bullet>();
                        b.Direction  = (target.GlobalPosition - _turrets[i].GlobalPosition).Normalized();
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
