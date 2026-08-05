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

            // Jauge en pause si sa greffe a été ABSORBÉE par une fusion équipée. Sans cela, le jeu
            // re-proposerait une greffe que le joueur porte déjà sous forme fusionnée, et il
            // pourrait la ré-équiper à côté de sa propre fusion — soit l'effet compté deux fois.
            if (def != null && IsFusionSourceEquipped(def.Id)) continue;

            if (_pending.Contains(gauge)) continue;

            _points[gauge] = PointsOf(gauge) + contribution.Points;

            if (_points[gauge] >= ThresholdOf(gauge))
            {
                _pending.Add(gauge);
                GaugeFilled?.Invoke(gauge);
            }
        }

        RouteFusionKill(aiType, isElite, isMiniBoss, isBoss);
    }

    /// <summary>
    /// Jauges de <b>fusion</b> — le mécanisme qui empêche les greffes de s'empiler sans fin.
    ///
    /// <para>Deux greffes portées ensemble finissent par se lier : la fusion prend leur place à
    /// toutes les deux et <b>libère un emplacement</b> (occupation 2 → 1). C'est la récompense
    /// structurelle qui rend un couple désirable — sans elle, le joueur accumule des greffes
    /// côte à côte et leurs effets s'additionnent indéfiniment.</para>
    ///
    /// <para>Une jauge de fusion n'accumule qu'à <b>trois</b> conditions, toutes reprises du design
    /// (§15.1) : les deux prérequis sont équipés <i>à l'instant du kill</i>, la victime est un
    /// basique ou une élite d'un archétype source, et la fusion n'est ni portée ni déjà proposée.
    /// Les champions et le boss ne comptent pas — la fusion se mérite sur la nuée des deux
    /// espèces qu'elle lie.</para>
    /// </summary>
    private static void RouteFusionKill(string aiType, bool isElite, bool isMiniBoss, bool isBoss)
    {
        if (isMiniBoss || isBoss) return;

        foreach (var fusion in Config.Fusions)
        {
            if (_equipped.Contains(fusion.Id)) continue;
            if (_pending.Contains(fusion.GaugeKey)) continue;

            bool ready = true;
            foreach (string required in fusion.Requires)
                if (!_equipped.Contains(required)) { ready = false; break; }

            if (!ready) continue;

            int points = fusion.KillPoints(aiType, isElite);
            if (points <= 0) continue;

            _points[fusion.GaugeKey] = PointsOf(fusion.GaugeKey) + points;

            if (_points[fusion.GaugeKey] >= ThresholdOf(fusion.GaugeKey))
            {
                _pending.Add(fusion.GaugeKey);
                GaugeFilled?.Invoke(fusion.GaugeKey);
            }
        }
    }

    /// <summary>
    /// La greffe est-elle <b>absorbée</b> par une fusion portée ? Sa jauge est alors en pause :
    /// c'est la garde anti double-effet.
    /// </summary>
    public static bool IsFusionSourceEquipped(string graftId)
    {
        foreach (string id in _equipped)
        {
            var fusion = Config.FusionById(id);
            if (fusion != null && fusion.Requires.Contains(graftId)) return true;
        }

        return false;
    }

    /// <summary>
    /// Accepte la greffe d'une jauge remplie. Si les emplacements sont pleins,
    /// <paramref name="replaceId"/> désigne celle qui cède sa place.
    /// </summary>
    public static bool Accept(string gauge, string? replaceId = null)
    {
        // Une jauge de fusion se résout autrement : elle retire deux greffes pour en poser une.
        if (AcceptFusion(gauge)) return true;

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

    /// <summary>
    /// Résout une jauge de fusion : les <b>deux</b> greffes sources cèdent leur place à la fusion.
    ///
    /// <para>L'occupation passe de 2 à 1, et c'est délibéré : une fusion est ainsi <b>toujours
    /// acceptable, même emplacements pleins</b> — elle devient la soupape quand on sature, jamais un
    /// écran de remplacement. Sans cette libération, viser un couple n'aurait aucun intérêt.</para>
    ///
    /// <para>Il n'y a <b>pas de défusion</b> : deux Rouilles liées ne se re-séparent pas. Relâcher la
    /// fusion, plus tard, les relâche toutes les deux d'un coup.</para>
    /// </summary>
    /// <returns>Faux si la jauge n'est pas une jauge de fusion — l'appelant traite alors une greffe.</returns>
    private static bool AcceptFusion(string gauge)
    {
        var fusion = Config.FusionForGauge(gauge);
        if (fusion == null || _equipped.Contains(fusion.Id)) return false;

        // Les prérequis peuvent avoir été remplacés entre le remplissage de la jauge et la réponse
        // du joueur : la fusion n'est alors plus possible, et la refuser silencieusement vaut mieux
        // que d'équiper une fusion dont les sources ont disparu.
        foreach (string required in fusion.Requires)
            if (!_equipped.Contains(required)) return false;

        _pending.Remove(gauge);
        _points[gauge] = 0;

        foreach (string required in fusion.Requires) _equipped.Remove(required);

        _equipped.Add(fusion.Id);
        GameSettings.DiscoverGraft(fusion.Id);
        GraftEquipped?.Invoke(fusion);

        Debug.Log($"[Assimilation] fusion forgee : {fusion.Id} " +
                  $"(absorbe {string.Join(" + ", fusion.Requires)}, {_equipped.Count}/{_slotCount}).");
        return true;
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
