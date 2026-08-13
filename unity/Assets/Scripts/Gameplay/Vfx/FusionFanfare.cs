using UnityEngine;

/// <summary>
/// La forge d'une <b>fusion</b>, vue depuis l'arène — le seul événement de la run qui transforme
/// définitivement l'arsenal.
///
/// <para><b>Ce qui existait avant.</b> Rien. <c>ApplyFusion</c> retirait une arme, en installait une
/// autre, jouait <c>sfx_fusion_evolve</c> et écrivait une ligne de journal. À l'écran : la modale se
/// referme, le jeu reprend, et une arme a discrètement changé de forme au milieu d'une nuée. La carte
/// la plus rare du jeu — celle qui demande une arme au niveau requis <i>et</i> le passif associé —
/// s'appliquait plus discrètement qu'un ramassage d'orbe d'XP.</para>
///
/// <para><b>Trois temps, pas un flash.</b> Un effet ponctuel se confond avec les impacts qui
/// l'entourent : dans un survivor, il y a en permanence quelque chose qui explose. Ce qui distingue
/// un événement rare, c'est sa <b>durée</b> et son <b>rythme</b> — trois ondes qui se succèdent sur
/// 0,45 s ne ressemblent à aucune mort d'ennemi, aussi grosse soit-elle. Le ralenti fait le reste :
/// il dit que le jeu lui-même s'interrompt pour ça.</para>
/// </summary>
/// <remarks>
/// ⚠ Le séquencement ignore l'échelle de temps. Il se joue <b>pendant</b> le ralenti qu'il déclenche
/// lui-même : compté en temps de jeu, un intervalle de 0,14 s durerait près d'une seconde à 18 % de
/// vitesse, et les trois ondes se détacheraient au lieu de s'enchaîner.
/// </remarks>
public static class FusionFanfare
{
    /// <summary>Or #FFCC44 — la couleur de la récompense dans tout le jeu.</summary>
    private static readonly Color Gold = new(1.000f, 0.800f, 0.267f);

    /// <summary>Violet #AA44FF — la couleur de la chimère, donc celle de ce qu'on devient.</summary>
    private static readonly Color Violet = new(0.667f, 0.267f, 1.000f);

    /// <summary>
    /// Joue la fanfare au point <paramref name="at"/>.
    /// </summary>
    /// <param name="owner">
    /// Propriétaire du séquencement — le joueur. S'il meurt entre deux ondes, l'effet s'arrête au
    /// lieu de continuer à peindre l'arène d'un événement qui n'a plus de sujet.
    /// </param>
    public static void Play(Vector2 at, UnityEngine.Object? owner = null)
    {
        // ─── Temps 0 : l'éclat ────────────────────────────────────────────────
        Vfx.Glow(at, Gold, 110f, 1.3f, 0.30f);
        Vfx.Shockwave(at, 190f, 0.42f, Gold);
        Vfx.Ring(at, 64f, Violet, 5f, 0.45f);
        Rays(at, 12, 70f, 230f, Gold);

        Vfx.Burst(at, Gold, new Color(Violet.r, Violet.g, Violet.b, 0f),
                  44, 150f, 380f, 7f, 0.55f);

        ScreenShake.Shake(8f, 0.32f);

        // Tenue courte et remontée rapide : le joueur est au milieu d'une nuée et reste vulnérable
        // pendant l'effet. Un ralenti de récompense qui le fait toucher se retourne contre lui.
        HitStop.Trigger(holdSeconds: 0.07f, rampSeconds: 0.28f, holdScale: 0.20f);

        // ─── Temps 0,14 et 0,30 : les deux ondes qui suivent ──────────────────
        GTween.Create(owner, ignoreTimeScale: true)
              .AppendInterval(0.14f)
              .AppendCallback(() =>
              {
                  Vfx.Shockwave(at, 290f, 0.48f, Violet);
                  Rays(at, 8, 90f, 200f, Violet);
              })
              .AppendInterval(0.16f)
              .AppendCallback(() =>
              {
                  Vfx.Shockwave(at, 380f, 0.52f, Gold);
                  Vfx.Glow(at, Violet, 70f, 0.8f, 0.35f);
              });
    }

    /// <summary>
    /// Couronne de rayons partant d'un anneau intérieur — jamais du centre exact.
    /// </summary>
    /// <remarks>
    /// Le trou au milieu n'est pas un détail de forme : des rayons issus d'un point unique se
    /// superposent tous au même endroit et y saturent au blanc, ce qui donne un projecteur posé sur
    /// le joueur — et le masque. En partant à 70 px, la couronne <b>encadre</b> le joueur au lieu de
    /// l'effacer, ce qui compte quand le jeu reprend une demi-seconde plus tard.
    /// </remarks>
    private static void Rays(Vector2 at, int count, float inner, float outer, Color color)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f + 0.13f;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

            Vfx.Beam(at + dir * inner, at + dir * outer, color, 4f, 0.26f);
        }
    }
}
