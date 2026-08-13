#if !UNITY_WEBGL
using DiscordRPC;
#endif
using UnityEngine;

/// <summary>
/// Discord Rich Presence : « joue à Chimera Protocol » sur le statut du joueur, avec l'écran courant
/// et le chrono de session.
///
/// <para><b>Tolérant à l'absence de Discord</b>, et ce n'est pas un détail : si le client n'est pas
/// lancé — le cas de la majorité des joueurs — l'initialisation échoue et le jeu continue sans rien
/// dire. Un enrichissement facultatif ne doit jamais pouvoir empêcher de jouer.</para>
///
/// <para>⚠ <b>Le portage l'avait perdu, et le réglage lui a survécu.</b> Le champ <c>Discord</c>
/// existait dans la sauvegarde, était migré depuis Godot, écrit, relu… et piloté par personne : un
/// interrupteur mort, exactement ce que l'écran des options s'interdit. C'est la même famille que les
/// données déclarées jamais consommées — sauf qu'ici c'était le <b>système entier</b> qui manquait
/// derrière son réglage.</para>
///
/// <para>Les clés d'images (<c>chimera</c>, <c>chimera_small</c>) renvoient aux Art Assets du portail
/// développeur Discord : elles ne vivent pas dans le dépôt.</para>
///
/// <para><b>⚠ Absente du build web, et il ne peut pas en être autrement.</b> La présence Discord
/// passe par un canal local — un tube nommé — vers le client installé sur la machine. Une page web
/// n'a accès ni à l'un ni à l'autre : la bibliothèque ouvre des sockets et des fils d'exécution que
/// WebGL ne fournit pas. Le type entier est donc retiré de cette plateforme, et l'interrupteur des
/// options avec lui (<see cref="Available"/>) — un réglage qu'on peut basculer sans effet est
/// exactement ce que l'écran des options s'interdit, et c'est le défaut que cette classe a déjà
/// corrigé une fois.</para>
/// </summary>
public sealed class DiscordPresence : MonoBehaviour
{
    public static DiscordPresence? Instance { get; private set; }

    /// <summary>
    /// La présence a-t-elle un sens sur cette plateforme ? <b>À consulter avant d'offrir le
    /// réglage</b> : sur le web, la réponse est non et l'interrupteur ne doit pas exister.
    /// </summary>
#if UNITY_WEBGL
    public const bool Available = false;
#else
    public const bool Available = true;
#endif

#if !UNITY_WEBGL
    private const string AppId = "1523258677715406990";
    private const string LargeImageKey = "chimera";
    private const string SmallImageKey = "chimera_small";

    private DiscordRpcClient? _client;
    private Timestamps? _sessionStart;

    /// <summary>La présence est-elle réellement connectée ? Observable par les bancs.</summary>
    public bool IsConnected => _client != null;
#else
    /// <summary>Jamais connectée sur le web : il n'y a pas de client Discord à joindre.</summary>
    public bool IsConnected => false;
#endif

    /// <summary>Mises à jour poussées depuis le lancement — distingue « connecté » de « muet ».</summary>
    public int Updates { get; private set; }

    /// <summary>
    /// S'installe seule au démarrage.
    /// </summary>
    /// <remarks>
    /// Jamais en mode batch : un banc n'a pas de statut à publier, et ouvrir un canal de
    /// communication local à chaque run d'une campagne de vingt runs ne servirait qu'à ralentir la
    /// mesure — et à faire clignoter le statut du joueur qui la lance.
    /// </remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        // Sur le web, ne rien installer du tout : un objet qui survit aux scènes pour ne rien faire
        // est du bruit dans la hiérarchie, et un `Instance` non nul laisserait croire aux appelants
        // qu'ils poussent un statut quelque part.
        if (!Available || Application.isBatchMode || Instance != null) return;

