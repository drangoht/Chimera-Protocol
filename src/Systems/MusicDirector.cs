using Godot;
using System.Collections.Generic;

/// <summary>
/// Singleton AutoLoad — musique adaptative pendant une run.
///
/// Chaque biome fournit deux versions du même morceau : <c>music_run_&lt;biome&gt;_calm</c>
/// (couplet, riff en retenue) et <c>music_run_&lt;biome&gt;_combat</c> (refrain, tout
/// ouvert). S'y ajoute <c>music_run_boss.ogg</c>, commun à tous les biomes. Une seule
/// piste est audible à la fois ; la bascule se fait par fondu croisé piloté par
/// <see cref="MusicIntensity"/>, jamais par une coupure. Direction sonore et prompts :
/// <c>docs/AUDIO_AI_PROMPTS.md</c>.
///
/// API publique :
///   PlayBiome(biomeId, fadeInSec)  — démarre la musique d'un biome
///   Stop(fadeOutSec)               — arrête tout (retour aux menus)
///   SetBossActive(bool)            — force l'entrée/sortie du thème de boss
///   IsActive / Intensity / Layer   — état courant (HUD de debug, tests)
///
/// Dégradation gracieuse : si les pistes d'un biome sont absentes, rien n'est joué
/// et <see cref="PlayBiome"/> renvoie false — l'appelant retombe alors sur
/// <see cref="AudioSystem.PlayMusic"/> et le jeu reste jouable. Le thème de boss,
/// lui, est facultatif : son absence ne fait que le supprimer du dispositif.
/// </summary>
public partial class MusicDirector : Node
{
    public static MusicDirector Instance { get; private set; } = null!;

    private const string MusicBasePath = "res://assets/audio/music/";

    /// <summary>Identifiant de la piste de boss, partagée par tous les biomes.</summary>
    private const string BossTrackId = "music_run_boss";

    /// <summary>L'état de la run est interrogé 4×/s : GetNodesInGroup alloue, inutile de le faire à 60 fps.</summary>
    private const float SampleIntervalSec = 0.25f;

    private static readonly MusicLayer[] AllLayers =
    {
        MusicLayer.Calm, MusicLayer.Combat, MusicLayer.Boss,
    };

    private readonly Dictionary<MusicLayer, AudioStreamPlayer> _players = new();
    private readonly Dictionary<MusicLayer, float> _weights = new();
    private readonly Dictionary<string, AudioStream?> _cache = new();

    private float _intensity;        // valeur lissée, décide de la piste
    private float _targetIntensity;  // dernière valeur échantillonnée
    private MusicLayer _layer = MusicLayer.Calm;
    private float _holdTimer;        // temps passé sur la piste courante
    private bool _bossActive;
    private bool _bossOverride;   // true = SetBossActive pilote, pas la détection auto
    private bool _hasBossTrack;
    private float _sampleTimer;
    private float _masterFade = 1f;      // fondu global d'entrée/sortie
    private bool _stopping;              // fondu sortant en cours
    private float _fadeOutRate = 1f;     // unités de _masterFade par seconde
    private Node2D? _player;             // cache du joueur (évite un GetNodesInGroup/frame)

    /// <summary>Biome dont les pistes sont chargées, null si inactif.</summary>
    public string? CurrentBiomeId { get; private set; }

    /// <summary>Vrai quand la musique de run tourne.</summary>
    public bool IsActive => CurrentBiomeId != null;

    /// <summary>Intensité lissée courante, dans [0, 1] (exposée pour le debug/HUD).</summary>
    public float Intensity => _intensity;

    /// <summary>Piste au premier plan (exposée pour le debug/HUD).</summary>
    public MusicLayer Layer => _layer;

    // -------------------------------------------------------------------------
    // Cycle de vie
    // -------------------------------------------------------------------------

    public override void _Ready()
    {
        Instance = this;

        // Même raison que dans AudioSystem : sans Always, les AudioStreamPlayer
        // enfants sont suspendus dès que GetTree().Paused passe à true et la
        // musique se couperait à chaque modale (level-up, pause, Assimilation).
        ProcessMode = ProcessModeEnum.Always;

        foreach (var layer in AllLayers)
        {
            var p = new AudioStreamPlayer
            {
                Name = $"Layer_{layer}",
                Bus = "Master",
                VolumeDb = MusicIntensity.Silence,
            };
            AddChild(p);
            _players[layer] = p;
            _weights[layer] = 0f;
        }
    }

    // -------------------------------------------------------------------------
    // API publique
    // -------------------------------------------------------------------------

