using UnityEngine;

/// <summary>
/// Comportements de déplacement du bestiaire (Lot 4).
///
/// <para>Le jeu compte <b>31 ennemis pour une poignée de comportements</b> : un ennemi est une
/// entrée de données, pas une classe. Cette structure est délibérée et doit survivre au portage —
/// la reproduire par 31 classes reviendrait à multiplier par sept la surface de code pour un
/// résultat identique.</para>
///
/// <para>Chaque comportement répond à une question de placement différente, et c'est cela qui fait
/// la variété perçue : foncer, zigzaguer, garder ses distances, ou avancer lentement mais
/// encaisser.</para>
/// </summary>
public static class EnemyAi
{
    /// <summary>
    /// Calcule la position suivante. Fonction <b>pure vis-à-vis de l'état du monde</b> : elle ne
    /// déplace rien, elle renvoie où aller — ce qui la rend vérifiable et rejouable.
    /// </summary>
    /// <param name="ai">Comportement issu des données.</param>
    /// <param name="self">Position actuelle de l'ennemi.</param>
    /// <param name="target">Position du joueur.</param>
    /// <param name="speed">Vitesse effective, ralentissements compris.</param>
    /// <param name="dt">Pas de temps.</param>
    /// <param name="phase">
    /// Phase propre à l'entité, entretenue par l'appelant. Elle porte le zigzag de l'IA erratique
    /// et l'oscillation du kiter : sans état par entité, une nuée entière zigzaguerait à
    /// l'unisson — un défaut immédiatement visible.
    /// </param>
    public static Vector2 Step(EnemyTable.AiType ai, Vector2 self, Vector2 target,
                               float speed, float dt, ref float phase)
    {
        Vector2 to = target - self;
        float dist = to.magnitude;
        if (dist < 0.001f) return self;

        Vector2 dir = to / dist;
        phase += dt;

        switch (ai)
        {
            case EnemyTable.AiType.ErraticChase:
            {
                // Zigzag perpendiculaire à l'approche : la trajectoire reste convergente, mais
                // devient difficile à anticiper pour une arme à visée.
                Vector2 side = new(-dir.y, dir.x);
                float wobble = Mathf.Sin(phase * 6f) * 0.55f;
                dir = (dir + side * wobble).normalized;
                break;
            }

            case EnemyTable.AiType.RangedKiter:
            case EnemyTable.AiType.ConeKiter:
            {
                // Garde sa distance : approche de loin, recule de près, tourne autour entre les
                // deux. C'est ce qui force le joueur à venir le chercher.
                const float preferred = 250f;
                if (dist > preferred * 1.15f) { /* approche */ }
                else if (dist < preferred * 0.85f) dir = -dir;
                else dir = new Vector2(-dir.y, dir.x);
                break;
            }

            case EnemyTable.AiType.SlowHunter:
                // Avance sans détour : sa menace vient de sa masse, pas de sa trajectoire.
                break;

            case EnemyTable.AiType.LungingChaser:
            {
                // Alterne marche et bonds : la vitesse moyenne reste modérée, mais les pointes
                // rendent le kiting risqué.
                float lunge = Mathf.Max(0f, Mathf.Sin(phase * 2.2f));
                speed *= 0.6f + lunge * 1.6f;
                break;
            }

            case EnemyTable.AiType.ChargingBruiser:
            {
                // Charge par à-coups, avec un temps mort lisible entre deux — le télégraphe est
                // ce qui rend la charge évitable, donc juste.
                float cycle = Mathf.Repeat(phase, 3f);
                speed *= cycle < 2f ? 0.45f : 2.6f;
                break;
            }

            case EnemyTable.AiType.ShieldedChaser:
                // Lent mais protégé : la réduction se joue côté dégâts, pas ici.
                speed *= 0.8f;
                break;

            case EnemyTable.AiType.BossCore:
                // ⚠ Le boss AVANCE vers le joueur — lentement (46 px/s de fiche), mais sans jamais
                // s'arrêter. Le port l'avait figé sur place, et le symptôme en jeu était sans appel :
                // « le boss se voit mais n'approche pas, il reste bloqué en haut de l'écran ». Sa
                // lenteur EST sa signature : elle laisse le temps de manœuvrer sans jamais offrir de
                // répit, là où un boss immobile se contourne et s'oublie.
                break;

            case EnemyTable.AiType.StraightChase:
            default:
                break;
        }

        return self + dir * speed * dt;
    }
}
