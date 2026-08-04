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

    private static readonly Dictionary<string, AudioClip?> _clips = new();
    private static readonly List<AudioSource> _voices = new();
    private static Transform? _root;

    /// <summary>Volume des effets, 0 à 1 — suit les réglages du joueur.</summary>
    public static float SfxVolume { get; set; } = 0.9f;

    /// <summary>Volume général, 0 à 1.</summary>
    public static float MasterVolume { get; set; } = 1f;

    /// <summary>Sons joués depuis le démarrage — observable par les bancs.</summary>
    public static int PlayedCount { get; private set; }

    /// <summary>
    /// Correction de mixage propre à un effet, en décibels.
    ///
    /// <para>Objectif : ne pas enterrer les tirs ennemis — ils restent le signal d'un danger qui
    /// arrive hors du champ — mais les faire passer <b>sous</b> ceux du joueur. Les aligner
    /// exactement (premier essai à −9 dB) ne suffisait pas : il faut mixer selon la
    /// <b>polyphonie réelle</b> (N sentinelles contre une arme), pas selon le niveau du fichier.</para>
    /// </summary>
    private static float MixGainDb(string sfxId) => sfxId switch
    {
        "sfx_weapon_sentinel_shoot"     => -12f,
        "sfx_enemy_sentinel_projectile" => -6f,
        _                               => 0f,
    };

    /// <summary>Joue un effet par son identifiant (nom de fichier sans extension).</summary>
    public static void PlaySfx(string sfxId, float pitchVariation = 0.06f)
    {
        var clip = Load(sfxId);
        if (clip == null) return;

        var voice = RentVoice();
        if (voice == null) return;   // toutes les voix occupées : mieux vaut un son perdu qu'un canal volé

        voice.clip = clip;
        voice.volume = Mathf.Clamp01(MasterVolume * SfxVolume) * DbToLinear(MixGainDb(sfxId));

        // Une légère variation de hauteur évite l'effet « mitraillette » quand le même son part
        // vingt fois en une seconde — c'est ce qui distingue une nuée d'un bug audio.
        voice.pitch = 1f + Random.Range(-pitchVariation, pitchVariation);
        voice.Play();

        PlayedCount++;
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

    private static AudioSource? RentVoice()
    {
        EnsureRoot();

        foreach (var voice in _voices)
            if (voice != null && !voice.isPlaying) return voice;

        if (_voices.Count >= MaxVoices) return null;

        var go = new GameObject($"Voice{_voices.Count}", typeof(AudioSource));
        go.transform.SetParent(_root, false);

        var source = go.GetComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;   // 2D : aucune atténuation par la distance

        _voices.Add(source);
        return source;
    }

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
        _root = null;
    }
}
