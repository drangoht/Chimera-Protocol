using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cinématique d'ouverture — six plans, puis le titre. <b>Portage plan par plan</b> d'<c>IntroScreen</c>.
///
/// <para><b>Elle porte le seul récit du jeu.</b> Chimera Protocol n'a ni dialogue ni codex narratif
/// en cours de partie : ces six lignes sont le seul endroit où l'on apprend ce qu'est la Convergence,
/// pourquoi la Rouille intègre au lieu de détruire, et ce que le joueur vient faire là.</para>
///
/// <para><b>Avec les vrais sprites du jeu</b>, comme sous Godot : le Noyau d'Aether, un drone qui se
/// corrompt, la nuée et le colosse, l'Arpenteur qui descend, et l'assimilation elle-même. Une
/// première version abstraite (des particules seules) disait le rythme mais pas le <i>sujet</i> —
/// or le dernier plan EST le pitch du jeu : on tue, on arrache, on devient.</para>
///
/// <para>Les plans vivent dans le <b>monde</b> et non dans le canevas : positions en pixels
/// (1 px = 1 unité comme tout le projet) et zoom lent par <c>orthographicSize</c>, ce qui traduit
/// directement les <c>Node2D</c> et le zoom de caméra de l'original. Seuls le texte, le voile et le
/// titre restent en interface.</para>
///
/// <para>⚠ <b>Toujours interruptible</b>, et le dire : une intro de vingt-cinq secondes qu'on ne peut
/// pas passer devient une punition dès la deuxième partie.</para>
/// </summary>
public sealed class IntroScreen : MonoBehaviour
{
    // ─── Assets, aux mêmes identifiants que sous Godot ────────────────────────
    private const string PlayerFrames   = "player";
    private const string DroneFrames    = "drone";
    private const string ColossusFrames = "colossus";
    private const string SwarmFrames    = "rustswarm";
    /// <summary>
    /// ⚠ Le Noyau vient de <c>Vfx/</c> et non de <c>Ui/</c>, alors que c'est la <b>même image</b>.
    ///
    /// <para>Les sprites d'interface sont importés à 100 px par unité, ceux du monde à 1. Chargée
    /// depuis <c>Ui/</c>, l'icône de 32 px mesurait 0,32 unité — soit, à l'échelle 5,5 du plan,
    /// <b>moins de deux pixels</b> à l'écran. Le Noyau d'Aether, sujet de deux plans sur six, était
    /// donc invisible, et l'on ne voyait que ses particules.</para>
    /// </summary>
    private const string NoyauIcon      = "Vfx/intro_noyau";
    private const string NoyauParticle  = "Vfx/vfx_particle_noyau";
    private const string FusionAura     = "Vfx/vfx_aura_fusionblade";

    private static readonly Color Cyan   = new(0.267f, 1f, 0.933f);
    private static readonly Color Violet = new(0.667f, 0.267f, 1f);
    private static readonly Color Rust   = new(0.85f, 0.42f, 0.18f);

    /// <summary>Durées de maintien des six plans — celles de la séquence d'origine.</summary>
    private static readonly float[] Holds = { 3.4f, 3.4f, 3.6f, 3.4f, 3.8f, 4.0f };

    private static readonly string[] BeatKeys =
    {
        "INTRO_BEAT_1", "INTRO_BEAT_2", "INTRO_BEAT_3",
        "INTRO_BEAT_4", "INTRO_BEAT_5", "INTRO_BEAT_6",
    };

    /// <summary>Demi-hauteur de caméra de référence — 720 p de haut, comme le jeu d'origine.</summary>
    private const float BaseZoom = 360f;

    /// <summary>Plan courant — observable pour les vérifications.</summary>
    public int ShotIndex { get; private set; } = -1;

    /// <summary>L'intro est-elle terminée ou passée ?</summary>
    public bool Finished { get; private set; }

    private Image? _fade;
    private Image? _flash;
    private Text? _line;
    private Text? _title;
    private Text? _tagline;
    private Text? _skipHint;
    private Transform? _stage;
    private Camera? _camera;

    private void Start()
    {
        _camera = Camera.main;
        if (_camera != null) _camera.orthographicSize = BaseZoom;

        BuildUi();

        // La piste d'intro n'est pas celle du menu : elle s'arrête avec la cinématique.
        MusicDirector.Instance?.PlaySingle("music_intro");

        StartCoroutine(Play());
    }

