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
    /// Gris de mise en veille — un élément <b>présent mais indisponible</b>. Il ne doit jamais être
    /// confondu avec la couleur de fond : un libellé peint au fond disparaît, ce qui se lit
    /// « l'affichage a un bug », alors qu'un gris se lit « pas maintenant ».
    /// </summary>
    private static readonly Color Dim = new(0.45f, 0.45f, 0.55f);

    /// <summary>Cyan éteint — la même information que <see cref="Cyan"/>, mais en attente.</summary>
    private static readonly Color DimCyan = new(0.16f, 0.42f, 0.40f);

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
    private Transform? _safetyRow;
    private Transform? _arsenalRows;
    private Text?  _regenLabel;
    private Image? _reserveFill;
    private GameObject? _reserveBar;
    private Text?  _dashLabel;
    private Image? _dashFill;
    private GameObject? _dashGauge;
    private Text?  _fpsLabel;
    private Text?  _bannerLabel;
    private Text?  _bossLabel;
    private Image? _bossFill;
    private GameObject? _bossPanel;

    private float _bannerLeft;

    /// <summary>
    /// HUD de la run en cours. Exposé pour que le gameplay puisse <b>annoncer</b> un événement rare —
    /// un Noyau de Secours consommé, par exemple — sans qu'une entité tienne une référence d'UI.
    /// </summary>
    public static HUD? Instance { get; private set; }

    private void Awake() => Instance = this;

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
        if (gm != null)
        {
            gm.OvertimeStarted += OnOvertimeStarted;
            gm.BossDown += OnBossDown;
        }

        // Les emplacements se redessinent à chaque greffe acquise — sans cet abonnement, la rangée
        // resterait vide toute la run alors que le joueur porte trois greffes.
        Assimilation.GraftEquipped += OnGraftEquipped;

        RefreshArsenal();
        RefreshGraftSlots();
        RefreshBiome();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (Player.Instance != null) Player.Instance.HealthChanged -= OnHealthChanged;

        var inv = InventorySystem.Instance;
        if (inv != null)
        {
            inv.WeaponChanged  -= OnArsenalChanged;
            inv.FusionApplied  -= OnArsenalChanged;
            inv.PassiveChanged -= OnArsenalChanged;
        }

        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.OvertimeStarted -= OnOvertimeStarted;
            gm.BossDown -= OnBossDown;
        }

        Assimilation.GraftEquipped -= OnGraftEquipped;
    }

    private void OnArsenalChanged(string id, int level) => RefreshArsenal();

    private void OnGraftEquipped(GraftTable.GraftDef def) => RefreshGraftSlots();

    private void OnOvertimeStarted() => Announce(Loc.T("HUD_BOSS_INCOMING"));

    /// <summary>
    /// Annonce la <b>complétion du niveau</b> à la chute du boss.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Le portage avait perdu ce bandeau (<c>RunStatsTracker</c> l'affichait sous Godot), et
    /// son absence est plus lourde qu'une omission décorative : la chute du boss <b>ne termine pas la
    /// run</b>, qui continue jusqu'à la mort du joueur. Sans annonce, la seule chose qui change à
    /// l'écran est qu'un ennemi a disparu — et le joueur ne peut pas savoir qu'il vient de débloquer
    /// le niveau suivant, ni que ce qu'il joue désormais est du rab.</para>
    ///
    /// <para>La seconde ligne n'est donc pas du décor : elle répond à la question que la première
    /// fait immédiatement naître — « et maintenant ? ».</para>
    /// </remarks>
    private void OnBossDown()
        => Announce($"{Loc.T("LEVEL_COMPLETE")}\n{Loc.T("HUD_RUN_CONTINUES")}", 7f);

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

        // ⚠ Une fusion se distingue du reste de l'arsenal, en doré et précédée d'un losange. Sans
        // cela, la carte la plus difficile à obtenir du jeu produit une ligne strictement identique
        // à celle d'une arme ramassée au deuxième niveau — et le joueur, qui ne connaît pas encore
        // les noms de fusion, n'a aucun moyen de savoir laquelle de ses armes a évolué.
        foreach (var (id, level) in inv.WeaponLevels)
        {
            bool fused = inv.IsFusion(id);
            AddArsenalRow(id, fused ? $"◆ {UiNames.Of(id)}  {level}" : $"{UiNames.Of(id)}  {level}",
                          fused ? Gold : OffWhite);
        }

        foreach (var (id, level) in inv.PassiveLevels)
            AddArsenalRow(id, $"· {UiNames.Of(id)}  {level}", OffWhite);
    }

    /// <summary>
    /// Une ligne d'arsenal : l'icône, puis le nom et le niveau.
    ///
    /// <para>La liste se reconstruit entièrement plutôt que de se mettre à jour. L'arsenal change au
    /// plus une fois par montée de niveau, et un cache de lignes devrait suivre les <b>fusions</b>,
    /// qui retirent deux armes pour en ajouter une.</para>
    /// </summary>
    private void AddArsenalRow(string id, string text, Color color)
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
        label.color = color;
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
            // « NIV » était écrit en dur, dans le seul élément d'interface visible en permanence.
            if (_levelLabel != null) _levelLabel.text = Loc.T("HUD_LEVEL", xp.CurrentLevel);
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
        UpdateRegen();
        UpdateSafetyPips();
        UpdateFps();
    }

    private Image[] _lifePips = System.Array.Empty<Image>();
    private Image[] _absorbPips = System.Array.Empty<Image>();
    private bool _safetyPipsBuilt;
    private int _lastLivesLeft = -1;
    private int _lastAbsorbLeft = -1;

    /// <summary>
    /// Dessine les filets de survie achetés au Hub : une pastille par charge, vive tant qu'elle est
    /// disponible, <b>éteinte</b> une fois dépensée.
    /// </summary>
    /// <remarks>
    /// <para>Une pastille dépensée s'éteint mais ne <b>disparaît pas</b> : sinon « il m'en restait
    /// une » et « je n'en ai jamais eu » s'affichent exactement pareil. C'est le correctif d'un
    /// retour joué sous Godot — le joueur ne pouvait pas savoir qu'une vie venait d'être consommée.</para>
    ///
    /// <para>Construites une seule fois, au premier passage où le joueur existe : les maxima sont
    /// figés au démarrage de la run par <see cref="Player.InitSafetyNets"/>. Si le cran IV « Sans
    /// filet » les met à zéro, la rangée reste simplement invisible — la règle du cran est déjà lue
    /// avant de lancer, la redire ici serait du bruit.</para>
    /// </remarks>
    private void UpdateSafetyPips()
    {
        if (_safetyRow == null) return;

        var player = Player.Instance;
        if (player == null) return;

        if (!_safetyPipsBuilt)
        {
            _safetyPipsBuilt = true;
            _lifePips   = BuildPips(player.ExtraLivesMax,    new Color(0.55f, 1f, 0.65f), 12f);
            _absorbPips = BuildPips(player.AbsorbChargesMax, new Color(0.45f, 0.72f, 1f), 8f);
            _safetyRow.gameObject.SetActive(_lifePips.Length > 0 || _absorbPips.Length > 0);
        }

        if (!_safetyRow.gameObject.activeSelf) return;

        if (player.ExtraLivesLeft != _lastLivesLeft)
        {
            _lastLivesLeft = player.ExtraLivesLeft;
            PaintPips(_lifePips, _lastLivesLeft);
        }

        if (player.AbsorbChargesLeft != _lastAbsorbLeft)
        {
            _lastAbsorbLeft = player.AbsorbChargesLeft;
            PaintPips(_absorbPips, _lastAbsorbLeft);
        }
    }

    /// <summary>
    /// Crée les pastilles d'un filet. Les Noyaux de Secours sont plus <b>hauts</b> que les Plaques :
    /// une charge qui sauve d'une mort ne doit pas se lire comme une charge qui absorbe un coup,
    /// même du coin de l'œil.
    /// </summary>
    private Image[] BuildPips(int count, Color tint, float height)
    {
        var pips = new Image[Mathf.Max(0, count)];

        for (int i = 0; i < pips.Length; i++)
        {
            var go = new GameObject("Pip", typeof(Image));
            go.transform.SetParent(_safetyRow, false);
            go.GetComponent<RectTransform>().sizeDelta = new Vector2(6f, height);

            var image = go.GetComponent<Image>();
            image.color = tint;
            image.raycastTarget = false;
            pips[i] = image;
        }

        return pips;
    }

    /// <summary>Allume les <paramref name="left"/> premières pastilles, éteint les suivantes.</summary>
    private static void PaintPips(Image[] pips, int left)
    {
        for (int i = 0; i < pips.Length; i++)
        {
            var c = pips[i].color;
            // L'alpha seul : la teinte porte l'identité du filet, l'intensité porte sa disponibilité.
            pips[i].color = new Color(c.r, c.g, c.b, i < left ? 1f : 0.18f);
        }
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

        // Le drapeau force l'affichage sans écrire dans les réglages : il sert à mesurer, pas à
        // configurer. C'est le seul instrument utilisable en web, où rien ne peut être injecté de
        // l'extérieur tant que le canevas tourne.
        if (!DebugHooks.ShowFps && !GameSettings.Current.ShowFps) { _fpsLabel.text = ""; return; }

        _fpsAccumulator += Time.unscaledDeltaTime;
        _fpsFrames++;

        if (_fpsAccumulator < 0.5f) return;

        _fpsLabel.text = Loc.T("HUD_FPS", (_fpsFrames / _fpsAccumulator).ToString("F0"));
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
        if (player == null || !player.DashEnabled)
        {
            _dashLabel.text = "";
            if (_dashGauge != null) _dashGauge.SetActive(false);
            return;
        }

        float ratio = player.DashReadyRatio;
        bool ready = ratio >= 1f;

        // ⚠ Le libellé passait en couleur de FOND pendant la recharge — c'est-à-dire qu'il
        // DISPARAISSAIT au moment précis où il a quelque chose à dire. Il reste donc toujours
        // lisible ; c'est la jauge, et non le texte, qui porte l'état.
        _dashLabel.text = Loc.T("HUD_DASH_HINT", InputRemap.DisplayName(GameAction.Dash));
        _dashLabel.color = ready ? Cyan : DimCyan;

        if (_dashGauge != null) _dashGauge.SetActive(true);

        if (_dashFill != null)
        {
            _dashFill.fillAmount = ratio;

            // Deux couleurs franches plutôt qu'un dégradé : la seule question posée à cette jauge
            // est binaire — « puis-je esquiver maintenant ? ». Le remplissage répond à la seconde,
            // « dans combien de temps ».
            _dashFill.color = ready ? Cyan : DimCyan;
        }
    }

    /// <summary>
    /// Régénération continue et sa <b>réserve</b> — les deux moitiés d'une même règle.
    /// </summary>
    /// <remarks>
    /// <para>Rien n'affichait la régénération : le modèle est pourtant porté en entier (débit,
    /// réserve anti-pic, suspension de 4 s sous le feu). Sous Godot, le même manque avait produit un
    /// retour de testeur — l'Auto-réparation était crue <i>active</i> alors qu'elle ne se voyait
    /// nulle part. Et la suspension aggrave le cas : sans affichage, un joueur touché voit sa
    /// régénération s'arrêter <b>sans raison visible</b> et lit une carte cassée, pas une règle.</para>
    ///
    /// <para>Sous le feu, le débit cède la place au <b>temps restant</b> : l'information actionnable
    /// est « dans combien de temps », pas « combien ».</para>
    /// </remarks>
    private void UpdateRegen()
    {
        var player = Player.Instance;
        if (player == null) return;

        var stats = player.Stats;
        float perSecond = stats.HpRegenPerSecond;
        bool suppressed = RegenReserve.IsSuppressed(stats.RegenSuppressLeft);

        if (_regenLabel != null)
        {
            if (perSecond <= 0.01f)
            {
                _regenLabel.text = "";
            }
            else if (suppressed)
            {
                // Glyphes limités à ce que porte Share Tech Mono.
                _regenLabel.text = $"♥ {stats.RegenSuppressLeft:0.0}s";
                _regenLabel.color = Dim;
            }
            else
            {
                _regenLabel.text = $"♥ +{perSecond:0.0}/s";
                _regenLabel.color = HealthGreen;
            }
        }

        // La réserve n'existe pas sans source : un liseré vide en permanence ne serait que du bruit.
        float capacity = RegenReserve.Capacity(perSecond, stats.MaxHp);
        bool show = capacity > 0.01f;

        if (_reserveBar != null && _reserveBar.activeSelf != show) _reserveBar.SetActive(show);
        if (!show || _reserveFill == null) return;

        _reserveFill.fillAmount = Mathf.Clamp01(stats.RegenReserveCharge / capacity);

        // Grisé tant que la régénération est coupée : un tampon figé sans explication se lirait
        // comme un bug.
        _reserveFill.color = suppressed ? Dim : Cyan;
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
            _bossLabel.text = $"{boss.DisplayName}   " +
                              $"{Loc.T("BOSS_PHASE", BossPhases.RomanNumeral(boss.Phase))}   " +
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

        // La jauge SOUS le libellé, sur la même largeur : une recharge se lit d'un coup d'œil
        // périphérique, ce qu'un pourcentage écrit ne permet pas — en plein combat, personne ne lit
        // un nombre à deux chiffres en bas de l'écran.
        _dashGauge = new GameObject("DashGauge", typeof(RectTransform));
        _dashGauge.transform.SetParent(canvasGo.transform, false);
        Place(_dashGauge, new Vector2(1f, 0f), new Vector2(-320f, 36f), new Vector2(300f, 8f));

        _dashFill = BuildBar(_dashGauge.transform, "Dash", new Vector2(0f, 1f),
                             Vector2.zero, new Vector2(300f, 8f), Cyan);

        _fpsLabel = BuildLabel(canvasGo.transform, "Fps", new Vector2(1f, 1f),
                               new Vector2(-140f, -24f), new Vector2(120f, 26f), OffWhite, TextAnchor.UpperRight);
        _fpsLabel.fontSize = 16;

        BuildBossPanel(canvasGo.transform);

        _bannerLabel = BuildLabel(canvasGo.transform, "Banner", new Vector2(0.5f, 0.5f),
                                  new Vector2(-400f, 170f), new Vector2(800f, 88f), Gold, TextAnchor.MiddleCenter);
        _bannerLabel.fontSize = 30;

        // ⚠ Deux lignes possibles (la complétion du niveau en tient deux) : sans ce débordement
        // vertical, uGUI TRONQUE la seconde sans le moindre signe — et c'est celle qui dit au joueur
        // ce qui se passe ensuite.
        _bannerLabel.verticalOverflow = VerticalWrapMode.Overflow;
        _bannerLabel.lineSpacing = 1.1f;
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
        // ⚠ Hauteur calculée, pas devinée. Le cadre « plaque blindée » porte une bordure de 16 px
        // en 9-slice : une hauteur ajustée au contenu fait chevaucher la dernière rangée — ici les
        // emplacements de greffes — sur le liseré du panneau.
        rect.sizeDelta = new Vector2(VitalsWidth, SlotsTop + SlotSize + VitalsPadding);
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

        // Liseré de réserve COLLÉ sous la barre de vie : la réserve absorbe le prochain coup, elle
        // appartient donc aux points de vie et non à une ligne d'état séparée.
        _reserveBar = new GameObject("ReserveBar", typeof(RectTransform));
        _reserveBar.transform.SetParent(panel.transform, false);
        Place(_reserveBar, new Vector2(0f, 1f), new Vector2(22f, -75f), new Vector2(VitalsWidth - 44f, 5f));

        _reserveFill = BuildBar(_reserveBar.transform, "Reserve", new Vector2(0f, 1f),
                                Vector2.zero, new Vector2(VitalsWidth - 44f, 5f), Cyan);
        _reserveBar.SetActive(false);

        _xpFill = BuildBar(panel.transform, "XpBar", new Vector2(0f, 1f),
                           new Vector2(22f, -84f), new Vector2(VitalsWidth - 44f, 10f), Cyan);

        // Le débit de régénération, sur sa propre ligne sous l'XP : c'est une valeur qui ne bouge
        // qu'à la prise d'une carte, sauf pendant les 4 s de suspension — où elle devient la seule
        // information utile de tout le bloc.
        _regenLabel = BuildLabel(panel.transform, "Regen", new Vector2(0f, 1f),
                                 new Vector2(22f, -96f), new Vector2(200f, 20f), HealthGreen,
                                 TextAnchor.UpperLeft);
        _regenLabel.fontSize = 16;

        // Rangée des filets de survie, entre la régénération et les greffes : une pastille par charge
        // achetée. Vide (donc invisible) si le joueur n'a rien acheté, ou si le cran IV les coupe.
        var safety = new GameObject("SafetyNets", typeof(RectTransform));
        safety.transform.SetParent(panel.transform, false);
        Place(safety, new Vector2(0f, 1f), new Vector2(22f, -SafetyTop), new Vector2(VitalsWidth - 44f, 14f));

        var safetyLayout = safety.AddComponent<HorizontalLayoutGroup>();
        safetyLayout.spacing = 4f;
        safetyLayout.childAlignment = TextAnchor.LowerLeft;
        safetyLayout.childForceExpandWidth = false;
        safetyLayout.childForceExpandHeight = false;
        safetyLayout.childControlWidth = false;
        safetyLayout.childControlHeight = false;

        _safetyRow = safety.transform;
        safety.SetActive(false);

        var slots = new GameObject("GraftSlots", typeof(RectTransform));
        slots.transform.SetParent(panel.transform, false);
        Place(slots, new Vector2(0f, 1f), new Vector2(22f, -SlotsTop), new Vector2(VitalsWidth - 44f, SlotSize));

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

    /// <summary>Ordonnée du haut de la rangée d'emplacements, depuis le haut du panneau.</summary>
    /// <remarks>
    /// Relevée de 98 à 120 pour loger la ligne de régénération sous la barre d'XP, puis à 142 pour la
    /// rangée des filets de survie. La hauteur du panneau en dérive
    /// (<c>SlotsTop + SlotSize + VitalsPadding</c>) : ajouter une rangée ne se paie donc qu'ici, et
    /// jamais en retouchant une hauteur devinée.
    /// </remarks>
    private const float SlotsTop = 142f;

    /// <summary>Ordonnée du haut de la rangée des filets de survie.</summary>
    private const float SafetyTop = 118f;

    /// <summary>Marge sous la dernière rangée — la bordure 9-slice du cadre, plus une respiration.</summary>
    private const float VitalsPadding = 22f;

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
