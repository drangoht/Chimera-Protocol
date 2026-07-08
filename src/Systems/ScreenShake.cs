using Godot;

/// <summary>
/// AutoLoad singleton — screen shake via offset Camera2D.
/// L'offset est animé par Tween (oscillation décroissante) et revient à Vector2.Zero.
/// Ne modifie jamais GlobalPosition du joueur ni les limites de la caméra.
/// </summary>
public partial class ScreenShake : Node
{
    public static ScreenShake? Instance { get; private set; }

    /// <summary>Réglage Options : si false, Shake() ne fait rien (accessibilité).</summary>
    public static bool Enabled = true;

    /// <summary>Facteur global appliqué à toutes les amplitudes (réduit le tremblement d'ensemble).</summary>
    private const float GlobalScale = 0.55f;

    /// <summary>Plafond d'amplitude (px) : évite que la caméra sorte de l'arène et révèle le bord.</summary>
    private const float MaxAmplitude = 9f;

    private Camera2D? _camera;
    private Tween?    _shakeTween;

    public override void _Ready()
    {
        Instance = this;
    }

    public void SetCamera(Camera2D cam) => _camera = cam;

    /// <summary>
    /// Déclenche un shake oscillant décroissant.
    /// </summary>
    /// <param name="amplitude">Amplitude max en pixels.</param>
    /// <param name="duration">Durée totale du shake en secondes.</param>
    public void Shake(float amplitude, float duration)
    {
        if (_camera is null || !Enabled) return;
        amplitude = Mathf.Min(amplitude * GlobalScale, MaxAmplitude);
        _shakeTween?.Kill();
        _shakeTween = CreateTween();
        _shakeTween.SetTrans(Tween.TransitionType.Sine);
        _shakeTween.SetEase(Tween.EaseType.Out);

        // N allers-retours pendant la durée, amplitude décroissante
        int steps = Mathf.Max(2, (int)(duration / 0.04f));
        for (int i = 0; i < steps; i++)
        {
            float t     = (float)i / steps;
            float decay = 1.0f - t;
            var target  = i % 2 == 0
                ? new Vector2(amplitude * decay, amplitude * 0.5f * decay)
                : new Vector2(-amplitude * decay, -amplitude * 0.4f * decay);
            _shakeTween.TweenProperty(_camera, "offset", target, duration / steps);
        }
        // Retour à zéro garanti
        _shakeTween.TweenProperty(_camera, "offset", Vector2.Zero, 0.04f);
    }

    /// <summary>
    /// Freeze frame : ralentit le jeu à 5% pendant <paramref name="duration"/> secondes.
    /// Ne PAS appeler si GetTree().Paused = true (le timer ne se déclencherait jamais).
    /// </summary>
    public async void HitStop(float duration = 0.05f)
    {
        Engine.TimeScale = 0.05f;
        await ToSignal(
            GetTree().CreateTimer(duration, processAlways: true, processInPhysics: false),
            SceneTreeTimer.SignalName.Timeout);
        Engine.TimeScale = 1.0f;
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
