using UnityEngine;

/// <summary>
/// Caméra de partie : elle <b>suit le joueur</b> et rend les sprites à leur taille native
/// (lot de parité visuelle).
///
/// <para><b>Deux défauts qu'elle corrige, tous deux visibles dès la première seconde de jeu.</b></para>
///
/// <list type="number">
///   <item><b>La caméra était fixe.</b> Le joueur s'éloignait du centre et finissait par jouer dans
///         un coin de l'écran — avec une arène de 1920 × 1216, la moitié de l'action se déroulait
///         hors champ.</item>
///   <item><b>L'échelle était fausse.</b> Une taille orthographique figée à 540 affiche 1080 unités
///         de haut quelle que soit la fenêtre : en 720p, tout était rendu aux deux tiers de sa
///         taille. Le projet travaille en <b>1 pixel = 1 unité</b> (décision structurante du
///         portage) ; la demi-hauteur doit donc valoir la moitié de la hauteur d'écran, sans quoi
///         cette équivalence ne tient plus à l'affichage.</item>
/// </list>
/// </summary>
[RequireComponent(typeof(Camera))]
public sealed class RunCamera : MonoBehaviour
{
    /// <summary>Souplesse de la poursuite, en secondes. Zéro = collée au joueur.</summary>
    public float SmoothTime = 0.12f;

    private Camera? _camera;
    private Vector3 _velocity;
    private Vector2 _shake;
    private int _lastHeight;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        ScreenShake.Reset();   // une secousse ne survit pas à la run qui l'a déclenchée
    }

    /// <summary>Position suivie, secousse déduite — voir <see cref="Follow"/>.</summary>
    private Vector3 StripShake(Vector3 position) => position - (Vector3)_shake;

    private void LateUpdate()
    {
        ApplyPixelScale();
        Follow();
    }

    /// <summary>
    /// Hauteur de monde visible, en unités. C'est la hauteur de viewport de Godot
    /// (<c>window/size/viewport_height</c>), et elle ne dépend <b>pas</b> de la fenêtre : le jeu
    /// d'origine est en <c>stretch/mode = "canvas_items"</c>, donc il montre toujours 1280 × 720
    /// unités de monde, quelle que soit la résolution, en étirant le rendu.
    /// </summary>
    private const float WorldViewHeight = 720f;

    /// <summary>
    /// Cadre une hauteur de monde <b>fixe</b>, comme Godot.
    /// </summary>
    /// <remarks>
    /// ⚠ Le portage calait la demi-hauteur sur <c>Screen.height / 2</c> pour tenir le « 1 px =
    /// 1 unité » du reste du projet. En 1920 × 1080 cela montrait <b>1920 unités de large</b> —
    /// c'est-à-dire l'arène entière (1920 × 1216). Deux conséquences, dont une invisible au test :
    /// le monde était rendu aux deux tiers de la taille de Godot, et surtout la caméra ne pouvait
    /// <b>plus se déplacer horizontalement</b> (son cadrage la bornait à zéro). Or une couche de
    /// parallaxe ne se voit que si la caméra bouge : l'atmosphère était bien construite, elle
    /// n'avait simplement jamais l'occasion de défiler. « Il manque l'effet parallaxe » se
    /// diagnostiquait donc dans la <i>caméra</i>, pas dans les couches.
    ///
    /// <para>Le « 1 px = 1 unité » reste vrai pour les <b>sprites</b> (un pixel de texture vaut une
    /// unité de monde) ; ce qui change ici, c'est le facteur d'affichage, exactement comme
    /// l'étirement de Godot.</para>
    /// </remarks>
    private void ApplyPixelScale()
    {
        if (_camera == null || Screen.height == _lastHeight) return;

        _lastHeight = Screen.height;
        _camera.orthographicSize = WorldViewHeight / 2f;
    }

    /// <summary>
    /// Suit le joueur, <b>bornée par l'arène</b> : sans ce cadrage, la caméra montre le vide au-delà
    /// des murs dès que le joueur longe un bord, et la limite cesse d'être lisible.
    /// </summary>
    private void Follow()
    {
        var player = Player.Instance;
        if (player == null || _camera == null) return;

        float halfViewY = _camera.orthographicSize;
        float halfViewX = halfViewY * _camera.aspect;

        // Une arène plus petite que l'écran se centre au lieu d'être cadrée de force.
        float limitX = Mathf.Max(0f, Arena.HalfWidth  - halfViewX);
        float limitY = Mathf.Max(0f, Arena.HalfHeight - halfViewY);

        Vector3 target = player.transform.position;
        target.x = Mathf.Clamp(target.x, -limitX, limitX);
        target.y = Mathf.Clamp(target.y, -limitY, limitY);
        target.z = transform.position.z;   // la profondeur de la caméra ne change jamais

        Vector3 followed = SmoothTime <= 0f
            ? target
            : Vector3.SmoothDamp(StripShake(transform.position), target, ref _velocity, SmoothTime);

        // La secousse s'ajoute APRÈS la poursuite et n'est jamais mémorisée dans la position suivie
        // (d'où StripShake) : sinon l'amortissement la poursuivrait, et la caméra dériverait au lieu
        // de revenir se centrer.
        ScreenShake.Advance(Time.deltaTime);
        _shake = ScreenShake.Offset;

        transform.position = followed + (Vector3)_shake;
    }
}
