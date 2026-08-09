using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Proposition d'une greffe quand une jauge se remplit (Lot 6).
///
/// <para>C'est un <b>choix</b>, pas une récompense automatique : accepter transforme le personnage et
/// occupe un emplacement ; refuser relève le seuil de cette jauge. Le refus doit donc coûter quelque
/// chose, sinon dire non en attendant mieux serait toujours gratuit.</para>
///
/// <para>Il passe par <see cref="ModalQueue"/> comme la montée de niveau : les deux peuvent tomber
/// dans la même frame, et deux écrans ouverts ensemble se disputeraient le focus et la pause.</para>
/// </summary>
public sealed class AssimilationScreen : MonoBehaviour
{
    /// <summary>Émis après résolution : <c>(jauge, acceptée)</c>.</summary>
    public event Action<string, bool>? Resolved;

    /// <summary>L'écran est-il visible ?</summary>
    public bool IsVisible => _root != null && _root.activeSelf;

    private GameObject? _root;
    private Text? _title;
    private Text? _body;
    private Button? _accept;
    private Button? _decline;
    private string _gauge = "";

    private void Awake()
    {
        BuildUi();
        Hide();
        ModalQueue.Opened += OnModalOpened;
    }

    private void OnDestroy() => ModalQueue.Opened -= OnModalOpened;

    private void OnModalOpened(ModalKind kind)
    {
        if (kind == ModalKind.Assimilation) Show();
    }

    /// <summary>Prépare la proposition et demande l'ouverture.</summary>
    public void Present(string gauge)
    {
        _gauge = gauge;
        ModalQueue.Request(ModalKind.Assimilation);
    }

    private void Show()
    {
        if (_root == null) return;

        // ⚠ Une jauge de FUSION n'a pas de greffe : `GraftForGauge` renvoie null pour elle. Sans ce
        // second chemin, l'écran se refermait aussitôt en refusant tout seul — la fusion, plus long
        // objectif du jeu, était perdue à l'instant où elle s'offrait, sans un mot.
        GraftTable.GraftDef? def = Assimilation.Config.GraftForGauge(_gauge)
                                ?? Assimilation.Config.FusionForGauge(_gauge);

        if (def == null) { Resolve(false); return; }

        _root.SetActive(true);

        var fusion = def as GraftTable.FusionDef;

        if (_title != null)
        {
            _title.text = fusion != null ? $"{Loc.T("ASSIM_FUSION_TAG")} — {def.Name}" : def.Name;
        }

        if (_body != null)
        {
            // L'état des emplacements est dit ICI : accepter avec un arsenal de greffes plein
            // remplace, et le joueur doit le savoir avant de choisir, pas après.
            //
            // ⚠ Une FUSION ne remplace rien : elle absorbe ses deux sources et LIBÈRE un
            // emplacement. Lui afficher « la plus ancienne cédera sa place » ferait refuser la
            // meilleure offre du jeu par crainte de perdre autre chose.
            string slots = fusion != null
                ? $"{string.Join(" + ", FusionSourceNames(fusion))} → {def.Name}   (un emplacement se libère)"
                : Assimilation.HasFreeSlot
                    ? $"Emplacements {Assimilation.Equipped.Count}/{Assimilation.SlotCount}"
                    : $"Emplacements PLEINS ({Assimilation.SlotCount}) — la plus ancienne cédera sa place";

            _body.text = $"{def.Description}\n\n{slots}\n" +
                         "Refuser relève le seuil de cette jauge.";
        }

        if (_accept != null && EventSystem.current != null)
            EventSystem.current.SetSelectedGameObject(_accept.gameObject);
    }

    private void Hide()
    {
        if (_root != null) _root.SetActive(false);
    }

    /// <summary>
    /// Accepte la greffe à la place du joueur — le pilote automatique (<c>--auto-play</c>).
    /// </summary>
    /// <remarks>
    /// Accepter, et non refuser : une run de banc doit produire le build le plus complet possible,
    /// puisque c'est lui qu'on mesure. Un bot qui décline toutes les greffes ferait conclure que
    /// l'Assimilation ne pèse rien — alors qu'elle n'aurait simplement jamais eu lieu.
    /// </remarks>
    public void AcceptForBench() => Resolve(accepted: true);

