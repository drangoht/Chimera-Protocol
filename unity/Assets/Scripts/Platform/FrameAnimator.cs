using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Lecteur d'animations image par image, remplaçant <c>AnimatedSprite2D</c> de Godot
/// (55 sites d'appel — docs/UNITY_MIGRATION_PLAN.md §4.2, §7.2).
///
/// <para>Reproduit le contrat dont le jeu se sert réellement : <c>Play(nom)</c>,
/// <see cref="AnimationFinished"/> (dont dépendent les animations de mort), <see cref="FlipX"/>
/// (équivalent de <c>flip_h</c>) et <see cref="SpeedScale"/>.</para>
///
/// <para><b>Tolérance à une animation absente — reprise d'un piège documenté du projet.</b> Sous
/// Godot, des sprites de faune sans animation <c>attack</c> produisaient <b>144 erreurs par
/// session</b> : la demande était rejouée à chaque frame et journalisée à chaque fois. Ici, une
/// animation manquante ne casse rien (on garde l'animation en cours, ou on se rabat sur
/// <c>idle</c>) et n'est signalée <b>qu'une fois par couple sprite/animation</b>. Le
/// comportement de jeu est identique ; c'est le bruit qui disparaît.</para>
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class FrameAnimator : MonoBehaviour
{
    /// <summary>Couples (sprite, animation) déjà signalés — pour ne pas répéter l'avertissement.</summary>
    private static readonly HashSet<string> _warned = new();

    private SpriteRenderer? _rendererCache;

    /// <summary>
    /// Rendu associé, résolu <b>à la demande</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ Ne pas remplacer par un champ assigné dans <c>Awake</c>. Unity ne garantit aucun ordre
    /// entre les <c>Awake</c> de deux composants : un appelant dont l'<c>Awake</c> tourne en premier
    /// obtiendrait <c>null</c> et lèverait une exception. Le coût réel de cette erreur est
    /// disproportionné — <b>une exception dans <c>Awake</c> empêche Unity d'appeler <c>Update</c>
    /// sur le composant</b>, qui devient donc entièrement inerte, sans que rien ne le signale
    /// ailleurs que dans le journal.
    /// </remarks>
    private SpriteRenderer? Renderer => _rendererCache != null
        ? _rendererCache
        : _rendererCache = GetComponent<SpriteRenderer>();
    private SpriteFramesAsset? _frames;
    private SpriteFramesAsset.Animation? _current;

    private float _elapsed;
    private int   _frameIndex;
    private bool  _playing;
    private bool  _finishedSignalled;

    /// <summary>Multiplicateur de cadence — équivalent de <c>speed_scale</c>.</summary>
    public float SpeedScale { get; set; } = 1f;

    /// <summary>Miroir horizontal — équivalent de <c>flip_h</c>.</summary>
    public bool FlipX
    {
        get => Renderer != null && Renderer.flipX;
        set { if (Renderer != null) Renderer.flipX = value; }
    }

    /// <summary>Nom de l'animation en cours, chaîne vide s'il n'y en a pas.</summary>
    public string CurrentAnimation => _current?.Name ?? "";

    /// <summary>Une animation est-elle en cours de lecture ?</summary>
    public bool IsPlaying => _playing;

    /// <summary>
    /// Émis à la fin d'une animation non bouclée — c'est ce signal qui déclenche la disparition
    /// d'un ennemi après son animation de mort.
    /// </summary>
    public event Action<string>? AnimationFinished;

    /// <summary>
    /// Installe le jeu d'animations — équivalent de <c>SetSpriteFrames</c>. Réinitialise la lecture.
    /// </summary>
    public void SetSpriteFrames(SpriteFramesAsset? frames)
    {
        _frames  = frames;
        _current = null;
        _playing = false;
        _elapsed = 0f;
        _frameIndex = 0;
    }

    /// <summary>
    /// Joue une animation. Si elle n'existe pas, conserve l'animation en cours ou se rabat sur
    /// <c>idle</c> — et ne le signale qu'une fois (voir la remarque de classe).
    /// </summary>
    /// <param name="restartIfSame">
    /// Faux par défaut : redemander l'animation déjà en cours ne la redémarre pas. C'est ce qui
    /// permet d'appeler <c>Play("move")</c> à chaque frame sans figer l'animation sur son
    /// premier dessin — l'erreur la plus courante avec ce genre de lecteur.
    /// </param>
    public void Play(string name, bool restartIfSame = false)
    {
        if (_frames == null) return;

        if (!restartIfSame && _current != null && _current.Name == name && _playing) return;

        var anim = _frames.Find(name);
        if (anim == null)
        {
            WarnOnce(name);
            if (_current != null) return;              // on garde ce qui tourne déjà
            anim = _frames.Find("idle");
            if (anim == null) return;
        }

        _current = anim;
        _elapsed = 0f;
        _frameIndex = 0;
        _playing = anim.Frames.Length > 0;
        _finishedSignalled = false;
        ApplyFrame();
    }

    /// <summary>Arrête la lecture sans changer l'image affichée.</summary>
    public void Stop() => _playing = false;

    private void Update()
    {
        if (!_playing || _current == null) return;

        int count = _current.Frames.Length;
        if (count == 0) { _playing = false; return; }

        float fps = _current.Speed * SpeedScale;
        if (fps <= 0f) return;

        _elapsed += Time.deltaTime;
        float frameDuration = 1f / fps;

        while (_elapsed >= frameDuration)
        {
            _elapsed -= frameDuration;
            _frameIndex++;

            if (_frameIndex >= count)
            {
                if (_current.Loop)
                {
                    _frameIndex = 0;
                }
                else
                {
                    // On reste sur la dernière image, comme Godot, et on ne signale qu'une fois :
                    // le rappel détruit souvent l'objet, le rejouer serait une double libération.
                    _frameIndex = count - 1;
                    _playing = false;

                    if (!_finishedSignalled)
                    {
                        _finishedSignalled = true;
                        AnimationFinished?.Invoke(_current.Name);
                    }
                    break;
                }
            }
        }

        ApplyFrame();
    }

    private void ApplyFrame()
    {
        if (_current == null || _current.Frames.Length == 0) return;
        var renderer = Renderer;
        if (renderer == null) return;

        int i = Mathf.Clamp(_frameIndex, 0, _current.Frames.Length - 1);
        renderer.sprite = _current.Frames[i];
    }

    private void WarnOnce(string animName)
    {
        string key = (_frames != null ? _frames.Id : "?") + ":" + animName;
        if (!_warned.Add(key)) return;

        Debug.LogWarning($"[FrameAnimator] animation '{animName}' absente de " +
                         $"'{(_frames != null ? _frames.Id : "?")}' — repli applique. " +
                         "Signale une seule fois.");
    }
}
