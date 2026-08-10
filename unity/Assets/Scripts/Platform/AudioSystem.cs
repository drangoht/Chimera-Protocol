using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Effets sonores — port d'<c>AudioSystem</c> (lot audio).
///
/// <para><b>Une réserve de sources, pas une par son.</b> Créer un <c>AudioSource</c> à chaque tir
/// produirait des centaines d'objets par seconde en nuée. Les sources sont donc recyclées, et leur
/// nombre est <b>plafonné</b> : au-delà, un son de plus n'ajoute rien d'audible et coûte un canal.</para>
///
/// <para>⚠ <b>Le mixage ne se résume pas au volume du fichier.</b> Les effets viennent de banques
/// différentes, jamais nivelées entre elles : leur niveau s'étale de −7,5 à −29,7 dB. Le cas criant,
/// mesuré côté Godot, est le tir de sentinelle — le plus fort de la banque, <b>+9,4 dB au-dessus du
/// tir du joueur</b>, et joué par chaque sentinelle à l'écran. Il se corrige ici, par une table, et
/// non en réencodant un asset CC0.</para>
/// </summary>
public static class AudioSystem
{
    /// <summary>Sources simultanées. Au-delà, le son suivant est ignoré plutôt que de voler un canal.</summary>
    private const int MaxVoices = 24;

    /// <summary>
    /// Copies simultanées d'un <b>même</b> effet. Au-delà, la plus ancienne est réutilisée : le son
    /// reste présent — la mort la plus récente s'entend toujours — il cesse seulement de s'empiler.
    ///
    /// <para><b>Pourquoi cette borne existe.</b> Signalé en jouant (2026-08-10) : le son sature « dès
    /// que les ennemis qui arrivent vers 2 min 30 sont touchés ». Ce sont les ennemis erratiques
    /// (11-17 PV, ils meurent d'un coup, six espèces de faune arrivent d'un bloc à la 3ᵉ minute) et
    /// ils jouent tous <c>sfx_enemy_drone_die</c> — <b>1,36 s et −12,0 dB RMS</b>, le plus long et le
    /// deuxième plus fort de toute la banque. Vingt morts par seconde × 1,36 s de queue, et les
    /// vingt-quatre voix portent la même explosion : ce n'est plus un son, c'est sa somme.</para>
    ///
    /// <para>C'est aussi une <b>divergence du portage</b> : le pool de Godot n'a que <b>huit</b>
    /// canaux et vole le plus ancien quand il déborde, ce qui bornait l'empilement sans que personne
    /// ait eu à y penser. Passer à vingt-quatre voix « pour ne perdre aucun son » a triplé
    /// l'amplitude atteignable — un plafond global généreux ne remplace pas un plafond
    /// <b>par son</b> : c'est la répétition du même clip qui s'additionne, pas la variété.</para>
    /// </summary>
    private const int MaxVoicesPerSfx = 3;

    private static readonly Dictionary<string, AudioClip?> _clips = new();
    private static readonly List<AudioSource> _voices = new();

    /// <summary>Effet porté par chaque voix, et instant de son déclenchement — index parallèles à <see cref="_voices"/>.</summary>
    private static readonly List<string> _voiceSfx = new();
    private static readonly List<float> _voiceStart = new();

    private static Transform? _root;

    /// <summary>Volume des effets, 0 à 1 — suit les réglages du joueur.</summary>
    public static float SfxVolume { get; set; } = 0.9f;

    /// <summary>Volume général, 0 à 1.</summary>
    public static float MasterVolume { get; set; } = 1f;

    /// <summary>Sons joués depuis le démarrage — observable par les bancs.</summary>
    public static int PlayedCount { get; private set; }

    private static readonly Dictionary<string, int> _playedById = new();

    /// <summary>
    /// Combien de fois un effet précis a été joué. Sert à attribuer un son à sa <b>source</b> : le
    /// compte global monte aussi quand le joueur encaisse un coup pendant la mesure, et une arme
    /// muette y passerait pour bruyante.
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Ce compteur ne prouve pas qu'un son est sorti</b> — le projet a déjà cru un jeu sonore
    /// parce que <see cref="PlayedCount"/> montait, alors qu'aucun <c>AudioListener</c> n'existait.
    /// Il dit seulement que l'appel a été fait avec un clip chargé ; c'est la moitié de la chaîne, et
    /// il se lit toujours à côté d'une vérification que le clip existe bien en asset.
    /// </remarks>
    public static int PlayedCountOf(string sfxId)
        => _playedById.TryGetValue(sfxId, out int n) ? n : 0;

