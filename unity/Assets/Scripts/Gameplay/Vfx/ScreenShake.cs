using UnityEngine;

/// <summary>
/// Secousse de caméra — portage du singleton <c>ScreenShake</c> de Godot.
///
/// <para>Elle ne décide de rien : elle produit un <b>décalage</b> que <see cref="RunCamera"/> ajoute
/// à sa position finale. C'est la même séparation que sous Godot, où la secousse écrivait
/// <c>Camera2D.offset</c> sans jamais toucher au cadrage ni à la position du joueur. Toute autre
/// approche (déplacer la caméra elle-même) se ferait écraser par la poursuite à la frame suivante,
/// ou pire, ferait sortir le cadrage de l'arène et révélerait le vide au-delà des murs.</para>
///
/// <para><b>Statique et sans objet de scène</b> : le décalage doit survivre au rechargement d'une
/// scène et être appelable depuis un impact de projectile, un boss ou une greffe, sans qu'aucun de
/// ces appelants n'ait à retrouver la caméra.</para>
/// </summary>
public static class ScreenShake
{
    /// <summary>Réglage d'accessibilité : à false, <see cref="Shake"/> ne fait rien.</summary>
    public static bool Enabled = true;

    /// <summary>Dosage global (0 = aucune secousse, 1 = nominal).</summary>
    public static float Intensity = 1f;

    /// <summary>Facteur appliqué à toutes les amplitudes — le tremblement d'ensemble était trop fort.</summary>
    private const float GlobalScale = 0.55f;

    /// <summary>
    /// Plafond d'amplitude, en pixels. Sans lui, un boss en phase III sort le cadrage de l'arène et
    /// découvre le vide derrière les murs : la limite du terrain cesse alors d'être lisible.
    /// </summary>
    private const float MaxAmplitude = 9f;

    /// <summary>Durée d'un aller-retour. Plus court = tremblement, plus long = balancement.</summary>
    private const float StepSeconds = 0.05f;

    private static float _amplitude;
    private static float _duration;
    private static float _elapsed;

    /// <summary>Décalage courant, lu par la caméra. Toujours nul quand aucune secousse n'est en cours.</summary>
    public static Vector2 Offset { get; private set; }

    /// <summary>
    /// Déclenche une secousse oscillante décroissante. Une nouvelle secousse <b>remplace</b> la
    /// précédente au lieu de s'y ajouter : en nuée, plusieurs impacts tombent dans la même frame et
    /// leur somme rendrait l'image illisible.
    /// </summary>
    public static void Shake(float amplitude, float duration)
    {
        if (!Enabled || duration <= 0f) return;

        amplitude = Mathf.Min(amplitude * GlobalScale * Intensity, MaxAmplitude);
        if (amplitude <= 0f) return;

        // Une secousse plus faible n'interrompt pas une secousse forte déjà en cours.
        if (_elapsed < _duration && amplitude < _amplitude * (1f - _elapsed / _duration)) return;

        _amplitude = amplitude;
        _duration = duration;
        _elapsed = 0f;
    }

    /// <summary>Fait avancer la secousse d'une frame. Appelé par la caméra, et par elle seule.</summary>
    public static void Advance(float deltaTime)
    {
        if (_elapsed >= _duration)
        {
            Offset = Vector2.zero;
            return;
        }

        _elapsed += deltaTime;

        if (_elapsed >= _duration)
        {
            // Retour à zéro garanti : une secousse qui s'arrête en cours d'oscillation laisserait le
            // cadrage décalé pour le reste de la partie.
            Offset = Vector2.zero;
            return;
        }

        float decay = 1f - _elapsed / _duration;
        float k = Mathf.Sin(_elapsed / StepSeconds * Mathf.PI);

        // Vertical volontairement moitié moins ample : une secousse isotrope donne le mal de mer,
        // là où une dominante horizontale se lit comme un choc.
        Offset = new Vector2(k * _amplitude * decay, k * _amplitude * 0.5f * decay);
    }

    /// <summary>Annule toute secousse en cours — au démarrage d'une run, ou à l'ouverture d'un écran.</summary>
    public static void Reset()
    {
        _amplitude = 0f;
        _duration = 0f;
        _elapsed = 0f;
        Offset = Vector2.zero;
    }
}
