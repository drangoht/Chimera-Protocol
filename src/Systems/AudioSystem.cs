using Godot;
using System.Collections.Generic;

/// <summary>
/// Singleton AutoLoad — gestion centralisee de l'audio du jeu.
/// Deux canaux separes : musique (fade in/out, loop) et SFX (pool de 8 AudioStreamPlayer).
/// Tous les fichiers audio sont optionnels au chargement : si un fichier est absent,
/// un warning est emis dans la console mais le jeu ne crash pas.
/// API publique :
///   PlayMusic(trackId, fadeInSec)   — demarre une piste musicale avec fondu optionnel
///   StopMusic(fadeOutSec)           — arrete la musique avec fondu optionnel
///   PlaySfx(sfxId)                  — joue un SFX depuis le pool (polyphonie 8 canaux)
///   MusicVolume / SfxVolume         — proprietes de volume (0.0 a 1.0)
/// </summary>
public partial class AudioSystem : Node
{
    public static AudioSystem Instance { get; private set; } = null!;

    // -------------------------------------------------------------------------
    // Constantes
    // -------------------------------------------------------------------------

    private const string MusicBasePath = "res://assets/audio/music/";
    private const string SfxBasePath   = "res://assets/audio/sfx/";
    private const int    SfxPoolSize   = 8;

    // -------------------------------------------------------------------------
    // Nœuds audio
    // -------------------------------------------------------------------------

    private AudioStreamPlayer _musicPlayer  = null!;
    private AudioStreamPlayer _musicFadeOut = null!; // Second canal pour le fondu enchaîne
    private readonly AudioStreamPlayer[] _sfxPool = new AudioStreamPlayer[SfxPoolSize];

    // -------------------------------------------------------------------------
    // Etat interne
    // -------------------------------------------------------------------------

    private float _musicVolume = 1.0f;
    private float _sfxVolume   = 1.0f;
    private int   _sfxPoolIndex = 0;   // Index cyclique du pool SFX

    // Cache des streams charges (evite de recharger depuis le disque)
    private readonly Dictionary<string, AudioStream?> _musicCache = new();
    private readonly Dictionary<string, AudioStream?> _sfxCache   = new();

    // -------------------------------------------------------------------------
    // Proprietes publiques
    // -------------------------------------------------------------------------

    /// <summary>Volume de la musique (0.0 = silence, 1.0 = volume max). Applique immediatement.</summary>
    public float MusicVolume
    {
        get => _musicVolume;
        set
        {
            _musicVolume = Mathf.Clamp(value, 0f, 1f);
            _musicPlayer.VolumeDb  = LinearToDb(_musicVolume);
            _musicFadeOut.VolumeDb = LinearToDb(_musicVolume);
        }
    }

    /// <summary>Volume des SFX (0.0 = silence, 1.0 = volume max). Applique aux prochains PlaySfx.</summary>
    public float SfxVolume
    {
        get => _sfxVolume;
        set => _sfxVolume = Mathf.Clamp(value, 0f, 1f);
    }

    /// <summary>Id de la piste musicale en cours de lecture (null si aucune).</summary>
    public string? CurrentTrackId { get; private set; }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    public override void _Ready()
    {
        Instance = this;

        // Canal musique principal
        _musicPlayer           = new AudioStreamPlayer();
        _musicPlayer.Name      = "MusicPlayer";
        _musicPlayer.Bus       = "Master";
        _musicPlayer.VolumeDb  = LinearToDb(_musicVolume);
        AddChild(_musicPlayer);

        // Canal musique pour le fondu sortant
        _musicFadeOut          = new AudioStreamPlayer();
        _musicFadeOut.Name     = "MusicFadeOut";
        _musicFadeOut.Bus      = "Master";
        _musicFadeOut.VolumeDb = LinearToDb(_musicVolume);
        AddChild(_musicFadeOut);

        // Pool SFX
        for (int i = 0; i < SfxPoolSize; i++)
        {
            var player      = new AudioStreamPlayer();
            player.Name     = $"SfxPool_{i}";
            player.Bus      = "Master";
            _sfxPool[i]     = player;
            AddChild(player);
        }

        // Pre-chargement des SFX les plus utilises
        PreloadSfx("sfx_xp_collect");
        PreloadSfx("sfx_player_hit");
        PreloadSfx("sfx_weapon_impulse_shoot");
        PreloadSfx("sfx_enemy_swarm_die");
        PreloadSfx("sfx_enemy_drone_die");
        PreloadSfx("sfx_levelup");
        PreloadSfx("sfx_fusion_evolve");
        PreloadSfx("sfx_ui_button");
    }

    // -------------------------------------------------------------------------
    // API musique
    // -------------------------------------------------------------------------

    /// <summary>
    /// Demarre la lecture d'une piste musicale.
    /// Si une autre piste est en cours, effectue un fondu enchaîne.
    /// Si la meme piste est deja en cours, ne fait rien.
    /// </summary>
    /// <param name="trackId">Id de la piste (ex. "music_menu"). Le fichier doit etre dans assets/audio/music/.</param>
    /// <param name="fadeInSec">Duree du fondu d'entree en secondes.</param>
    public void PlayMusic(string trackId, float fadeInSec = 0.5f)
    {
        if (CurrentTrackId == trackId) return;

        var stream = LoadMusic(trackId);
        if (stream == null)
        {
            GD.PrintErr($"[AudioSystem] Musique introuvable : {trackId} ({MusicBasePath}{trackId}.ogg/.wav)");
            return;
        }

        // Transfere la lecture courante sur le canal de fondu sortant
        if (_musicPlayer.Playing)
        {
            // Copie le stream et la position de lecture vers le canal sortant
            _musicFadeOut.Stream   = _musicPlayer.Stream;
            _musicFadeOut.VolumeDb = _musicPlayer.VolumeDb;
            _musicFadeOut.Play(_musicPlayer.GetPlaybackPosition());
            _musicPlayer.Stop();

            // Fondu sortant sur _musicFadeOut
            FadeOut(_musicFadeOut, fadeInSec);
        }

        // Demarre la nouvelle piste sur le canal principal
        _musicPlayer.Stream   = stream;
        _musicPlayer.VolumeDb = LinearToDb(0f); // Commence silencieux
        _musicPlayer.Play();

        CurrentTrackId = trackId;

        // Fondu entrant
        FadeIn(_musicPlayer, fadeInSec, _musicVolume);
    }

