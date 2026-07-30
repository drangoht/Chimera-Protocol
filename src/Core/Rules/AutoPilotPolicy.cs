using System.Collections.Generic;
using Vec = System.Numerics.Vector2;

/// <summary>
/// Politique de déplacement du <b>bot de banc</b> (<c>--auto-play</c>) — quelle direction prendre à
/// chaque instant, à partir des menaces, des orbes et des murs.
///
/// <para><b>Pourquoi cette classe existe.</b> Jusqu'ici le bot <c>--auto-play</c> ne se déplaçait
/// pas : il résolvait les modales (level-up, assimilation) et restait planté au centre. Deux
/// conséquences qui vidaient le banc de sa substance :</para>
/// <list type="bullet">
///   <item>sans <c>--invuln</c> il mourait en ~20 s, donc <b>aucune mesure de survie</b> ;</item>
///   <item>avec <c>--invuln</c> il survivait, mais la colonne des dégâts subis valait zéro — là
///         encore, rien à mesurer sur la seule question qui intéressait l'équilibrage d'overtime.</item>
/// </list>
/// <para>S'y ajoutait un biais de progression : immobile, il ramassait peu d'XP et plafonnait au
/// niveau ~73 en 17 min de jeu là où une session jouée atteint 124 dès la 13ᵉ (cf.
/// <c>DebugHooks.SaturateArsenal</c>). Le banc ne mesurait donc pas la même run que le joueur.</para>
///
/// <para><b>Ce que cette politique n'est pas.</b> Elle ne prétend pas jouer aussi bien qu'un humain,
/// et ses chiffres absolus ne valent pas validation d'équilibrage. Elle vise la <b>reproductibilité</b> :
/// la même politique face à deux réglages produit un écart imputable au réglage, pas au joueur. C'est
/// exactement ce qui manquait — le relevé du 2026-07-29 montre deux sessions humaines différant d'un
/// facteur 2,4 en survie <i>à l'entrée en overtime, là où le réglage testé n'a encore aucun effet</i>.</para>
///
/// <para><b>Méthode : échantillonnage directionnel.</b> On note <see cref="Directions"/> caps répartis
/// sur le cercle en projetant la position à <see cref="LookAheadPx"/> px, et on garde le meilleur
/// score. Un champ de potentiel (somme de répulsions) serait plus court mais s'annule quand le joueur
/// est <b>encerclé</b> — précisément le cas dominant en overtime — et colle aux coins. L'échantillonnage
/// choisit naturellement le secteur le moins dense et refuse les caps qui sortent de l'arène.</para>
/// </summary>
public static class AutoPilotPolicy
{
    /// <summary>Nombre de caps testés (résolution angulaire de 22,5°).</summary>
    public const int Directions = 16;

    /// <summary>
    /// Longueur du couloir de fuite évalué, en pixels. Volontairement longue : le joueur court plus
    /// vite que la faune, sa seule ressource est la <b>ligne droite</b>. Une projection courte (90 px,
    /// premier essai) faisait zigzaguer le bot dans la nuée — il mourait vers la 4ᵉ minute, sans
    /// jamais voir l'overtime que le banc est censé mesurer.
    /// </summary>
    public const float LookAheadPx = 220f;

    /// <summary>
    /// Points évalués le long du couloir. Un seul point (l'arrivée) laisse choisir un cap qui
    /// « saute » par-dessus un mur d'ennemis : c'est la traversée entière qui doit être sûre, pas la
    /// destination.
    /// </summary>
    public const int Steps = 3;

    /// <summary>Au-delà, une menace n'entre plus dans le score (coupe le coût avec 300 ennemis).</summary>
    public const float ThreatRadiusPx = 320f;

    /// <summary>Au-delà, un orbe n'attire plus : le bot ne traverse pas la carte pour un orbe.</summary>
    public const float PickupRadiusPx = 300f;

    /// <summary>Marge à partir de laquelle le mur commence à repousser.</summary>
    public const float WallMarginPx = 120f;

    /// <summary>Poids du terme de menace (le danger prime sur tout le reste).</summary>
    public const float ThreatWeight = 1f;

    /// <summary>Poids du terme d'attraction des orbes.</summary>
    public const float PickupWeight = 0.35f;

    /// <summary>Poids de la pénalité de mur.</summary>
    public const float WallWeight = 2.5f;

    /// <summary>
    /// Pénalité de changement de cap. Sans elle le bot vibre entre deux caps de score voisin et
    /// n'avance pas — un sur-place qui se lit comme de la mortalité alors qu'il s'agit d'un artefact.
    /// Volontairement élevée : la survie tient à des fuites longues et engagées, pas à des corrections
    /// permanentes.
    /// </summary>
    public const float TurnWeight = 0.6f;