    private void Update()
    {
        _elapsed += Time.unscaledDeltaTime;

        // ⚠ Garde de départ. Une touche encore enfoncée au lancement — ou l'activation de la fenêtre
        // par le système — passe la cinématique avant qu'elle n'ait affiché une image, et le joueur
        // ne comprend pas ce qu'il vient de rater. Une demi-seconde suffit à l'éviter sans rendre le
        // saut moins immédiat.
        if (_elapsed < SkipGuard) return;

        // Ensuite : n'importe quelle touche, n'importe quel clic. Un joueur qui veut passer ne doit
        // pas chercher LA touche.
        if (!Finished && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) Skip();
    }

    /// <summary>Délai avant qu'une entrée puisse passer la cinématique, en secondes.</summary>
    private const float SkipGuard = 0.6f;

    private float _elapsed;

    /// <summary>Interrompt la cinématique et passe au menu.</summary>
    public void Skip()
    {
        if (Finished) return;
        Finished = true;

        StopAllCoroutines();
        SceneRoot.ChangeScene(GameScenes.MainMenu);
    }

    private IEnumerator Play()
    {
        yield return FadeImage(_fade, 1f, 0f, 1f);   // ouverture sur le noir

        for (int i = 0; i < BeatKeys.Length; i++)
        {
            ShotIndex = i;

            ClearStage();
            if (_line != null) _line.text = Loc.T(BeatKeys[i]);

            Debug.Log($"[Intro] plan {i + 1}/{BeatKeys.Length} a t = {_elapsed:F1} s");

            StartCoroutine(RunShot(i));

            yield return FadeText(_line, 0f, 1f, 0.7f);
            yield return new WaitForSecondsRealtime(Holds[i]);
            yield return FadeText(_line, 1f, 0f, 0.4f);
        }

        yield return RevealTitle();

        Finished = true;
        SceneRoot.ChangeScene(GameScenes.MainMenu);
    }

    /// <summary>Lance le plan demandé. Les six correspondent aux <c>Shot*</c> du jeu d'origine.</summary>
    private IEnumerator RunShot(int index) => index switch
    {
        0 => ShotConvergenceCore(),
        1 => ShotDroneCorruption(),
        2 => ShotRustSwarm(),
        3 => ShotSanctuaryCore(),
        4 => ShotArpenteurDescent(),
        _ => ShotAssimilation(),
    };

    // ─── Plans ────────────────────────────────────────────────────────────────

    /// <summary>Plan 1 — le Noyau d'Aether pulse dans le noir. Zoom lent avant.</summary>
    private IEnumerator ShotConvergenceCore()
    {
        var core = MakeSprite(NoyauIcon, Vector2.zero, 5.5f, Violet);

        StartCoroutine(Pulse(core, 5.5f, 6.2f, 1.3f));
        StartCoroutine(Emit(new Vector2(0f, -40f), Violet, 42, 55f, 25f, Vector2.up, 2.6f, 6f));
        StartCoroutine(SlowZoom(1f, 1.12f, 6.5f));

        yield break;
    }

    /// <summary>Plan 2 — un drone dérive, sa couleur vire à la rouille : la fusion.</summary>
    private IEnumerator ShotDroneCorruption()
    {
        var drone = MakeAnimated(DroneFrames, "move", At(360f, 300f), 4.5f, new Color(0.6f, 0.85f, 1f));

        StartCoroutine(MoveTo(drone, At(760f, 340f), 5f));
        StartCoroutine(Emit(At(560f, 320f), Violet, 24, 40f, 180f, Vector2.up, 1.8f, 6f));
        StartCoroutine(SlowZoom(1.05f, 1f, 6.5f));

        // Corruption progressive de la teinte : bleu machine → rouille.
        yield return new WaitForSecondsRealtime(1.4f);
        yield return TintTo(drone, new Color(0.6f, 0.85f, 1f), Rust, 1.6f);

        if (drone != null)
            StartCoroutine(Emit(drone.transform.position, Rust, 30, 70f, 180f, Vector2.right, 2f, 2f));
    }

