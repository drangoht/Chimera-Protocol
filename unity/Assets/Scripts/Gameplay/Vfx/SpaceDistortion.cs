using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Rend un élément de <b>décor</b> déformable par un champ gravitationnel — il se penche, s'écrase et
/// glisse vers ce qui l'attire, puis reprend exactement sa place.
///
/// <para><b>Pourquoi ça n'est pas un effet de plus.</b> Une singularité dessinée reste une image posée
/// sur l'arène : on voit un tourbillon, on ne voit pas l'arène tourbillonner. La différence tient à ce
/// qui bouge — tant que le sol et les piliers restent parfaitement droits <i>à côté</i> du puits, le
/// joueur lit « un effet joue ici », jamais « l'espace se tord ici ». C'est la même leçon que la
/// parallaxe : ce qui se lit, c'est l'<b>écart</b> entre les couches, pas le contenu d'une couche.</para>
///
/// <para><b>La déformation porte sur le RENDU, jamais sur le modèle.</b> Ce composant écrit dans le
/// transform de l'objet de décor, mais le blocage des obstacles ne passe pas par là : il vit dans
/// <see cref="ArenaObstacles"/>, une liste de centres que rien ici ne touche. Un pilier penché bloque
/// donc exactement là où il bloquait — sans quoi le décor mentirait, ce que le projet tient pour pire
/// qu'une absence de décor.</para>
///
/// <para>Jumeau d'<see cref="IrradiationWarp"/>, qui fait le même travail sur les <b>corps</b> (et que
/// la singularité emploie aussi) : l'un tord ce qui vit, l'autre tord ce qui tient en place. Ils sont
/// séparés parce qu'un décor doit en plus se <b>déplacer</b> vers le centre, ce qu'un ennemi fait déjà
/// pour de vrai.</para>
/// </summary>
public sealed class SpaceDistortion : MonoBehaviour
{
    /// <summary>Déplacement maximal vers le centre, en pixels.</summary>
    /// <remarks>
    /// ⚠ Volontairement petit. Un pilier qui glisse franchement vers le puits se lit « le décor est
    /// mal placé » — et surtout, il s'écarterait de la masse qu'il bloque réellement. Ce qu'on veut
    /// est un <i>fléchissement</i>, assez pour que l'œil voie l'espace céder, assez peu pour que la
    /// silhouette reste sur son point de blocage.
    /// </remarks>
    private const float PullPx = 9f;

    /// <summary>Inclinaison maximale, en degrés — le décor se couche vers ce qui l'aspire.</summary>
    private const float TiltDegrees = 11f;

    /// <summary>Étirement maximal, en fraction de la taille du sprite.</summary>
    private const float StretchAmplitude = 0.16f;

    /// <summary>Vitesse d'ondulation, en radians par seconde.</summary>
    private const float WarpSpeed = 6.5f;

    /// <summary>Vitesse d'entrée et de sortie de la déformation (unités par seconde).</summary>
    private const float BlendSpeed = 4f;

    private static readonly List<SpaceDistortion> All = new();

    /// <summary>Éléments de décor déformables en place — observable pour les vérifications.</summary>
    public static int Registered => All.Count;

    /// <summary>
    /// Éléments effectivement pliés lors du dernier appel à <see cref="Field"/>.
    /// </summary>
    /// <remarks>
    /// ⚠ Un <b>compte</b>, pas un exemple. Un relevé qui cite le premier objet venu ne distingue pas
    /// « la déformation est trop faible » de « aucun décor n'est enregistré » — deux causes opposées
    /// dont une seule se corrige en montant l'amplitude.
    /// </remarks>
    public static int LastBentCount { get; private set; }

    private Vector3 _restPosition;
    private Vector3 _restScale;
    private Quaternion _restRotation;
    private bool _captured;

    private float _level;
    private float _pending;
    private Vector2 _toCenter = Vector2.right;
    private float _phase;

    /// <summary>Intensité appliquée à l'instant — observable pour les vérifications.</summary>
    public float Level => _level;

    private void OnEnable()
    {
        Capture();
        All.Add(this);
    }

    private void OnDisable()
    {
        All.Remove(this);
        Restore();
    }

    private void Capture()
    {
        if (_captured) return;

        _restPosition = transform.position;
        _restScale = transform.localScale;
        _restRotation = transform.localRotation;
        _captured = true;
    }

