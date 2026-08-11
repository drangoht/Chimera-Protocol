using UnityEngine;

/// <summary>
/// Musique adaptative — port de <c>MusicDirector</c> (lot audio).
///
/// <para>Chaque biome a <b>deux versions du même morceau</b> (couplet calme, refrain de combat) et le
/// jeu passe de l'une à l'autre par <b>fondu croisé</b>, selon l'intensité de l'action. Un thème de
/// boss commun prend le dessus quand le Noyau apparaît.</para>
///
/// <para>⚠ <b>Jamais en superposition.</b> Ces pistes ne sont pas synchronisées entre elles : les
/// jouer ensemble ne donnerait pas une couche de plus mais deux morceaux décalés. Le fondu croise
/// donc les volumes, il n'additionne pas les musiques.</para>
///
/// <para>Toutes les décisions chiffrées — calcul de l'intensité, seuils d'entrée et de sortie du
/// combat, durée de maintien, durée des fondus — viennent de <see cref="MusicIntensity"/> (logique
/// pure, partagée avec Godot).</para>
/// </summary>
public sealed class MusicDirector : MonoBehaviour
{
    public static MusicDirector? Instance { get; private set; }

    /// <summary>Intensité lissée, entre 0 et 1 — observable pour les bancs et le diagnostic.</summary>
    public float Intensity { get; private set; }

    /// <summary>Couche au premier plan. La décision vient de <see cref="MusicIntensity.Select"/>.</summary>
    private MusicLayer _layer = MusicLayer.Calm;

    /// <summary>Piste dominante : « calm », « combat » ou « boss » — pour le banc et le diagnostic.</summary>
    public string CurrentTrack => NameOf(_layer);

    private static string NameOf(MusicLayer layer) => layer switch
    {
        MusicLayer.Boss   => "boss",
        MusicLayer.Combat => "combat",
        _                 => "calm",
    };

    /// <summary>Volume de la musique, 0 à 1.</summary>
    public float MusicVolume { get; set; } = 0.8f;

    private AudioSource? _calm;
    private AudioSource? _combat;
    private AudioSource? _boss;

    private float _holdLeft;
    private string _biome = "sanctuaire";

    private void Awake()
    {
        Instance = this;

        _calm   = CreateSource("Calm");
        _combat = CreateSource("Combat");
        _boss   = CreateSource("Boss");
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Charge les pistes du biome et démarre la lecture.</summary>
    public void PlayBiome(string biomeId)
    {
        _biome = biomeId;

        Assign(_calm,   $"Audio/music/music_run_{biomeId}_calm");
        Assign(_combat, $"Audio/music/music_run_{biomeId}_combat");
        Assign(_boss,   "Audio/music/music_run_boss");

        // Les trois démarrent ENSEMBLE et ne s'arrêtent plus : c'est ce qui permet au fondu de
        // basculer instantanément sans réamorcer une piste au milieu d'une mesure. Seuls les volumes
        // bougent — deux pistes sont simplement inaudibles.
        Restart(_calm);
        Restart(_combat);
        Restart(_boss);

        Intensity = 0f;
        _layer = MusicLayer.Calm;
        _holdLeft = 0f;
        ApplyVolumes(1f, 0f, 0f);
    }

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;

        var gm = GameManager.Instance;
        var player = Player.Instance;
        if (gm == null || player == null) return;

        float healthRatio = player.Stats.MaxHp > 0f ? player.Stats.CurrentHp / player.Stats.MaxHp : 1f;
        float target = MusicIntensity.Compute(EnemyBase.Active.Count, gm.RunTime, healthRatio);

        Intensity = MusicIntensity.Smooth(Intensity, target, dt);

        UpdateLayer(dt);
        UpdateMix(dt);
    }

