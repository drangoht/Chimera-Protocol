using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Lecture des fichiers texte de <c>StreamingAssets</c> — <b>le seul endroit du projet qui sait que
/// le web n'a pas de disque</b>.
///
/// <para><b>Le problème qu'elle résout.</b> Sur Windows, <c>Application.streamingAssetsPath</c> est un
/// dossier : <c>File.ReadAllText</c> y répond immédiatement, et <see cref="DataFiles"/> comme
/// <see cref="Loc"/> pouvaient charger leur contenu paresseusement, au premier besoin. En WebGL ce
/// même chemin est une <b>URL</b> : il n'existe aucun système de fichiers, la lecture ne peut être
/// qu'un aller-retour réseau, donc asynchrone — et un appelant synchrone n'a rien à quoi se
/// raccrocher.</para>
///
/// <para><b>La parade est de déplacer l'attente, pas de la répandre.</b> Tout est chargé <b>une
/// fois</b>, avant la première scène de jeu (<see cref="BootScreen"/>), dans le cache que
/// <see cref="Read"/> consulte. Les seize sites d'appel de <c>DataFiles.Load</c> et tous les
/// <c>Loc.T</c> restent synchrones et inchangés : aucun écran n'a besoin de savoir sur quelle
/// plateforme il tourne.</para>
/// </summary>
public static class StreamingText
{
    /// <summary>Contenu déjà lu, indexé par chemin relatif à <c>StreamingAssets</c> (séparateur <c>/</c>).</summary>
    private static readonly Dictionary<string, string> _cache = new();

    /// <summary>Le préchargement a-t-il eu lieu ? Observable par les bancs et par l'écran de démarrage.</summary>
    /// <remarks>
    /// ⚠ <b>À consulter par tout ce qui change de scène automatiquement.</b> Un pilote qui quitte la
    /// scène de démarrage avant que ceci ne soit vrai fait démarrer le jeu sur des tables vides —
    /// c'est arrivé au premier essai dans un navigateur (voir <see cref="Install"/>).
    /// </remarks>
    public static bool Preloaded { get; private set; }

    /// <summary>Nombre de fichiers réellement chargés — distingue « préchargé » de « préchargé à vide ».</summary>
    public static int Count => _cache.Count;

    /// <summary>
    /// Manifeste des fichiers à précharger, écrit <b>par le build</b> dans <c>Resources</c>.
    /// </summary>
    /// <remarks>
    /// ⚠ Il n'est pas maintenu à la main, et c'est délibéré. Une liste de fichiers recopiée dans le
    /// code aurait exactement le mode d'échec dont ce projet a déjà payé le prix dix fois : on ajoute
    /// un JSON de tuning, le jeu continue de marcher sur Windows — où le disque répond à tout — et il
    /// se met à manquer une donnée <b>en web uniquement</b>, sans erreur au build. Le manifeste étant
    /// produit par l'acte qui construit le binaire, il ne peut pas décrire autre chose que ce que le
    /// binaire embarque.
    /// </remarks>
    public const string ManifestResource = "streaming_manifest";

    /// <summary>
    /// Contenu d'un fichier, par son chemin relatif à <c>StreamingAssets</c>
    /// (ex. <c>data/weapons.json</c>). <c>null</c> s'il manque.
    /// </summary>
    public static string? Read(string relativePath)
    {
        string key = Normalize(relativePath);
        if (_cache.TryGetValue(key, out string? cached)) return cached;

#if UNITY_WEBGL && !UNITY_EDITOR
        // Aucun repli possible : il n'y a pas de disque à interroger. Se taire renverrait un jeu aux
        // valeurs par défaut — des armes sans paliers, une interface affichant ses clés — sans que
        // rien n'indique la cause. Le préchargement est donc un invariant, et sa violation se crie.
        Debug.LogError($"[StreamingText] '{key}' demandé avant le préchargement. " +
                       $"En WebGL, tout accès à StreamingAssets doit passer par BootScreen. " +
                       $"({Count} fichiers en cache)");
        return null;
#else
        string full = Path.Combine(Application.streamingAssetsPath, key);
        if (!File.Exists(full)) return null;

        string text = File.ReadAllText(full);
        _cache[key] = text;
        return text;
#endif
    }

    /// <summary>
    /// Lance le chargement <b>au plus tôt et hors de toute scène</b>.
    /// </summary>
    /// <remarks>
    /// <para><b>Le défaut que ceci corrige, constaté au premier essai dans un navigateur.</b> Le
    /// chargement était porté par la coroutine de <see cref="BootScreen"/>. Or
    /// <c>BenchAutoPlay</c> s'installe en <c>AfterSceneLoad</c> — donc sur la scène de démarrage —
    /// et change de scène dès sa première image quand <c>--auto-play</c> est actif. La coroutine
    /// mourait avec la scène, à mi-chargement : la partie démarrait sur des tables vides, et tout le
    /// texte du jeu sortait sous forme de <b>clés</b> (« HUD_LEVEL », « BIOME_NEON_NAME »).</para>
    ///
    /// <para>Poser l'invariant « rien ne lit avant le démarrage » ne suffisait donc pas : il faut
    /// qu'aucun tiers ne <b>puisse</b> l'annuler. Porté par un objet qui survit aux scènes, le
    /// chargement va désormais à son terme quoi qu'il arrive ; à charge des pilotes automatiques
    /// d'attendre <see cref="Preloaded"/> — la garde vit dans <c>BenchAutoPlay</c>.</para>
    ///
    /// <para>C'est la même leçon que le retour du glaive : corriger la <b>classe</b> de défaut, pas
    /// le site où on l'a trouvé.</para>
    /// </remarks>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Install()
    {
        if (Preloaded || _host != null) return;

        _host = new GameObject("[StreamingText]");
        Object.DontDestroyOnLoad(_host);
        _host.AddComponent<StreamingTextLoader>();
    }