    /// <summary>Plan 3 — la Rouille déferle : la nuée traverse, le colosse se dresse.</summary>
    private IEnumerator ShotRustSwarm()
    {
        var colossus = MakeAnimated(ColossusFrames, "move", At(880f, 380f), 3.4f,
                                    new Color(Rust.r, Rust.g, Rust.b, 0f));

        StartCoroutine(FadeSprite(colossus, 0f, 1f, 1.4f));
        StartCoroutine(MoveTo(colossus, At(880f, 340f), 2.5f));

        for (int i = 0; i < 7; i++)
        {
            var start = At(-60f - i * 40f, 250f + _rng.Next(-90, 160));
            var swarm = MakeAnimated(SwarmFrames, "move", start, 2.6f, new Color(0.9f, 0.55f, 0.35f));

            var target = At(700f + _rng.Next(-60, 120), 360f + _rng.Next(-100, 100));
            StartCoroutine(MoveTo(swarm, target, 3.2f + (float)_rng.NextDouble()));
        }

        StartCoroutine(Emit(At(300f, 360f), Rust, 60, 120f, 40f, Vector2.right, 1.6f, 7f));
        StartCoroutine(SlowZoom(1.12f, 1f, 7f));

        yield break;
    }

    /// <summary>Plan 4 — un Noyau pur luit dans le calme retrouvé : l'espoir.</summary>
    private IEnumerator ShotSanctuaryCore()
    {
        var core = MakeSprite(NoyauIcon, new Vector2(0f, 20f), 5f, Cyan);

        StartCoroutine(PulseTint(core, Cyan, new Color(0.6f, 1f, 0.95f), 1.1f));
        StartCoroutine(Emit(new Vector2(0f, -60f), Cyan, 50, 45f, 30f, Vector2.up, 2.2f, 6f));
        StartCoroutine(SlowZoom(1f, 1.08f, 6.5f));

        yield break;
    }

