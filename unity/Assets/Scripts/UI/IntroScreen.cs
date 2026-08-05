using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Cinématique d'ouverture — six temps de narration, puis le titre (port d'<c>IntroScreen</c>).
///
/// <para><b>Elle porte le seul récit du jeu.</b> Chimera Protocol n'a ni dialogue ni codex narratif
/// en cours de partie : ces six lignes sont le seul endroit où l'on apprend ce qu'est la Convergence,
/// pourquoi la Rouille intègre au lieu de détruire, et ce que le joueur vient faire là. Sans elles,
/// le jeu s'ouvre sur un menu et l'on tire sur des formes sans savoir sur quoi.</para>
///
/// <para><b>Visuels procéduraux</b>, comme sous Godot : chaque plan est une figure abstraite —
/// convergence, corruption, déferlement, garde, descente, arrachement. Elles ne représentent rien
/// littéralement ; elles donnent un mouvement à lire pendant qu'on lit le texte.</para>
///
/// <para>⚠ <b>Toujours interruptible</b>, et le dire. Une intro de vingt-cinq secondes qu'on ne peut
/// pas passer devient une punition dès la deuxième partie — l'indication est affichée en
/// permanence, et n'importe quelle touche ou clic suffit.</para>
/// </summary>
public sealed class IntroScreen : MonoBehaviour
{
    /// <summary>Durée d'un plan, en secondes — le temps de lire deux lignes sans se presser.</summary>
    private const float ShotHold = 3.6f;

    /// <summary>Durée des fondus entre plans.</summary>
    private const float FadeTime = 0.6f;

    /// <summary>Temps d'affichage du titre avant le menu.</summary>
    private const float TitleHold = 2.6f;

    /// <summary>Plans, dans l'ordre : clé de texte, teinte, figure.</summary>
    private static readonly (string Key, Color Tint, Figure Shape)[] Shots =
    {
        ("INTRO_BEAT_1", new Color(0.45f, 0.85f, 1.00f), Figure.Converge),
        ("INTRO_BEAT_2", new Color(0.85f, 0.45f, 1.00f), Figure.Corrupt),
        ("INTRO_BEAT_3", new Color(1.00f, 0.55f, 0.30f), Figure.Swarm),
        ("INTRO_BEAT_4", new Color(0.40f, 1.00f, 0.85f), Figure.Guard),
        ("INTRO_BEAT_5", new Color(0.70f, 0.75f, 0.95f), Figure.Descend),
        ("INTRO_BEAT_6", new Color(0.95f, 0.35f, 0.75f), Figure.Graft),
    };

    private enum Figure { Converge, Corrupt, Swarm, Guard, Descend, Graft }

    /// <summary>Plan courant — observable pour les vérifications.</summary>
    public int ShotIndex { get; private set; } = -1;

    /// <summary>L'intro est-elle terminée ou passée ?</summary>
    public bool Finished { get; private set; }

    private Image? _fade;
    private Text? _line;
    private Text? _title;
    private Text? _tagline;
    private Text? _skipHint;
    private RectTransform? _stage;
    private readonly System.Collections.Generic.List<RectTransform> _motes = new();
    private Figure _figure = Figure.Converge;
    private float _shotTime;

    private void Start()
    {
        BuildUi();

        // La piste d'intro n'est pas celle du menu : elle s'arrête avec la cinématique.
        MusicDirector.Instance?.PlaySingle("music_intro");

        StartCoroutine(Play());
    }

    private void Update()
    {
        _shotTime += Time.unscaledDeltaTime;
        AnimateFigure();

        // N'importe quelle touche, n'importe quel clic. Un joueur qui veut passer ne doit pas
        // chercher LA touche : il en presse une, au hasard, et ça doit marcher.
        if (!Finished && (Input.anyKeyDown || Input.GetMouseButtonDown(0))) Skip();
    }

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
        yield return Fade(1f, 0f, 1f);   // ouverture sur le noir

        for (int i = 0; i < Shots.Length; i++)
        {
            ShotIndex = i;
            BeginShot(Shots[i]);

            yield return Alpha(_line, 0f, 1f, FadeTime);
            yield return new WaitForSecondsRealtime(ShotHold);
            yield return Alpha(_line, 1f, 0f, FadeTime * 0.7f);
        }