    /// <summary>
    /// Direction à prendre (normalisée), ou <see cref="Vec.Zero"/> si aucun cap n'est jouable.
    /// </summary>
    /// <param name="self">Position du joueur.</param>
    /// <param name="previous">Direction retenue à l'appel précédent (inertie) ; zéro si aucune.</param>
    /// <param name="threats">Positions des menaces (ennemis, projectiles).</param>
    /// <param name="pickups">Positions des orbes à ramasser.</param>
    /// <param name="halfWidth">Demi-largeur jouable de l'arène (murs déduits).</param>
    /// <param name="halfHeight">Demi-hauteur jouable de l'arène (murs déduits).</param>
    public static Vec ChooseDirection(
        Vec self,
        Vec previous,
        IReadOnlyList<Vec> threats,
        IReadOnlyList<Vec> pickups,
        float halfWidth,
        float halfHeight)
    {
        Vec best = Vec.Zero;
        float bestScore = float.NegativeInfinity;

        for (int i = 0; i < Directions; i++)
        {
            double angle = 2.0 * System.Math.PI * i / Directions;
            var dir = new Vec((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));
            float score = ScoreDirection(self, previous, dir, threats, pickups, halfWidth, halfHeight);
            if (score > bestScore)
            {
                bestScore = score;
                best = dir;
            }
        }

        return best;
    }

    /// <summary>
    /// Score d'un cap : plus il est haut, plus le cap est souhaitable. Exposé pour que les tests
    /// puissent comparer deux caps sans passer par la boucle de sélection.
    /// </summary>
    public static float ScoreDirection(
        Vec self,
        Vec previous,
        Vec direction,
        IReadOnlyList<Vec> threats,
        IReadOnlyList<Vec> pickups,
        float halfWidth,
        float halfHeight)
    {
        float score = 0f;

        // Le couloir est évalué en plusieurs points : c'est la traversée qui doit être sûre. Les pas
        // lointains pèsent moins (l'avenir est incertain, et la foule aura bougé).
        for (int step = 1; step <= Steps; step++)
        {
            float travel = LookAheadPx * step / Steps;
            float weight = 1f / step;
            Vec probe = self + direction * travel;

            // --- Menaces : coût en 1/d, borné pour qu'un contact ne fasse pas exploser le score. Le
            // 1/d (et non 1/d²) garde une pente utile à moyenne distance : en overtime, TOUTES les
            // directions sont mauvaises de près, et c'est le champ lointain qui doit départager.
            if (threats != null)
            {
                foreach (var t in threats)
                {
                    float d = Vec.Distance(probe, t);
                    if (d > ThreatRadiusPx) continue;
                    score -= weight * ThreatWeight * (ThreatRadiusPx / System.Math.Max(d, 24f));
                }
            }

            // --- Orbes : bonus décroissant. Poids faible devant les menaces — le bot ne doit jamais
            // plonger dans la foule pour un orbe, mais doit ramasser ce qui est sur son chemin (sans
            // quoi sa courbe de niveau ne ressemble à aucune run jouée).
            if (pickups != null)
            {
                foreach (var p in pickups)
                {
                    float d = Vec.Distance(probe, p);
                    if (d > PickupRadiusPx) continue;
                    score += weight * PickupWeight * (1f - d / PickupRadiusPx);
                }
            }

            // --- Murs : un cap qui sort de l'arène est éliminé, et l'approche du bord est pénalisée
            // progressivement. Sans ce terme, fuir la foule revient à se coincer dans un coin — la
            // mort la plus fréquente des bots de kiting, et un biais systématique sur la survie.
            float overX = System.Math.Abs(probe.X) - (halfWidth  - WallMarginPx);
            float overY = System.Math.Abs(probe.Y) - (halfHeight - WallMarginPx);
            if (System.Math.Abs(probe.X) > halfWidth || System.Math.Abs(probe.Y) > halfHeight)
                score -= 1000f * weight;
            if (overX > 0f) score -= weight * WallWeight * (overX / WallMarginPx) * ThreatRadiusPx / 100f;
            if (overY > 0f) score -= weight * WallWeight * (overY / WallMarginPx) * ThreatRadiusPx / 100f;
        }

        // --- Inertie : favorise le maintien du cap à score voisin (cf. TurnWeight).
        if (previous != Vec.Zero)
        {
            float alignment = Vec.Dot(Vec.Normalize(previous), direction); // -1 (demi-tour) … 1 (tout droit)
            score += TurnWeight * alignment * ThreatRadiusPx / 100f;
        }

        return score;
    }
}
