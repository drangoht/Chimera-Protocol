using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Écran de pause (Lot 5).
///
/// <para><b>Un piège documenté du projet est reproduit ici volontairement.</b> Sous Godot, cet écran
/// a fini par devenir <i>inutilisable</i> en fin de run : titre, corps et boutons vivaient dans un
/// seul conteneur centré, <b>sans défilement ni plafond</b>. Avec 5 armes niveau 20, 4 passifs et
/// 5 greffes multilignes, le panneau dépassait la fenêtre — et, <i>parce qu'il était centré</i>,
/// débordait des <b>deux</b> côtés. Le bouton « Quitter la partie » se retrouvait hors cadre : plus
/// aucun moyen d'abandonner.</para>
///
/// <para>D'où la structure retenue : <b>seul le corps défile</b>. Le titre et les boutons vivent en
/// dehors de la zone de défilement, donc restent atteignables quelle que soit la longueur du
/// contenu.</para>
/// </summary>
public sealed class PauseScreen : MonoBehaviour
{
    /// <summary>Le joueur demande la reprise.</summary>
    public event Action? Resumed;

    /// <summary>Le joueur demande à quitter la partie.</summary>
    public event Action? QuitRequested;

    /// <summary>L'écran est-il affiché ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    private GameObject? _root;
    private Text? _body;
    private Button? _firstButton;

    private void Awake()
    {
        BuildUi();
        SetVisible(false);
    }

    /// <summary>Bascule pause / reprise.</summary>
    public void Toggle()
    {
        if (IsVisible) Resume();
        else Open();
    }

