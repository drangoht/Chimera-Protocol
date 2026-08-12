using UnityEngine;

/// <summary>
/// Fait apparaître l'Aimant aux moments tirés par <see cref="MagnetSchedule"/> (port de
/// <c>MagnetSpawner</c>).
///
/// <para><b>Trois apparitions par run, pas une cadence.</b> Le Noyau d'Aether tombe toutes les 45 s :
/// c'est une ressource, elle se budgétise. L'Aimant est un <i>événement</i> — tôt, au milieu, puis
/// juste avant le boss — et l'amélioration <c>bonus_magnet</c> en ajoute une par niveau, en overtime,
/// là où le sol se couvre d'orbes qu'un joueur ne peut plus aller chercher à la main.</para>
///
/// <para>Le spawner est un objet de <b>scène</b> : il est donc recréé — donc reprogrammé — à chaque
/// run, exactement comme sous Godot. Rien ici n'est statique, et deux parties de suite dans le même
/// processus ne partagent aucun compteur.</para>
/// </summary>
public sealed class MagnetSpawner : MonoBehaviour
{
    /// <summary>Marge minimale par rapport aux bords de l'arène, en pixels.</summary>
    public const float MinMargin = 150f;

    /// <summary>Aimants posés depuis le début de la run — observable pour les vérifications.</summary>
    public int SpawnedCount { get; private set; }

    /// <summary>Instants d'apparition tirés au démarrage — observable pour les vérifications.</summary>
    public float[] SpawnTimes { get; private set; } = System.Array.Empty<float>();

    private int _nextIndex;
    private bool _scheduled;

    private void Awake() => MagnetPickup.ResetCounters();

    /// <summary>
    /// Tire le calendrier de la run.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Surtout pas dans <c>Start</c></b>, et c'est un piège que ce projet a déjà payé sur les
    /// charges de Renouveler/Passer. L'ordre d'appel des <c>Start</c> n'est pas garanti par Unity, et
    /// <see cref="RunBootstrap"/> a le sien : il applique <c>--force-meta</c> et <c>--run-duration</c>
    /// dans le sien. Lu au démarrage, le niveau de <c>bonus_magnet</c> vaudrait tantôt celui de la
    /// sauvegarde, tantôt celui du drapeau, <b>selon la frame</b>. Lu à la première image de jeu,
    /// tous les <c>Start</c> sont derrière nous.
    /// </remarks>
    private void Schedule()
    {
        _scheduled = true;

        // ⚠ Le tirage passe par `Gd`, le flux du jeu, et non par `UnityEngine.Random` : sous
        // `--seed`, deux runs de même graine doivent poser leurs Aimants aux mêmes secondes.
        SpawnTimes = MagnetSchedule.SpawnTimes(
            MetaProgression.LevelOf("bonus_magnet"),
            GameManager.Instance != null ? GameManager.Instance.RunDurationSeconds : 780,
            (min, max) => (float)Gd.RandRange(min, max));

        Debug.Log($"[Aimant] {SpawnTimes.Length} apparitions prevues : " +
                  string.Join(", ", System.Array.ConvertAll(SpawnTimes, t => $"{t:F0}s")));
    }

    private void Update()
    {
        var gm = GameManager.Instance;
        if (gm == null || gm.RunEnded) return;

        if (!_scheduled) Schedule();
        if (_nextIndex >= SpawnTimes.Length) return;

        if (gm.RunTime < SpawnTimes[_nextIndex]) return;

        _nextIndex++;
        Spawn();
    }

    /// <summary>Pose un Aimant à une position tirée au sort dans l'arène, hors des bords.</summary>
    /// <remarks>
    /// La marge n'est pas cosmétique, pour la même raison que celle du Noyau : un objet collé à une
    /// paroi force le joueur à s'y acculer, et le seul ramassage qui demande de traverser l'arène
    /// deviendrait celui qui demande de se piéger.
    /// </remarks>
    public void Spawn()
    {
        float halfW = Mathf.Max(0f, Arena.HalfWidth - MinMargin);
        float halfH = Mathf.Max(0f, Arena.HalfHeight - MinMargin);

        var position = new Vector3((float)Gd.RandRange(-halfW, halfW),
                                   (float)Gd.RandRange(-halfH, halfH), 0f);
        SpawnAt(position);

        Debug.Log($"[Aimant] apparu en {position.x:F0}, {position.y:F0} " +
                  $"(t = {GameManager.Instance?.RunTime ?? 0f:F0} s, total {SpawnedCount}).");
    }

    /// <summary>
    /// Pose un Aimant à un endroit précis.
    /// </summary>
    /// <remarks>
    /// <b>Aucun gabarit</b> : l'Aimant dessine sa propre silhouette (<see cref="MagnetSprite"/>), donc
    /// il n'a rien à référencer. Un prefab n'apporterait qu'un maillon de plus à casser en silence —
    /// et le portage a déjà perdu trois sprites sur cette chaîne-là.
    /// </remarks>
    public void SpawnAt(Vector3 position)
    {
        var go = new GameObject("Magnet", typeof(SpriteRenderer), typeof(MagnetPickup));
        go.transform.position = position;

        SpawnedCount++;
    }
}