    /// <summary>Le clip existe-t-il et se charge-t-il ? La <b>cause</b> d'un son absent, pas son symptôme.</summary>
    public static bool CanLoad(string sfxId) => Load(sfxId) != null;

    /// <summary>
    /// Copies de <paramref name="sfxId"/> réellement en vol à cet instant — ce que
    /// <see cref="PlayedCountOf"/> ne dit pas : lui compte des déclenchements, celui-ci mesure
    /// l'<b>empilement</b>, c'est-à-dire ce qui s'additionne à l'oreille et finit par saturer.
    /// </summary>
    /// <remarks>
    /// Sans sortie audio (banc <c>-nographics</c>, absence d'<c>AudioListener</c>), aucune voix ne
    /// joue jamais et ce relevé renvoie zéro. Zéro ne vaut donc pas « rien ne s'empile » mais
    /// « rien n'a été mesuré » — un banc doit distinguer les deux au lieu de conclure au vert.
    /// </remarks>
    public static int VoicesPlaying(string sfxId)
    {
        int n = 0;
        for (int i = 0; i < _voices.Count; i++)
            if (_voices[i] != null && _voices[i].isPlaying && _voiceSfx[i] == sfxId) n++;

        return n;
    }

    /// <summary>
    /// Correction de mixage propre à un effet, en décibels.
    ///
    /// <para>Objectif : ne pas enterrer les tirs ennemis — ils restent le signal d'un danger qui
    /// arrive hors du champ — mais les faire passer <b>sous</b> ceux du joueur. Les aligner
    /// exactement (premier essai à −9 dB) ne suffisait pas : il faut mixer selon la
    /// <b>polyphonie réelle</b> (N sentinelles contre une arme), pas selon le niveau du fichier.</para>
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Le niveau du fichier ne suffit pas à régler un son ici</b> : les clips sont importés avec
    /// <c>forceToMono</c> et <c>normalize</c>, donc Unity ramène leur crête à pleine échelle et efface
    /// toute atténuation gravée dans le WAV. Un son trop présent se corrige donc <b>dans cette
    /// table</b>, jamais en réencodant l'asset plus bas — c'est la même conclusion que côté Godot,
    /// pour une raison différente.
    /// </remarks>
    private static float MixGainDb(string sfxId) => sfxId switch
    {
        "sfx_weapon_sentinel_shoot"     => -12f,
        "sfx_enemy_sentinel_projectile" => -6f,

        // Salve de la Volée Multiple : le fichier est déjà plus doux que l'impulsion, mais l'arme
        // tire plusieurs fois par seconde en fin de run, par-dessus les autres armes équipées. C'est
        // la polyphonie qui décide du gain, pas le niveau du fichier — même raisonnement que pour le
        // tir de sentinelle. Résultat : ~7 dB sous l'impulsion à l'oreille.
        "sfx_weapon_scatter_shoot"      => -11f,

        // Mort des ennemis erratiques — le son le plus fort de la banque après le tir de sentinelle,
        // et joué par la faune la plus nombreuse et la plus fragile du jeu. Aligné sur l'AUTRE son de
        // mort de fourrage (sfx_enemy_swarm_die, −21,0 dB RMS) dont il ne devrait pas se distinguer
        // par le volume : ces deux-là racontent la même chose, un ennemi jetable qui tombe. Le
        // plafond par son borne l'empilement ; ce gain corrige le niveau du fichier lui-même.
        "sfx_enemy_drone_die"           => -9f,

        _                               => 0f,
    };

