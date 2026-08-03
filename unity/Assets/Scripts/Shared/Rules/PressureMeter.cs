using System;

/// <summary>
/// <b>Mesure de la pression ressentie</b> — compte les <i>frôlements</i> (passages en zone critique de
/// PV) plutôt que de moyenner des flux (logique pure, testable).
///
/// <para><b>Pourquoi cet instrument existe.</b> Le lot 1 de la saturation a été validé sur le
/// <b>temps soutenable</b> (part du temps où les PV rendus couvrent les PV perdus). Ce critère a été
/// tenu — le cran I valait −10,0 % relatif, 4/4, bien au-delà du seuil de 6 % — et le testeur qui a
/// joué <b>les cinq crans</b> a répondu « pas de difficulté particulière » (2026-08-01). Pire : le
/// cran V fait tomber le temps soutenable de <b>89,3 % à 67,7 %</b> et <b>tue le bot</b> là où le
/// cran 0 ne le tuait jamais, et reste malgré tout imperceptible. <b>Un critère qu'un cran peut
/// écraser d'un facteur quatre sans que personne ne le sente ne mesure pas la difficulté.</b></para>
///
/// <para><b>Ce qui manquait.</b> Le temps soutenable compare deux <i>débits</i> moyennés sur une
/// fenêtre : il répond à « le joueur s'use-t-il ? ». Or le joueur ne s'use pas — il jette 80 % des
/// soins qu'on lui offre et passe l'overtime à PV pleins ; il meurt d'un <b>pic</b>. Une grandeur
/// moyennée est aveugle à l'événement : un plongeon à 10 % des PV suivi d'une remontée complète
/// laisse un débit net inchangé, et ne se voit dans aucune moyenne. C'est pourtant <b>exactement</b>
/// ce qu'un joueur appelle « c'était difficile ». <see cref="PowerTelemetry"/> ne relevait les PV
/// qu'une fois toutes les 15 s de jeu : entre deux relevés, le creux n'existait pas.</para>
///
/// <para><b>Le parti pris : compter des événements, pas des débits.</b> On observe la barre de vie à
/// la fréquence de la frame et on relève trois choses par fenêtre — le <b>plus bas</b> ratio atteint,
/// le <b>nombre</b> de frôlements, et le <b>temps</b> passé en zone critique. Un jeu se sent difficile
/// quand ces trois grandeurs bougent ; il se sent facile quand elles restent plates, quoi que disent
/// les flux.</para>
///
/// <para><b>L'hystérésis n'est pas un détail.</b> Sans elle, un joueur qui oscille autour du seuil
/// compterait un frôlement par frame — soit des centaines — et la métrique mesurerait la fréquence de
/// rafraîchissement, pas le danger. Un frôlement n'est donc compté qu'au <b>franchissement
/// descendant</b> de <see cref="DangerRatio"/>, et il faut être remonté au-dessus de
/// <see cref="SafeRatio"/> pour qu'un suivant puisse l'être. Les deux seuils sont volontairement
/// écartés : c'est ce qui fait qu'un « frôlement » désigne un <i>épisode</i> et non un échantillon.</para>
///
/// <para>Mesures : <c>docs/TEST_REPORT.md</c> (2026-08-01). Design : <c>docs/GDD.md</c> §34.5.</para>
/// </summary>
public sealed class PressureMeter
{
    /// <summary>
    /// Fraction des PV max sous laquelle le joueur est en <b>zone critique</b>. À 30 %, la barre est
    /// visiblement basse et un seul coup d'overtime peut finir la run : c'est le moment que le joueur
    /// raconte après coup. Un seuil plus haut compterait des égratignures, un seuil plus bas ne
    /// compterait que les morts.
    /// </summary>
    public const float DangerRatio = 0.30f;

    /// <summary>
    /// Fraction des PV max au-dessus de laquelle le joueur est considéré <b>tiré d'affaire</b>, ce qui
    /// réarme le compteur. L'écart avec <see cref="DangerRatio"/> est l'hystérésis : il garantit qu'un
    /// épisode compte pour un, quelle que soit l'agitation de la barre autour du seuil.
    /// </summary>
    public const float SafeRatio = 0.55f;

    private bool _inDanger;

    /// <summary>Nombre de frôlements ouverts pendant la fenêtre courante.</summary>
    public int CloseCalls { get; private set; }

    /// <summary>
    /// Plus bas ratio PV/PV max observé pendant la fenêtre (1 = jamais entamé). C'est la profondeur du
    /// creux, là où <see cref="CloseCalls"/> n'en donne que le nombre.
    /// </summary>
    public float LowestRatio { get; private set; } = 1f;

    /// <summary>Secondes passées sous <see cref="DangerRatio"/> pendant la fenêtre.</summary>
    public float DangerSeconds { get; private set; }

    private float _windowSeconds;

    /// <summary>Part de la fenêtre passée en zone critique (0 si la fenêtre est vide).</summary>
    public float DangerFraction => _windowSeconds > 0f ? DangerSeconds / _windowSeconds : 0f;

    /// <summary>
    /// Observe la barre de vie sur une frame. Sans joueur (<paramref name="maxHp"/> ≤ 0) l'échantillon
    /// est ignoré : entre deux scènes, des PV nuls ne sont pas un frôlement.
    /// </summary>
    public void Observe(float currentHp, float maxHp, float deltaSeconds)
    {
        if (maxHp <= 0f || deltaSeconds < 0f) return;

        float ratio = Math.Clamp(currentHp / maxHp, 0f, 1f);
        _windowSeconds += deltaSeconds;
        if (ratio < LowestRatio) LowestRatio = ratio;

        if (_inDanger)
        {
            DangerSeconds += deltaSeconds;
            // Sortie d'épisode : il faut repasser franchement au-dessus du seuil bas, pas seulement
            // le frôler par le dessus, sinon un seul creux se compterait en dizaines d'épisodes.
            if (ratio > SafeRatio) _inDanger = false;
        }
        else if (ratio < DangerRatio)
        {
            _inDanger = true;
            CloseCalls++;
            DangerSeconds += deltaSeconds;
        }
    }

    /// <summary>
    /// Clôt la fenêtre d'échantillonnage : compteurs remis à zéro, <b>état d'hystérésis conservé</b>.
    /// Un creux à cheval sur deux échantillons est un seul épisode et doit rester compté une fois —
    /// remettre <c>_inDanger</c> à faux ici gonflerait mécaniquement le nombre de frôlements avec la
    /// fréquence d'échantillonnage, c'est-à-dire ferait dépendre la mesure du réglage de l'instrument.
    /// </summary>
    public void ResetWindow()
    {
        CloseCalls = 0;
        DangerSeconds = 0f;
        _windowSeconds = 0f;
        // Reparti de 1, il est réécrit dès la frame suivante par Observe : aucune fenêtre ne peut
        // hériter du creux de la précédente.
        LowestRatio = 1f;
    }

    /// <summary>Réinitialisation complète (nouvelle run), état d'hystérésis compris.</summary>
    public void Reset()
    {
        ResetWindow();
        _inDanger = false;
    }
}
