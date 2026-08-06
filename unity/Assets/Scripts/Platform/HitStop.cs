using UnityEngine;

/// <summary>
/// Ralenti bref sur un coup décisif — portage de <c>ScreenShake.HitStop</c> de Godot.
///
/// <para><b>Ce que le portage avait perdu.</b> Sous Godot, la chute du Noyau Rouillé passait par
/// <c>Engine.TimeScale = 0,05</c> pendant 0,1 s. Rien de tel n'existait ici : le boss disparaissait
/// à la vitesse d'un ennemi ordinaire, alors que c'est le seul événement du jeu qui conclut un
/// niveau.</para>
///
/// <para><b>Et pourquoi il ne fallait pas le recopier tel quel.</b> 0,1 s à 5 % dure exactement cinq
/// millisecondes de temps de jeu : à l'écran, cela ne se lit pas comme un ralenti mais comme un
/// hoquet — « pas très visible » est le mot juste. Le coup se compose donc en deux temps : une
/// <b>tenue</b> franche, puis une <b>remontée progressive</b> vers la vitesse nominale, qui est ce
/// qui donne la sensation de reprise. Un ralenti sans remontée n'est qu'un blocage.</para>
///
/// <para><b>Et surtout : il se rend TOUJOURS.</b> C'est le vrai piège de ce genre d'effet. La version
/// Godot restaurait <c>1,0</c> depuis un <c>await</c> — c'est-à-dire depuis un objet qui peut mourir
/// avant son échéance, et qui écrasait au passage le réglage <c>--timescale</c> du banc. Ici :</para>
/// <list type="bullet">
///   <item>l'avance est portée par <see cref="PlatformHost"/>, qui survit aux changements de scène —
///         pas par la caméra ni par le boss, tous deux détruits pendant l'effet ;</item>
///   <item>elle est comptée en temps <b>non mis à l'échelle</b> : compter en temps de jeu pendant
///         qu'on ralentit le jeu allongerait l'effet dans les mêmes proportions, et à 5 % il durerait
///         vingt fois trop longtemps ;</item>
///   <item>la vitesse rendue est celle de <see cref="SceneRoot.ResumeScale"/> et non 1,0, sinon une
///         campagne de banc en temps accéléré retomberait silencieusement à la vitesse normale ;</item>
///   <item>rien n'est écrit pendant une <b>pause</b> : la pause possède <c>timeScale</c>, et la
///         sortie de pause restaure déjà la vitesse nominale.</item>
/// </list>
/// </summary>
public static class HitStop
{
    /// <summary>Vitesse pendant la tenue, en fraction de la vitesse nominale.</summary>
    private const float HoldScale = 0.08f;

    /// <summary>Durée de la tenue, en secondes réelles.</summary>
    private const float HoldSeconds = 0.16f;

    /// <summary>Durée de la remontée vers la vitesse nominale, en secondes réelles.</summary>
    private const float RampSeconds = 0.55f;

    private static float _elapsed;
    private static float _hold;
    private static float _ramp;
    private static float _scale = 1f;
    private static bool _running;

    /// <summary>Un ralenti est-il en cours ?</summary>
    public static bool Active => _running;

    /// <summary>Vitesse imposée à l'instant, en fraction du nominal (1 = aucun ralenti).</summary>
    public static float CurrentFraction => _running ? _scale : 1f;

    /// <summary>
    /// Déclenche le ralenti. Un nouvel appel <b>redémarre</b> l'effet plutôt que de s'y ajouter :
    /// deux ralentis cumulés se multiplieraient, et le jeu s'arrêterait pour de bon.
    /// </summary>
    public static void Trigger(float holdSeconds = HoldSeconds, float rampSeconds = RampSeconds,
                               float holdScale = HoldScale)
    {
        _hold = Mathf.Max(0f, holdSeconds);
        _ramp = Mathf.Max(0.01f, rampSeconds);
        _scale = Mathf.Clamp(holdScale, 0.01f, 1f);
        _elapsed = 0f;
        _running = true;

        // Le premier pas est posé tout de suite : attendre la frame suivante rendrait l'effet
        // dépendant de la cadence, et il manquerait précisément l'image du coup.
        Apply(_scale);
    }

    /// <summary>Fait avancer le ralenti. Appelé par <see cref="PlatformHost"/>, et par lui seul.</summary>
    public static void Advance(float unscaledDeltaTime)
    {
        if (!_running) return;

        _elapsed += unscaledDeltaTime;

        if (_elapsed < _hold) { Apply(_scale); return; }

        float t = Mathf.Clamp01((_elapsed - _hold) / _ramp);

        if (t >= 1f)
        {
            _running = false;
            Apply(1f);
            return;
        }

        // Remontée en douceur : une rampe linéaire depuis 8 % passe l'essentiel de sa course dans
        // des vitesses trop lentes pour être jouables, et le joueur reprend la main avant que
        // l'image ne le suggère.
        Apply(Mathf.Lerp(_scale, 1f, t * t * (3f - 2f * t)));
    }

    /// <summary>Annule tout ralenti et rend la vitesse nominale — changement de scène, fin de run.</summary>
    public static void Reset()
    {
        if (!_running) return;

        _running = false;
        Apply(1f);
    }

    private static void Apply(float fraction)
    {
        // La pause possède timeScale : écrire par-dessus ferait repartir le jeu sous une modale.
        if (SceneRoot.Paused) return;

        Time.timeScale = SceneRoot.ResumeScale * fraction;
    }
}
