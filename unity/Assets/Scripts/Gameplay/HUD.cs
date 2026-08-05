using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Affichage de run — barre de vie, barre d'XP, niveau, chrono, victimes (Lot 2).
///
/// <para><b>Construit en code</b>, conformément à la stack retenue (uGUI + procédural, §1) : l'UI du
/// projet Godot est elle aussi bâtie en C#, ce qui rend la traduction quasi mécanique — un
/// <c>Control</c> + <c>anchors</c> devient un <c>RectTransform</c> + ancres.</para>
///
/// <para>Le texte utilise la police du jeu (Share Tech Mono) via <see cref="UiFonts"/> — la police
/// par défaut d'Unity donnait une interface qui ne « ressemblait » plus au jeu, même avec des
/// couleurs et des ancrages justes.</para>
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

    /// <summary>
    /// Vert de la barre de vie — celui du jeu publié. Le portage l'avait mise en <b>rouge</b> : une
    /// barre pleine y paraît alors déjà critique, et l'information « je vais mal » n'a plus de
    /// couleur disponible pour se dire.
    /// </summary>
    private static readonly Color HealthGreen = new(0.29f, 0.94f, 0.62f);

    /// <summary>Rouge de la barre du boss — la couleur de la MENACE, pas celle du joueur.</summary>
    private static readonly Color BossRed = new(0.90f, 0.25f, 0.35f);

    private Image? _healthFill;
    private Image? _xpFill;
    private Text?  _levelLabel;
    private Text?  _healthLabel;
    private Text?  _timerLabel;
    private Text?  _biomeLabel;
    private Text?  _killsLabel;
    private Text?  _coreLabel;
    private Transform? _graftSlots;
    private Transform? _arsenalRows;
    private Text?  _dashLabel;
    private Text?  _fpsLabel;
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

        // Les emplacements se redessinent à chaque greffe acquise — sans cet abonnement, la rangée
        // resterait vide toute la run alors que le joueur porte trois greffes.
        Assimilation.GraftEquipped += OnGraftEquipped;

        RefreshArsenal();
        RefreshGraftSlots();
        RefreshBiome();
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

        Assimilation.GraftEquipped -= OnGraftEquipped;
    }

    private void OnArsenalChanged(string id, int level) => RefreshArsenal();

    private void OnGraftEquipped(GraftTable.GraftDef def) => RefreshGraftSlots();

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
        if (_arsenalRows == null) return;

        for (int i = _arsenalRows.childCount - 1; i >= 0; i--)
            Destroy(_arsenalRows.GetChild(i).gameObject);

        var inv = InventorySystem.Instance;
        if (inv == null) return;

        foreach (var (id, level) in inv.WeaponLevels) AddArsenalRow(id, $"{UiNames.Of(id)}  {level}");
        foreach (var (id, level) in inv.PassiveLevels) AddArsenalRow(id, $"· {UiNames.Of(id)}  {level}");
    }

    /// <summary>
    /// Une ligne d'arsenal : l'icône, puis le nom et le niveau.
    ///
    /// <para>La liste se reconstruit entièrement plutôt que de se mettre à jour. L'arsenal change au
    /// plus une fois par montée de niveau, et un cache de lignes devrait suivre les <b>fusions</b>,
    /// qui retirent deux armes pour en ajouter une.</para>
    /// </summary>
    private void AddArsenalRow(string id, string text)
    {
        if (_arsenalRows == null) return;

        var row = new GameObject("Row", typeof(RectTransform));
        row.transform.SetParent(_arsenalRows, false);

        var element = row.AddComponent<LayoutElement>();
        element.minHeight = ArsenalLineHeight;
        element.preferredHeight = ArsenalLineHeight;

        var sprite = UiIcons.For(id);
        if (sprite != null)
        {
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(row.transform, false);

            var image = iconGo.AddComponent<UnityEngine.UI.Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = new Vector2(0f, 0f);
            iconRect.anchorMax = new Vector2(0f, 1f);
            iconRect.pivot = new Vector2(0f, 0.5f);
            iconRect.sizeDelta = new Vector2(ArsenalLineHeight, 0f);
            iconRect.anchoredPosition = Vector2.zero;
        }

        var labelGo = new GameObject("Label", typeof(Text));
        labelGo.transform.SetParent(row.transform, false);

        var label = labelGo.GetComponent<Text>();
        label.font = UiFonts.Main;
        label.fontSize = ArsenalFontSize;
        label.color = OffWhite;
        label.alignment = TextAnchor.MiddleLeft;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.text = text;

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(ArsenalLineHeight + 6f, 0f);
        labelRect.offsetMax = Vector2.zero;
    }

    /// <summary>Corps du libellé d'arsenal.</summary>
    private const int ArsenalFontSize = 16;

    /// <summary>Hauteur d'une ligne d'arsenal — elle fixe aussi le côté de l'icône.</summary>
    private const float ArsenalLineHeight = 22f;

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
                _timerLabel.text = $"+{ot / 60:00}:{ot % 60:00}";
                _timerLabel.color = Gold;
            }
            else
            {
                int left = Mathf.Max(0, gm.RunDurationSeconds - Mathf.FloorToInt(gm.RunTime));
                _timerLabel.text = $"{left / 60:00}:{left % 60:00}";
            }
        }

        if (gm != null && _killsLabel != null) _killsLabel.text = $"☠ {gm.Kills}";
        if (gm != null && _coreLabel != null) _coreLabel.text = gm.CoresCollected.ToString();

        UpdateBossBar();
        UpdateBanner();
        UpdateDash();
        UpdateFps();
    }

    /// <summary>
    /// Écrit le nom du secteur et son effet. Fait une seule fois : le biome ne change pas en cours
    /// de run, et le relire à chaque frame ferait une recherche de traduction soixante fois par
    /// seconde pour un texte immuable.
    /// </summary>
    private void RefreshBiome()
    {
        if (_biomeLabel == null) return;

        string? id = GameManager.Instance?.CurrentBiomeId ?? RunConfig.BiomeId;
        if (string.IsNullOrEmpty(id)) { _biomeLabel.text = ""; return; }

        string slug = id!.ToUpperInvariant();
        _biomeLabel.text = $"{Loc.T($"BIOME_{slug}_NAME")}   ·   {Loc.T($"BIOME_{slug}_EFFECT")}";
    }

    private float _fpsAccumulator;
    private int _fpsFrames;

    /// <summary>
    /// Compteur d'images, si le joueur l'a demandé dans les options. <b>Moyenné sur une demi-seconde</b> :
    /// afficher la valeur instantanée donne un nombre qui saute trop pour être lu, et rend le
    /// diagnostic d'une chute de cadence plus difficile, pas plus facile.
    /// </summary>
    private void UpdateFps()
    {
        if (_fpsLabel == null) return;

        if (!GameSettings.Current.ShowFps) { _fpsLabel.text = ""; return; }

        _fpsAccumulator += Time.unscaledDeltaTime;
        _fpsFrames++;

        if (_fpsAccumulator < 0.5f) return;

        _fpsLabel.text = $"{_fpsFrames / _fpsAccumulator:F0} IPS";
        _fpsAccumulator = 0f;
        _fpsFrames = 0;
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

        // Le chiffre suit la barre : une barre dit une proportion, le chiffre dit la marge — et
        // c'est la marge qui décide si l'on peut encore encaisser un coup.
        if (_healthLabel != null) _healthLabel.text = $"{current:F0} / {max:F0}";
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("HUDCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo);

        BuildVitals(canvasGo.transform);
        BuildTimer(canvasGo.transform);

        // Arsenal en bas à gauche : une LIGNE par arme — icône puis libellé —, empilées vers le
        // haut depuis un pivot bas, sinon la liste sortirait de l'écran dès la sixième arme.
        //
        // ⚠ Icône et texte vivent dans la MÊME ligne, et non en deux colonnes parallèles. Une
        // colonne d'icônes posée à côté d'un bloc de texte oblige à deviner l'interligne exact
        // d'uGUI : au premier essai le décalage était invisible sur une arme et valait deux lignes
        // entières sur dix. Une mise en page ne se devine pas, elle se délègue.
        var list = new GameObject("Arsenal", typeof(RectTransform));
        list.transform.SetParent(canvasGo.transform, false);

        var listRect = list.GetComponent<RectTransform>();
        listRect.anchorMin = listRect.anchorMax = new Vector2(0f, 0f);
        listRect.pivot = new Vector2(0f, 0f);
        listRect.anchoredPosition = new Vector2(24f, 24f);
        listRect.sizeDelta = new Vector2(360f, 0f);

        var listLayout = list.AddComponent<VerticalLayoutGroup>();
        listLayout.spacing = 2f;
        listLayout.childAlignment = TextAnchor.LowerLeft;
        listLayout.childForceExpandHeight = false;
        listLayout.childControlHeight = true;
        listLayout.childControlWidth = true;

        // La liste prend la hauteur de son contenu, donc grandit vers le haut depuis son pivot bas.
        list.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        _arsenalRows = list.transform;

        // En bas à droite, hors du chemin du regard : une capacité s'annonce sans occuper le centre.
        _dashLabel = BuildLabel(canvasGo.transform, "Dash", new Vector2(1f, 0f),
                                new Vector2(-320f, 60f), new Vector2(300f, 26f), Cyan, TextAnchor.LowerRight);
        _dashLabel.fontSize = 18;

        _fpsLabel = BuildLabel(canvasGo.transform, "Fps", new Vector2(1f, 1f),
                               new Vector2(-140f, -24f), new Vector2(120f, 26f), OffWhite, TextAnchor.UpperRight);
        _fpsLabel.fontSize = 16;

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
                             new Vector2(0f, -24f), new Vector2(800f, 18f), BossRed);

        _bossPanel = panel;
        panel.SetActive(false);
    }

    /// <summary>Barre à deux couches : un fond sombre, un remplissage horizontal par-dessus.</summary>
    /// <summary>
    /// Bloc vital, en haut à gauche : <b>un panneau</b> portant le niveau, les points de vie chiffrés,
    /// la barre de vie, la barre d'XP et les emplacements de greffes.
    ///
    /// <para>Le portage posait quatre éléments nus sur le décor. Trois écarts avec le jeu publié
    /// (<c>docs/ui_v1160_levelup.png</c>), et aucun n'est décoratif :</para>
    /// <list type="number">
    ///   <item><b>Le panneau</b> détache le HUD du sol de l'arène. Sans lui, une barre de vie posée
    ///         sur une tuile claire devient illisible au pire moment.</item>
    ///   <item><b>Les PV chiffrés</b> (« 129 / 140 ») : une barre dit une proportion, pas une marge.
    ///         « Il me reste un coup » ne se lit pas sur une fraction.</item>
    ///   <item><b>Les emplacements de greffes</b> : ils disent combien il en reste à prendre. La
    ///         chimère est le troisième axe de progression du jeu et n'apparaissait nulle part
    ///         pendant la run.</item>
    /// </list>
    /// </summary>
    private void BuildVitals(Transform parent)
    {
        var panel = BuildFrame(parent, "Vitals", FrameAccent.Violet);

        var rect = panel.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(VitalsWidth, 152f);
        rect.anchoredPosition = new Vector2(20f, -20f);

        _levelLabel = BuildLabel(panel.transform, "Level", new Vector2(0f, 1f),
                                 new Vector2(22f, -12f), new Vector2(200f, 34f), OffWhite,
                                 TextAnchor.UpperLeft);
        _levelLabel.fontSize = 28;

        // Les PV chiffrés à DROITE, en regard du niveau : les deux nombres que le joueur cherche
        // sont ainsi aux deux bouts de la même ligne, jamais à se chercher l'un l'autre.
        _healthLabel = BuildLabel(panel.transform, "Health", new Vector2(1f, 1f),
                                  new Vector2(-22f, -12f), new Vector2(220f, 34f), HealthGreen,
                                  TextAnchor.UpperRight);
        _healthLabel.fontSize = 24;
        var healthLabelRect = _healthLabel.GetComponent<RectTransform>();
        healthLabelRect.pivot = new Vector2(1f, 1f);

        _healthFill = BuildBar(panel.transform, "HealthBar", new Vector2(0f, 1f),
                               new Vector2(22f, -54f), new Vector2(VitalsWidth - 44f, 20f), HealthGreen);

        _xpFill = BuildBar(panel.transform, "XpBar", new Vector2(0f, 1f),
                           new Vector2(22f, -80f), new Vector2(VitalsWidth - 44f, 10f), Cyan);

        var slots = new GameObject("GraftSlots", typeof(RectTransform));
        slots.transform.SetParent(panel.transform, false);
        Place(slots, new Vector2(0f, 1f), new Vector2(22f, -98f), new Vector2(VitalsWidth - 44f, SlotSize));

        var slotsLayout = slots.AddComponent<HorizontalLayoutGroup>();
        slotsLayout.spacing = 8f;
        slotsLayout.childForceExpandWidth = false;
        slotsLayout.childControlWidth = false;
        slotsLayout.childControlHeight = false;

        _graftSlots = slots.transform;
    }

    /// <summary>
    /// Chrono centré, <b>souligné</b>, et sous lui le nom du secteur avec son effet.
    ///
    /// <para>Le nom du biome n'est pas un rappel inutile : son effet — « ennemis +18 % rapides »,
    /// « +20 % d'XP » — change la façon de jouer la run en cours, et le joueur l'a choisi plusieurs
    /// minutes plus tôt sur un autre écran.</para>
    /// </summary>
    private void BuildTimer(Transform parent)
    {
        _timerLabel = BuildLabel(parent, "Timer", new Vector2(0.5f, 1f),
                                 new Vector2(-260f, -18f), new Vector2(520f, 44f), OffWhite,
                                 TextAnchor.UpperCenter);
        _timerLabel.fontSize = 34;

        var rule = new GameObject("TimerRule", typeof(Image));
        rule.transform.SetParent(parent, false);
        Place(rule, new Vector2(0.5f, 1f), new Vector2(-90f, -60f), new Vector2(180f, 3f));
        rule.GetComponent<Image>().color = Violet;

        _biomeLabel = BuildLabel(parent, "Biome", new Vector2(0.5f, 1f),
                                 new Vector2(-460f, -70f), new Vector2(920f, 26f), Violet,
                                 TextAnchor.UpperCenter);
        _biomeLabel.fontSize = 19;

        // Compteur de Noyaux d'Aether en haut à droite, avec son icône — la place et la forme du jeu
        // publié. C'est la monnaie de méta-progression : elle se compte pendant la run, pas
        // seulement à la fin.
        var coreIcon = new GameObject("CoreIcon", typeof(RectTransform));
        coreIcon.transform.SetParent(parent, false);
        Place(coreIcon, new Vector2(1f, 1f), new Vector2(-92f, -18f), new Vector2(30f, 30f));

        var coreImage = coreIcon.AddComponent<Image>();
        coreImage.sprite = UiIcons.For("xp_bonus");   // le pictogramme de Noyau du jeu
        coreImage.preserveAspect = true;
        coreImage.raycastTarget = false;
        coreImage.color = Violet;

        _coreLabel = BuildLabel(parent, "Cores", new Vector2(1f, 1f),
                                new Vector2(-56f, -18f), new Vector2(48f, 30f), Violet,
                                TextAnchor.UpperLeft);
        _coreLabel.fontSize = 24;

        // Éliminations juste en dessous : elles étaient collées au chrono, où elles brouillaient la
        // seule information qu'on y cherche.
        _killsLabel = BuildLabel(parent, "Kills", new Vector2(1f, 1f),
                                 new Vector2(-200f, -52f), new Vector2(180f, 30f), Gold,
                                 TextAnchor.UpperRight);
        _killsLabel.fontSize = 20;
        _killsLabel.GetComponent<RectTransform>().pivot = new Vector2(1f, 1f);
    }

    /// <summary>
    /// Une pastille d'emplacement de greffe : l'icône si elle est portée, un cadre vide sinon.
    /// <b>Les emplacements libres restent affichés</b> — c'est ce qui dit qu'il reste de la place.
    /// </summary>
    private void RefreshGraftSlots()
    {
        if (_graftSlots == null) return;

        for (int i = _graftSlots.childCount - 1; i >= 0; i--)
            Destroy(_graftSlots.GetChild(i).gameObject);

        var equipped = Assimilation.Equipped;

        for (int i = 0; i < Assimilation.SlotCount; i++)
        {
            bool filled = i < equipped.Count;

            var slot = BuildFrame(_graftSlots, "Slot" + i, FrameAccent.Violet);
            var slotRect = slot.GetComponent<RectTransform>();
            slotRect.sizeDelta = new Vector2(SlotSize, SlotSize);

            if (!filled) continue;

            var sprite = UiIcons.For(equipped[i]);
            if (sprite == null) continue;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(slot.transform, false);

            var image = iconGo.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            image.raycastTarget = false;

            var iconRect = iconGo.GetComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.offsetMin = new Vector2(5f, 5f);
            iconRect.offsetMax = new Vector2(-5f, -5f);
        }
    }

    /// <summary>
    /// Objet d'interface portant un cadre « plaque blindée ».
    ///
    /// <para>Le HUD ne peut pas appeler <c>UiStyle</c> : il appartient à <c>Gameplay</c>, que
    /// l'assemblage <c>UI</c> référence déjà. Il passe donc par <see cref="UiFrames"/>, la partie de
    /// la fabrique qui vit dans <c>Platform</c> — ce qui garde la règle « aucun style ad hoc »
    /// intacte : la texture, le découpage et le repli restent décidés à un seul endroit.</para>
    /// </summary>
    private static GameObject BuildFrame(Transform parent, string name, FrameAccent accent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        var image = go.AddComponent<Image>();
        image.raycastTarget = false;   // le HUD ne se clique pas : il ne doit rien intercepter

        if (!UiFrames.Apply(image, $"ui_frame_button_{UiFrames.Slug(accent)}"))
            image.color = Background;

        return go;
    }

    /// <summary>Largeur du bloc vital, en pixels de référence.</summary>
    private const float VitalsWidth = 460f;

    /// <summary>Côté d'un emplacement de greffe.</summary>
    private const float SlotSize = 42f;

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
        text.font = UiFonts.Main;
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
