using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affichage de run — barre de vie, barre d'XP, niveau, chrono, victimes (Lot 2).
///
/// <para><b>Construit en code</b>, conformément à la stack retenue (uGUI + procédural, §1) : l'UI du
/// projet Godot est elle aussi bâtie en C#, ce qui rend la traduction quasi mécanique — un
/// <c>Control</c> + <c>anchors</c> devient un <c>RectTransform</c> + ancres.</para>
///
/// <para>⚠ <b>Provisoire sur un point</b> : le texte utilise la police intégrée d'Unity. La vraie
/// police du jeu (Share Tech Mono, AA activé, corps 16) demande un asset TextMeshPro à générer —
/// c'est un travail du lot d'interface, pas du cœur de run. Le reste (barres, ancrages, couleurs)
/// est déjà à sa place définitive.</para>
///
/// <para>Les couleurs viennent de la palette du projet : fond <c>#1A1A2E</c>, cyan <c>#44FFEE</c>,
/// violet <c>#AA44FF</c>, or <c>#FFCC44</c>, blanc cassé <c>#D9D9F2</c>.</para>
/// </summary>
public sealed class HUD : MonoBehaviour
{
    private static readonly Color Background = new(0.102f, 0.102f, 0.180f, 0.85f);
    private static readonly Color Cyan       = new(0.267f, 1.000f, 0.933f);
    private static readonly Color Violet     = new(0.667f, 0.267f, 1.000f);
    private static readonly Color Gold       = new(1.000f, 0.800f, 0.267f);
    private static readonly Color OffWhite    = new(0.851f, 0.851f, 0.949f);
    private static readonly Color HealthRed  = new(0.90f, 0.25f, 0.35f);

    private Image? _healthFill;
    private Image? _xpFill;
    private Text?  _levelLabel;
    private Text?  _timerLabel;

    private void Start()
    {
        BuildUi();

        if (Player.Instance != null)
        {
            Player.Instance.HealthChanged += OnHealthChanged;
            OnHealthChanged(Player.Instance.Stats.CurrentHp, Player.Instance.Stats.MaxHp);
        }
    }

    private void OnDestroy()
    {
        if (Player.Instance != null) Player.Instance.HealthChanged -= OnHealthChanged;
    }

    private void Update()
    {
        var xp = XpSystem.Instance;
        if (xp != null)
        {
            int threshold = Mathf.Max(1, xp.XpToNextLevel);
            if (_xpFill != null) _xpFill.fillAmount = Mathf.Clamp01((float)xp.CurrentXp / threshold);
            if (_levelLabel != null) _levelLabel.text = $"NIV {xp.CurrentLevel}";
        }

        var gm = GameManager.Instance;
        if (gm != null && _timerLabel != null)
        {
            int total = Mathf.FloorToInt(gm.RunTime);
            _timerLabel.text = $"{total / 60:00}:{total % 60:00}   {gm.Kills} elim.";
        }
    }

    private void OnHealthChanged(float current, float max)
    {
        if (_healthFill != null) _healthFill.fillAmount = max > 0f ? Mathf.Clamp01(current / max) : 0f;
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;

        // Mise à l'échelle par résolution de référence : sans cela, le HUD garde une taille en
        // pixels et devient minuscule en 1440p — le pixel art, lui, doit rester net.
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _healthFill = BuildBar(canvasGo.transform, "Health", new Vector2(0f, 1f),
                               new Vector2(24f, -24f), new Vector2(420f, 22f), HealthRed);

        _xpFill = BuildBar(canvasGo.transform, "Xp", new Vector2(0f, 1f),
                           new Vector2(24f, -54f), new Vector2(420f, 12f), Cyan);

        _levelLabel = BuildLabel(canvasGo.transform, "Level", new Vector2(0f, 1f),
                                 new Vector2(24f, -76f), new Vector2(220f, 26f), Gold, TextAnchor.UpperLeft);

        _timerLabel = BuildLabel(canvasGo.transform, "Timer", new Vector2(0.5f, 1f),
                                 new Vector2(-110f, -24f), new Vector2(220f, 26f), OffWhite, TextAnchor.UpperCenter);
    }

    /// <summary>Barre à deux couches : un fond sombre, un remplissage horizontal par-dessus.</summary>
    private static Image BuildBar(Transform parent, string name, Vector2 anchor,
                                  Vector2 offset, Vector2 size, Color fillColor)
    {
        var backGo = new GameObject(name + "Bg", typeof(Image));
        backGo.transform.SetParent(parent, false);
        Place(backGo, anchor, offset, size);
        backGo.GetComponent<Image>().color = Background;

        var fillGo = new GameObject(name + "Fill", typeof(Image));
        fillGo.transform.SetParent(backGo.transform, false);

        var rt = fillGo.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2f, 2f);
        rt.offsetMax = new Vector2(-2f, -2f);

        var img = fillGo.GetComponent<Image>();
        img.color = fillColor;
        img.type = Image.Type.Filled;
        img.fillMethod = Image.FillMethod.Horizontal;
        img.fillOrigin = (int)Image.OriginHorizontal.Left;
        img.fillAmount = 1f;
        return img;
    }

    private static Text BuildLabel(Transform parent, string name, Vector2 anchor, Vector2 offset,
                                   Vector2 size, Color color, TextAnchor alignment)
    {
        var go = new GameObject(name, typeof(Text));
        go.transform.SetParent(parent, false);
        Place(go, anchor, offset, size);

        var text = go.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 20;
        text.color = color;
        text.alignment = alignment;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.text = "";
        return text;
    }

    private static void Place(GameObject go, Vector2 anchor, Vector2 offset, Vector2 size)
    {
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = size;
    }
}
