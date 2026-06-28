using Godot;

/// <summary>
/// Singleton AutoLoad — flash blanc aveuglant 0.3 s déclenché lors d'une fusion/évolution.
/// CanvasLayer layer=99 (sous FadeOverlay menus à layer=100).
/// Décrit dans GDD §12 : "reset de l'œil" avant l'éclatement de la nouvelle palette.
/// Ajoute : screen shake 8 px / 0.25 s + chromatic aberration 0→4→0 en 0.30 s.
/// </summary>
public partial class FusionFlash : CanvasLayer
{
    public static FusionFlash? Instance { get; private set; }

    private ColorRect _flashRect = null!;
    private ShaderMaterial? _chromaMat;

    public override void _Ready()
    {
        Instance = this;
        ProcessMode = ProcessModeEnum.Always;
        _flashRect = GetNode<ColorRect>("FlashRect");

        // Récupère le ShaderMaterial de ChromaticFX/ChromaRect
        var chromaRect = GetNodeOrNull<ColorRect>("ChromaticFX/ChromaRect");
        _chromaMat = chromaRect?.Material as ShaderMaterial;
    }

    /// <summary>
    /// Déclenche un flash blanc aveuglant : montée en 0.1 s, descente en 0.25 s = 0.35 s total.
    /// En parallèle : screen shake 8 px / 0.25 s + chromatic aberration 0→4→0 en 0.30 s.
    /// Le HitStop n'est PAS appelé ici (fusion depuis LevelUpScreen = tree pausé).
    /// </summary>
    public void TriggerFlash()
    {
        // S'assurer que l'alpha est à 0 avant de rejouer (appels rapprochés)
        _flashRect.Color = new Color(1f, 1f, 1f, 0f);

        // Flash blanc existant
        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(_flashRect, "color:a", 0.85f, 0.1)
             .SetEase(Tween.EaseType.Out)
             .SetTrans(Tween.TransitionType.Quad);
        tween.TweenProperty(_flashRect, "color:a", 0f, 0.25)
             .SetEase(Tween.EaseType.In)
             .SetTrans(Tween.TransitionType.Quad);

        // Screen shake — uniquement si le jeu n'est pas pausé (fusion hors LevelUpScreen)
        if (!GetTree().Paused)
            ScreenShake.Instance?.Shake(8f, 0.25f);

        // Chromatic aberration 0 → 4 en 0.1 s → 0 en 0.2 s
        if (_chromaMat != null)
        {
            _chromaMat.SetShaderParameter("strength", 0.0f);
            var chromaTween = CreateTween();
            chromaTween.SetPauseMode(Tween.TweenPauseMode.Process);
            chromaTween.TweenMethod(
                Callable.From<float>(v => _chromaMat.SetShaderParameter("strength", v)),
                0.0f, 4.0f, 0.1);
            chromaTween.TweenMethod(
                Callable.From<float>(v => _chromaMat.SetShaderParameter("strength", v)),
                4.0f, 0.0f, 0.2);
        }
    }

    public override void _ExitTree()
    {
        Instance = null;
    }
}
