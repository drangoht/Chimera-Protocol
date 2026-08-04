using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assimilation — <b>devenir une chimère</b>, troisième axe de progression du jeu (Lot 6).
///
/// <para>Chaque archétype d'ennemi alimente une <b>jauge</b> en le tuant ; quand elle se remplit, le
/// jeu propose la greffe correspondante. C'est le seul axe qui change une run <b>en cours</b> : le
/// Hub agit avant, l'arsenal pendant mais dans un registre connu, l'Assimilation transforme le
/// personnage lui-même.</para>
///
/// <para>Toute décision chiffrée vient de <see cref="GraftTable"/> (logique pure, partagée avec
/// Godot) : routage des éliminations, seuils effectifs, pénalité de refus, nombre d'emplacements.
/// Ce composant tient l'<b>état</b> et rien d'autre.</para>
/// </summary>
public static class Assimilation
{
    private static GraftTable.GraftConfig? _config;

    private static readonly Dictionary<string, int> _points = new(StringComparer.Ordinal);
    private static readonly Dictionary<string, int> _declined = new(StringComparer.Ordinal);
    private static readonly List<string> _equipped = new();
    private static readonly HashSet<string> _pending = new(StringComparer.Ordinal);

    private static int _slotCount = 3;
    private static double _gaugeSpeedBonus;

    /// <summary>Émis quand une jauge atteint son seuil : l'écran de proposition s'y branche.</summary>
    public static event Action<string>? GaugeFilled;

    /// <summary>Émis quand une greffe est équipée — le porteur applique alors ses effets.</summary>
    public static event Action<GraftTable.GraftDef>? GraftEquipped;

    /// <summary>Configuration lue depuis <c>grafts.json</c>.</summary>
    public static GraftTable.GraftConfig Config
    {
        get
        {
            if (_config != null) return _config;

            string? json = DataFiles.Load("grafts.json");
            _config = json != null ? GraftTable.Parse(json) : new GraftTable.GraftConfig();
            Debug.Log($"[Assimilation] {_config.Grafts.Count} greffes chargees.");
            return _config;
        }
    }

    /// <summary>Greffes actuellement portées.</summary>
    public static IReadOnlyList<string> Equipped => _equipped;

    /// <summary>Emplacements disponibles pour cette run.</summary>
    public static int SlotCount => _slotCount;

    /// <summary>Reste-t-il de la place ?</summary>
    public static bool HasFreeSlot => _equipped.Count < _slotCount;

    /// <summary>Points accumulés dans une jauge.</summary>
    public static int PointsOf(string gauge) => _points.TryGetValue(gauge, out int p) ? p : 0;

    /// <summary>
    /// Seuil effectif d'une jauge : celui des données, réduit par l'amélioration méta
    /// « métabolisme », puis relevé à chaque refus — refuser une greffe ne la fait pas disparaître,
    /// elle revient simplement plus tard.
    /// </summary>
    public static int ThresholdOf(string gauge)
    {
        int baseThreshold = Config.Thresholds.TryGetValue(gauge, out int t) ? t : 1;
        int effective = GraftTable.EffectiveThreshold(baseThreshold, _gaugeSpeedBonus);

        int declines = _declined.TryGetValue(gauge, out int d) ? d : 0;
        for (int i = 0; i < declines; i++)
            effective = GraftTable.DeclinedThreshold(effective, Config.DeclineThresholdMultiplier);

        return effective;
    }

    /// <summary>Remet l'état à zéro pour une nouvelle run, et relit les améliorations méta.</summary>
    public static void ResetForRun()
    {
        _points.Clear();
        _declined.Clear();
        _equipped.Clear();
        _pending.Clear();

        _slotCount = GraftTable.SlotCount(Config, MetaProgression.LevelOf("graft_slots"));
        _gaugeSpeedBonus = MetaProgression.LevelOf("graft_metabolism") * 0.10;
    }

    /// <summary>
    /// Route une élimination vers les jauges. Appelé à chaque mort d'ennemi — donc plusieurs
    /// centaines de fois par minute : rien ici ne doit allouer inutilement.
    /// </summary>
    public static void OnEnemyKilled(string aiType, bool isElite, bool isMiniBoss, bool isBoss)
    {
        if (Config.Grafts.Count == 0) return;

        foreach (var contribution in GraftTable.RouteKill(Config, aiType, isElite, isMiniBoss, isBoss))
        {
            string gauge = contribution.Gauge;

            // Une jauge dont la greffe est DÉJÀ portée est en pause : sans cela, le jeu proposerait
            // sans fin une greffe que le joueur possède, et la jauge d'un autre archétype ne
            // progresserait jamais en comparaison.
            var def = Config.GraftForGauge(gauge);
            if (def != null && _equipped.Contains(def.Id)) continue;
            if (_pending.Contains(gauge)) continue;

            _points[gauge] = PointsOf(gauge) + contribution.Points;

            if (_points[gauge] >= ThresholdOf(gauge))
            {
                _pending.Add(gauge);
                GaugeFilled?.Invoke(gauge);
            }
        }
    }

    /// <summary>
    /// Accepte la greffe d'une jauge remplie. Si les emplacements sont pleins,
    /// <paramref name="replaceId"/> désigne celle qui cède sa place.
    /// </summary>
    public static bool Accept(string gauge, string? replaceId = null)
    {
        var def = Config.GraftForGauge(gauge);
        if (def == null) return false;

        _pending.Remove(gauge);
        _points[gauge] = 0;

        if (!HasFreeSlot)
        {
            if (replaceId == null || !_equipped.Remove(replaceId)) return false;
        }

        _equipped.Add(def.Id);
        GameSettings.DiscoverGraft(def.Id);
        GraftEquipped?.Invoke(def);

        Debug.Log($"[Assimilation] greffe equipee : {def.Id} ({_equipped.Count}/{_slotCount}).");
        return true;
    }

    /// <summary>
    /// Refuse la greffe. Sa jauge repart de zéro avec un seuil <b>relevé</b> : le refus a un prix,
    /// sinon il serait toujours gratuit de dire non en attendant mieux.
    /// </summary>
    public static void Decline(string gauge)
    {
        _pending.Remove(gauge);
        _points[gauge] = 0;
        _declined[gauge] = (_declined.TryGetValue(gauge, out int d) ? d : 0) + 1;
    }

    /// <summary>Une greffe est-elle portée ?</summary>
    public static bool Has(string graftId) => _equipped.Contains(graftId);

    /// <summary>
    /// Perk de départ « emplacement bonus » : ajoute des emplacements pour la run courante, au-dessus
    /// de ceux dérivés de la méta. Remis à zéro au prochain <see cref="ResetForRun"/>.
    /// </summary>
    public static void AddBonusSlots(int count)
    {
        if (count <= 0) return;
        _slotCount += count;
    }

    /// <summary>
    /// Perk de départ « greffe offerte » : équipe d'office une greffe au début de la run. Elle occupe
    /// un emplacement — c'est une avance, pas un cadeau gratuit.
    /// </summary>
    public static bool GrantStartingGraft(string graftId)
    {
        if (_equipped.Contains(graftId)) return false;

        var def = Config.GraftById(graftId);
        if (def == null)
        {
            Debug.LogError($"[Assimilation] perk : greffe inconnue '{graftId}'.");
            return false;
        }

        _equipped.Add(graftId);
        GraftEquipped?.Invoke(def);
        return true;
    }

    /// <summary>Oublie la configuration chargée — réservé aux bancs.</summary>
    public static void Reset()
    {
        _config = null;
        ResetForRun();
    }
}
