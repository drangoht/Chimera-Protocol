using UnityEngine;

/// <summary>
/// <b>Déforme</b> un ennemi tant qu'il séjourne dans un champ irradiant.
///
/// <para><b>Pourquoi une déformation, et pas une teinte.</b> Toutes les autres marques d'état du jeu
/// sont des ajouts : une couleur, des particules, un calque. Le champ de la Lame de Fusion doit dire
/// autre chose — non pas « celui-ci est touché » mais « <i>ce qui entre là-dedans ne tient plus en
/// place</i> ». Un corps qui ondule, s'étire et se tord dit cela sans un seul pixel de plus, et se
/// distingue immédiatement du gel et de la brûlure, qui posent des choses sur la silhouette au lieu
/// de la travailler.</para>
///
/// <para><b>La déformation porte sur le SPRITE, jamais sur l'entité.</b> Écrire dans le transform de
/// l'ennemi déplacerait sa position réelle et son rayon de contact : le joueur serait touché par un
/// corps qui n'est plus là où il paraît. Le composant travaille donc sur le rendu, et restaure
/// exactement l'échelle qu'il a trouvée — un effet visuel ne laisse pas de trace dans le modèle.</para>
/// </summary>
public sealed class IrradiationWarp : MonoBehaviour
{
    /// <summary>Amplitude maximale de l'étirement, en fraction de la taille du sprite.</summary>
    /// <remarks>
    /// ⚠ Réglé bas <b>à dessein</b>. Une déformation ample donne un sprite méconnaissable, et le
    /// joueur perd ce qu'il lui faut d'abord : savoir <i>à quel ennemi</i> il a affaire. L'effet doit
    /// se voir sur la masse en mouvement, pas sur l'exemplaire isolé.
    /// </remarks>
    private const float WarpAmplitude = 0.22f;

    /// <summary>Amplitude de la torsion, en degrés.</summary>
    private const float TwistDegrees = 7f;

    /// <summary>Vitesse d'ondulation, en radians par seconde.</summary>
    private const float WarpSpeed = 11f;

    /// <summary>Vitesse d'entrée et de sortie de la déformation (unités par seconde).</summary>
    /// <remarks>
    /// Une déformation qui s'installe et se retire d'un coup <b>saute</b>, et une entité qui longe le
    /// bord du champ clignoterait entre deux formes. Le fondu la rend continue, donc lisible comme
    /// une intensité qui dépend de la distance.
    /// </remarks>
    private const float BlendSpeed = 5f;

    private SpriteRenderer? _sprite;
    private Vector3 _restScale = Vector3.one;
    private Quaternion _restRotation = Quaternion.identity;
    private bool _captured;

    private float _level;
    private float _phase;

    /// <summary>Intensité de déformation appliquée à l'instant — observable pour les vérifications.</summary>
    public float Level => _level;

    /// <summary>Le sprite est-il déformé à l'instant ?</summary>
    public bool Warped => _level > 0.02f;

    /// <summary>
    /// Signale que l'entité est <b>dans</b> le champ, à cette intensité. Appelé à chaque image par la
    /// source ; l'absence d'appel vaut sortie du champ, et la déformation se résorbe d'elle-même.
    /// </summary>
    /// <remarks>
    /// ⚠ Ce sens de contrat est délibéré : une source qui devrait annoncer les <i>sorties</i>
    /// oublierait celles causées par sa propre disparition (arme fusionnée, run terminée), et les
    /// ennemis resteraient tordus pour toujours.
    /// </remarks>
    public void Sustain(float intensity) => _target = Mathf.Clamp01(intensity);

    private float _target;

    private void Awake() => _sprite = GetComponentInChildren<SpriteRenderer>();

    private void LateUpdate()
    {
        // ⚠ LateUpdate, et non Update : l'animation image par image écrit le sprite pendant Update.
        // Une déformation posée avant elle serait écrasée une image sur deux, ce qui se lit
        // « l'effet vacille » — un symptôme qu'on chercherait longtemps du côté du calcul.
        float dt = Time.deltaTime;

        _level = Mathf.MoveTowards(_level, _target, BlendSpeed * dt);
        _target = 0f;   // il faut être re-signalé à chaque image pour rester déformé

        if (_sprite == null) return;

        if (!_captured)
        {
            _restScale = _sprite.transform.localScale;
            _restRotation = _sprite.transform.localRotation;
            _captured = true;
        }

        if (_level <= 0.001f)
        {
            _sprite.transform.localScale = _restScale;
            _sprite.transform.localRotation = _restRotation;
            return;
        }

        _phase += dt * WarpSpeed;

        // Étirement en OPPOSITION de phase entre les deux axes : ce qui s'allonge se rétrécit
        // ailleurs, donc la masse paraît conservée. Deux axes en phase donneraient une pulsation —
        // un objet qui grossit et rapetisse, ce qui se lit « il respire », pas « il se déforme ».
        float wobble = Mathf.Sin(_phase) * WarpAmplitude * _level;

        _sprite.transform.localScale = new Vector3(
            _restScale.x * (1f + wobble),
            _restScale.y * (1f - wobble * 0.85f),
            _restScale.z);

        _sprite.transform.localRotation = _restRotation * Quaternion.Euler(
            0f, 0f, Mathf.Sin(_phase * 0.63f + 1.1f) * TwistDegrees * _level);
    }

    private void OnDisable()
    {
        // Rendre sa forme à l'entité même si le composant s'éteint en cours de déformation : sans
        // cela, un ennemi mis en réserve reviendrait tordu à sa réutilisation.
        if (_sprite == null || !_captured) return;

        _sprite.transform.localScale = _restScale;
        _sprite.transform.localRotation = _restRotation;
    }
}