    private void Resolve(bool accepted)
    {
        Hide();
        ModalQueue.Close(ModalKind.Assimilation);

        if (accepted)
        {
            // Emplacements pleins : la plus ancienne greffe cède la place. Un choix explicite serait
            // meilleur, mais un écran qui ne peut PAS se résoudre bloquerait la run — le pire cas.
            string? replaced = Assimilation.HasFreeSlot || Assimilation.Equipped.Count == 0
                ? null
                : Assimilation.Equipped[0];

            Assimilation.Accept(_gauge, replaced);
        }
        else
        {
            Assimilation.Decline(_gauge);
        }

        Resolved?.Invoke(_gauge, accepted);
    }

    private void BuildUi()
    {
        var canvasGo = new GameObject("AssimilationCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 100);   // au-dessus du HUD et de tout effet plein écran

        _root = canvasGo;
        UiStyle.Scrim(canvasGo.transform);

        var panel = UiStyle.Panel(canvasGo.transform, "Panel", FrameAccent.Violet);
        var panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(920f, 460f);
        panelRect.anchoredPosition = Vector2.zero;

        var heading = UiStyle.Label(panel.transform, Loc.T("ASSIM_TITLE"), 30,
                                    UiPalette.Violet, TextAnchor.UpperCenter);
        var headingRect = heading.GetComponent<RectTransform>();
        headingRect.anchorMin = new Vector2(0f, 1f);
        headingRect.anchorMax = new Vector2(1f, 1f);
        headingRect.pivot = new Vector2(0.5f, 1f);
        headingRect.offsetMin = new Vector2(24f, -66f);
        headingRect.offsetMax = new Vector2(-24f, -18f);

        _title = UiStyle.Label(panel.transform, "", 34, UiPalette.Gold, TextAnchor.UpperCenter);
        var titleRect = _title.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.offsetMin = new Vector2(24f, -122f);
        titleRect.offsetMax = new Vector2(-24f, -70f);

        _body = UiStyle.Label(panel.transform, "", 20, UiPalette.OffWhite, TextAnchor.UpperLeft);
        var bodyRect = _body.GetComponent<RectTransform>();
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(36f, 100f);
        bodyRect.offsetMax = new Vector2(-36f, -130f);

        _accept = UiStyle.TextButton(panel.transform, Loc.T("ASSIM_ASSIMILATE"), FrameAccent.Cyan);
        Place(_accept, new Vector2(0.28f, 0f));
        _accept.onClick.AddListener(() => Resolve(true));

        _decline = UiStyle.TextButton(panel.transform, Loc.T("ASSIM_REJECT"), FrameAccent.Steel);
        Place(_decline, new Vector2(0.72f, 0f));
        _decline.onClick.AddListener(() => Resolve(false));

        // Chaîne de focus explicite : sans elle, la manette ne peut pas passer d'un bouton à l'autre.
        var acceptNav = _accept.navigation;
        acceptNav.mode = Navigation.Mode.Explicit;
        acceptNav.selectOnRight = _decline;
        _accept.navigation = acceptNav;

        var declineNav = _decline.navigation;
        declineNav.mode = Navigation.Mode.Explicit;
        declineNav.selectOnLeft = _accept;
        _decline.navigation = declineNav;
    }

    /// <summary>Noms lisibles des greffes qu'une fusion absorbe.</summary>
    private static System.Collections.Generic.List<string> FusionSourceNames(GraftTable.FusionDef fusion)
    {
        var names = new System.Collections.Generic.List<string>();

        foreach (string id in fusion.Requires)
        {
            var source = Assimilation.Config.GraftById(id);
            names.Add(source != null ? source.Name : id);
        }

        return names;
    }

    private static void Place(Button button, Vector2 anchor)
    {
        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = new Vector2(340f, 62f);
        rect.anchoredPosition = new Vector2(0f, 20f);
    }
}
