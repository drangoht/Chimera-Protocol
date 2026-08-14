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
    /// <remarks>
    /// Sans paramètres explicites, ce sont ceux du <b>catalogue chargé</b> qui servent — donc le bloc
    /// <c>echoesFormula</c> de <c>meta_upgrades.json</c>. L'écran s'appuyait auparavant sur un jeu de
    /// valeurs codées côté moteur : identiques à celles du fichier, donc invisibles, mais retoucher
    /// le fichier n'aurait rien changé au montant crédité.
    ///
    /// <para>La <b>frontière standard/overtime</b> vient du <see cref="GameManager"/>, qui est le seul
    /// à connaître celle de la run jouée : le cran « Compte à rebours » la ramène de 780 s à 484 s, et
    /// c'est à partir de là que le temps doit être amorti.</para>
    /// </remarks>
    public void Show(bool victory, int runSeconds, int kills, int cores, double tierMult = 1.0,
                     MetaUpgradeTable.EchoParams? settings = null)
    {
        // Source UNIQUE du total : l'animation ci-dessous ne fera que le parcourir.
        var (total, overtimeBonus) = (settings ?? MetaProgression.Catalog.Echoes)
            .Detailed(runSeconds, kills, cores, tierMult, GameManager.Instance?.RunDurationSeconds);

        EchoesEarned = total;
        DisplayedEchoes = 0;

        if (_title != null)
        {
            _title.text = victory ? Loc.T("RUNEND_VICTORY") : Loc.T("RUNEND_DEATH");
            _title.color = victory ? UiPalette.Gold : UiPalette.Rust;
        }

        if (_stats != null)
        {
            // Le Bonus de Surcharge n'apparaît que s'il a été gagné : une ligne « + 0 » sur une run
            // qui n'a pas atteint l'overtime annoncerait une récompense manquée, alors qu'il n'y
            // avait rien à manquer. ⚠ Sa clé de traduction attendait dans `ui.csv`, dans les trois
            // langues, sans un seul appelant — le bonus était nul par construction, donc invisible,
            // donc jamais réclamé.
            string bonus = overtimeBonus > 0
                ? $"\n{Loc.T("RUNEND_OVERTIME_BONUS")} : +{overtimeBonus}"
                : "";

            _stats.text = $"{Loc.T("RUNEND_TIME")} : {runSeconds / 60:00}:{runSeconds % 60:00}\n" +
                          $"{Loc.T("RUNEND_KILLS")} : {kills}\n" +
                          $"{Loc.T("RUNEND_CORES")} : {cores}{bonus}";
        }

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
            if (_echoes != null) _echoes.text = Loc.T("RUNEND_ECHOES_GAINED", DisplayedEchoes);
            yield return null;
        }

        // Atterrissage exact sur le total : un arrondi d'interpolation ne doit jamais laisser
        // l'affichage à une unité près du montant crédité.
        DisplayedEchoes = EchoesEarned;
        if (_echoes != null) _echoes.text = Loc.T("RUNEND_ECHOES_GAINED", DisplayedEchoes);
        _countUp = null;
    }

    /// <summary>Termine immédiatement l'animation — pour un joueur pressé, ou pour un banc.</summary>
    public void SkipAnimation()
    {
        if (_countUp != null) { StopCoroutine(_countUp); _countUp = null; }
        DisplayedEchoes = EchoesEarned;
        if (_echoes != null) _echoes.text = Loc.T("RUNEND_ECHOES_GAINED", DisplayedEchoes);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("RunEndCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 120);

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform, 0.85f);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Gold);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = UiCanvas.PanelSize(new Vector2(820f, 560f));
        rect.anchoredPosition = Vector2.zero;

        var column = UiStyle.NewUiObject("Column", panel.transform);
        UiStyle.Stretch(column, 32f);
        var layout = column.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 18f;
        layout.childForceExpandHeight = false;
        layout.childControlHeight = true;
        layout.childControlWidth = true;

        // Les libellés de construction sont ceux d'un écran encore vide : `Show` les réécrit tous.
        // Ils passent quand même par la table — un écran ouvert par un chemin qui oublierait `Show`
        // afficherait sinon du français figé dans les trois langues.
        _title = UiStyle.Label(column.transform, Loc.T("RUNEND_DEATH"), 40, UiPalette.Rust,
                               TextAnchor.UpperCenter);
        UiStyle.Separator(column.transform, UiPalette.Gold);
        _stats = UiStyle.Label(column.transform, "", 22, UiPalette.OffWhite, TextAnchor.UpperCenter);
        _echoes = UiStyle.Label(column.transform, Loc.T("RUNEND_ECHOES_GAINED", 0), 32,
                                UiPalette.Gold, TextAnchor.UpperCenter);

        // ⚠ `RUNEND_MENU` et non `RUNEND_HUB` : ce bouton va au MENU PRINCIPAL (cf. `RunHud`), pas au
        // Hub. La clé du Hub existe et se serait posée là sans qu'aucun test ne bronche — elle aurait
        // simplement promis au joueur une destination qui n'est pas la sienne.
        var button = UiStyle.TextButton(column.transform, Loc.T("RUNEND_MENU"), FrameAccent.Cyan);
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
