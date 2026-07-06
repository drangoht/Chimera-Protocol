using Godot;
using System.Collections.Generic;

/// <summary>
/// AutoLoad — cœur du système d'Assimilation (greffes). Tient les jauges de points par archétype
/// (routées depuis les kills via <see cref="GraftTable"/>), les slots de greffes équipées, la pause
/// de jauge d'une greffe possédée et la reprise depuis la valeur mémorisée, et émet
/// <see cref="GaugeFilled"/> quand une jauge atteint son seuil effectif. Toute décision chiffrée est
/// déléguée à <see cref="GraftTable"/> (logique pure). L'application des effets vit dans
/// <see cref="GraftManager"/> (côté Player). Voir docs/DESIGN_ASSIMILATION.md Partie II.
/// </summary>
public partial class AssimilationSystem : Node
{
    public static AssimilationSystem Instance { get; private set; } = null!;

    /// <summary>Émis quand une jauge atteint son seuil (déclenche l'AssimilationScreen).</summary>
    [Signal] public delegate void GaugeFilledEventHandler(string gaugeKey);

    private GraftTable.GraftConfig _config = new();
    public GraftTable.GraftConfig Config => _config;

    // Points accumulés par clé de jauge (mémorisés : une greffe retirée reprend sa jauge d'ici).
    private readonly Dictionary<string, float> _points = new();
    // Jauge refusée ce cycle → seuil ×declineThresholdMultiplier jusqu'au prochain remplissage.
    private readonly Dictionary<string, bool> _declined = new();
    // Greffes équipées (ordre = ordre des slots).
    private readonly List<string> _equipped = new();
    // Jauges en attente de résolution à l'écran (évite de re-émettre GaugeFilled en boucle).
    private readonly HashSet<string> _pending = new();

    private int _slotCount = 3;
    private float _gaugeSpeedBonus = 0f;

    /// <summary>Incrémenté à chaque changement de slots (le HUD compare pour se rafraîchir).</summary>
    public int GraftsVersion { get; private set; }

    public int SlotCount => _slotCount;
    public IReadOnlyList<string> EquippedGrafts => _equipped;
    public bool HasFreeSlot => _equipped.Count < _slotCount;

    public override void _Ready()
    {
        Instance = this;
        LoadJson();
    }

    private void LoadJson()
    {
        using var f = Godot.FileAccess.Open("res://data/grafts.json", Godot.FileAccess.ModeFlags.Read);
        if (f == null)
        {
            GD.PrintErr("[AssimilationSystem] grafts.json introuvable.");
            return;
        }
        _config = GraftTable.Parse(f.GetAsText());
        GD.Print($"[AssimilationSystem] {_config.Grafts.Count} greffes chargées.");
    }

    /// <summary>Réinitialise l'état avant chaque run (comme LevelUpSystem.Reset). Lit les upgrades méta.</summary>
    public void Reset()
    {
        _points.Clear();
        _declined.Clear();
        _equipped.Clear();
        _pending.Clear();

        var meta = MetaProgressionSystem.Instance;
        int slotBonus = meta?.GetUpgradeLevel("graft_slots") ?? 0;
        _slotCount = GraftTable.SlotCount(_config, slotBonus);
        // graft_metabolism : -10 % de seuil / niveau, max -30 % (3 niveaux).
        _gaugeSpeedBonus = (meta?.GetUpgradeLevel("graft_metabolism") ?? 0) * 0.10f;

        GraftsVersion++;
    }

    // -------------------------------------------------------------------------
    // Routage des kills → jauges
    // -------------------------------------------------------------------------

    /// <summary>Appelé par GameManager.NotifyEnemyKilled(enemy). Route via GraftTable et remplit les jauges.</summary>
    public void OnEnemyKilled(string aiType, bool isElite, bool isMiniBoss, bool isBoss)
    {
        if (_config.Grafts.Count == 0) return;

        var contribs = GraftTable.RouteKill(_config, aiType ?? "", isElite, isMiniBoss, isBoss);
        foreach (var c in contribs)
        {
            var def = _config.GraftForGauge(c.Gauge);
            // Jauge en pause tant que sa greffe est équipée (§12.3).
            if (def != null && _equipped.Contains(def.Id)) continue;
            if (_pending.Contains(c.Gauge)) continue; // déjà en attente d'écran

            _points[c.Gauge] = _points.GetValueOrDefault(c.Gauge) + c.Points;

            if (_points[c.Gauge] >= EffectiveThreshold(c.Gauge))
            {
                _pending.Add(c.Gauge);
                EmitSignal(SignalName.GaugeFilled, c.Gauge);
            }
        }

        RouteFusionKill(aiType ?? "", isElite, isMiniBoss, isBoss);
    }

    /// <summary>
    /// Jauges de fusion (§15.1) : n'accumulent QUE si les 2 greffes prérequises sont équipées et
    /// que le kill est un basique/élite d'un archétype source (les champions ne comptent pas).
    /// </summary>
    private void RouteFusionKill(string aiType, bool isElite, bool isMiniBoss, bool isBoss)
    {
        if (isMiniBoss || isBoss) return;

        foreach (var fusion in _config.Fusions)
        {
            if (_equipped.Contains(fusion.Id)) continue;           // fusion déjà équipée
            if (_pending.Contains(fusion.GaugeKey)) continue;      // déjà en attente d'écran

            bool ready = true;
            foreach (var r in fusion.Requires) if (!_equipped.Contains(r)) { ready = false; break; }
            if (!ready) continue;                                  // requires pas entièrement équipé

            int pts = fusion.KillPoints(aiType, isElite);
            if (pts <= 0) continue;

            _points[fusion.GaugeKey] = _points.GetValueOrDefault(fusion.GaugeKey) + pts;
            if (_points[fusion.GaugeKey] >= EffectiveThreshold(fusion.GaugeKey))
            {
                _pending.Add(fusion.GaugeKey);
                EmitSignal(SignalName.GaugeFilled, fusion.GaugeKey);
            }
        }
    }

