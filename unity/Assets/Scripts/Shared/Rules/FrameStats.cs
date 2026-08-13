using System;

/// <summary>
/// Statistiques de cadence sur une fenêtre de temps — logique pure et testable.
///
/// <para><b>Pourquoi pas une simple moyenne.</b> Un survivor ne se juge pas à sa cadence moyenne : le
/// joueur ne ressent pas les images rendues, il ressent celles qui ont <b>manqué</b>. Trente images
/// à 120 et une à 200 ms font une excellente moyenne et un à-coup parfaitement visible — c'est la
/// même erreur de méthode que mesurer la pression subie par une moyenne de dégâts, où le creux
/// disparaît dans le lissage.</para>
///
/// <para>D'où trois chiffres et non un : la moyenne dit le confort général, la <b>pire image</b> dit
/// l'à-coup, et le <b>nombre d'images sous un seuil</b> dit s'il s'agit d'un accident isolé ou d'un
/// régime.</para>
/// </summary>
public sealed class FrameStats
{
    private double _seconds;
    private double _worst;
    private int _frames;
    private int _below;

    /// <summary>Seuil au-dessous duquel une image est comptée comme manquée (images/seconde).</summary>
    public double Threshold { get; }

    /// <param name="threshold">
    /// 30 par défaut : en deçà, un jeu d'action cesse d'être agréable bien avant de devenir
    /// injouable. Ce n'est pas la cible, c'est le plancher.
    /// </param>
    public FrameStats(double threshold = 30.0) => Threshold = threshold;

    /// <summary>Enregistre une image, par sa durée en secondes.</summary>
    public void Add(double deltaSeconds)
    {
        // Une durée nulle ou négative ne vient pas du rendu : elle vient d'une pause, d'un changement
        // de scène ou d'un premier appel. La compter écraserait la mesure vers le haut.
        if (deltaSeconds <= 0.0) return;

        _seconds += deltaSeconds;
        _frames++;

        if (deltaSeconds > _worst) _worst = deltaSeconds;
        if (deltaSeconds > 1.0 / Threshold) _below++;
    }

    /// <summary>Images enregistrées.</summary>
    public int Frames => _frames;

    /// <summary>Durée couverte, en secondes.</summary>
    public double Seconds => _seconds;

    /// <summary>Cadence moyenne sur la fenêtre. <c>0</c> tant que rien n'a été enregistré.</summary>
    public double AverageFps => _seconds > 0.0 ? _frames / _seconds : 0.0;

    /// <summary>Durée de l'image la plus lente, en millisecondes — l'à-coup que le joueur voit.</summary>
    public double WorstFrameMs => _worst * 1000.0;

    /// <summary>Images passées sous le seuil.</summary>
    public int FramesBelowThreshold => _below;

    /// <summary>Part des images passées sous le seuil, de 0 à 1.</summary>
    public double ShareBelowThreshold => _frames > 0 ? (double)_below / _frames : 0.0;

    /// <summary>Repart à zéro — une fenêtre de mesure ne traîne pas la précédente.</summary>
    public void Reset()
    {
        _seconds = 0.0;
        _worst = 0.0;
        _frames = 0;
        _below = 0;
    }

    /// <summary>Ligne de relevé, lisible dans un journal comme dans une console de navigateur.</summary>
    public string Format() =>
        FormattableString.Invariant(
            $"moy={AverageFps:F1} pire={WorstFrameMs:F0}ms sous{Threshold:F0}={ShareBelowThreshold * 100.0:F0}% ({_frames} images)");
}
