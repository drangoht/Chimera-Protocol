using UnityEngine;

/// <summary>
/// Apparition des ennemis — port du cœur de <c>EnemySpawner</c> (Lot 2).
///
/// <para><b>Les courbes ne sont pas réécrites</b> : <see cref="SpawnCurve"/> et
/// <see cref="EnemyScaling"/> sont de la logique pure partagée avec le projet Godot et couvertes
/// par la suite de tests. Ce composant ne fait que les interroger et instancier — c'est
/// exactement la répartition « les nœuds délèguent » du projet d'origine, et c'est ce qui garantit
/// que la difficulté est <b>la même</b> sur les deux moteurs.</para>
///
/// <para>⚠ Le plafond de population est une contrainte de tenue en charge, pas d'équilibrage : le
/// projet vise 200-300 entités simultanées.</para>
/// </summary>
public sealed class EnemySpawner : MonoBehaviour
{
    /// <summary>Plafond d'entités simultanées — repris de <c>Constants.MaxEnemies</c>.</summary>
    public const int MaxEnemies = 200;

    [Header("Apparition")]
    [Tooltip("Prefab d'ennemi à instancier.")]
    public GameObject? EnemyPrefab;

    [Tooltip("Prefab d'orbe d'XP laissé par les ennemis à leur mort.")]
    public GameObject? XpOrbPrefab;

    [Tooltip("Distance d'apparition autour du joueur, hors champ.")]
    public float SpawnRadius = 700f;

    [Header("Scaling (repris de enemies.json)")]
    [Tooltip("Croissance des PV par minute de jeu.")]
    public float HpScalingPerMinute = 0.12f;

    [Tooltip("Croissance des dégâts par minute de jeu.")]
    public float DamageScalingPerMinute = 0.06f;

    /// <summary>Multiplicateur de densité (difficulté × palier de menace × cran de saturation).</summary>
    public float TotalSpawnMult { get; set; } = 1f;

    /// <summary>Temps de jeu écoulé, en secondes — pilote toutes les courbes.</summary>
    public float ElapsedSeconds { get; private set; }

    /// <summary>Ennemis créés depuis le début de la run.</summary>
    public int TotalSpawned { get; private set; }

    private float _spawnTimer;

    private void Update()
    {
        if (Player.Instance == null || Player.Instance.IsDead) return;

        float dt = Time.deltaTime;
        ElapsedSeconds += dt;
        float minutes = ElapsedSeconds / 60f;

        // Cadence pilotée par un INTERVALLE décroissant, comme sous Godot — et non par un débit :
        // les deux ne produisent pas la même distribution dans le temps.
        _spawnTimer += dt;
        float interval = SpawnCurve.SpawnInterval(minutes);
        if (_spawnTimer < interval) return;

        _spawnTimer = 0f;
        TrySpawnBatch(minutes, SpawnCurve.BatchCount(minutes));
    }

    private void TrySpawnBatch(float minutes, int count)
    {
        int cap = Mathf.Min(SpawnCurve.MaxEnemies(minutes, TotalSpawnMult), MaxEnemies);

        for (int i = 0; i < count; i++)
        {
            if (EnemyBase.Active.Count >= cap) return;
            SpawnOne(minutes);
        }
    }

    private void SpawnOne(float minutes)
    {
        if (EnemyPrefab == null) return;

        // Apparition sur un cercle autour du joueur : hors champ, mais jamais dans son dos immédiat.
        float angle = Gd.Randf() * Mathf.PI * 2f;
        Vector2 offset = new(Mathf.Cos(angle) * SpawnRadius, Mathf.Sin(angle) * SpawnRadius);
        Vector2 pos = (Vector2)Player.Instance!.transform.position + offset;

        pos.x = Mathf.Clamp(pos.x, -Arena.HalfWidth, Arena.HalfWidth);
        pos.y = Mathf.Clamp(pos.y, -Arena.HalfHeight, Arena.HalfHeight);

        var go = Instantiate(EnemyPrefab, pos, Quaternion.identity, transform);

        // Unity conserve l'état actif du gabarit ; Godot, lui, produit TOUJOURS un nœud actif avec
        // Instantiate() + AddChild(). On reproduit la sémantique d'origine — sans quoi un gabarit
        // désactivé donne des ennemis qui existent, ne bougent pas et ne se signalent nulle part.
        go.SetActive(true);

        var enemy = go.GetComponent<EnemyBase>();
        if (enemy == null) return;

        enemy.XpOrbPrefab = XpOrbPrefab;

        // Le scaling vient de la logique pure : mêmes chiffres que sous Godot, par construction.
        enemy.ApplyScaling(
            EnemyScaling.Scaled(enemy.MaxHp,  minutes, HpScalingPerMinute,     1f),
            EnemyScaling.Scaled(enemy.Damage, minutes, DamageScalingPerMinute, 1f));

        TotalSpawned++;
    }

    /// <summary>Remet le compteur de temps à zéro pour une nouvelle run.</summary>
    public void ResetForRun()
    {
        ElapsedSeconds = 0f;
        TotalSpawned = 0;
        _spawnTimer = 0f;
    }
}