    /// <summary>Seuil effectif d'une jauge (bonus méta + malus de refus éventuel).</summary>
    public int EffectiveThreshold(string gauge)
    {
        int baseTh = _config.Thresholds.GetValueOrDefault(gauge, 9999);
        int eff = GraftTable.EffectiveThreshold(baseTh, _gaugeSpeedBonus);
        if (_declined.GetValueOrDefault(gauge))
            eff = GraftTable.DeclinedThreshold(eff, _config.DeclineThresholdMultiplier);
        return eff;
    }

    /// <summary>Ratio de remplissage [0,1] d'une jauge (pour le HUD, optionnel).</summary>
    public float GaugeRatio(string gauge)
    {
        int th = EffectiveThreshold(gauge);
        return th > 0 ? Mathf.Clamp(_points.GetValueOrDefault(gauge) / th, 0f, 1f) : 0f;
    }

    // -------------------------------------------------------------------------
    // Résolutions depuis l'AssimilationScreen
    // -------------------------------------------------------------------------

    /// <summary>Équipe la greffe de la jauge dans un slot libre (§13.3, cas slot libre).</summary>
    public void Assimilate(string gauge)
    {
        var def = _config.GraftForGauge(gauge);
        if (def != null && !_equipped.Contains(def.Id) && HasFreeSlot)
        {
            _equipped.Add(def.Id);
            _declined[gauge] = false;
            EquipOnPlayer(def);
            Discover(def.Id);
            GraftsVersion++;
        }
        _pending.Remove(gauge);
    }

    /// <summary>Refuse la greffe : jauge remise à 0, seuil ×1.5 pour le prochain cycle (§12.3).</summary>
    public void Reject(string gauge)
    {
        _points[gauge] = 0f;
        _declined[gauge] = true;
        _pending.Remove(gauge);
    }

    /// <summary>Remplace <paramref name="oldGraftId"/> par la greffe de la jauge (§13.3, cas slots pleins).</summary>
    public void Replace(string gauge, string oldGraftId)
    {
        var def = _config.GraftForGauge(gauge);
        if (def == null) { _pending.Remove(gauge); return; }

        if (_equipped.Remove(oldGraftId))
        {
            RemoveFromPlayer(oldGraftId);
            // La jauge de l'ancienne greffe reprend depuis sa valeur mémorisée (points intacts).
        }

        if (!_equipped.Contains(def.Id))
        {
            _equipped.Add(def.Id);
            _declined[gauge] = false;
            EquipOnPlayer(def);
            Discover(def.Id);
        }
        GraftsVersion++;
        _pending.Remove(gauge);
    }

    /// <summary>Conserve les 3 greffes actuelles (refus de remplacement) : même effet qu'un refus.</summary>
    public void Keep(string gauge) => Reject(gauge);

    // -------------------------------------------------------------------------
    // Fusions (§15)
    // -------------------------------------------------------------------------

    public IReadOnlyList<GraftTable.FusionDef> Fusions => _config.Fusions;
    public GraftTable.FusionDef? FusionForGauge(string gaugeKey) => _config.FusionForGauge(gaugeKey);

    /// <summary>
    /// Accepte une fusion (§15.1) : retire les 2 greffes sources et équipe la fusion → occupation 2→1
    /// (un slot se libère). Ne déclenche jamais d'écran de remplacement.
    /// </summary>
    public void AssimilateFusion(string gaugeKey)
    {
        var fusion = _config.FusionForGauge(gaugeKey);
        if (fusion != null && !_equipped.Contains(fusion.Id))
        {
            bool ready = true;
            foreach (var r in fusion.Requires) if (!_equipped.Contains(r)) { ready = false; break; }
            if (ready)
            {
                foreach (var r in fusion.Requires)          // retire les 2 greffes sources
                    if (_equipped.Remove(r)) RemoveFromPlayer(r);

                _equipped.Add(fusion.Id);                   // équipe la fusion (occupation −1)
                _declined[gaugeKey] = false;
                EquipOnPlayer(fusion);
                Discover(fusion.Id);
                GraftsVersion++;
            }
        }
        _pending.Remove(gaugeKey);
    }

    /// <summary>Refuse la fusion : jauge remise à 0, seuil ×1.5 au prochain cycle. Les 2 greffes restent.</summary>
    public void RejectFusion(string gaugeKey)
    {
        _points[gaugeKey] = 0f;
        _declined[gaugeKey] = true;
        _pending.Remove(gaugeKey);
    }

    public GraftTable.GraftDef? GraftForGauge(string gauge) => _config.GraftForGauge(gauge);
    public GraftTable.GraftDef? GraftById(string id) => _config.GraftById(id);

    // -------------------------------------------------------------------------
    // Application côté Player (délégué à GraftManager)
    // -------------------------------------------------------------------------

    private void EquipOnPlayer(GraftTable.GraftDef def)
    {
        var player = GameManager.Instance?.PlayerInstance;
        player?.Grafts?.Equip(def);
        FusionFlash.Instance?.TriggerFlash();
        AudioSystem.Instance?.PlaySfx("sfx_fusion_evolve");
    }

    private void RemoveFromPlayer(string graftId)
    {
        var player = GameManager.Instance?.PlayerInstance;
        player?.Grafts?.Unequip(graftId);
    }

    private static void Discover(string graftId)
        => GameSettings.Instance?.DiscoverGraft(graftId);
}