        yield return RevealTitle();

        Finished = true;
        SceneRoot.ChangeScene(GameScenes.MainMenu);
    }

    private void BeginShot((string Key, Color Tint, Figure Shape) shot)
    {
        _figure = shot.Shape;
        _shotTime = 0f;

        if (_line != null)
        {
            _line.text = Loc.T(shot.Key);
            _line.color = new Color(0.90f, 0.92f, 1f, 0f);
        }

        BuildMotes(shot.Tint);
    }

    /// <summary>
    /// Le titre, en clôture : flash blanc, puis le nom du jeu et sa promesse. C'est la seule phrase
    /// que le joueur doit retenir — « ne tue pas les monstres, deviens-les » est la mécanique même.
    /// </summary>
    private IEnumerator RevealTitle()
    {
        foreach (var mote in _motes) if (mote != null) mote.gameObject.SetActive(false);

        yield return Fade(0f, 0.9f, 0.12f);
        yield return Fade(0.9f, 0f, 0.5f);

        if (_title != null) _title.gameObject.SetActive(true);
        if (_tagline != null) _tagline.gameObject.SetActive(true);
        if (_skipHint != null) _skipHint.gameObject.SetActive(false);

        yield return Alpha(_title, 0f, 1f, 0.8f);
        yield return Alpha(_tagline, 0f, 1f, 0.6f);
        yield return new WaitForSecondsRealtime(TitleHold);
    }

    // ─── Figures ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Anime les particules du plan courant. Six comportements, un seul système : c'est le
    /// <b>mouvement</b> qui distingue les plans, pas la forme — une convergence, une dispersion, une
    /// pluie et une descente se reconnaissent sans qu'on ait à dessiner quoi que ce soit.
    /// </summary>
    private void AnimateFigure()
    {
        if (_stage == null) return;

        float t = _shotTime;

        for (int i = 0; i < _motes.Count; i++)
        {
            var mote = _motes[i];
            if (mote == null) continue;

            float phase = i / (float)Mathf.Max(1, _motes.Count);
            float angle = phase * Mathf.PI * 2f;

            Vector2 position;
            switch (_figure)
            {
                case Figure.Converge:
                    // Tout tombe vers un point : l'Aether et les réseaux se rejoignent.
                    float pull = Mathf.Repeat(t * 0.35f + phase, 1f);
                    position = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (280f * (1f - pull));
                    break;

                case Figure.Corrupt:
                    // Une orbite qui se déforme : la machine ne tourne plus rond.
                    float wobble = 1f + 0.35f * Mathf.Sin(t * 3f + phase * 8f);
                    position = new Vector2(Mathf.Cos(angle + t * 0.8f), Mathf.Sin(angle + t * 0.8f))
                             * 150f * wobble;
                    break;

                case Figure.Swarm:
                    // Un déferlement latéral, dense et irrégulier.
                    float x = Mathf.Repeat(t * 260f + phase * 900f, 900f) - 450f;
                    position = new Vector2(x, Mathf.Sin(phase * 20f + t) * 130f);
                    break;

                case Figure.Guard:
                    // Un anneau stable autour d'un centre : les Sanctuaires tiennent.
                    position = new Vector2(Mathf.Cos(angle + t * 0.4f), Mathf.Sin(angle + t * 0.4f)) * 190f;
                    break;

                case Figure.Descend:
                    // Une pluie verticale : quelqu'un doit descendre.
                    float y = 260f - Mathf.Repeat(t * 190f + phase * 520f, 520f);
                    position = new Vector2((phase - 0.5f) * 620f, y);
                    break;

                default:
                    // Arrachement : les fragments partent du centre et reviennent, happés.
                    float breathe = 0.4f + 0.6f * Mathf.Abs(Mathf.Sin(t * 1.4f + phase * 3f));
                    position = new Vector2(Mathf.Cos(angle * 3f), Mathf.Sin(angle * 2f)) * 230f * breathe;
                    break;
            }

            mote.anchoredPosition = position;
        }
    }

    private void BuildMotes(Color tint)
    {
        foreach (var mote in _motes)
        {
            if (mote == null) continue;

            mote.gameObject.SetActive(true);
            var image = mote.GetComponent<Image>();
            if (image != null) image.color = tint;
        }
    }

    // ─── Fondus ───────────────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, float seconds)
    {
        if (_fade == null) yield break;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            float a = Mathf.Lerp(from, to, t / seconds);
            _fade.color = new Color(0f, 0f, 0f, a);
            yield return null;
        }

        _fade.color = new Color(0f, 0f, 0f, to);
    }

    private static IEnumerator Alpha(Text? text, float from, float to, float seconds)
    {
        if (text == null) yield break;

        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            var c = text.color;
            text.color = new Color(c.r, c.g, c.b, Mathf.Lerp(from, to, t / seconds));
            yield return null;
        }

        var final = text.color;
        text.color = new Color(final.r, final.g, final.b, to);
    }

    // ─── Construction ─────────────────────────────────────────────────────────

    private void BuildUi()
    {
        var canvasGo = new GameObject("IntroCanvas",
            typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        UiCanvas.Configure(canvasGo, 200);

        var background = UiStyle.NewUiObject("Background", canvasGo.transform);
        background.AddComponent<Image>().color = UiPalette.BgDeep;
        UiStyle.Stretch(background, 0f);

        var stageGo = UiStyle.NewUiObject("Stage", canvasGo.transform);
        _stage = stageGo.GetComponent<RectTransform>();
        _stage.anchorMin = _stage.anchorMax = new Vector2(0.5f, 0.5f);
        _stage.pivot = new Vector2(0.5f, 0.5f);
        _stage.sizeDelta = new Vector2(1000f, 600f);
        _stage.anchoredPosition = new Vector2(0f, 60f);

        for (int i = 0; i < MoteCount; i++)
        {
            var mote = UiStyle.NewUiObject("Mote", _stage);
            var image = mote.AddComponent<Image>();
            image.raycastTarget = false;

            var rect = mote.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(7f, 7f);

            _motes.Add(rect);
        }

        _line = UiStyle.Label(canvasGo.transform, "", 26, UiPalette.OffWhite, TextAnchor.UpperCenter);
        var lineRect = _line.GetComponent<RectTransform>();
        lineRect.anchorMin = new Vector2(0.5f, 0f);
        lineRect.anchorMax = new Vector2(0.5f, 0f);
        lineRect.pivot = new Vector2(0.5f, 0f);
        lineRect.sizeDelta = new Vector2(1400f, 120f);
        lineRect.anchoredPosition = new Vector2(0f, 190f);

        _title = UiStyle.Label(canvasGo.transform, Loc.T("INTRO_TITLE"), 76,
                               UiPalette.Violet, TextAnchor.MiddleCenter);
        Center(_title, new Vector2(1400f, 110f), 40f);
        _title.gameObject.SetActive(false);

        _tagline = UiStyle.Label(canvasGo.transform, Loc.T("INTRO_TAGLINE"), 28,
                                 UiPalette.Cyan, TextAnchor.MiddleCenter);
        Center(_tagline, new Vector2(1400f, 60f), -60f);
        _tagline.gameObject.SetActive(false);

        // ⚠ Affichée en PERMANENCE. Une intro qu'on ne sait pas passer se subit — et le projet a
        // déjà perdu une session entière sur une capacité dont la touche n'était annoncée nulle part.
        _skipHint = UiStyle.Label(canvasGo.transform, "— " + Loc.T("INTRO_SKIP") + " —", 18,
                                  UiPalette.Dim, TextAnchor.LowerCenter);
        var hintRect = _skipHint.GetComponent<RectTransform>();
        hintRect.anchorMin = new Vector2(0.5f, 0f);
        hintRect.anchorMax = new Vector2(0.5f, 0f);
        hintRect.pivot = new Vector2(0.5f, 0f);
        hintRect.sizeDelta = new Vector2(900f, 30f);
        hintRect.anchoredPosition = new Vector2(0f, 40f);

        // Le voile de fondu vit AU-DESSUS de tout le reste : c'est lui qui ouvre et qui ferme.
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

    /// <summary>Nombre de particules de scène — assez pour faire une figure, assez peu pour rester lisible.</summary>
    private const int MoteCount = 48;
}
