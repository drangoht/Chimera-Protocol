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

    /// <summary>Une unité du monde = un pixel d'écran, à toute résolution.</summary>
    private void ApplyPixelScale()
    {
        if (_camera == null || Screen.height == _lastHeight) return;

        _lastHeight = Screen.height;
        _camera.orthographicSize = Screen.height / 2f;
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
