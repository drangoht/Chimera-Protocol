using System.Collections;
using System.IO;
using UnityEngine;

/// <summary>
/// Enregistre les <b>rushes vidéo</b> du trailer, une prise par lancement (<c>--record=&lt;prise&gt;</c>).
///
/// <para><b>Pourquoi cet outil existe.</b> Sous Godot, les rushes venaient du Movie Maker
/// (<c>--write-movie</c>) : rendu à cadence fixe, audio compris, en une option de ligne de commande.
/// Unity n'a pas d'équivalent dans un build — <c>Unity Recorder</c> ne tourne que dans l'éditeur.
/// Le portage a donc emporté la capture avec le moteur, et <c>tools/build_trailer.py</c> est resté
/// seul, à monter des rushes que plus rien ne produisait.</para>
///
/// <para><b>Le principe.</b> <see cref="Time.captureFramerate"/> découple le temps du jeu du temps
/// réel : chaque image avance d'exactement 1/60 s de temps de jeu, quel que soit le temps qu'elle a
/// coûté à calculer et à écrire. On obtient le rendu à cadence fixe du Movie Maker, sans frame
/// perdue ni déchirure, au prix d'une capture qui dure plusieurs fois la durée du plan.</para>
///
/// <para><b>Ce que ça change par rapport aux rushes Godot.</b> Là-bas on filmait de longues runs et
/// l'on <i>cherchait</i> les bons plans dans une planche-contact, timecode par timecode — un
/// recalage manuel que la moindre recapture invalidait, puisque les runs sont aléatoires. Ici chaque
/// plan est <b>mis en scène</b> puis filmé pour sa durée exacte : le montage n'a plus à deviner où
/// regarder, et une recapture rend le même découpage.</para>
///
/// <para>⚠ <b>Pas de son.</b> Un build Unity ne sait pas écrire le mix de l'<c>AudioListener</c> en
/// même temps qu'il ralentit son horloge ; l'audio suivrait le temps réel et dériverait de plusieurs
/// secondes par plan. Les rushes sont donc muets et la bande-son est remontée au mixage, depuis les
/// pistes du jeu (cf. <c>MUSIC_EDL</c> dans <c>tools/build_trailer.py</c>).</para>
///
/// <para>⚠ Il tourne <b>avec un rendu</b> : en <c>-batchmode -nographics</c>, les images seraient
/// noires et <see cref="WaitForEndOfFrame"/> ne rendrait jamais la main.</para>
/// </summary>
public sealed class TrailerRecorder : MonoBehaviour
{
    // ─── Format ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Largeur et hauteur de capture. 720p, comme les rushes Godot : le montage passe en 1440p par un
    /// agrandissement <b>×2 exact</b> au plus proche voisin, seul facteur qui garde le pixel art net.
    /// Filmer en 1080p imposerait un ×1,5 non entier, donc du flou sur chaque bord.
    /// </summary>
    private const int Width = 1280;
    private const int Height = 720;

    private string _name = "";
    private string _dir = "";
    private int _fps = 60;
    private int _frame;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        string take = "";

        foreach (string arg in System.Environment.GetCommandLineArgs())
            if (arg.StartsWith("--record=", System.StringComparison.Ordinal))
                take = arg.Substring("--record=".Length);

        if (take.Length == 0) return;

