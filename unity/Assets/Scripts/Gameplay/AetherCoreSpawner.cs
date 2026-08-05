using UnityEngine;

/// <summary>
/// Fait apparaître un Noyau d'Aether à intervalle régulier (port d'<c>AetherCoreSpawner</c>).
///
/// <para>Le rythme et la marge viennent de <c>meta_upgrades.json</c> — 45 s, jamais à moins de 150 px
/// d'un mur. La marge n'est pas cosmétique : un Noyau collé à une paroi force le joueur à s'y
/// acculer, et le seul objet du jeu qui demande de traverser le danger deviendrait celui qui demande
/// de se piéger.</para>
///
/// <para>Le premier n'apparaît qu'après un intervalle complet : commencer par un Noyau offert
/// affaiblirait le seul moment où le joueur choisit d'aller au contact.</para>
/// </summary>
public sealed class AetherCoreSpawner : MonoBehaviour
{
    /// <summary>Gabarit du Noyau — assigné à la construction de la scène.</summary>
    public GameObject? CorePrefab;

    /// <summary>Marge minimale par rapport aux bords de l'arène, en pixels.</summary>
    public const float MinMargin = 150f;

    /// <summary>Intervalle de repli si la donnée manque, en secondes.</summary>
    public const float DefaultInterval = 45f;

    /// <summary>Noyaux posés depuis le début de la run — observable pour les vérifications.</summary>
    public int SpawnedCount { get; private set; }

    private float _interval = DefaultInterval;
    private float _timer;

    private void Start() => _interval = LoadInterval();

    /// <summary>
    /// Lit <c>aetherCores.periodicSpawnIntervalSeconds</c>. La valeur vient des <b>données</b> et
    /// n'est pas recopiée ici : c'est la règle du projet — aucune constante d'équilibrage en dur.
    /// </summary>
    private static float LoadInterval()
    {
        string? json = DataFiles.Load("meta_upgrades.json");
        if (json == null) return DefaultInterval;

        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("aetherCores", out var cores)
                && cores.TryGetProperty("periodicSpawnIntervalSeconds", out var value))
                return value.GetSingle();
        }
        catch (System.Text.Json.JsonException e)
        {
            Debug.LogWarning($"[AetherCoreSpawner] meta_upgrades.json illisible : {e.Message}");
        }

        return DefaultInterval;
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.RunEnded) return;

        _timer += Time.deltaTime;
        if (_timer < _interval) return;

        _timer -= _interval;
        Spawn();
    }

    /// <summary>Pose un Noyau à une position tirée au sort dans l'arène, hors des bords.</summary>
    public void Spawn()
    {
        if (CorePrefab == null) return;

        float halfW = Mathf.Max(0f, Arena.HalfWidth - MinMargin);
        float halfH = Mathf.Max(0f, Arena.HalfHeight - MinMargin);

        // ⚠ `Gd.RandRange(double, double)` rend un double, comme sous Godot — la signature est celle
        // du moteur d'origine et ne se convertit pas seule. C'est le même appel que le spawner Godot.
        var position = new Vector3((float)Gd.RandRange(-halfW, halfW),
                                   (float)Gd.RandRange(-halfH, halfH), 0f);
        SpawnAt(position);

        // Tracé : un Noyau apparaît toutes les 45 s, à une position tirée au sort dans une arène
        // plus large que l'écran — il peut donc très bien naître hors du champ de la caméra. Sans
        // cette ligne, « la cadence fonctionne-t-elle ? » ne se vérifie qu'en arpentant l'arène.
        Debug.Log($"[Noyau] apparu en {position.x:F0}, {position.y:F0} " +
                  $"(t = {GameManager.Instance?.RunTime ?? 0f:F0} s, total {SpawnedCount})");
    }

    /// <summary>
    /// Pose un Noyau à un endroit précis — la mort d'un Colosse Greffé, celle du boss.
    /// </summary>
    public static void SpawnAt(Vector3 position)
    {
        var spawner = FindFirstObjectByType<AetherCoreSpawner>();
        if (spawner == null || spawner.CorePrefab == null) return;

        var go = Instantiate(spawner.CorePrefab, position, Quaternion.identity);

        // ⚠ Godot rend actif tout nœud instancié ; Unity conserve l'état du gabarit. Sans cela, le
        // Noyau existe dans la hiérarchie, ne s'affiche pas et ne se ramasse jamais.
        go.SetActive(true);

        spawner.SpawnedCount++;
    }
}