    /// <summary>
    /// Choisit la couche au premier plan, avec <b>hystérésis et durée de maintien</b>. Sans elles,
    /// l'intensité oscillant autour du seuil ferait clignoter la musique plusieurs fois par minute —
    /// le défaut le plus fatigant qu'une musique adaptative puisse produire.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Les seuils ne sont pas relus ici.</b> <see cref="MusicIntensity.Select"/> porte
    /// l'hystérésis (entrer au-dessus de <c>CombatEnter</c>, ne ressortir que sous <c>CombatExit</c>)
    /// et la priorité du boss ; ce corps ne garde que ce qui demande une horloge, le <b>verrou de
    /// durée</b>. Cette machine à états existait ici en double, réécrite à la main, pendant que la
    /// version testée dormait — d'où <c>Select</c> jamais appelée par le jeu.
    ///
    /// <para>Le boss ne patiente pas : son apparition doit s'entendre sur-le-champ.</para>
    /// </remarks>
    private void UpdateLayer(float dt)
    {
        bool bossAlive = false;
        foreach (var enemy in EnemyBase.Active)
            if (enemy is RustedCore && !enemy.IsDead) { bossAlive = true; break; }

        if (_holdLeft > 0f) _holdLeft -= dt;

        var wanted = MusicIntensity.Select(_layer, Intensity, bossAlive);
        if (wanted == _layer) return;

        bool urgent = wanted == MusicLayer.Boss || _layer == MusicLayer.Boss;
        if (!urgent && _holdLeft > 0f) return;

        _layer = wanted;
        _holdLeft = MusicIntensity.MinHoldSec;
    }

    private void UpdateMix(float dt)
    {
        float fade = _layer == MusicLayer.Boss
            ? MusicIntensity.BossCrossfadeSec
            : MusicIntensity.CrossfadeSec;
        float step = fade > 0f ? dt / fade : 1f;

        Approach(_calm,   _layer == MusicLayer.Calm   ? 1f : 0f, step);
        Approach(_combat, _layer == MusicLayer.Combat ? 1f : 0f, step);
        Approach(_boss,   _layer == MusicLayer.Boss   ? 1f : 0f, step);
    }

    private void Approach(AudioSource? source, float target, float step)
    {
        if (source == null) return;

        float ceiling = Mathf.Clamp01(MusicVolume * AudioSystem.MasterVolume);
        source.volume = Mathf.MoveTowards(source.volume, target * ceiling, step * ceiling);
    }

    private void ApplyVolumes(float calm, float combat, float boss)
    {
        float ceiling = Mathf.Clamp01(MusicVolume * AudioSystem.MasterVolume);
        if (_calm   != null) _calm.volume   = calm * ceiling;
        if (_combat != null) _combat.volume = combat * ceiling;
        if (_boss   != null) _boss.volume   = boss * ceiling;
    }

    private AudioSource CreateSource(string name)
    {
        var go = new GameObject("Music" + name, typeof(AudioSource));
        go.transform.SetParent(transform, false);

        var source = go.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        source.spatialBlend = 0f;
        source.volume = 0f;
        return source;
    }

    private static void Assign(AudioSource? source, string resourcePath)
    {
        if (source == null) return;

        var clip = Resources.Load<AudioClip>(resourcePath);
        if (clip == null)
        {
            Debug.LogError($"[MusicDirector] piste introuvable : '{resourcePath}'.");
            return;
        }
        source.clip = clip;
    }

    private static void Restart(AudioSource? source)
    {
        if (source == null || source.clip == null) return;

        source.volume = 0f;
        source.Play();
    }

    /// <summary>Joue une piste unique (menu, hub) en coupant l'adaptatif.</summary>
    public void PlaySingle(string resourceName)
    {
        Assign(_calm, "Audio/music/" + resourceName);
        Restart(_calm);

        if (_combat != null) _combat.Stop();
        if (_boss != null) _boss.Stop();

        _layer = MusicLayer.Calm;
        ApplyVolumes(1f, 0f, 0f);
    }
}