    /// <summary>Plan 5 — « Ce sera toi » : l'Arpenteur avance, l'aura de fusion s'allume.</summary>
    private IEnumerator ShotArpenteurDescent()
    {
        var aura = MakeSprite(FusionAura, new Vector2(0f, -30f), 0.5f, new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
        var player = MakeAnimated(PlayerFrames, "run_down", At(640f, 220f), 2.6f, Color.white);

        StartCoroutine(MoveTo(player, new Vector2(0f, -40f), 3.4f));
        StartCoroutine(ScaleTo(player, 2.6f, 5.5f, 3.4f));
        StartCoroutine(Emit(new Vector2(0f, -120f), Cyan, 40, 60f, 60f, Vector2.up, 1.8f, 7f));
        StartCoroutine(SlowZoom(1f, 1.15f, 7f));

        yield return new WaitForSecondsRealtime(1.6f);

        StartCoroutine(FadeSprite(aura, 0f, 0.9f, 1.2f));
        yield return ScaleTo(aura, 0.5f, 7f, 1.8f);
    }

    /// <summary>
    /// Plan 6 — l'assimilation, <b>le pitch du jeu</b> : mise à mort, arrachement d'un fragment,
    /// mutation. C'est le plan qui justifie tous les autres, et il se joue en trois temps précis.
    /// </summary>
    private IEnumerator ShotAssimilation()
    {
        var playerPos = new Vector2(0f, -40f);
        var player = MakeAnimated(PlayerFrames, "idle", playerPos, 5.5f, Color.white);

        // L'ennemi source est l'Essaim de Rouille : le premier croisé, et celui de la première greffe.
        var enemyStart = playerPos + new Vector2(300f, 95f);
        var enemyStop = playerPos + new Vector2(95f, 46f);
        var enemy = MakeAnimated(SwarmFrames, "move", enemyStart, 2.9f, Rust);

        Vector2 pull = (playerPos - enemyStop).normalized;

        StartCoroutine(SlowZoom(1f, 1.1f, 4f));

        // 1) Mise à mort : l'ennemi fond sur le joueur et s'arrête NET — pas de ralenti.
        yield return MoveTo(enemy, enemyStop, 1f);
        yield return TintTo(enemy, Rust, new Color(1.6f, 1.35f, 1.15f), 0.08f);
        yield return TintTo(enemy, new Color(1.6f, 1.35f, 1.15f), Rust, 0.12f);

        // 2) Arrachement : l'ennemi se désagrège, un fragment file vers le joueur en virant de la
        //    rouille vers le cyan — la couleur de ce que le joueur devient.
        StartCoroutine(FadeSprite(enemy, 1f, 0f, 0.6f));
        StartCoroutine(Emit(enemyStop, Rust, 26, 150f, 20f, pull, 1.8f, 1.2f));

        yield return new WaitForSecondsRealtime(0.35f);
        StartCoroutine(Emit(enemyStop + pull * 45f, Cyan, 22, 150f, 16f, pull, 1.6f, 1.2f));

        // 3) Mutation : le fragment atteint le joueur — l'aura éclôt, et sa teinte change.
        yield return new WaitForSecondsRealtime(0.85f);

        var aura = MakeSprite(FusionAura, playerPos, 0.5f, new Color(Cyan.r, Cyan.g, Cyan.b, 0f));
        StartCoroutine(ScaleTo(aura, 0.5f, 4f, 0.3f));
        StartCoroutine(FadeSprite(aura, 0f, 0.8f, 0.3f));
        StartCoroutine(TintTo(player, Color.white, new Color(0.75f, 0.85f, 0.85f), 0.5f));

        yield return new WaitForSecondsRealtime(0.3f);
        yield return FadeSprite(aura, 0.8f, 0f, 1.2f);
    }

    /// <summary>Flash blanc, puis le titre et sa promesse — la phrase à retenir.</summary>
    private IEnumerator RevealTitle()
    {
        ClearStage();

        yield return FadeImage(_flash, 0f, 0.85f, 0.2f);
        yield return FadeImage(_flash, 0.85f, 0f, 0.6f);

        if (_title != null) _title.gameObject.SetActive(true);
        if (_tagline != null) _tagline.gameObject.SetActive(true);
        if (_skipHint != null) _skipHint.gameObject.SetActive(false);

        yield return FadeText(_title, 0f, 1f, 0.8f);
        yield return FadeText(_tagline, 0f, 1f, 0.6f);
        yield return new WaitForSecondsRealtime(2.6f);
    }

    // ─── Fabriques ────────────────────────────────────────────────────────────

    /// <summary>
    /// Convertit une position de l'original — origine en haut à gauche, 1280 × 720 — en coordonnées
    /// de monde Unity, centrées et l'axe Y vers le haut. Les valeurs des plans restent ainsi
    /// <b>identiques</b> à celles du jeu publié, ce qui rend la comparaison possible.
    /// </summary>
    private static Vector2 At(float godotX, float godotY) => new(godotX - 640f, 360f - godotY);

    private SpriteRenderer? MakeSprite(string resourcePath, Vector2 position, float scale, Color tint)
    {
        var sprite = Resources.Load<Sprite>(resourcePath);
        if (sprite == null)
        {
            Debug.LogWarning($"[Intro] sprite introuvable : {resourcePath}");
            return null;
        }

        var go = new GameObject("Shot_" + resourcePath, typeof(SpriteRenderer));
        go.transform.SetParent(_stage, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * scale;

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = tint;
        renderer.sortingOrder = 10;

        return renderer;
    }

    /// <summary>
    /// Sprite <b>animé</b> — les jeux d'images du jeu, joués en boucle. Repli sur « idle » puis sur
    /// la première animation disponible, comme l'original : un plan sans sprite vaut mieux qu'un plan
    /// qui n'existe pas, mais il ne doit jamais rester vide sans raison.
    /// </summary>
    private SpriteRenderer? MakeAnimated(string framesId, string animation, Vector2 position,
                                         float scale, Color tint)
    {
        var frames = SpriteFramesLibrary.Get(framesId);
        if (frames == null)
        {
            Debug.LogWarning($"[Intro] jeu d'animations introuvable : {framesId}");
            return null;
        }

        var clip = frames.Find(animation) ?? frames.Find("idle")
                ?? (frames.Animations.Length > 0 ? frames.Animations[0] : null);

        if (clip == null || clip.Frames.Length == 0) return null;

        var go = new GameObject("Shot_" + framesId, typeof(SpriteRenderer));
        go.transform.SetParent(_stage, false);
        go.transform.localPosition = position;
        go.transform.localScale = Vector3.one * scale;

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = clip.Frames[0];
        renderer.color = tint;
        renderer.sortingOrder = 10;

        StartCoroutine(PlayFrames(renderer, clip));
        return renderer;
    }

    private IEnumerator PlayFrames(SpriteRenderer renderer, SpriteFramesAsset.Animation clip)
    {
        float step = clip.Speed > 0f ? 1f / clip.Speed : 0.125f;

        for (int i = 0; renderer != null; i++)
        {
            renderer.sprite = clip.Frames[i % clip.Frames.Length];
            yield return new WaitForSecondsRealtime(step);
        }
    }

    /// <summary>
    /// Gerbe de particules — <c>CpuParticles2D</c> de l'original, rendu par des sprites qui dérivent
    /// et s'éteignent. Les valeurs (nombre, vitesse, dispersion, échelle) sont celles des plans.
    /// </summary>
    private IEnumerator Emit(Vector2 origin, Color color, int amount, float velocity, float spreadDeg,
                             Vector2 direction, float scale, float duration)
    {
        var sprite = Resources.Load<Sprite>(NoyauParticle);
        if (sprite == null) yield break;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            for (int i = 0; i < Mathf.Max(1, amount / 12); i++)
                StartCoroutine(OneParticle(sprite, origin, color, velocity, spreadDeg, direction, scale));

            elapsed += 0.18f;
            yield return new WaitForSecondsRealtime(0.18f);
        }
    }

    private IEnumerator OneParticle(Sprite sprite, Vector2 origin, Color color, float velocity,
                                    float spreadDeg, Vector2 direction, float scale)
    {
        var go = new GameObject("Particle", typeof(SpriteRenderer));
        go.transform.SetParent(_stage, false);
        go.transform.localPosition = origin;

        float size = scale * (0.6f + 0.4f * (float)_rng.NextDouble());
        go.transform.localScale = Vector3.one * size;

        var renderer = go.GetComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 8;

        float angle = ((float)_rng.NextDouble() - 0.5f) * spreadDeg * Mathf.Deg2Rad;
        float cos = Mathf.Cos(angle), sin = Mathf.Sin(angle);
        var dir = new Vector2(direction.x * cos - direction.y * sin, direction.x * sin + direction.y * cos);

        float speed = velocity * (0.6f + 0.4f * (float)_rng.NextDouble());
        const float Life = 2.2f;

        for (float t = 0f; t < Life && go != null; t += Time.unscaledDeltaTime)
        {
            go.transform.localPosition += (Vector3)(dir * speed * Time.unscaledDeltaTime);
            renderer.color = new Color(color.r, color.g, color.b, 1f - t / Life);
            yield return null;
        }

        if (go != null) Destroy(go);
    }

    // ─── Animations élémentaires ──────────────────────────────────────────────

    private IEnumerator MoveTo(SpriteRenderer? target, Vector2 destination, float seconds)
    {
        if (target == null) yield break;

        Vector3 from = target.transform.localPosition;

        for (float t = 0f; t < seconds && target != null; t += Time.unscaledDeltaTime)
        {
            // Sinus adouci aux deux bouts : le mouvement de l'original, jamais linéaire.
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            target.transform.localPosition = Vector3.Lerp(from, destination, k);
            yield return null;
        }
    }

    private IEnumerator ScaleTo(SpriteRenderer? target, float from, float to, float seconds)
    {
        if (target == null) yield break;

        for (float t = 0f; t < seconds && target != null; t += Time.unscaledDeltaTime)
        {
            float k = Mathf.SmoothStep(0f, 1f, t / seconds);
            target.transform.localScale = Vector3.one * Mathf.Lerp(from, to, k);
            yield return null;
        }
    }

    private IEnumerator TintTo(SpriteRenderer? target, Color from, Color to, float seconds)
    {
        if (target == null) yield break;

        for (float t = 0f; t < seconds && target != null; t += Time.unscaledDeltaTime)
        {
            target.color = Color.Lerp(from, to, t / seconds);
            yield return null;
        }

        if (target != null) target.color = to;
    }

    private IEnumerator FadeSprite(SpriteRenderer? target, float from, float to, float seconds)
    {
        if (target == null) yield break;

        for (float t = 0f; t < seconds && target != null; t += Time.unscaledDeltaTime)
        {
            var c = target.color;
            target.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }
    }

    private IEnumerator Pulse(SpriteRenderer? target, float low, float high, float halfPeriod)
    {
        while (target != null)
        {
            yield return ScaleTo(target, low, high, halfPeriod);
            yield return ScaleTo(target, high, low, halfPeriod);
        }
    }

    private IEnumerator PulseTint(SpriteRenderer? target, Color a, Color b, float halfPeriod)
    {
        while (target != null)
        {
            yield return TintTo(target, a, b, halfPeriod);
            yield return TintTo(target, b, a, halfPeriod);
        }
    }

    /// <summary>Zoom lent de la caméra — le <c>SlowZoom</c> de l'original, en orthographique.</summary>
    private IEnumerator SlowZoom(float from, float to, float seconds)
    {
        if (_camera == null) yield break;

        for (float t = 0f; t < seconds && _camera != null; t += Time.unscaledDeltaTime)
        {
            // Un zoom AVANT réduit la demi-hauteur : le facteur est donc au dénominateur.
            float zoom = Mathf.Lerp(from, to, t / seconds);
            _camera.orthographicSize = BaseZoom / zoom;
            yield return null;
        }
    }

    private static IEnumerator FadeImage(Image? image, float from, float to, float seconds)
    {
        if (image == null) yield break;

        var rgb = image.color;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            image.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }

        image.color = new Color(rgb.r, rgb.g, rgb.b, to);
    }

