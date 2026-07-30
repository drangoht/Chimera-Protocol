using System.Collections.Generic;
using Godot;
using Vec = System.Numerics.Vector2;

/// <summary>
/// Pont entre la scène et <see cref="AutoPilotPolicy"/> : collecte les menaces et les orbes autour du
/// joueur, puis renvoie la direction à jouer. Actif <b>uniquement</b> sous <c>--auto-play</c>.
///
/// <para>Toute la décision vit dans la règle pure (testée) ; ici on ne fait que lire la scène, à
/// cadence réduite. Le recalcul n'a pas lieu à chaque frame : à 300 ennemis, parcourir deux groupes
/// 60 fois par seconde coûterait plus cher que le reste du banc, et un cap réévalué toutes les
/// <see cref="RepathInterval"/> s de jeu suffit très largement à kiter — un humain ne corrige pas
/// plus vite.</para>
///
/// <para><b>Limite assumée</b> : les projectiles ennemis ne sont pas comptés comme menaces (ils ne
/// sont dans aucun groupe). Le bot esquive la foule, pas les tirs. En overtime la mortalité vient
/// massivement du contact, mais un relevé sur un biome à tireurs (Néon) sous-estimera légèrement la
/// difficulté ressentie par un humain.</para>
/// </summary>
public sealed class BenchAutoPilot
{
    /// <summary>Période de réévaluation du cap, en secondes de jeu.</summary>
    public const float RepathInterval = 0.15f;

    /// <summary>Plafond de menaces retenues (les plus proches) — borne le coût en nuée.</summary>
    public const int MaxThreats = 40;

    /// <summary>Plafond d'orbes retenus.</summary>
    public const int MaxPickups = 12;

    /// <summary>Distance sous laquelle le bot déclenche son dash s'il en dispose.</summary>
    public const float DashPanicPx = 110f;

    private Vector2 _direction = Vector2.Zero;
    private float   _sinceRepath;
    private bool    _wantsDash;

    /// <summary>Vrai si une menace est assez proche pour justifier une ruade (lu par le Player).</summary>
    public bool WantsDash => _wantsDash;

    /// <summary>
    /// Direction à jouer cette frame. Recalculée toutes les <see cref="RepathInterval"/> s de jeu,
    /// conservée entre deux recalculs.
    /// </summary>
    public Vector2 Steer(Node2D player, float delta)
    {
        _sinceRepath += delta;
        if (_sinceRepath < RepathInterval) return _direction;
        _sinceRepath = 0f;

        var tree = player.GetTree();
        if (tree == null) return _direction;

        var self = player.GlobalPosition;

        var threats = Collect(tree, Constants.GroupEnemies, self,
                              AutoPilotPolicy.ThreatRadiusPx + AutoPilotPolicy.LookAheadPx,
                              MaxThreats, out float nearest);
        var pickups = Collect(tree, Constants.GroupXpOrbs, self,
                              AutoPilotPolicy.PickupRadiusPx + AutoPilotPolicy.LookAheadPx,
                              MaxPickups, out _);

        _wantsDash = nearest <= DashPanicPx;

        float halfW = Constants.ArenaWidth  / 2f - Constants.WallThickness;
        float halfH = Constants.ArenaHeight / 2f - Constants.WallThickness;

        var dir = AutoPilotPolicy.ChooseDirection(
            new Vec(self.X, self.Y),
            new Vec(_direction.X, _direction.Y),
            threats, pickups, halfW, halfH);

        _direction = new Vector2(dir.X, dir.Y);
        return _direction;
    }

    /// <summary>
    /// Positions du groupe demandé dans un rayon, tronquées aux <paramref name="max"/> plus proches.
    /// Le tri n'a lieu que si le plafond est dépassé : en début de run la liste est courte, et en
    /// nuée on paie un tri sur quelques dizaines d'éléments toutes les 0,15 s — négligeable.
    /// </summary>
    private static List<Vec> Collect(SceneTree tree, string group, Vector2 self,
                                     float radius, int max, out float nearest)
    {
        var found = new List<(float Dist, Vec Pos)>();
        nearest = float.MaxValue;

        foreach (var node in tree.GetNodesInGroup(group))
        {
            if (node is not Node2D n || !GodotObject.IsInstanceValid(n)) continue;
            float d = self.DistanceTo(n.GlobalPosition);
            if (d < nearest) nearest = d;
            if (d > radius) continue;
            found.Add((d, new Vec(n.GlobalPosition.X, n.GlobalPosition.Y)));
        }

        if (found.Count > max)
        {
            found.Sort((a, b) => a.Dist.CompareTo(b.Dist));
            found.RemoveRange(max, found.Count - max);
        }

        var result = new List<Vec>(found.Count);
        foreach (var f in found) result.Add(f.Pos);
        return result;
    }
}