    private void Restore()
    {
        if (!_captured) return;

        transform.position = _restPosition;
        transform.localScale = _restScale;
        transform.localRotation = _restRotation;
    }

    /// <summary>
    /// Applique un champ centré sur <paramref name="center"/> à tout le décor enregistré.
    /// </summary>
    /// <remarks>
    /// À appeler <b>à chaque image</b> tant que le champ existe : l'absence d'appel vaut disparition,
    /// et la déformation se résorbe d'elle-même. Ce sens de contrat est le même que celui
    /// d'<see cref="IrradiationWarp"/>, et pour la même raison — une source qui devrait annoncer sa
    /// propre disparition oublierait de le faire en disparaissant.
    /// </remarks>
    /// <param name="strength">Intensité au centre, de 0 à 1.</param>
    /// <returns>Nombre d'éléments touchés.</returns>
    public static int Field(Vector2 center, float radius, float strength = 1f)
    {
        int bent = 0;
        if (radius <= 0.01f) { LastBentCount = 0; return 0; }

        // Le champ déborde franchement le rayon qui blesse : une déformation qui s'arrêterait net à
        // la limite d'aspiration dessinerait ce cercle en creux — soit exactement le contour dont on
        // cherche à se débarrasser.
        float reach = radius * 1.6f;

        for (int i = All.Count - 1; i >= 0; i--)
        {
            var d = All[i];
            if (d == null) { All.RemoveAt(i); continue; }

            Vector2 offset = center - (Vector2)d._restPosition;
            float dist = offset.magnitude;
            if (dist > reach) continue;

            // Décroissance quadratique : franche près du puits, presque nulle au bord. Linéaire, le
            // champ pencherait tout le décor de l'arène du même petit angle — ce qui se lit comme un
            // défaut de rendu global, pas comme une source locale.
            float k = 1f - dist / reach;
            d.Sustain(dist > 0.01f ? offset / dist : Vector2.right, k * k * strength);
            bent++;
        }

        LastBentCount = bent;
        return bent;
    }

    // ⚠ Pas de `Reset()` global appelé au démarrage d'une run, contrairement aux viviers d'effets :
    // le registre se vide tout seul (OnDisable part avec la scène, et la boucle ci-dessus purge les
    // « faux null » d'Unity). Une remise à zéro depuis RunBootstrap.Start effacerait au contraire les
    // piliers si l'arène s'était construite avant lui — l'ordre des Start n'est pas garanti.

    /// <summary>Signale que l'objet est dans un champ, à cette intensité, tirant vers cette direction.</summary>
    private void Sustain(Vector2 toCenter, float intensity)
    {
        _toCenter = toCenter;
        _pending = Mathf.Max(_pending, Mathf.Clamp01(intensity));
    }

    private void LateUpdate()
    {
        // LateUpdate pour la même raison qu'IrradiationWarp : ce qui écrit le transform pendant
        // Update (rien ici aujourd'hui, mais la parallaxe et les tremblements de caméra y touchent)
        // ne doit pas être écrasé une image sur deux.
        float dt = Time.deltaTime;

        _level = Mathf.MoveTowards(_level, _pending, BlendSpeed * dt);
        _pending = 0f;   // il faut être re-signalé à chaque image pour rester déformé

        if (!_captured) return;

        if (_level <= 0.001f)
        {
            Restore();
            return;
        }

        _phase += dt * WarpSpeed;

        transform.position = _restPosition + (Vector3)(_toCenter * (PullPx * _level));

        // Étirement en opposition de phase entre les deux axes : la masse paraît conservée, donc
        // l'objet se *déforme* au lieu de respirer.
        float wobble = Mathf.Sin(_phase) * StretchAmplitude * _level;

        transform.localScale = new Vector3(
            _restScale.x * (1f + wobble),
            _restScale.y * (1f - wobble * 0.8f),
            _restScale.z);

        // Le décor se COUCHE vers le puits : une rotation positive autour de Z emmène le sommet vers
        // la gauche, donc pencher vers un centre situé à droite demande un angle négatif. C'est cette
        // inclinaison — plus que le glissement — qui donne au champ une orientation lisible, y compris
        // sur un pilier isolé.
        float lean = -_toCenter.x * TiltDegrees * _level;

        transform.localRotation = _restRotation * Quaternion.Euler(
            0f, 0f, lean + Mathf.Sin(_phase * 0.71f) * 2.5f * _level);
    }
}
