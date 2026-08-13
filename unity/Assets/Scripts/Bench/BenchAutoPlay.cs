using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Résout à la place du joueur les écrans qui attendent un choix — montée de niveau, assimilation
/// (<c>--auto-play</c>).
///
/// <para><b>C'est ce qui rend une run headless mesurable au-delà de la deuxième minute.</b> Sans lui,
/// la partie se fige au premier passage de niveau : la modale met le temps de jeu à zéro et attend un
/// clic qui ne viendra jamais. Tout ce que le banc existe pour mesurer — la courbe de puissance,
/// l'entrée en overtime, le temps de mise à mort du boss — se trouve après ce point d'arrêt.</para>
///
/// <para>Le composant vit sur son <b>propre objet</b>, comme les autres outils de banc : posé sur un
/// écran, son maintien entre les scènes le ferait survivre par-dessus la partie suivante.</para>
///
/// <para>⚠ <b>Ce que ce pilote ne remplace pas.</b> Il tire ses cartes au hasard. Le build obtenu est
/// donc <i>un</i> build possible, pas celui d'un joueur : aucun relevé sous ce drapeau ne dit ce
/// qu'un humain <b>choisirait</b>, et c'est la limite qui a été rappelée à chaque campagne — les
/// cartes de surcharge existent pour produire un arbitrage que le banc ne sait pas juger.</para>
/// </summary>
public sealed class BenchAutoPlay : MonoBehaviour
{
    /// <summary>
    /// S'installe seul au démarrage, si le drapeau est là.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Il ne peut pas être posé par le démarrage de la run</b> : ce composant lit les écrans, donc
    /// il appartient à l'assemblage de banc, qui référence l'interface — laquelle référence déjà le
    /// jeu. L'y faire instancier créerait un cycle, et le compilateur annoncerait simplement que le
    /// type « n'existe pas », ce qui envoie chercher une faute de frappe.
    ///
    /// <para>Il survit aux changements de scène et cherche ses écrans à chaque image : le menu, la
    /// partie et le retour au menu s'enchaînent sans qu'il ait à être réinstallé.</para>
    /// </remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (!DebugHooks.AutoPlay) return;

        var host = new GameObject("[BenchAutoPlay]");
        host.AddComponent<BenchAutoPlay>();
        DontDestroyOnLoad(host);

        Debug.Log("[BenchAutoPlay] --auto-play : les ecrans de choix se resolvent seuls.");
    }

    /// <summary>Cartes prises depuis le début de la run — observable par les bancs.</summary>
    public int CardsTaken { get; private set; }

    /// <summary>Greffes acceptées depuis le début de la run.</summary>
    public int GraftsAccepted { get; private set; }

    private LevelUpScreen? _levelUp;
    private AssimilationScreen? _assimilation;

    private bool _runLaunched;

    private void Update()
    {
        EnsureRunStarted();
        QuitWhenRunEnded();

        // Recherchés à chaque image tant qu'ils manquent : ces écrans sont créés par la scène de jeu,
        // dont l'ordre d'initialisation n'est pas garanti — les résoudre une fois pour toutes dans
        // Start laisserait le pilote inerte une fois sur deux.
        if (_levelUp == null) _levelUp = FindFirstObjectByType<LevelUpScreen>();
        if (_assimilation == null) _assimilation = FindFirstObjectByType<AssimilationScreen>();

        if (_levelUp != null && _levelUp.IsVisible && _levelUp.ChooseForBench(Gd.Randf))
            CardsTaken++;

        if (_assimilation != null && _assimilation.IsVisible)
        {
            _assimilation.AcceptForBench();
            GraftsAccepted++;
        }
    }

    /// <summary>
    /// Lance la partie si le jeu est resté au menu.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Sans cela, le drapeau ne mesurait rien du tout.</b> Le pilote s'installait, annonçait
    /// qu'il prenait la main, et le jeu attendait sagement au menu principal qu'on clique sur
    /// « Jouer » — le journal de puissance restait vide, et la seule trace était une ligne dans le
    /// log disant que tout allait bien. Un banc doit ouvrir lui-même la porte qu'il vient franchir.
    /// </remarks>
    private void EnsureRunStarted()
    {
        if (_runLaunched || GameManager.Instance != null) return;

        // ⚠ Ne JAMAIS partir de la scène de démarrage. Ce pilote s'installe en `AfterSceneLoad`,
        // donc sur cette scène-là, et changeait de scène dès sa première image : la partie démarrait
        // sur des tables vides — tout le texte du jeu sortait en clés (« HUD_LEVEL »). Invisible sur
        // Windows, où le disque répond dans l'image même ; systématique dans un navigateur, où le
        // chargement dure des secondes.
        //
        // ⚠ La garde porte sur la SCÈNE, et pas seulement sur `StreamingText.Preloaded` : les deux
        // changements de scène partiraient alors dans la même image — celui-ci vers la partie, celui
        // de l'écran de démarrage vers l'intro — et le second gagnerait, en laissant ce pilote
        // convaincu d'avoir lancé la run. Le jeu attendait alors sagement au menu, exactement le
        // défaut que `EnsureRunStarted` existe pour empêcher.
        if (!StreamingText.Preloaded) return;
        if (SceneManager.GetActiveScene().name == GameScenes.Boot) return;

        _runLaunched = true;
        SceneRoot.ChangeScene(GameScenes.Game);

        Debug.Log($"[BenchAutoPlay] lancement de la run — biome {RunConfig.BiomeId}, " +
                  $"saturation {RunConfig.Saturation}.");
    }

    /// <summary>
    /// Quitte le processus quand la run est finie, <b>en headless seulement</b>.
    /// </summary>
    /// <remarks>
    /// <para>Une campagne enchaîne des dizaines de runs : chacune doit rendre la main d'elle-même,
    /// sinon l'outil qui la pilote ne peut que la tuer au chronomètre — et tuer un processus au
    /// milieu d'une écriture est précisément ce qui produit un journal tronqué.</para>
    ///
    /// <para>Jamais avec écran : un joueur qui aurait ce drapeau verrait sa fenêtre se fermer à la
    /// seconde où il meurt, sans bilan.</para>
    /// </remarks>
    private void QuitWhenRunEnded()
    {
        if (!Application.isBatchMode) return;

        var gm = GameManager.Instance;
        if (gm == null || !gm.RunEnded) return;

        Debug.Log($"[BenchAutoPlay] run terminee a {gm.RunTime:0}s — {gm.Kills} eliminations, " +
                  $"{CardsTaken} cartes, {GraftsAccepted} greffes.");

        Application.Quit(0);
        enabled = false;
    }
}
