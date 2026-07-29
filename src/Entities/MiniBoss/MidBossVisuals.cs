using Godot;

/// <summary>
/// Dimensionnement visuel des mid-boss de biome (Colosse en Fusion, Sentinelle Cryo, Gardien Néon).
///
/// <b>Pourquoi cette classe existe.</b> Les trois mid-boss ont été livrés avec un sprite de
/// <b>48×48</b>, justifié dans <c>tools/generate_midboss_sprites.py</c> par « plus imposant que la
/// faune (32) sans égaler le boss de fin (64) ». La prémisse est fausse : le boss de fin n'est pas à
/// 64 à l'écran, il est rendu à <c>Scale = 2,4</c> (cf. <c>RustedCore._Ready</c>), soit <b>154 px</b>.
/// Le vrai voisinage d'un mid-boss, ce sont ses pairs de rôle <c>mini_boss</c> — <c>rust_stalker</c>,
/// <c>aether_revenant</c>, <c>master_sentinel</c> — qui ont tous un sprite natif de <b>64</b>. Les
/// trois nouveaux étaient donc 25 % plus petits que <i>tous</i> les autres champions, et non
/// seulement que le boss.
///
/// <b>Ce n'est pas qu'un défaut esthétique : la hitbox débordait du sprite.</b> Leurs statistiques ont
/// été écrites pour un champion de la taille de ses pairs — le Colosse a un <c>contactRadius</c> de
/// 36, soit un diamètre dangereux de <b>72 px</b>, pour un corps qui n'en occupait que 48 à l'écran.
/// Le joueur encaissait à une distance où il ne voyait rien : la zone de contact doit être ce qu'il
/// voit, pas une marge invisible autour.
///
/// <b>Cible retenue : 72 px</b> (<c>48 × 1,5</c>), calée sur le <c>contactRadius</c> du Colosse plutôt
/// que sur un jugement de goût. Le facteur 1,5 conserve des pixels réguliers (alternance 1/2 unité) et
/// place les champions de biome entre les mini-boss globaux (64) et le boss de fin (154) — une
/// hiérarchie qui suit le rôle.
///
/// <b>Pourquoi agrandir plutôt que régénérer les PNG en 72.</b> C'est le procédé déjà retenu pour le
/// boss de fin. Le générateur dessine dans un espace logique de 48 en coordonnées entières
/// (<c>rect</c>/<c>disc</c> itèrent sur <c>range(int(y0), int(y1)+1)</c>) : y injecter un facteur
/// laisserait des rangées vides entre les formes. Avec <c>texture_filter = Nearest</c>, le résultat à
/// l'écran est identique à un agrandissement au plus proche voisin.
/// </summary>
public static class MidBossVisuals
{
    /// <summary>
    /// Facteur appliqué à l'<c>AnimatedSprite2D</c> des mid-boss : 48 × 1,5 = <b>72 px</b> à l'écran.
    /// </summary>
    public const float SpriteScale = 1.5f;

    /// <summary>
    /// Applique <see cref="SpriteScale"/> à <paramref name="sprite"/>. Ne touche qu'au sprite : les
    /// <see cref="ChampionOverlay"/> (bouclier orbital, cône de gel) sont dessinés en unités monde
    /// depuis la racine et ne doivent pas suivre l'échelle du corps, sous peine de désaccorder leur
    /// portée de la portée réelle de l'effet.
    /// </summary>
    public static void ApplyTo(AnimatedSprite2D? sprite)
    {
        if (sprite != null)
            sprite.Scale = new Vector2(SpriteScale, SpriteScale);
    }
}