    /// <summary>Arrete la musique avec un fondu sortant optionnel.</summary>
    /// <param name="fadeOutSec">Duree du fondu de sortie en secondes.</param>
    public void StopMusic(float fadeOutSec = 0.5f)
    {
        if (!_musicPlayer.Playing) return;

        CurrentTrackId = null;
        FadeOut(_musicPlayer, fadeOutSec);
    }

    // -------------------------------------------------------------------------
    // API SFX
    // -------------------------------------------------------------------------

    /// <summary>
    /// Joue un SFX depuis le pool de canaux.
    /// Si tous les canaux sont occupes, le canal le plus ancien est reutilise (interruption).
    /// Null-safe sur le fichier : si le fichier est absent, log warning et ne crash pas.
    /// </summary>
    /// <param name="sfxId">Id du SFX (ex. "sfx_weapon_impulse_shoot"). Fichier dans assets/audio/sfx/.</param>
    public void PlaySfx(string sfxId)
    {
        var stream = LoadSfx(sfxId);
        if (stream == null)
        {
            GD.PrintErr($"[AudioSystem] SFX introuvable : {sfxId} ({SfxBasePath}{sfxId}.wav)");
            return;
        }

        // Cherche un canal libre, sinon reutilise le prochain canal cyclique
        var player = FindFreeSfxPlayer();
        if (player == null)
        {
            player = _sfxPool[_sfxPoolIndex % SfxPoolSize];
            _sfxPoolIndex++;
        }

        player.Stream   = stream;
        player.VolumeDb = LinearToDb(_sfxVolume);
        player.Play();
    }

    // -------------------------------------------------------------------------
    // Chargement des assets (avec cache et null-guard)
    // -------------------------------------------------------------------------

    private AudioStream? LoadMusic(string trackId)
    {
        if (_musicCache.TryGetValue(trackId, out var cached)) return cached;

        // OGG en priorite (format final), WAV accepte comme placeholder de dev
        string oggPath = $"{MusicBasePath}{trackId}.ogg";
        string wavPath = $"{MusicBasePath}{trackId}.wav";
        var stream = TryLoadStream(oggPath) ?? TryLoadStream(wavPath);

        _musicCache[trackId] = stream;
        return stream;
    }

    private AudioStream? LoadSfx(string sfxId)
    {
        if (_sfxCache.TryGetValue(sfxId, out var cached)) return cached;

        // Essaie WAV en premier, puis OGG
        string wavPath = $"{SfxBasePath}{sfxId}.wav";
        string oggPath = $"{SfxBasePath}{sfxId}.ogg";

        var stream = TryLoadStream(wavPath) ?? TryLoadStream(oggPath);

        _sfxCache[sfxId] = stream;
        return stream;
    }

    private void PreloadSfx(string sfxId)
    {
        // Appel a LoadSfx pour mettre en cache ; le warning est emis si absent
        LoadSfx(sfxId);
    }

    private static AudioStream? TryLoadStream(string path)
    {
        // ResourceLoader.Exists (et non FileAccess.FileExists) : sensible aux remaps
        // d'import. A l'export, le .wav source n'est PAS embarqué (seul le .sample
        // importé l'est) ; FileAccess.FileExists renverrait toujours false et casserait
        // tout l'audio du build. ResourceLoader.Exists fonctionne en editeur ET a l'export.
        if (!ResourceLoader.Exists(path)) return null;

        var stream = GD.Load<AudioStream>(path);
        if (stream == null)
            GD.PrintErr($"[AudioSystem] Echec de chargement : {path}");

        return stream;
    }

    // -------------------------------------------------------------------------
    // Utilitaires internes
    // -------------------------------------------------------------------------

    private AudioStreamPlayer? FindFreeSfxPlayer()
    {
        foreach (var p in _sfxPool)
            if (!p.Playing) return p;
        return null;
    }

    /// <summary>Fondu entrant via Tween sur VolumeDb.</summary>
    private void FadeIn(AudioStreamPlayer player, float durationSec, float targetVolume)
    {
        if (durationSec <= 0f)
        {
            player.VolumeDb = LinearToDb(targetVolume);
            return;
        }

        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(
            player,
            "volume_db",
            (double)LinearToDb(targetVolume),
            durationSec
        ).From((double)LinearToDb(0f));
    }

    /// <summary>Fondu sortant via Tween sur VolumeDb, puis Stop.</summary>
    private void FadeOut(AudioStreamPlayer player, float durationSec)
    {
        if (durationSec <= 0f)
        {
            player.Stop();
            return;
        }

        var tween = CreateTween();
        tween.SetPauseMode(Tween.TweenPauseMode.Process);
        tween.TweenProperty(
            player,
            "volume_db",
            (double)LinearToDb(0f),
            durationSec
        );
        tween.TweenCallback(Callable.From(player.Stop));
    }

    /// <summary>Convertit un volume lineaire [0..1] en decibels pour Godot AudioStreamPlayer.</summary>
    private static float LinearToDb(float linear)
    {
        if (linear <= 0f) return -80f; // silence absolu
        return Mathf.LinearToDb(linear);
    }
}