    /// <summary>Joue un effet par son identifiant (nom de fichier sans extension).</summary>
    public static void PlaySfx(string sfxId, float pitchVariation = 0.06f)
    {
        var clip = Load(sfxId);
        if (clip == null) return;

        var voice = RentVoice(sfxId);
        if (voice == null) return;   // toutes les voix occupées : mieux vaut un son perdu qu'un canal volé

        voice.clip = clip;
        voice.volume = Mathf.Clamp01(MasterVolume * SfxVolume) * DbToLinear(MixGainDb(sfxId));

        // Une légère variation de hauteur évite l'effet « mitraillette » quand le même son part
        // vingt fois en une seconde — c'est ce qui distingue une nuée d'un bug audio.
        voice.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        voice.Play();

        PlayedCount++;
        _playedById[sfxId] = PlayedCountOf(sfxId) + 1;
    }

    /// <summary>Charge (et met en cache) un effet. Un identifiant inconnu est signalé une seule fois.</summary>
    private static AudioClip? Load(string sfxId)
    {
        if (_clips.TryGetValue(sfxId, out var cached)) return cached;

        var clip = Resources.Load<AudioClip>("Audio/sfx/" + sfxId);
        if (clip == null)
            Debug.LogError($"[AudioSystem] son introuvable : 'Audio/sfx/{sfxId}'.");

        _clips[sfxId] = clip;
        return clip;
    }

    /// <summary>
    /// Réserve une voix pour <paramref name="sfxId"/>, en bornant le nombre de copies simultanées de
    /// ce <b>même</b> effet (<see cref="MaxVoicesPerSfx"/>).
    /// </summary>
    /// <remarks>
    /// L'ordre des trois cas n'est pas indifférent : le plafond par son passe <b>avant</b> la
    /// recherche d'une voix libre. L'inverse — chercher une voix libre d'abord — laisserait vingt
    /// morts simultanées prendre vingt voix, puisqu'il en reste toujours de libres au moment où on
    /// regarde ; le plafond ne mordrait qu'une fois la réserve entière consommée, c'est-à-dire trop
    /// tard.
    /// </remarks>
    private static AudioSource? RentVoice(string sfxId)
    {
        EnsureRoot();

        // 1. Trop de copies de CE son déjà en vol → réutiliser la plus ancienne d'entre elles. Le son
        //    reste audible (l'événement le plus récent l'emporte), il cesse simplement de s'additionner.
        int same = 0, oldest = -1;
        float oldestStart = float.MaxValue;

        for (int i = 0; i < _voices.Count; i++)
        {
            if (_voices[i] == null || !_voices[i].isPlaying || _voiceSfx[i] != sfxId) continue;

            same++;
            if (_voiceStart[i] < oldestStart) { oldestStart = _voiceStart[i]; oldest = i; }
        }

        if (same >= MaxVoicesPerSfx && oldest >= 0) return Claim(oldest);

        // 2. Une voix libre.
        for (int i = 0; i < _voices.Count; i++)
            if (_voices[i] != null && !_voices[i].isPlaying) return Claim(i);

        // 3. En créer une, tant que la réserve n'est pas pleine.
        if (_voices.Count >= MaxVoices) return null;

        var go = new GameObject($"Voice{_voices.Count}", typeof(AudioSource));
        go.transform.SetParent(_root, false);

        var source = go.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D : aucune atténuation par la distance

        _voices.Add(source);
        _voiceSfx.Add(sfxId);
        _voiceStart.Add(Now);
        return source;

        AudioSource Claim(int i)
        {
            _voiceSfx[i] = sfxId;
            _voiceStart[i] = Now;
            return _voices[i]!;
        }
    }

    /// <summary>
    /// Horloge des voix. <b>Non affectée par l'échelle de temps</b> : le jeu met <c>timeScale</c> à
    /// zéro pendant un passage de niveau, et une horloge gelée figerait l'âge de toutes les voix —
    /// « la plus ancienne » deviendrait alors indécidable au moment précis où la nuée reprend.
    /// </summary>
    private static float Now => Time.unscaledTime;

    private static void EnsureRoot()
    {
        if (_root != null) return;

        var go = new GameObject("[Audio]");
        Object.DontDestroyOnLoad(go);
        _root = go.transform;
    }

    private static float DbToLinear(float db) => db == 0f ? 1f : Mathf.Pow(10f, db / 20f);

    /// <summary>Oublie les voix — à appeler si la scène qui les portait a disparu.</summary>
    public static void Reset()
    {
        _voices.Clear();
        _voiceSfx.Clear();
        _voiceStart.Clear();
        _root = null;
    }
}