    private static IEnumerator FadeText(Text? text, float from, float to, float seconds)
    {
        if (text == null) yield break;

        var rgb = text.color;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            text.color = new Color(rgb.r, rgb.g, rgb.b, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }

        text.color = new Color(rgb.r, rgb.g, rgb.b, to);
    }

    /// <summary>Vide le plateau entre deux plans — l'équivalent du <c>RebuildStage</c> d'origine.</summary>
    private void ClearStage()
    {
        if (_stage == null) return;

        for (int i = _stage.childCount - 1; i >= 0; i--)
            Destroy(_stage.GetChild(i).gameObject);
    }

    /// <summary>
    /// ⚠ Générateur PRIVÉ, jamais <c>UnityEngine.Random</c> : l'intro ne doit pas décaler les
    /// tirages d'une campagne à graine fixe.
    /// </summary>
    private readonly System.Random _rng = new(0x1470);

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var stageGo = new GameObject("Stage");
        stageGo.transform.SetParent(transform, false);
        _stage = stageGo.transform;

        var canvasGo = new GameObject("IntroCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 200);

        _line = UiStyle.Label(canvasGo.transform, "", 26, UiPalette.OffWhite, TextAnchor.UpperCenter);
        var lineRect = _line.GetComponent<RectTransform>();
        lineRect.anchorMin = lineRect.anchorMax = new Vector2(0.5f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(1400f, 120f);
        lineRect.anchoredPosition = new Vector2(0f, 150f);
        _line.color = new Color(0.90f, 0.92f, 1f, 0f);

        _title = UiStyle.Label(canvasGo.transform, Loc.T("INTRO_TITLE"), 76,
                               UiPalette.Violet, TextAnchor.MiddleCenter);
        Center(_title, new Vector2(1400f, 110f), 40f);
        _title.gameObject.SetActive(false);

        _tagline = UiStyle.Label(canvasGo.transform, Loc.T("INTRO_TAGLINE"), 28,
                                 UiPalette.Cyan, TextAnchor.MiddleCenter);
        Center(_tagline, new Vector2(1400f, 60f), -60f);
        _tagline.gameObject.SetActive(false);

        // ⚠ Affiché en PERMANENCE : une intro qu'on ne sait pas passer se subit.
        _skipHint = UiStyle.Label(canvasGo.transform, "— " + Loc.T("INTRO_SKIP") + " —", 18,
                                  UiPalette.Dim, TextAnchor.LowerCenter);
        var hintRect = _skipHint.GetComponent<RectTransform>();
        hintRect.anchorMin = hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(900f, 30f);
        hintRect.anchoredPosition = new Vector2(0f, 40f);

        // Flash puis voile : le premier sert au titre, le second à l'ouverture et à la fermeture.
        var flashGo = UiStyle.NewUiObject("Flash", canvasGo.transform);
        _flash = flashGo.AddComponent<Image>();
        _flash.color = new Color(1f, 1f, 1f, 0f);
        _flash.raycastTarget = false;
        UiStyle.Stretch(flashGo, 0f);

        var fadeGo = UiStyle.NewUiObject("Fade", canvasGo.transform);
        _fade = fadeGo.AddComponent<Image>();
        _fade.color = Color.black;
        _fade.raycastTarget = false;
        UiStyle.Stretch(fadeGo, 0f);
    }

    private static void Center(Text text, Vector2 size, float y)
    {
        var rect = text.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = new Vector2(0f, y);
    }
}
