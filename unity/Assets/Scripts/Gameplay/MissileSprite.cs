using UnityEngine;

/// <summary>
/// Silhouette du missile de l'Essaim Traqueur : un <b>fuselage à ogive</b> avec ailerons arrière et
/// flamme de propulsion — la forme que dessine son icône (<c>ui_icon_seeker</c>).
///
/// <para><b>Ce qu'elle remplace.</b> Le missile empruntait <c>weapon_bullet_rail</c> teinté en or —
/// une barre droite. Rien ne cassait : il partait, suivait sa cible, touchait. Mais à l'écran
/// l'Essaim Traqueur <b>tirait des traits</b>, exactement comme le Canon à Impulsions, alors que sa
/// carte montre un missile violet à traînée. Signalé en jouant le 2026-08-09 : « l'essaim traqueur
/// envoie uniquement des traits ». <b>Quatrième</b> primitive d'emprunt arrivée jusqu'à l'écran dans
/// ce portage, après les carrés blancs des drones, les motifs de sol et la lame boomerang.</para>
///
/// <para><b>De l'énergie et du métal, mais qui TOURNE</b> : le missile s'oriente vers sa cible, il ne
/// peut donc pas porter d'ombrage cuit — <see cref="Pseudo3D"/> suppose une lumière fixe venue du
/// haut-gauche, qu'une pièce en rotation emporterait avec elle. C'est la règle posée par
/// <see cref="GlaiveSprite"/>, et elle vaut ici pour la même raison. Il garde en revanche le
/// <b>contour</b> du brief, sans quoi un objet violet se perd sur les sols sombres du Néon.</para>
/// </summary>
public static class MissileSprite
{
    private const int Width = 28;
    private const int Height = 14;

    /// <summary>
    /// Le missile pointe vers la <b>droite</b> (+X), comme tout projectile du jeu : c'est la
    /// convention que suit la rotation appliquée au lancement.
    /// </summary>
    private const float NoseX = 25f;

    private const float BodyBackX = 9f;        // arrière du fuselage
    private const float BodyHalf = 2.4f;       // demi-hauteur du fuselage
    private const float TaperFrom = 18f;       // début de l'effilement vers l'ogive

    // Ailerons : un V COURT et détaché du fuselage. Un aileron long se confond avec le corps et la
    // silhouette redevient une goutte — c'est ce qu'a montré le premier essai, agrandi.
    private const float FinBackX = 6f;
    private const float FinFrontX = 11f;
    private const float FinHalf = 5.5f;

    /// <summary>Violet d'Aether — la teinte de la carte et du Codex.</summary>
    public static readonly Color Body = new(0.667f, 0.267f, 1f);

    /// <summary>Ogive presque blanche : c'est elle qui donne le sens de vol au premier coup d'œil.</summary>
    private static readonly Color Nose = new(0.95f, 0.90f, 1f);

    /// <summary>Flamme de propulsion — ce qui distingue un missile d'une balle allongée.</summary>
    private static readonly Color Flame = new(1f, 0.61f, 0.24f);
    private static readonly Color Ember = new(1f, 0.85f, 0.48f);

    private static Sprite? _sprite;

    public static Sprite Get()
    {
        if (_sprite != null) return _sprite;

        var px = new Color[Width * Height];
        float axis = Height * 0.5f;

        for (int y = 0; y < Height; y++)
        for (int x = 0; x < Width; x++)
        {
            float fx = x + 0.5f;
            float dy = Mathf.Abs(y + 0.5f - axis);

            // ── Fuselage droit, puis ogive ──────────────────────────────────
            // Le corps garde une hauteur CONSTANTE jusqu'à `TaperFrom`, et ne s'effile qu'ensuite.
            // Un effilement continu depuis l'arrière donnait une goutte : le missile ne se lisait
            // qu'à sa couleur, pas à sa forme.
            if (fx > BodyBackX && fx <= NoseX)
            {
                float taper = Mathf.InverseLerp(TaperFrom, NoseX, fx);
                float half = fx <= TaperFrom ? BodyHalf : Mathf.Lerp(BodyHalf, 0.5f, taper);

                if (dy <= half)
                {
                    px[y * Width + x] = taper > 0.45f ? Nose : Body;
                    continue;
                }
            }

            // ── Ailerons arrière, en V ──────────────────────────────────────
            // Ils disent « engin guidé » plutôt que « trait ». Ils sont volontairement PLUS HAUTS
            // que le fuselage et s'arrêtent net : c'est le décrochement qui les rend lisibles à
            // cette taille, pas leur surface.
            if (fx >= FinBackX && fx <= FinFrontX)
            {
                float t = Mathf.InverseLerp(FinFrontX, FinBackX, fx);
                float span = Mathf.Lerp(BodyHalf, FinHalf, t);

                if (dy <= span && dy >= BodyHalf - 0.5f)
                {
                    px[y * Width + x] = Body;
                    continue;
                }
            }

            // ── Tuyère et flamme ────────────────────────────────────────────
            // La flamme part du fuselage sans rupture : détachée, elle se lisait comme un éclat d'or
            // flottant derrière le missile plutôt que comme sa propulsion.
            if (fx <= BodyBackX && fx >= 2f)
            {
                float t = Mathf.InverseLerp(BodyBackX, 2f, fx);
                if (dy <= Mathf.Lerp(BodyHalf, 0.7f, t))
                {
                    px[y * Width + x] = t > 0.5f ? Ember : Flame;
                    continue;
                }
            }
        }

        Pseudo3D.AddOutline(px, Width, Height);
        _sprite = Pseudo3D.Make(px, Width, Height);
        return _sprite;
    }
}
