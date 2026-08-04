using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Écran de fin de run — bilan et Échos gagnés (Lot 5).
///
/// <para><b>Le piège reproduit ici est subtil et coûteux.</b> Le total affiché est animé (il monte
/// progressivement), et il doit correspondre <b>exactement</b> à ce qui est crédité au joueur. Sous
/// Godot, ces deux valeurs venaient de deux calculs différents : dès qu'un multiplicateur entrait en
/// jeu — le bonus par cran de saturation — la somme animée divergeait du total réellement gagné.
/// Le joueur voyait un chiffre, en recevait un autre.</para>
///
/// <para>La parade tient en une règle : <b>une seule source</b>. Le total est calculé une fois par
/// <see cref="EchoFormula"/>, puis l'animation ne fait que le parcourir. Elle ne recalcule
/// jamais.</para>
/// </summary>
public sealed class RunEndScreen : MonoBehaviour
{
    /// <summary>Le joueur demande à revenir au menu.</summary>
    public event Action? Dismissed;

    /// <summary>L'écran est-il affiché ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    /// <summary>Total d'Échos gagné — calculé une seule fois, source unique de l'affichage.</summary>
    public int EchoesEarned { get; private set; }

    /// <summary>Valeur actuellement affichée par le compteur animé.</summary>
    public int DisplayedEchoes { get; private set; }

    private GameObject? _root;
    private Text? _title;
    private Text? _stats;
    private Text? _echoes;
    private Button? _firstButton;
    private Coroutine? _countUp;

    private void Awake()
    {
        BuildUi();
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>
    /// Affiche le bilan. Le total d'Échos est calculé <b>ici et une seule fois</b> ; l'animation
    /// ne fait que le parcourir.
    /// </summary>
    public void Show(bool victory, int runSeconds, int kills, int cores, double tierMult = 1.0,
                     EchoSettings? settings = null)
    {
        // Source UNIQUE du total : l'animation ci-dessous ne fera que le parcourir.
        EchoesEarned = (settings ?? EchoSettings.Default).Total(runSeconds, kills, cores, tierMult);
        DisplayedEchoes = 0;

        if (_title != null)
        {
            _title.text = victory ? "VICTOIRE" : "FIN DE RUN";
            _title.color = victory ? UiPalette.Gold : UiPalette.Rust;
        }

        if (_stats != null)
            _stats.text = $"Temps : {runSeconds / 60:00}:{runSeconds % 60:00}\n" +
                          $"Éliminations : {kills}\n" +
                          $"Noyaux d'Aether : {cores}";

        if (_root != null) _root.SetActive(true);

        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);

        if (_countUp != null) StopCoroutine(_countUp);
        _countUp = StartCoroutine(CountUp());
    }

    /// <summary>
    /// Fait défiler le compteur jusqu'au total. <b>Ne recalcule rien</b> : c'est ce qui garantit que
    /// l'affichage et le crédit ne peuvent pas diverger.
    /// </summary>
    private IEnumerator CountUp()
    {
        const float duration = 0.9f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            // Temps réel : l'écran s'ouvre souvent avec le jeu figé, une animation asservie au
            // temps de jeu ne jouerait jamais.
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            DisplayedEchoes = Mathf.RoundToInt(Mathf.Lerp(0f, EchoesEarned, t));
            if (_echoes != null) _echoes.text = $"+{DisplayedEchoes} Échos";
            yield return null;
        }

        // Atterrissage exact sur le total : un arrondi d'interpolation ne doit jamais laisser
        // l'affichage à une unité près du montant crédité.
        DisplayedEchoes = EchoesEarned;
        if (_echoes != null) _echoes.text = $"+{DisplayedEchoes} Échos";
        _countUp = null;
    }

    /// <summary>Termine immédiatement l'animation — pour un joueur pressé, ou pour un banc.</summary>
    public void SkipAnimation()
    {
        if (_countUp != null) { StopCoroutine(_countUp); _countUp = null; }
        DisplayedEchoes = EchoesEarned;
        if (_echoes != null) _echoes.text = $"+{DisplayedEchoes} Échos";
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("RunEndCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 120;

        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform, 0.85f);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Gold);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(820f, 560f);
        rect.anchoredPosition = Vector2.zero;

        var column = UiStyle.NewUiObject("Column", panel.transform);
        UiStyle.Stretch(column, 32f);
        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        _title = UiStyle.Label(column.transform, "FIN DE RUN", 40, UiPalette.Rust, TextAnchor.UpperCenter);
        UiStyle.Separator(column.transform, UiPalette.Gold);
        _stats = UiStyle.Label(column.transform, "", 22, UiPalette.OffWhite, TextAnchor.UpperCenter);
        _echoes = UiStyle.Label(column.transform, "+0 Échos", 32, UiPalette.Gold, TextAnchor.UpperCenter);

        var button = UiStyle.TextButton(column.transform, "Retour au menu", FrameAccent.Cyan);
        button.onClick.AddListener(() =>
        {
            if (_root != null) _root.SetActive(false);
            SceneRoot.Paused = false;
            Dismissed?.Invoke();
        });
        _firstButton = button;

        var le = button.gameObject.AddComponent<LayoutElement>();
        le.minHeight = 64f;
    }
}
