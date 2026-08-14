using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

/// <summary>
/// Annonce qu'une version plus récente est publiée, et propose d'aller la chercher.
///
/// <para><b>Pour qui.</b> Les joueurs qui ont téléchargé l'archive depuis le web n'ont aucune mise à
/// jour automatique : sans ce bandeau, ils restent indéfiniment sur la version de leur premier
/// téléchargement, et tout correctif publié ne les atteint jamais. Ceux qui passent par l'application
/// itch reçoivent déjà l'auto-update — le contrôle se tait alors.</para>
///
/// <para><b>Le manifeste est la source de vérité</b> (<c>version.json</c>, poussé sur le dépôt à
/// chaque release) : il porte le numéro et l'adresse de téléchargement. Le jeu ne devine rien, et
/// changer d'hébergeur ne demande pas de republier un binaire.</para>
///
/// <para>⚠ <b>Le contrôle était absent du portage</b> alors que sa règle de comparaison
/// (<see cref="VersionCompare"/>) avait bien été portée, testée, et n'était appelée par personne.
/// Troisième occurrence du même défaut après les icônes et la présence Discord : une règle sans son
/// système se lit exactement comme une fonctionnalité présente.</para>
/// </summary>
public sealed class UpdateBanner : MonoBehaviour
{
    /// <summary>Source de vérité de la dernière version publiée.</summary>
    private const string ManifestUrl =
        "https://raw.githubusercontent.com/drangoht/Chimera-Protocol/main/version.json";

    /// <summary>Au-delà, on renonce : le jeu doit démarrer sans attendre le réseau.</summary>
    private const int TimeoutSeconds = 5;

    private string _downloadUrl = "https://drangoht.itch.io/chimera-protocol";

    /// <summary>Version annoncée par le manifeste, vide tant que rien n'a été reçu.</summary>
    public string RemoteVersion { get; private set; } = "";

    /// <summary>Le bandeau a-t-il été affiché ? Observable par les bancs.</summary>
    public bool Shown { get; private set; }

    private Transform? _parent;

    /// <summary>Lance le contrôle. Le bandeau se construira sous <paramref name="parent"/>.</summary>
    public void Check(Transform parent)
    {
        _parent = parent;

        // Un joueur web est, par construction, sur la dernière version : la page sert le build
        // courant. Le bandeau n'aurait donc rien à annoncer — et, s'il le faisait, il proposerait
        // d'aller télécharger ce que le navigateur exécute déjà.
        if (Application.platform == RuntimePlatform.WebGLPlayer) return;

        // Lancé depuis l'application itch : l'auto-update s'en charge, et deux mécanismes qui
        // annoncent la même chose se contredisent tôt ou tard.
        if (!string.IsNullOrEmpty(System.Environment.GetEnvironmentVariable("ITCHIO_API_KEY"))) return;

        // Un banc n'a rien à télécharger, et une campagne de vingt runs n'a pas à interroger un
        // serveur vingt fois.
        if (Application.isBatchMode) return;

        StartCoroutine(Fetch());
    }

    private IEnumerator Fetch()
    {
        using var request = UnityWebRequest.Get(ManifestUrl);
        request.timeout = TimeoutSeconds;

        yield return request.SendWebRequest();

        // ⚠ Silence complet en cas d'échec : le jeu se joue hors ligne, et un joueur sans réseau ne
        // doit pas voir d'erreur pour une fonctionnalité qui ne le concerne pas.
        if (request.result != UnityWebRequest.Result.Success) yield break;

        var manifest = VersionManifest.Parse(request.downloadHandler.text);
        if (manifest.Version.Length == 0) yield break;

        RemoteVersion = manifest.Version;
        if (manifest.Url.Length > 0) _downloadUrl = manifest.Url;

        if (VersionCompare.IsNewer(manifest.Version, BuildInfo.Version)) Show(manifest.Version);
    }

    /// <summary>
    /// Bandeau discret en haut de l'écran : le numéro, et un bouton qui ouvre la page.
    /// </summary>
    /// <remarks>
    /// Accent <b>doré</b>, comme les Échos et les coûts : il annonce une valeur disponible, pas un
    /// avertissement. Un bandeau rouge ferait lire « quelque chose ne va pas ».
    /// </remarks>
    private void Show(string version)
    {
        if (_parent == null) return;

        var row = UiStyle.Panel(_parent, "BandeauMaj", FrameAccent.Gold);

        var rect = row.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        // ⚠ Dimensionné sur le libellé le PLUS LONG des trois langues, jamais sur le français seul :
        // « Récupérer sur itch.io » repassait à la ligne dans un bouton calé au plus juste, et un
        // texte qui se casse en deux se lit comme un défaut d'affichage.
        rect.sizeDelta = new Vector2(780f, 60f);
        rect.anchoredPosition = new Vector2(0f, -12f);

        var label = UiStyle.Label(row.transform, Loc.T("UPDATE_AVAILABLE", version), 20,
                                  UiPalette.Gold, TextAnchor.MiddleLeft);

        var labelRect = label.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0f, 0f);
        labelRect.anchorMax = new Vector2(1f, 1f);
        labelRect.offsetMin = new Vector2(24f, 0f);
        labelRect.offsetMax = new Vector2(-290f, 0f);

        var button = UiStyle.TextButton(row.transform, Loc.T("UPDATE_DOWNLOAD"), FrameAccent.Gold);

        var buttonRect = button.GetComponent<RectTransform>();
        buttonRect.anchorMin = buttonRect.anchorMax = new Vector2(1f, 0.5f);
        buttonRect.pivot = new Vector2(1f, 0.5f);
        buttonRect.sizeDelta = new Vector2(268f, 44f);
        buttonRect.anchoredPosition = new Vector2(-14f, 0f);

        button.onClick.AddListener(() =>
        {
            AudioSystem.PlaySfx("sfx_ui_button");
            Application.OpenURL(_downloadUrl);
        });

        Shown = true;
    }
}

/// <summary>
/// Lecture du manifeste de version — deux champs, analysés sans dépendance.
/// </summary>
/// <remarks>
/// Volontairement tolérant : un manifeste illisible ou incomplet ne doit rien faire, jamais lever.
/// Ce fichier vit hors du binaire et peut être réécrit à tout moment ; s'y fier aveuglément
/// reviendrait à laisser un fichier distant décider si le jeu démarre.
/// </remarks>
public readonly struct VersionManifest
{
    public readonly string Version;
    public readonly string Url;

    private VersionManifest(string version, string url)
    {
        Version = version;
        Url = url;
    }

    public static VersionManifest Parse(string json)
    {
        if (string.IsNullOrEmpty(json)) return new VersionManifest("", "");

        return new VersionManifest(Field(json, "version"), Field(json, "url"));
    }

    private static string Field(string json, string key)
    {
        int at = json.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal);
        if (at < 0) return "";

        int colon = json.IndexOf(':', at);
        if (colon < 0) return "";

        int open = json.IndexOf('"', colon);
        if (open < 0) return "";

        int close = json.IndexOf('"', open + 1);
        return close > open ? json.Substring(open + 1, close - open - 1) : "";
    }
}