    /// <summary>
    /// Démarre la musique d'un biome, sur la piste calme. Renvoie false si les
    /// pistes sont absentes (l'appelant doit alors se rabattre sur une piste
    /// classique).
    /// </summary>
    public bool PlayBiome(string biomeId, float fadeInSec = 2.0f)
    {
        if (CurrentBiomeId == biomeId) return true;

        var calm = LoadTrack(BiomeTrackId(biomeId, MusicLayer.Calm));
        var combat = LoadTrack(BiomeTrackId(biomeId, MusicLayer.Combat));

        if (calm == null || combat == null)
        {
            GD.Print($"[MusicDirector] Pistes absentes pour '{biomeId}' " +
                     $"({BiomeTrackId(biomeId, MusicLayer.Calm)}) — repli sur la musique simple.");
            return false;
        }

        var boss = LoadTrack(BossTrackId);
        _hasBossTrack = boss != null;

        _players[MusicLayer.Calm].Stream = calm;
        _players[MusicLayer.Combat].Stream = combat;
        if (boss != null) _players[MusicLayer.Boss].Stream = boss;

        // Calme et combat tournent ensemble en permanence : basculer d'une piste
        // à l'autre reprend la musique là où elle en est plutôt que de renvoyer
        // le joueur au premier temps du morceau à chaque vague.
        foreach (var layer in AllLayers) _weights[layer] = 0f;
        _weights[MusicLayer.Calm] = 1f;

        _players[MusicLayer.Calm].Play();
        _players[MusicLayer.Combat].Play();
        // Le thème de boss, lui, n'est PAS lancé ici : il doit démarrer à son
        // premier temps quand le boss arrive, pas être pris en cours de route.

        CurrentBiomeId = biomeId;
        _layer = MusicLayer.Calm;
        _intensity = 0f;
        _targetIntensity = 0f;
        _holdTimer = 0f;
        _bossActive = false;
        _bossOverride = false;
        _masterFade = fadeInSec > 0f ? 0f : 1f;
        _stopping = false;
        _sampleTimer = 0f;
        _player = null;

        ApplyGains();

        // La musique adaptative remplace la piste simple : couper l'autre canal
        AudioSystem.Instance?.StopMusic(fadeInSec);

        return true;
    }

    /// <summary>Arrête toutes les pistes avec un fondu sortant.</summary>
    public void Stop(float fadeOutSec = 1.0f)
    {
        if (!IsActive && !_stopping) return;

        CurrentBiomeId = null;
        _player = null;

        if (fadeOutSec <= 0f)
        {
            _stopping = false;
            _masterFade = 0f;
            StopAllPlayers();
            return;
        }

        // `_stopping` maintient `_Process` actif pendant la descente : sans lui,
        // le fondu ne serait jamais *appliqué* aux players (la boucle sort dès
        // que CurrentBiomeId est null) et la musique se couperait net.
        _stopping = true;
        _fadeOutRate = 1f / fadeOutSec;
    }

    /// <summary>
    /// Force l'entrée (ou la sortie) du thème de boss, en prenant le pas sur la
    /// détection automatique. Appeler <see cref="ClearBossOverride"/> pour rendre
    /// la main à la détection automatique.
    /// </summary>
    public void SetBossActive(bool active)
    {
        _bossOverride = true;
        _bossActive = active;
    }

    /// <summary>Rend la main à la détection automatique de boss.</summary>
    public void ClearBossOverride() => _bossOverride = false;

    // -------------------------------------------------------------------------
    // Boucle
    // -------------------------------------------------------------------------

    public override void _Process(double delta)
    {
        float dt = (float)delta;

        // Fondu sortant : on continue d'appliquer les gains jusqu'au silence
        if (_stopping)
        {
            _masterFade = Mathf.Max(0f, _masterFade - dt * _fadeOutRate);
            ApplyGains();

            if (_masterFade <= 0f)
            {
                _stopping = false;
                StopAllPlayers();
            }
            return;
        }

        if (!IsActive) return;

        // Fondu d'entrée global
        if (_masterFade < 1f)
            _masterFade = Mathf.Min(1f, _masterFade + dt / 2.0f);

        // L'état de la run est ÉCHANTILLONNÉ 4×/s (GetNodesInGroup alloue), mais le
        // lissage tourne à CHAQUE frame : lisser au rythme de l'échantillonnage
        // ferait bouger l'intensité par paliers de 250 ms.
        _sampleTimer -= dt;
        if (_sampleTimer <= 0f)
        {
            _sampleTimer = SampleIntervalSec;
            _targetIntensity = SampleTargetIntensity();
        }

        _intensity = MusicIntensity.Smooth(_intensity, _targetIntensity, dt);

        UpdateLayer(dt);
        ApplyGains();
    }

