using System.Collections.Generic;
using UnityEngine;
using Vec = System.Numerics.Vector2;

/// <summary>
/// Pont entre la scène et <see cref="AutoPilotPolicy"/> : relève les menaces et les orbes autour du
/// joueur, puis lui impose un cap. Actif <b>uniquement</b> sous <c>--auto-play</c>.
///
/// <para><b>Pourquoi un bot qui se déplace change tout.</b> Un bot immobile meurt en vingt secondes
/// sans <c>--invuln</c> — donc aucune survie mesurable ; et avec <c>--invuln</c>, la colonne des
/// dégâts subis vaut zéro — donc rien à mesurer non plus sur la seule question qui intéresse
/// l'équilibrage de fin de partie. Il faut qu'il kite, ramasse et esquive pour que <b>mourir</b>
/// redevienne une information.</para>
///
/// <para>Toute la décision vit dans la règle pure, testée ; ici on ne fait que lire la scène, et à
/// cadence réduite : parcourir deux listes de trois cents entités soixante fois par seconde coûterait
/// plus cher que le jeu lui-même, et un cap réévalué toutes les <see cref="RepathInterval"/> secondes
/// suffit très largement — un humain ne corrige pas plus vite.</para>
///
/// <para>⚠ <b>Limite assumée</b> : les projectiles ennemis ne comptent pas comme menaces. Le bot
/// esquive la foule, pas les tirs. En overtime la mortalité vient massivement du contact, mais un
/// relevé sur un biome à tireurs sous-estimera la difficulté qu'un humain ressent.</para>
/// </summary>
public sealed class BenchAutoPilot : MonoBehaviour
{
    /// <summary>Période de réévaluation du cap, en secondes de jeu.</summary>
    public const float RepathInterval = 0.15f;

    /// <summary>Plafond de menaces retenues (les plus proches) — borne le coût en nuée.</summary>
    public const int MaxThreats = 40;

    /// <summary>Plafond d'orbes retenus.</summary>
    public const int MaxPickups = 12;

    /// <summary>Distance sous laquelle le bot déclenche sa ruade, s'il en dispose.</summary>
    public const float DashPanicPx = 110f;

    private Vector2 _direction;
    private float _sinceRepath;

    /// <summary>Réévaluations effectuées — observable par les bancs.</summary>
    public int Repaths { get; private set; }

    private void Update()
    {
        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        _sinceRepath += Time.deltaTime;

        if (_sinceRepath >= RepathInterval)
        {
            _sinceRepath = 0f;
            Repath(player);
        }

        // Le cap est imposé À CHAQUE IMAGE, pas seulement au recalcul : c'est le même champ que lit
        // le clavier, donc le mouvement traverse exactement le chemin d'un joueur humain — sinon la
        // mesure porterait sur autre chose que le jeu.
        player.ExternalMoveOverride = _direction;
    }

    private void Repath(Player player)
    {
        Repaths++;

        Vector2 self = player.transform.position;

        var threats = CollectEnemies(self, AutoPilotPolicy.ThreatRadiusPx + AutoPilotPolicy.LookAheadPx,
                                     MaxThreats, out float nearest);
        var pickups = CollectOrbs(self, AutoPilotPolicy.PickupRadiusPx + AutoPilotPolicy.LookAheadPx,
                                  MaxPickups);

        // Une ruade quand ça touche de trop près. Elle passe par le même point d'entrée que la touche
        // du joueur : le bot ne peut pas dasher plus souvent que sa recharge ne le permet.
        if (nearest <= DashPanicPx) player.TriggerDashForBench();

        var chosen = AutoPilotPolicy.ChooseDirection(
            new Vec(self.x, self.y),
            new Vec(_direction.x, _direction.y),
            threats, pickups,
            Arena.HalfWidth - WallMargin, Arena.HalfHeight - WallMargin);

        _direction = new Vector2(chosen.X, chosen.Y);
    }

    /// <summary>Épaisseur du mur, retirée du demi-format pour que le bot ne vise pas la bordure.</summary>
    private const float WallMargin = 24f;

    private static List<Vec> CollectEnemies(Vector2 self, float radius, int max, out float nearest)
    {
        var found = new List<(float Dist, Vec Pos)>();
        nearest = float.MaxValue;

        foreach (var enemy in EnemyBase.Active)
        {
            if (enemy == null || enemy.IsDead) continue;

            float d = Vector2.Distance(self, enemy.transform.position);
            if (d < nearest) nearest = d;
            if (d > radius) continue;

            found.Add((d, new Vec(enemy.transform.position.x, enemy.transform.position.y)));
        }

        return Nearest(found, max);
    }

    private static List<Vec> CollectOrbs(Vector2 self, float radius, int max)
    {
        var found = new List<(float Dist, Vec Pos)>();

        foreach (var orb in XpOrb.Active)
        {
            if (orb == null) continue;

            float d = Vector2.Distance(self, orb.transform.position);
            if (d > radius) continue;

            found.Add((d, new Vec(orb.transform.position.x, orb.transform.position.y)));
        }

        return Nearest(found, max);
    }

    /// <summary>
    /// Les <paramref name="max"/> plus proches. Le tri n'a lieu que si le plafond est dépassé : en
    /// début de run la liste est courte, et en nuée on paie un tri sur quelques dizaines d'éléments
    /// toutes les 0,15 s — négligeable.
    /// </summary>
    private static List<Vec> Nearest(List<(float Dist, Vec Pos)> found, int max)
    {
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