    private static GameObject? _host;

    /// <summary>
    /// Charge tout <c>StreamingAssets</c> en mémoire. À appeler <b>avant</b> le premier écran.
    /// </summary>
    /// <remarks>
    /// <para>Volontairement exécutée sur <b>toutes</b> les plateformes, et non derrière un
    /// <c>#if UNITY_WEBGL</c>. Un chemin de code qui ne s'emprunte que sur la cible qu'on ne teste
    /// jamais en développement est un chemin qu'on découvre cassé à la publication. Ici, chaque
    /// lancement sur Windows emprunte le même code — les 0,2 Mo de données coûtent quelques
    /// millisecondes, et le préchargement est vérifié en permanence.</para>
    ///
    /// <para><c>UnityWebRequest</c> et non <c>File</c> pour la même raison : c'est la seule API qui
    /// fonctionne des deux côtés.</para>
    /// </remarks>
    public static IEnumerator PreloadAll()
    {
        if (Preloaded) yield break;

        string root = Application.streamingAssetsPath;

        foreach (string relative in ManifestEntries())
        {
            string url = Uri(root, relative);

            using var request = UnityWebRequest.Get(url);
            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                // Un fichier de tuning absent doit se voir immédiatement (même parti pris que
                // DataFiles avant ce portage), mais il ne doit pas empêcher les autres de charger :
                // un jeu amputé d'une table est plus diagnosticable qu'un écran noir.
                Debug.LogError($"[StreamingText] échec de chargement : {url} ({request.error})");
                continue;
            }

            _cache[relative] = request.downloadHandler.text;
        }

        Preloaded = true;
        Debug.Log($"[StreamingText] {Count} fichiers préchargés depuis {root}");
    }

    /// <summary>
    /// Liste des fichiers à charger : le manifeste du build, ou le dossier lui-même quand on tourne
    /// dans l'éditeur (où aucun build n'a encore eu lieu).
    /// </summary>
    private static IEnumerable<string> ManifestEntries()
    {
        var manifest = Resources.Load<TextAsset>(ManifestResource);

        if (manifest != null)
        {
            foreach (string line in manifest.text.Split('\n'))
            {
                string entry = line.Trim();
                if (entry.Length > 0) yield return Normalize(entry);
            }
            yield break;
        }

#if UNITY_EDITOR
        // Mode Play : le manifeste n'existe que si un build a déjà tourné. Le dossier fait autorité.
        string root = Application.streamingAssetsPath;
        if (!Directory.Exists(root)) yield break;

        foreach (string path in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
        {
            if (path.EndsWith(".meta", System.StringComparison.Ordinal)) continue;
            yield return Normalize(path.Substring(root.Length + 1));
        }
#else
        Debug.LogError($"[StreamingText] manifeste '{ManifestResource}' absent du build — " +
                       $"aucune donnée ne sera chargée.");
#endif
    }

    /// <summary>URL lisible par <c>UnityWebRequest</c> des deux côtés.</summary>
    /// <remarks>
    /// En WebGL, <c>streamingAssetsPath</c> est déjà une URL http(s) et doit être laissée telle
    /// quelle ; sur un disque local, il faut le schéma <c>file://</c> — sans lui, un chemin Windows
    /// commençant par <c>C:</c> est interprété comme un protocole inconnu.
    /// </remarks>
    private static string Uri(string root, string relative)
    {
        string joined = $"{root}/{relative}";
        return joined.Contains("://") ? joined : $"file:///{joined}";
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    /// <summary>Oublie tout — réservé aux tests, qui rejouent plusieurs configurations.</summary>
    public static void Reset()
    {
        _cache.Clear();
        Preloaded = false;
    }
}

/// <summary>
/// Porteur de la coroutine de chargement — <b>sa seule raison d'être est de survivre aux scènes</b>.
/// </summary>
/// <remarks>
/// Une coroutine appartient au composant qui la lance : détruire l'objet l'interrompt, sans erreur ni
/// trace. Porté par un objet de scène, le chargement des données pouvait donc être abandonné à
/// mi-parcours par n'importe quel changement de scène — ce qui est exactement arrivé.
/// </remarks>
public sealed class StreamingTextLoader : MonoBehaviour
{
    private IEnumerator Start() => StreamingText.PreloadAll();
}