    /// <summary>
    /// Choisit la piste au premier plan et fait progresser le fondu croisé.
    /// </summary>
    private void UpdateLayer(float dt)
    {
        _holdTimer += dt;

        bool boss = _bossActive && _hasBossTrack;
        var desired = MusicIntensity.Select(_layer, _intensity, boss);

        // Le boss entre et sort sans délai (c'est un événement, pas une tendance) ;
        // pour tout le reste, une piste garde la main un minimum de temps, sinon
        // une accalmie de trois secondes suffirait à déclencher un aller-retour.
        bool bossTransition = desired == MusicLayer.Boss || _layer == MusicLayer.Boss;
        if (desired != _layer && (bossTransition || _holdTimer >= MusicIntensity.MinHoldSec))
        {
            // Le thème de boss repart de son premier temps à chaque combat.
            if (desired == MusicLayer.Boss)
                _players[MusicLayer.Boss].Play();

            _layer = desired;
            _holdTimer = 0f;
        }

        float fade = bossTransition ? MusicIntensity.BossCrossfadeSec : MusicIntensity.CrossfadeSec;
        foreach (var layer in AllLayers)
        {
            float target = layer == _layer ? 1f : 0f;
            _weights[layer] = MusicIntensity.Approach(_weights[layer], target, dt, fade);
        }

        // Boss retombé à zéro : on libère le lecteur pour qu'il reprenne au début
        // au prochain combat (sinon le thème entrerait en plein milieu).
        if (_layer != MusicLayer.Boss && _weights[MusicLayer.Boss] <= 0f
            && _players[MusicLayer.Boss].Playing)
            _players[MusicLayer.Boss].Stop();
    }

    /// <summary>
    /// Lit l'état de la run pour en déduire l'intensité cible, et détecte au
    /// passage la présence d'un boss ou mini-boss.
    /// </summary>
    /// <remarks>
    /// La détection se fait ici plutôt que par un signal du spawner : elle reste
    /// juste quel que soit le chemin d'apparition (vague d'overtime, hook
    /// <c>--debug-boss</c>) et se réarme toute seule quand le boss meurt.
    /// </remarks>
    private float SampleTargetIntensity()
    {
        var tree = GetTree();
        if (tree == null) return _intensity;

        var enemies = tree.GetNodesInGroup(Constants.GroupEnemies);

        if (!_bossOverride)
        {
            _bossActive = false;
            foreach (var node in enemies)
            {
                if (node is EnemyBase e && (e.AssimIsBoss || e.AssimIsMiniBoss))
                {
                    _bossActive = true;
                    break;
                }
            }
        }

        float elapsed = RunStatsTracker.Instance?.ElapsedSeconds ?? 0f;
        float healthRatio = ReadHealthRatio(tree);

        return MusicIntensity.Compute(enemies.Count, elapsed, healthRatio);
    }

    private float ReadHealthRatio(SceneTree tree)
    {
        // Le joueur est re-cherché seulement s'il a disparu (mort, changement de
        // scène) : GetNodesInGroup à chaque échantillon serait du gaspillage.
        if (_player == null || !IsInstanceValid(_player))
        {
            var found = tree.GetFirstNodeInGroup(Constants.GroupPlayer);
            _player = found as Node2D;
        }

        if (_player is Player p && p.Stats != null && p.Stats.MaxHp > 0f)
            return p.Stats.CurrentHp / p.Stats.MaxHp;

        return 1f;
    }

    private void ApplyGains()
    {
        float master = (AudioSystem.Instance?.MusicVolume ?? 1f) * _masterFade;
        float masterDb = master <= 0.001f
            ? MusicIntensity.Silence
            : Mathf.LinearToDb(master);

        foreach (var layer in AllLayers)
        {
            float db = MusicIntensity.WeightToDb(_weights[layer]);
            _players[layer].VolumeDb = db <= MusicIntensity.Silence
                ? MusicIntensity.Silence
                : db + masterDb;
        }
    }

    private void StopAllPlayers()
    {
        foreach (var p in _players.Values) p.Stop();
        foreach (var layer in AllLayers) _weights[layer] = 0f;
    }

    // -------------------------------------------------------------------------
    // Chargement
    // -------------------------------------------------------------------------

    private static string BiomeTrackId(string biomeId, MusicLayer layer) =>
        $"music_run_{biomeId}_{layer.ToString().ToLowerInvariant()}";

    private AudioStream? LoadTrack(string trackId)
    {
        if (_cache.TryGetValue(trackId, out var cached)) return cached;

        string path = $"{MusicBasePath}{trackId}.ogg";
        AudioStream? stream = null;

        // ResourceLoader.Exists (et non FileAccess) : à l'export, seul le
        // fichier importé est embarqué (cf. AudioSystem.TryLoadStream).
        if (ResourceLoader.Exists(path))
        {
            stream = GD.Load<AudioStream>(path);

            // Les OGG sont importés avec loop=false par défaut : sans bouclage
            // natif, la piste s'arrêterait au bout d'un tour et le fondu croisé
            // n'aurait plus rien à ramener.
            if (stream is AudioStreamOggVorbis ogg)
                ogg.Loop = true;
        }

        _cache[trackId] = stream;
        return stream;
    }
}