    /// <summary>Ouvre l'écran et met le jeu en pause.</summary>
    /// <param name="bodyText">Corps imposé — réservé aux bancs ; sinon l'état réel de la run.</param>
    public void Open(string? bodyText = null)
    {
        if (_body != null) _body.text = bodyText ?? BuildReport();

        SetVisible(true);
        SceneRoot.Paused = true;

        // Focus initial : sans lui, l'écran est infranchissable à la manette — et c'est l'écran par
        // lequel on quitte la partie.
        if (_firstButton != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
    }

    /// <summary>Ferme l'écran et relance le jeu.</summary>
    public void Resume()
    {
        SetVisible(false);
        SceneRoot.Paused = false;
        Resumed?.Invoke();
    }

    /// <summary>
    /// L'état de la run, en cinq sections — <b>Mission, Joueur, Armes, Passifs, Greffes</b>, comme le
    /// jeu publié.
    ///
    /// <para><b>C'est le seul écran où le joueur peut LIRE sa run.</b> Le HUD dit ce qui change
    /// (PV, chrono, niveau) ; il ne dit pas ce qu'on a construit — un multiplicateur de dégâts, une
    /// réduction de recharge, le niveau exact de chaque arme. Le portage n'affichait qu'un texte
    /// passé par l'appelant, c'est-à-dire <b>rien</b> en jeu : la pause y servait uniquement à
    /// quitter.</para>
    /// </summary>
    private static string BuildReport()
    {
        var sb = new System.Text.StringBuilder();

        var gm = GameManager.Instance;
        var xp = XpSystem.Instance;
        var stats = Player.Instance?.Stats;
        var inv = InventorySystem.Instance;

        int elapsed = gm != null ? Mathf.FloorToInt(gm.RunTime) : 0;
        int left = gm != null ? Mathf.Max(0, gm.RunDurationSeconds - elapsed) : 0;

        sb.AppendLine(Loc.T("PAUSE_MISSION"));
        Stat(sb, Loc.T("PAUSE_TIME_SURVIVED"), $"{elapsed / 60:00}:{elapsed % 60:00}");
        Stat(sb, Loc.T("PAUSE_TIME_LEFT"), $"{left / 60:00}:{left % 60:00}");
        Stat(sb, Loc.T("PAUSE_LEVEL"), $"{xp?.CurrentLevel ?? 1}");
        Stat(sb, Loc.T("PAUSE_XP"), $"{xp?.CurrentXp ?? 0} / {xp?.XpToNextLevel ?? 0}");
        Stat(sb, Loc.T("PAUSE_KILLS"), $"{gm?.Kills ?? 0}");
        Stat(sb, Loc.T("PAUSE_CORES"), $"{gm?.CoresCollected ?? 0}");

        sb.AppendLine();
        sb.AppendLine(Loc.T("PAUSE_PLAYER"));

        if (stats == null)
        {
            Stat(sb, "—", Loc.T("PAUSE_UNAVAILABLE"));
        }
        else
        {
            Stat(sb, Loc.T("PAUSE_HP"), $"{stats.CurrentHp:F0} / {stats.MaxHp:F0}");
            Stat(sb, Loc.T("PAUSE_SPEED"), $"{stats.Speed:F0} px/s");
            Stat(sb, Loc.T("PAUSE_DMG_MULT"), $"×{stats.DamageMultiplier:F2}");
            Stat(sb, Loc.T("PAUSE_DMG_REDUC"), $"{stats.DamageReduction * 100f:F0} %");
            Stat(sb, Loc.T("PAUSE_CD_REDUC"), $"{stats.CooldownReduction * 100f:F0} %");
        }

        sb.AppendLine();
        sb.AppendLine(Loc.T("PAUSE_WEAPONS"));

        if (inv == null || inv.WeaponLevels.Count == 0)
        {
            sb.AppendLine("   " + Loc.T("PAUSE_NONE"));
        }
        else
        {
            foreach (var (id, level) in inv.WeaponLevels)
            {
                // Une FUSION se signale : c'est l'arme la plus forte du jeu, et elle est
                // indiscernable d'une arme ordinaire dans une liste de noms.
                // ⚠ `Contains` sur une chaîne résout vers MemoryExtensions et exige un
                // StringComparison : c'est bien la collection qu'on interroge, pas le texte.
                string mark = System.Linq.Enumerable.Contains(inv.AppliedFusions, id) ? "✦ " : "  ";
                Stat(sb, mark + UiNames.Of(id), $"Niv. {level}");
            }
        }

        if (inv != null && inv.PassiveLevels.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine(Loc.T("PAUSE_PASSIVES"));

            foreach (var (id, level) in inv.PassiveLevels)
                Stat(sb, "  " + UiNames.Of(id), $"Niv. {level}");
        }

        sb.AppendLine();
        sb.AppendLine(Loc.T("PAUSE_GRAFTS"));

        if (Assimilation.Equipped.Count == 0)
        {
            sb.AppendLine("   " + Loc.T("PAUSE_NONE"));
        }
        else
        {
            foreach (string id in Assimilation.Equipped)
            {
                var def = Assimilation.Config.GraftById(id);
                sb.AppendLine("   " + (def != null ? def.Name : id));
            }
        }

        return sb.ToString();
    }

    /// <summary>Une ligne « libellé … valeur », en colonnes alignées par une police à chasse fixe.</summary>
    private static void Stat(System.Text.StringBuilder sb, string label, string value)
        => sb.AppendLine($"   {label.PadRight(26, '.')} {value}");


    private void SetVisible(bool visible)
    {
        if (_root != null) _root.SetActive(visible);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("PauseCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 110);   // au-dessus du HUD et des effets plein écran

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Steel);
        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(760f, 700f);
        rect.anchoredPosition = Vector2.zero;

        // ─── Titre : HORS zone de défilement ──────────────────────────────────
        var title = UiStyle.Label(panel.transform, "PAUSE", 40, UiPalette.Cyan, TextAnchor.UpperCenter);
        var titleRect = title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -90f);
        titleRect.offsetMax = new Vector2(-24f, -24f);

        // ─── Corps : SEUL élément qui défile ──────────────────────────────────
        var scrollGo = UiStyle.NewUiObject("BodyScroll", panel.transform);
        var scrollRect = scrollGo.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(24f, 120f);   // laisse la place aux boutons, en bas
        scrollRect.offsetMax = new Vector2(-24f, -100f);

        var scroll = scrollGo.AddComponent<ScrollRect>();
        UiStyle.ConfigureScroll(scroll);
        scrollGo.AddComponent<RectMask2D>();

        var content = UiStyle.NewUiObject("Content", scrollGo.transform);
        var contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);

