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

    /// <summary>Association d'un id de bestiaire à son prefab — pour les champions et le boss.</summary>
    [System.Serializable]
    public sealed class NamedPrefab
    {
        public string Id = "";
        public GameObject? Prefab;
    }

    [Header("Apparition")]
    [Tooltip("Prefab d'ennemi à instancier.")]
    public GameObject? EnemyPrefab;

    [Tooltip("Prefab d'orbe d'XP laissé par les ennemis à leur mort.")]
    public GameObject? XpOrbPrefab;

    [Tooltip("Prefabs dédiés des champions et du boss, par id de bestiaire.")]
    public NamedPrefab[] ChampionPrefabs = System.Array.Empty<NamedPrefab>();

    [Tooltip("Prefab de repli des champions sans classe dédiée (mini-boss globaux).")]
    public GameObject? MiniBossPrefab;

    [Tooltip("Distance d'apparition autour du joueur, hors champ.")]
    public float SpawnRadius = 700f;

    [Tooltip("Distance d'apparition du boss — dans le champ, puisqu'il ne se déplace pas.")]
    public float BossSpawnRadius = 380f;

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

    /// <summary>Prochaine vague massive (le « horde » périodique).</summary>
    private float _waveTimer = 25f;

    /// <summary>
    /// Prochain boss. Démarre à 4 s : le premier Noyau arrive donc <b>à l'instant</b> où le décompte
    /// atteint zéro, ce qui est exactement ce que le HUD annonce au joueur.
    /// </summary>
    private float _bossTimer = 4f;

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

        // ── Overtime : le temps imparti est écoulé → escalade ─────────────────
        // Les deux temps de référence sont DÉCOUPLÉS (OvertimeEscalation) : la densité reçoit une
        // accélération franche, le scaling des PV/dégâts une pente bien plus douce. Les confondre
        // déversait l'accélérateur destiné à la densité — déjà saturée — sur les statistiques, à
        // travers un terme quadratique (GDD §31).
        var gm = GameManager.Instance;
        bool overtime = gm?.Overtime ?? false;
        float otMin   = overtime ? gm!.OvertimeSeconds / 60f : 0f;

        float tDensity = minutes + OvertimeEscalation.DensityMinutes(otMin);
        float tStat    = minutes + OvertimeEscalation.StatMinutes(otMin);

        // Cadence pilotée par un INTERVALLE décroissant, comme sous Godot — et non par un débit :
        // les deux ne produisent pas la même distribution dans le temps.
        _spawnTimer += dt;
        if (_spawnTimer >= SpawnCurve.SpawnInterval(tDensity))
        {
            _spawnTimer = 0f;
            TrySpawnBatch(tDensity, tStat, SpawnCurve.BatchCount(tDensity));
        }

        // Vagues : surcharge périodique d'un gros essaim (le « horde » de Vampire Survivors).
        _waveTimer -= dt;
        if (_waveTimer <= 0f)
        {
            TrySpawnBatch(tDensity, tStat, SpawnCurve.WaveSize(tDensity, TotalSpawnMult));
            _waveTimer = overtime ? Mathf.Max(8f, 18f - otMin * 2f) : 25f;
        }

        if (!overtime) return;

        // ── Boucle de fin de partie ───────────────────────────────────────────
        _bossTimer -= dt;
        if (_bossTimer <= 0f)
        {
            SpawnBoss(tStat);
            _bossTimer = Mathf.Max(28f, 50f - otMin * 2f);
        }
    }

    private void TrySpawnBatch(float tDensity, float tStat, int count)
    {
        int cap = Mathf.Min(SpawnCurve.MaxEnemies(tDensity, TotalSpawnMult), MaxEnemies);

        for (int i = 0; i < count; i++)
        {
            if (EnemyBase.Active.Count >= cap) return;
            SpawnOne(tStat);
        }
    }

    private void SpawnOne(float minutes)
    {
        // Identité tirée des données : c'est ce qui donne 31 ennemis pour une poignée de
        // comportements. Sans bestiaire chargé, on retombe sur les valeurs du prefab générique.
        var def = PickDefinition(minutes);

        // Plafond d'exemplaires d'un champion : sans lui, un mini-boss réapparaît à chaque lot et
        // l'arène finit peuplée de champions plutôt que de faune.
        if (def != null && def.IsChampion && EnemyBase.CountOf(def.Id) >= def.MaxSimultaneous) return;

        Spawn(def, minutes, elitePromotion: true);
    }

    /// <summary>
    /// Fait apparaître un ennemi. <paramref name="def"/> nul retombe sur le prefab générique et ses
    /// valeurs d'inspecteur — le cœur de run reste jouable même sans bestiaire.
    /// </summary>
    private EnemyBase? Spawn(EnemyTable.EnemyDef? def, float minutes, bool elitePromotion)
    {
        var prefab = PrefabFor(def);
        if (prefab == null) return null;

        // Apparition sur un cercle autour du joueur : hors champ, mais jamais dans son dos immédiat.
        //
        // ⚠ Le boss est l'exception, et elle est nécessaire : son IA `boss_core` **ne poursuit pas**
        // — il tient sa position. Apparu au rayon ordinaire, il restait hors écran jusqu'à la fin de
        // la run, et le joueur concluait qu'il n'arrivait jamais. On le pose donc dans le champ.
        float radius = def != null && def.Ai == EnemyTable.AiType.BossCore ? BossSpawnRadius : SpawnRadius;

        float angle = Gd.Randf() * Mathf.PI * 2f;
        Vector2 offset = new(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
        Vector2 pos = (Vector2)Player.Instance!.transform.position + offset;

        pos.x = Mathf.Clamp(pos.x, -Arena.HalfWidth, Arena.HalfWidth);
        pos.y = Mathf.Clamp(pos.y, -Arena.HalfHeight, Arena.HalfHeight);

        var go = Instantiate(prefab, pos, Quaternion.identity, transform);

        // Unity conserve l'état actif du gabarit ; Godot, lui, produit TOUJOURS un nœud actif avec
        // Instantiate() + AddChild(). On reproduit la sémantique d'origine — sans quoi un gabarit
        // désactivé donne des ennemis qui existent, ne bougent pas et ne se signalent nulle part.
        go.SetActive(true);

        var enemy = go.GetComponent<EnemyBase>();
        if (enemy == null) return null;

        enemy.XpOrbPrefab = XpOrbPrefab;

        float hpPerMinute = HpScalingPerMinute;
        float dmgPerMinute = DamageScalingPerMinute;

        if (def != null)
        {
            enemy.DefId = def.Id;
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

        // Le boss prend l'incarnation de son biome — sprite, teinte et signature d'attaque.
        if (enemy is RustedCore boss)
        {
            boss.SetBiome(Biome ?? GameManager.Instance?.CurrentBiomeId);
            boss.AddPrefab = EnemyPrefab;
        }

        // Promotion en élite APRÈS le scaling : les multiplicateurs d'affixe s'appliquent aux
        // valeurs de la minute courante, pas aux valeurs de fiche. Jamais sur un champion : leur
        // TTK est calibré séparément.
        if (elitePromotion && (def == null || !def.IsChampion)) TryPromoteToElite(enemy, minutes);

        TotalSpawned++;
        return enemy;
    }

    /// <summary>
    /// Prefab d'un type d'ennemi : sa classe dédiée si elle existe, le repli des champions pour un
    /// mini-boss non encore porté, la faune générique sinon.
    /// </summary>
    private GameObject? PrefabFor(EnemyTable.EnemyDef? def)
    {
        if (def == null) return EnemyPrefab;

        foreach (var entry in ChampionPrefabs)
            if (entry != null && entry.Id == def.Id && entry.Prefab != null) return entry.Prefab;

        if (def.IsChampion && MiniBossPrefab != null) return MiniBossPrefab;
        return EnemyPrefab;
    }

    /// <summary>
    /// Fait apparaître le Noyau Rouillé — l'arrivée du boss est ce que <b>signifie</b> la fin du
    /// décompte. Le plafond d'exemplaires est respecté : un second boss avant la mort du premier
    /// rendait autrefois la mise à mort impossible.
    /// </summary>
    private void SpawnBoss(float tStat)
    {
        if (!_bestiary.TryGetValue(BossId, out var def)) return;
        if (EnemyBase.CountOf(BossId) >= Mathf.Max(1, def.MaxSimultaneous)) return;

        Spawn(def, tStat, elitePromotion: false);
        Debug.Log($"[EnemySpawner] Noyau Rouille invoque a t={ElapsedSeconds:F0}s.");
    }

    /// <summary>Id du boss de fin de niveau — condition de victoire des cinq biomes.</summary>
    public const string BossId = "rusted_core";

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
        _waveTimer = 25f;
        _bossTimer = 4f;
    }
}