        // Installé par code, et non posé dans une scène : les prises traversent Intro, MainMenu et
        // Game, et un composant de scène mourrait au premier changement — au milieu de la prise.
        var host = new GameObject("[TrailerRecorder]");
        DontDestroyOnLoad(host);
        host.AddComponent<TrailerRecorder>()._name = take;
    }

    private void Start()
    {
        foreach (string arg in System.Environment.GetCommandLineArgs())
        {
            if (arg.StartsWith("--record-dir=", System.StringComparison.Ordinal))
                _dir = arg.Substring("--record-dir=".Length);
            else if (arg.StartsWith("--record-fps=", System.StringComparison.Ordinal)
                  && int.TryParse(arg.Substring("--record-fps=".Length), out int fps) && fps > 0)
                _fps = fps;
        }

        if (_dir.Length == 0) _dir = Path.Combine("trailer", "frames", _name);
        Directory.CreateDirectory(_dir);

        // ⚠ La résolution est IMPOSÉE. Les réglages du joueur sont rejoués au démarrage (plein écran
        // compris), et un poste en 1440p aurait rendu des rushes qu'aucun agrandissement entier ne
        // ramène au format du montage — sans que rien ne le signale avant l'encodage.
        Screen.SetResolution(Width, Height, FullScreenMode.Windowed);
        QualitySettings.vSyncCount = 0;

        // Le découplage lui-même. À partir d'ici, une image = 1/_fps s de temps de jeu, qu'elle ait
        // coûté 3 ms ou 300.
        Time.captureFramerate = _fps;

        Debug.Log($"[TrailerRecorder] prise '{_name}' — {_fps} fps vers {Path.GetFullPath(_dir)}");
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        // Le temps que la fenêtre prenne sa nouvelle taille : la première image sort sinon au format
        // précédent, et ffmpeg refuse une séquence dont la première image n'a pas la taille des
        // autres.
        for (int i = 0; i < 10; i++) yield return null;

        if (Screen.width != Width || Screen.height != Height)
            Debug.LogWarning($"[TrailerRecorder] fenetre {Screen.width}x{Screen.height} " +
                             $"au lieu de {Width}x{Height} — le montage attend {Width}x{Height}.");

        yield return Take();

        Debug.Log($"[TrailerRecorder] {_frame} images ecrites ({_frame / (float)_fps:F1} s de video).");
        Application.Quit(0);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // LES PRISES
    //
    // Chacune lance sa mise en scène en parallèle puis filme sans interruption : les timecodes du
    // montage sont donc CONNUS D'AVANCE, et non relevés après coup sur une planche-contact.
    // ─────────────────────────────────────────────────────────────────────────

    private IEnumerator Take() => _name switch
    {
        "intro" => TakeIntro(),
        "menu" => TakeMenu(),
        "meta" => TakeMeta(),
        "levelup" => TakeLevelUp(),
        "chimera" => TakeChimera(),
        "boss" => TakeGameplay(26f),
        _ => TakeGameplay(22f),          // gp_<biome> : le biome vient de --biome=
    };

    /// <summary>
    /// La cinématique d'ouverture, jouée seule (six plans puis le dévoilement du titre).
    /// </summary>
    /// <remarks>
    /// Aucune mise en scène : c'est le seul récit du jeu, il se suffit. La prise déborde volontiers
    /// sur la fin — le titre animé sert de carton final au trailer, et couper trop tôt coûterait une
    /// recapture de quarante secondes.
    /// </remarks>
    private IEnumerator TakeIntro()
    {
        if (FindFirstObjectByType<IntroScreen>() == null) SceneRoot.ChangeScene(GameScenes.Intro);
        yield return Record(44f);
    }

    /// <summary>Le menu principal, sans tampon de version (<c>--trailer</c>).</summary>
    private IEnumerator TakeMenu()
    {
        SceneRoot.ChangeScene(GameScenes.MainMenu);
        yield return Wait(1.5f);
        yield return Record(6f);
    }

    /// <summary>
    /// La méta : Hub, carte des niveaux, les trois onglets du Codex, les défis.
    /// </summary>
    /// <remarks>
    /// <para>Les écrans sont <b>montés à la main</b> plutôt que rejoints par des clics simulés — même
    /// raison que pour la tournée de captures : on filme l'habillage, pas la navigation, et un clic
    /// dépend d'un <c>EventSystem</c> et d'une position de souris que la capture ne contrôle pas.</para>
    /// <para>⚠ Sans la progression de vitrine, une installation neuve filmerait un jeu <b>vide</b> :
    /// Codex masqué, Hub sans Échos, aucun défi. Exact, et invendable.</para>
    /// </remarks>
    private IEnumerator TakeMeta()
    {
        SceneRoot.ChangeScene(GameScenes.MainMenu);
        yield return Wait(1.5f);

        Showcase();

        var host = new GameObject("[TrailerStage]");
        StartCoroutine(MetaStage(host));

        // 6 écrans × 3 s, plus une seconde de marge : la prise ne doit pas se terminer AVANT sa mise
        // en scène, sinon le dernier écran manque et la coupe se voit.
        yield return Record(19f);
        Destroy(host);
    }

    private IEnumerator MetaStage(GameObject host)
    {
        var hub = host.AddComponent<HubScreen>();
        yield return null;
        hub.Show();
        yield return Wait(3f);
        hub.Hide();

        var levels = host.AddComponent<LevelSelectScreen>();
        yield return null;
        levels.Show();
        yield return Wait(3f);
        levels.Hide();

        var codex = host.AddComponent<CodexScreen>();
        yield return null;
        codex.Show();
        yield return Wait(3f);

        // Les onglets sont trois écrans, pas un : l'arsenal et la chimère portent la promesse du jeu
        // — les armes qu'on construit, les créatures qu'on devient — là où le bestiaire ne montre que
        // ce qu'on affronte.
        codex.SelectTab(CodexScreen.Tab.Arsenal);
        yield return Wait(3f);

        codex.SelectTab(CodexScreen.Tab.Chimera);
        yield return Wait(3f);
        codex.Hide();

        var challenges = host.AddComponent<ChallengeScreen>();
        yield return null;
        challenges.Show();
        yield return Wait(3f);
        challenges.Hide();
    }

    /// <summary>
    /// Une run filmée : le personnage est piloté (<c>--auto-play</c>), l'arène peuplée à la main pour
    /// que le premier plan ne soit pas une arène vide.
    /// </summary>
    /// <remarks>
    /// <para>Les drapeaux font le reste et viennent de la ligne de commande — <c>--biome=</c> pour le
    /// décor et la faune, <c>--start-at=</c> pour la minute de run (donc la densité et la puissance
    /// de la faune), <c>--saturate-arsenal</c> pour un build de fin de partie, <c>--invuln</c> pour
    /// qu'une mort ne coupe pas le plan en deux.</para>
    /// <para>⚠ <b>Les modales sont écartées à chaque image.</b> Un build saturé monte de niveau
    /// toutes les quelques secondes ; sans cela, la moitié du rush montre trois cartes par-dessus
    /// l'action qu'on venait filmer.</para>
    /// </remarks>
    private IEnumerator TakeGameplay(float seconds)
    {
        SceneRoot.ChangeScene(GameScenes.Game);

        // La scène monte son joueur, son spawner et son décor dans leurs Start : filmer avant, c'est
        // filmer un écran noir.
        yield return Wait(2.5f);

        SpawnSwarmAroundPlayer(26);
        StartCoroutine(KeepClear());

        yield return Record(seconds);
    }

    /// <summary>L'écran de montée de niveau — la décision qui rythme un survivor.</summary>
    /// <remarks>
    /// Il est <b>présenté à la main</b>. L'attendre reviendrait à filmer une arène en espérant qu'une
    /// modale s'ouvre pendant les cinq secondes du plan ; et le tirage serait celui du hasard, alors
    /// que ce plan doit montrer des cartes lisibles.
    /// </remarks>
    private IEnumerator TakeLevelUp()
    {
        SceneRoot.ChangeScene(GameScenes.Game);
        yield return Wait(2.5f);

        SpawnSwarmAroundPlayer(18);
        yield return Wait(2f);

        FindFirstObjectByType<LevelUpScreen>()?.Present(LevelUpPool.BuildOverload());
        yield return Record(5f);
    }

    /// <summary>
    /// Le corps de la chimère : trois jeux de greffes portés, en gros plan.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Zoom obligatoire.</b> Le joueur mesure 32 px sur une image de 1280 : à l'échelle du jeu,
    /// un appendice de 12 px occupe une dizaine de pixels et le plan ne montre rien de ce qu'il
    /// prétend montrer. C'est le différenciateur du jeu — il doit remplir l'écran.
    /// </remarks>
    private IEnumerator TakeChimera()
    {
        SceneRoot.ChangeScene(GameScenes.Game);
        yield return Wait(2.5f);

        var player = Player.Instance;
        if (player == null) { yield return Record(1f); yield break; }

        // La scène est vidée : un nuage de fusion ou une nuée au premier plan recouvrirait
        // exactement la silhouette qu'on vient filmer.
        InventorySystem.Instance?.ResetForRun();
        player.GetComponent<GraftManager>()?.ClearAll();

        foreach (var enemy in EnemyBase.Active.ToArray())
            if (enemy != null) Destroy(enemy.gameObject);

        var body = player.GetComponent<ChimeraBody>() ?? player.gameObject.AddComponent<ChimeraBody>();
        var camera = Camera.main;
        float restSize = camera != null ? camera.orthographicSize : 0f;
        if (camera != null) camera.orthographicSize = restSize * 0.22f;

        StartCoroutine(ChimeraStage(body));
        StartCoroutine(KeepClear());

        yield return Record(9f);
        if (camera != null) camera.orthographicSize = restSize;
    }

    private IEnumerator ChimeraStage(ChimeraBody body)
    {
        foreach (string[] grafts in new[]
                 {
                     new[] { "grafted_carapace", "aiming_eye" },
                     new[] { "stalker_wave", "erratic_servos", "swarm_symbiote" },
                     new[] { "fusion_charge_blindee", "fusion_ruche_tourelles" },
                 })
        {
            body.Build(grafts);
            Debug.Log($"[TrailerRecorder] chimere : {body.PartCount} appendice(s).");
            yield return Wait(3f);
        }
    }

    // ─── Mise en scène partagée ──────────────────────────────────────────────

    /// <summary>
    /// Progression de <b>vitrine</b>, en mémoire seulement — jumelle de celle de la tournée de
    /// captures.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Rien n'est écrit sur disque.</b> Un outil ne laisse pas sa mise en scène dans la
    /// sauvegarde du joueur, pas plus qu'un drapeau de banc.
    /// </remarks>
    private static void Showcase()
    {
        var settings = GameSettings.Current;
        var inventory = InventorySystem.Instance;

        if (inventory != null)
            foreach (string id in inventory.AllWeaponIds)
                if (!settings.DiscoveredWeapons.Contains(id)) settings.DiscoveredWeapons.Add(id);

        foreach (var graft in Assimilation.Config.Grafts)
            if (!settings.DiscoveredGrafts.Contains(graft.Id)) settings.DiscoveredGrafts.Add(graft.Id);

        foreach (string biome in LevelThreat.Order)
        {
            settings.Completions[biome] = 1;
            settings.SaturationBeatenByLevel[biome] = 1;
            if (!settings.HighScores.TryGetValue(biome, out int best) || best <= 0)
                settings.HighScores[biome] = 13 * 60 + 42;
        }

        if (MetaProgression.CurrentEchoes < 4000) MetaProgression.Save.Meta.CurrentEchoes = 4000;
    }

    /// <summary>
    /// Écarte les écrans modaux à chaque image, tant que la prise dure.
    /// </summary>
    /// <remarks>
    /// Un arsenal saturé fait monter de niveau toutes les quelques secondes, et l'Assimilation ouvre
    /// sa propre modale : les deux mettent le jeu à <c>timeScale = 0</c> — donc les armes cessent de
    /// tirer — et recouvrent l'écran. Sans cette purge, un rush de vingt secondes en contient huit
    /// d'écran de choix.
    /// </remarks>
    private IEnumerator KeepClear()
    {
        while (true)
        {
            var levelUp = FindFirstObjectByType<LevelUpScreen>();
            if (levelUp != null && levelUp.IsVisible) levelUp.DismissForBench();

            var assimilation = FindFirstObjectByType<AssimilationScreen>();
            if (assimilation != null && assimilation.IsVisible) assimilation.AcceptForBench();

            yield return null;
        }
    }

    /// <summary>
    /// Pose une couronne d'ennemis autour du joueur, à portée des armes de zone comme de mêlée.
    /// </summary>
    /// <remarks>
    /// ⚠ Le gabarit seul ne porte <b>aucun sprite</b> : c'est le spawner qui pose le jeu d'animations,
    /// et sa méthode est privée. Sans ce câblage, la nuée se déplace, frappe, meurt — et reste
    /// totalement invisible à l'image. La tournée de captures s'est fait prendre exactement ainsi.
    /// </remarks>
    private static void SpawnSwarmAroundPlayer(int count)
    {
        var spawner = FindFirstObjectByType<EnemySpawner>();
        var player = Player.Instance;
        if (spawner == null || spawner.EnemyPrefab == null || player == null) return;

        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f;
            float radius = 90f + (i % 5) * 60f;
            Vector2 at = (Vector2)player.transform.position
                       + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;

            var go = Instantiate(spawner.EnemyPrefab, at, Quaternion.identity);
            go.SetActive(true);

            var enemy = go.GetComponent<EnemyBase>();
            var frames = SpriteFramesLibrary.ForEnemy("rust_swarm", "");
            if (enemy != null && frames != null) enemy.SetSpriteFrames(frames);
        }
    }

    // ─── Capture ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Attente exprimée en <b>temps de capture</b> — la seule horloge qui vaille ici.
    /// </summary>
    /// <remarks>
    /// <c>WaitForSecondsRealtime</c> lirait le temps mur, qui n'a plus aucun rapport avec la durée du
    /// plan : une image coûte plusieurs dizaines de millisecondes à écrire, si bien qu'une « attente
    /// de 3 s » tiendrait moins d'une demi-seconde dans le film. <c>Time.unscaledDeltaTime</c>, lui,
    /// vaut exactement 1/fps sous <see cref="Time.captureFramerate"/>, et continue de courir quand
    /// une modale met le jeu en pause.
    /// </remarks>
    private static IEnumerator Wait(float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime) yield return null;
    }

    /// <summary>
    /// Filme <paramref name="seconds"/> secondes : une image par frame, sans interruption.
    /// </summary>
    /// <remarks>
    /// <para><see cref="ScreenCapture.CaptureScreenshotAsTexture"/> est <b>synchrone</b>, à la
    /// différence de <c>CaptureScreenshot</c> qui écrit sur les images suivantes. C'est plus lent —
    /// c'est le point : la capture doit rendre une image et une seule par frame de jeu, sinon la
    /// séquence saute sans que rien ne le dise, et le mouvement du film devient irrégulier là où le
    /// jeu était fluide.</para>
    /// <para>La numérotation est continue sur toute la prise : ffmpeg lit la séquence par son
    /// motif <c>%05d</c>, et un trou dans les numéros arrête la lecture à cet endroit.</para>
    /// </remarks>
    private IEnumerator Record(float seconds)
    {
        int frames = Mathf.RoundToInt(seconds * _fps);

        for (int i = 0; i < frames; i++)
        {
            yield return new WaitForEndOfFrame();

            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(_dir, $"f{_frame:00000}.png"), texture.EncodeToPNG());
            Destroy(texture);

            _frame++;
        }
    }
}