        // ⚠ Largeur remise à ZÉRO. Un RectTransform naît en 100 × 100 : étiré entre deux ancres
        // horizontales, il vaut alors « largeur du parent + 100 » et déborde de 50 px de CHAQUE
        // côté de sa fenêtre de défilement. Le masque rogne le reste, et ce sont les premières
        // lettres de chaque ligne qui disparaissent — un défaut qu'on lit comme une faute de texte
        // et non comme un défaut de mise en page.
        contentRect.sizeDelta = Vector2.zero;
        contentRect.offsetMin = Vector2.zero;
        contentRect.offsetMax = Vector2.zero;

        var fitter = content.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        content.AddComponent<VerticalLayoutGroup>().childForceExpandHeight = false;

        scroll.content = contentRect;
        scroll.viewport = scrollRect;

        _body = UiStyle.Label(content.transform, "", 20, UiPalette.OffWhite);

        // ─── Boutons : HORS zone de défilement, donc toujours atteignables ────
        var buttonRow = UiStyle.NewUiObject("Buttons", panel.transform);
        var rowRect = buttonRow.GetComponent<RectTransform>();
        rowRect.anchorMin = new Vector2(0f, 0f);
        rowRect.anchorMax = new Vector2(1f, 0f);
        rowRect.pivot = new Vector2(0.5f, 0f);
        rowRect.offsetMin = new Vector2(24f, 24f);
        rowRect.offsetMax = new Vector2(-24f, 96f);

        var layout = buttonRow.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = 16f;
        layout.childForceExpandWidth = true;
        layout.childControlWidth = true;
        layout.childControlHeight = true;

        // Les libellés viennent de la table — ils annoncent aussi leur touche (« [Échap] »).
        var resume = UiStyle.TextButton(buttonRow.transform, Loc.T("PAUSE_RESUME"), FrameAccent.Cyan);
        resume.onClick.AddListener(Resume);
        _firstButton = resume;

        // ⚠ Options ACCESSIBLE DEPUIS LA PAUSE. Sans ce bouton, un joueur qui trouve la musique trop
        // forte ou la secousse gênante doit abandonner sa run pour y toucher — c'est précisément
        // pendant une partie qu'on s'en aperçoit. Le jeu publié l'a ; le portage l'avait perdu.
        var options = UiStyle.TextButton(buttonRow.transform, Loc.T("PAUSE_OPTIONS"), FrameAccent.Steel);
        options.onClick.AddListener(OpenOptions);

        var quit = UiStyle.TextButton(buttonRow.transform, Loc.T("PAUSE_QUIT"), FrameAccent.Danger);
        quit.onClick.AddListener(() =>
        {
            SetVisible(false);
            SceneRoot.Paused = false;   // ne jamais quitter en laissant le temps figé
            QuitRequested?.Invoke();
        });

        Chain(resume, options, quit);
    }

    /// <summary>Chaîne de focus explicite, circulaire, sur une rangée de boutons.</summary>
    private static void Chain(params Button[] buttons)
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            var nav = buttons[i].navigation;
            nav.mode = Navigation.Mode.Explicit;
            nav.selectOnLeft = buttons[(i - 1 + buttons.Length) % buttons.Length];
            nav.selectOnRight = buttons[(i + 1) % buttons.Length];
            buttons[i].navigation = nav;
        }
    }

    private OptionsScreen? _options;

    /// <summary>
    /// Ouvre les options par-dessus la pause. Elles vivent sur le <b>même objet</b>, donc au-dessus
    /// dans l'empilement des canevas — et le jeu reste figé pendant qu'on règle.
    /// </summary>
    private void OpenOptions()
    {
        if (_options == null)
        {
            _options = gameObject.AddComponent<OptionsScreen>();

            // Pas de remise à zéro depuis une partie en cours : la run écrirait son résultat
            // par-dessus en s'achevant (record, complétion, cran battu), et le joueur verrait
            // revenir une progression qu'il vient d'effacer.
            _options.AllowFullReset = false;

            _options.Closed += () =>
            {
                if (_firstButton != null && EventSystem.current != null)
                    EventSystem.current.SetSelectedGameObject(_firstButton.gameObject);
            };
        }

        _options.Show();
    }
}
