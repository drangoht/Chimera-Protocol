using System.Collections.Generic;
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

    private Dictionary<string, EnemyTable.EnemyDef> _bestiary = new();
    private readonly Pcg32 _rng = new(0UL);

    /// <summary>Biome courant — restreint le pool aux ennemis qui lui appartiennent.</summary>
    public string? Biome { get; set; }

    /// <summary>Élites créées depuis le début de la run — observable pour les tests et le HUD.</summary>
    public int ElitesSpawned { get; private set; }

    /// <summary>Taille du bestiaire chargé.</summary>
    public int BestiarySize => _bestiary.Count;

    private void Awake()
    {
        // ⚠ Seul enemies.json est chargé. enemies_biome_expansion.json ressemble à un fichier de
        // données mais n'en est pas un : aucun code du jeu ne le lit, ses entrées existent déjà ici
        // SANS leur framesPath, et le fusionner rendrait 20 ennemis invisibles.
        string? json = DataFiles.Load("enemies.json");
        if (json == null) return;

        _bestiary = EnemyTable.Parse(json);
        Debug.Log($"[EnemySpawner] {_bestiary.Count} types d'ennemis charges.");
    }

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

        // Identité tirée des données : c'est ce qui donne 31 ennemis pour une poignée de
        // comportements. Sans bestiaire chargé, on retombe sur les valeurs du prefab.
        var def = PickDefinition(minutes);
        float hpPerMinute = HpScalingPerMinute;
        float dmgPerMinute = DamageScalingPerMinute;

        if (def != null)
        {
            enemy.MaxHp = def.MaxHp;
            enemy.Speed = def.Speed;
            enemy.Damage = def.DamagePerSecond;
            enemy.XpValue = def.XpValue;
            enemy.Ai = def.Ai;
            hpPerMinute = def.HpScalingPerMinute;
            dmgPerMinute = def.DamageScalingPerMinute;

            // Sans ce câblage, l'ennemi se déplace, frappe et meurt — totalement INVISIBLE.
            var frames = SpriteFramesLibrary.ForEnemy(def.Id, def.FramesPath);
            if (frames != null) enemy.SetSpriteFrames(frames);
        }

        // Le scaling vient de la logique pure : mêmes chiffres que sous Godot, par construction.
        enemy.ApplyScaling(
            EnemyScaling.Scaled(enemy.MaxHp,  minutes, hpPerMinute,  1f),
            EnemyScaling.Scaled(enemy.Damage, minutes, dmgPerMinute, 1f));

        // Promotion en élite APRÈS le scaling : les multiplicateurs d'affixe s'appliquent aux
        // valeurs de la minute courante, pas aux valeurs de fiche.
        TryPromoteToElite(enemy, minutes);

        TotalSpawned++;
    }

    /// <summary>
    /// Tire une définition d'ennemi dans le pool éligible, pondérée par <c>spawnWeight</c>.
    /// Renvoie <c>null</c> si aucun bestiaire n'est chargé.
    /// </summary>
    private EnemyTable.EnemyDef? PickDefinition(float minutes)
    {
        if (_bestiary.Count == 0) return null;

        var pool = EnemyTable.Eligible(_bestiary.Values, minutes, Biome);
        if (pool.Count == 0) return null;

        float total = 0f;
        foreach (var (_, w) in pool) total += w;
        if (total <= 0f) return pool[0].Def;

        float roll = _rng.NextFloat() * total;
        foreach (var (def, w) in pool)
        {
            roll -= w;
            if (roll <= 0f) return def;
        }
        return pool[^1].Def;
    }

    /// <summary>
    /// Tente de promouvoir l'ennemi en élite. La fréquence et le plafond viennent
    /// d'<see cref="EliteAffixTable"/> — le plafond est <b>dur</b> et volontaire : au-delà, une nuée
    /// d'élites cesse d'être « une texture » et fait peser régénération, explosions et sprites
    /// agrandis sur les 200-300 entités simultanées visées.
    /// </summary>
    private void TryPromoteToElite(EnemyBase enemy, float minutes)
    {
        float chance = EliteAffixTable.EliteChance(minutes, EliteFrequencyMult, EliteChanceCap);
        if (_rng.NextFloat() >= chance) return;

        var affixes = EliteAffixTable.All;
        var affix = affixes[_rng.RangeInt(0, affixes.Length - 1)];

        enemy.ApplyElite(affix);
        ElitesSpawned++;
    }

    [Header("Élites")]
    [Tooltip("Multiplicateur de fréquence des élites (cran de saturation « Élite ordinaire »).")]
    public float EliteFrequencyMult = 1f;

    [Tooltip("Plafond de probabilité d'élite. Paramètre, et non simple facteur : voir EliteAffixTable.")]
    public float EliteChanceCap = EliteAffixTable.MaxChance;

    /// <summary>Force la graine du tirage, pour rendre une campagne de banc reproductible.</summary>
    public void SeedSpawns(ulong seed) => _rng.Seed(seed);

    /// <summary>Remet le compteur de temps à zéro pour une nouvelle run.</summary>
    public void ResetForRun()
    {
        ElapsedSeconds = 0f;
        TotalSpawned = 0;
        ElitesSpawned = 0;
        _spawnTimer = 0f;
    }
}