        var host = new GameObject("[DiscordPresence]");
        host.AddComponent<DiscordPresence>();
        DontDestroyOnLoad(host);
    }

    /// <summary>
    /// Préférence du joueur, <b>poussée</b> par les réglages.
    /// </summary>
    /// <remarks>
    /// ⚠ Poussée et non lue : cette couche ne peut pas dépendre du jeu sans créer un cycle
    /// d'assemblages — la même contrainte que pour la langue et les volumes. Et il faut un champ
    /// statique, pas seulement un appel sur l'instance : les réglages peuvent être lus <b>avant</b>
    /// que ce composant n'existe, et la préférence serait alors poussée dans le vide.
    /// </remarks>
    public static bool PreferenceEnabled { get; set; } = true;

    private void Awake()
    {
        Instance = this;

        // Un joueur qui a coupé la présence ne doit pas la voir réapparaître, fût-ce le temps d'une
        // frame, au lancement suivant.
        if (PreferenceEnabled) Connect();
    }

    /// <summary>Active ou coupe la présence à chaud — c'est l'interrupteur des options.</summary>
    public void SetEnabled(bool enabled)
    {
        if (enabled == IsConnected) return;

        if (enabled) Connect();
        else Shutdown();
    }

    private void Connect()
    {
#if UNITY_WEBGL
        // Inatteignable : rien ne s'installe sur cette plateforme (voir Install). La branche existe
        // pour que la classe compile sans la bibliothèque, qui est retirée du build web.
        //
        // ⚠ La condition est UNITY_WEBGL seul, sans `&& !UNITY_EDITOR`, et c'est le point important :
        // l'éditeur basculé sur la plateforme Web compile SANS la bibliothèque — elle y est désactivée
        // par l'importateur, exactement comme dans le build. Ajouter l'exception pour l'éditeur ferait
        // référencer un type absent et casserait la compilation dès qu'on change de plateforme.
#else
        try
        {
            _client = new DiscordRpcClient(AppId);
            _client.Initialize();

            _sessionStart = Timestamps.Now;
            SetInMenus();

            // ⚠ Tracé même en cas de SUCCÈS. Sans cette ligne, un journal muet ne distingue pas
            // « connectée » de « jamais installée » — et c'est la seconde qui était vraie pendant
            // tout le portage. Le projet a déjà payé ce raisonnement avec un jeu muet dont tous les
            // compteurs montaient.
            Debug.Log($"[DiscordPresence] connectee (app {AppId}).");
        }
        catch (System.Exception e)
        {
            // Discord absent ou canal indisponible : on se désactive proprement, sans jamais bloquer
            // le jeu ni écrire une erreur — ce n'est pas un défaut, c'est le cas courant.
            Debug.Log($"[DiscordPresence] desactivee ({e.Message})");

            _client?.Dispose();
            _client = null;
        }
#endif
    }

    /// <summary>Présence « dans les menus » — l'état par défaut.</summary>
    public void SetInMenus() => Push("In the menus", null);

    /// <summary>Présence « en run » : le biome joué, et le cran s'il y en a un.</summary>
    public void SetInRun(string biomeName, int saturation)
    {
        string state = saturation > 0 ? $"{biomeName} — saturation {saturation}" : biomeName;
        Push("In a run", state.Length > 0 ? state : null);
    }

    private void Push(string details, string? state)
    {
#if UNITY_WEBGL
        _ = details; _ = state;
#else
        if (_client == null) return;

        try
        {
            _client.SetPresence(new RichPresence
            {
                Details = details,
                State = state,
                Timestamps = _sessionStart,
                Assets = new Assets
                {
                    LargeImageKey = LargeImageKey,
                    LargeImageText = "Chimera Protocol",
                    SmallImageKey = SmallImageKey,
                },
            });

            Updates++;
        }
        catch (System.Exception e)
        {
            Debug.Log($"[DiscordPresence] mise a jour ignoree ({e.Message})");
        }
#endif
    }

    /// <summary>
    /// Ferme proprement le canal.
    /// </summary>
    /// <remarks>
    /// Sur <c>OnApplicationQuit</c> <b>et</b> sur <c>OnDestroy</c> : une fermeture de fenêtre ne passe
    /// pas par les mêmes rappels qu'un arrêt du mode Play, et un canal laissé ouvert fige le statut du
    /// joueur sur « en partie » jusqu'au redémarrage de Discord.
    /// </remarks>
    private void OnApplicationQuit() => Shutdown();

    private void OnDestroy()
    {
        Shutdown();
        if (Instance == this) Instance = null;
    }

    private void Shutdown()
    {
#if !UNITY_WEBGL
        if (_client == null) return;

        try { _client.ClearPresence(); } catch { /* le canal peut déjà être fermé */ }
        try { _client.Dispose(); } catch { /* idem */ }

        _client = null;
#endif
    }
}
