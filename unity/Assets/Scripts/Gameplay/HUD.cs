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
    private Text?  _arsenalLabel;
    private Text?  _dashLabel;
    private Text?  _bannerLabel;
    private Text?  _bossLabel;
    private Image? _bossFill;
    private GameObject? _bossPanel;

    private float _bannerLeft;

    private void Start()
    {
        BuildUi();

        if (Player.Instance != null)
        {
            Player.Instance.HealthChanged += OnHealthChanged;
            OnHealthChanged(Player.Instance.Stats.CurrentHp, Player.Instance.Stats.MaxHp);
        }

        var inv = InventorySystem.Instance;
        if (inv != null)
        {
            inv.WeaponChanged  += OnArsenalChanged;
            inv.FusionApplied  += OnArsenalChanged;
            inv.PassiveChanged += OnArsenalChanged;
        }

        var gm = GameManager.Instance;
        if (gm != null) gm.OvertimeStarted += OnOvertimeStarted;

        RefreshArsenal();
    }

    private void OnDestroy()
    {
        if (Player.Instance != null) Player.Instance.HealthChanged -= OnHealthChanged;

        var inv = InventorySystem.Instance;
        if (inv != null)
        {
            inv.WeaponChanged  -= OnArsenalChanged;
            inv.FusionApplied  -= OnArsenalChanged;
            inv.PassiveChanged -= OnArsenalChanged;
        }

        var gm = GameManager.Instance;
        if (gm != null) gm.OvertimeStarted -= OnOvertimeStarted;
    }

    private void OnArsenalChanged(string id, int level) => RefreshArsenal();

    private void OnOvertimeStarted() => Announce("LE NOYAU ROUILLÉ ARRIVE");

    /// <summary>Affiche un bandeau temporaire au centre de l'écran.</summary>
    public void Announce(string message, float seconds = 4f)
    {
        if (_bannerLabel == null) return;
        _bannerLabel.text = message;
        _bannerLeft = seconds;
    }

    /// <summary>
    /// Liste ce que le joueur porte. <b>Sans elle, prendre une carte ne se voit nulle part</b> : le
    /// joueur ne peut ni savoir ce qu'il a, ni constater qu'une arme est montée de niveau — et une
    /// arme sans effet visible devient alors indiscernable d'une arme absente.
    /// </summary>
    private void RefreshArsenal()
    {
        if (_arsenalLabel == null) return;

        var inv = InventorySystem.Instance;
        if (inv == null) { _arsenalLabel.text = ""; return; }

        var sb = new System.Text.StringBuilder();
        foreach (var (id, level) in inv.WeaponLevels) sb.AppendLine($"{Pretty(id)}  {level}");
        foreach (var (id, level) in inv.PassiveLevels) sb.AppendLine($"· {Pretty(id)}  {level}");

        _arsenalLabel.text = sb.ToString();
    }

    /// <summary>
    /// Rend un identifiant lisible faute de traduction : <c>tesla_coil</c> → <c>TESLA COIL</c>. La
    /// vraie table de localisation appartient au lot d'interface ; afficher l'id brut vaut toujours
    /// mieux que de n'afficher rien.
    /// </summary>
    private static string Pretty(string id) => id.Replace('_', ' ').ToUpperInvariant();

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
            // En overtime, le chrono compte le temps PASSÉ au-delà du temps imparti : c'est ce
            // dépassement qui est récompensé, et ne rien afficher laisserait croire que rien n'a
            // changé au moment le plus dangereux de la run.
            if (gm.Overtime)
            {
                int ot = Mathf.FloorToInt(gm.OvertimeSeconds);
                _timerLabel.text = $"SURCHARGE +{ot / 60:00}:{ot % 60:00}   {gm.Kills} elim.";
                _timerLabel.color = Gold;
            }
            else
            {
                int left = Mathf.Max(0, gm.RunDurationSeconds - Mathf.FloorToInt(gm.RunTime));
                _timerLabel.text = $"{left / 60:00}:{left % 60:00}   {gm.Kills} elim.";
            }
        }

        UpdateBossBar();
        UpdateBanner();
        UpdateDash();
    }

    /// <summary>
    /// Affiche l'esquive <b>et sa touche</b> dès qu'une greffe l'accorde.
    ///
    /// <para>⚠ Défaut déjà commis côté Godot : la capacité n'était annoncée nulle part — ni au HUD,
    /// ni dans la description, ni à l'acquisition — et une run entière a été jouée sans savoir
    /// qu'une touche existait. Une capacité qui ne s'annonce pas n'existe pas pour le joueur.</para>
    /// </summary>
    private void UpdateDash()
    {
        if (_dashLabel == null) return;

        var player = Player.Instance;
        if (player == null || !player.DashEnabled) { _dashLabel.text = ""; return; }

        bool ready = player.DashReadyRatio >= 1f;
        _dashLabel.text = ready
            ? $"{InputRemap.DisplayName(GameAction.Dash)} — esquive"
            : $"{InputRemap.DisplayName(GameAction.Dash)} — esquive  {player.DashReadyRatio * 100f:F0} %";
        _dashLabel.color = ready ? Cyan : Background;
    }

    /// <summary>
    /// Barre du boss : elle n'apparaît que s'il vit. Elle porte sa phase, son cap et sa distance —
    /// il avance lentement (46 px/s), donc savoir <b>d'où</b> il vient vaut autant que savoir qu'il
    /// est là.
    /// </summary>
    private void UpdateBossBar()
    {
        RustedCore? boss = null;
        foreach (var e in EnemyBase.Active)
            if (e is RustedCore rc && !rc.IsDead) { boss = rc; break; }

        if (_bossPanel != null) _bossPanel.SetActive(boss != null);
        if (boss == null) return;

        if (_bossFill != null) _bossFill.fillAmount = boss.HpRatio;

        if (_bossLabel != null)
        {
            var player = Player.Instance;
            string bearing = "";
            if (player != null)
            {
                Vector2 d = (Vector2)boss.transform.position - (Vector2)player.transform.position;
                // Un cap et une distance, parce que la barre seule dit « il existe » sans dire « où ».
                bearing = $"   {Compass(d)} {d.magnitude:F0}";
            }

            // Le pourcentage est affiché en toutes lettres : contre un boss à 5 000 PV, une barre qui
            // descend de 0,2 % par seconde se lit « elle ne bouge pas ». Le chiffre, lui, bouge.
            _bossLabel.text = $"{boss.DisplayName}   PHASE {BossPhases.RomanNumeral(boss.Phase)}   " +
                              $"{boss.HpRatio * 100f:F1} %{bearing}";
        }
    }

    private static string Compass(Vector2 d)
    {
        float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        string[] points = { "E", "NE", "N", "NO", "O", "SO", "S", "SE" };
        return points[Mathf.RoundToInt(angle / 45f) % 8];
    }

    private void UpdateBanner()
    {
        if (_bannerLabel == null) return;

        if (_bannerLeft <= 0f) { _bannerLabel.text = ""; return; }
        _bannerLeft -= Time.unscaledDeltaTime;
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
                                 new Vector2(-160f, -24f), new Vector2(320f, 26f), OffWhite, TextAnchor.UpperCenter);

        // Arsenal en bas à gauche : la liste grandit vers le haut depuis un pivot bas, sinon elle
        // sortirait de l'écran dès la sixième arme.
        _arsenalLabel = BuildLabel(canvasGo.transform, "Arsenal", new Vector2(0f, 0f),
                                   new Vector2(24f, 320f), new Vector2(320f, 300f), OffWhite, TextAnchor.LowerLeft);
        _arsenalLabel.fontSize = 16;

        // En bas à droite, hors du chemin du regard : une capacité s'annonce sans occuper le centre.
        _dashLabel = BuildLabel(canvasGo.transform, "Dash", new Vector2(1f, 0f),
                                new Vector2(-320f, 60f), new Vector2(300f, 26f), Cyan, TextAnchor.LowerRight);
        _dashLabel.fontSize = 18;

        BuildBossPanel(canvasGo.transform);

        _bannerLabel = BuildLabel(canvasGo.transform, "Banner", new Vector2(0.5f, 0.5f),
                                  new Vector2(-320f, 160f), new Vector2(640f, 40f), Gold, TextAnchor.MiddleCenter);
        _bannerLabel.fontSize = 30;
    }

    private void BuildBossPanel(Transform parent)
    {
        var panel = new GameObject("BossPanel", typeof(RectTransform));
        panel.transform.SetParent(parent, false);
        Place(panel, new Vector2(0.5f, 1f), new Vector2(-400f, -64f), new Vector2(800f, 46f));

        _bossLabel = BuildLabel(panel.transform, "BossName", new Vector2(0f, 1f),
                                new Vector2(0f, 0f), new Vector2(800f, 22f), Gold, TextAnchor.UpperCenter);
        _bossLabel.fontSize = 18;

        _bossFill = BuildBar(panel.transform, "Boss", new Vector2(0f, 1f),
                             new Vector2(0f, -24f), new Vector2(800f, 18f), HealthRed);

        _bossPanel = panel;
        panel.SetActive(false);
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

        // ⚠ Sans sprite, Unity IGNORE fillAmount sur une Image de type Filled : la barre reste
        // pleine en permanence, sans erreur. Symptôme trompeur — « les valeurs ne changent pas »
        // alors que le jeu fonctionne.
        img.sprite = UiPrimitives.White;
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
