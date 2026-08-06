using UnityEngine;

/// <summary>
/// Nuage <b>volumétrique</b> porté par une entité — une masse sans contour, là où un anneau dessine
/// une frontière.
///
/// <para><b>Pourquoi cette classe existe.</b> Les auras du jeu étaient toutes rendues par un cercle
/// (<c>Vfx.Ring</c>) plus un halo central. Le cercle a un défaut de fond : il affirme une
/// <i>limite</i>. Sur le Voile de Givre, cela dessinait un disque de portée d'arme — la même forme
/// que le Champ de Surcharge, que la Nova, que n'importe quelle zone — alors que ce qu'il fallait
/// montrer était une <b>brume glacée</b>, dont le propre est justement de ne pas avoir de bord.</para>
///
/// <para><b>Persistant, et non redessiné à chaque tir.</b> Une aura pulsée au rythme de la recharge
/// clignote : elle apparaît, s'éteint, réapparaît, et le joueur lit « l'effet se déclenche » alors
/// que l'aura est permanente. Les bouffées vivent donc dans des objets à demeure, qui dérivent
/// lentement — et, accessoirement, ne prennent rien au vivier partagé d'effets, dont une aura
/// active en continu ferait disparaître tout le reste.</para>
/// </summary>
public sealed class AuraCloud : MonoBehaviour
{
    /// <summary>Une bouffée du nuage : sa place dans le disque, sa taille et sa dérive propres.</summary>
    private struct Puff
    {
        public SpriteRenderer Renderer;
        public float Angle;        // position angulaire courante, en radians
        public float Radius;       // distance au centre, en fraction du rayon de l'aura
        public float Spin;         // vitesse angulaire propre, en radians/s
        public float Breathe;      // vitesse de respiration du rayon
        public float Phase;        // déphasage, pour que rien ne batte à l'unisson
        public float Size;         // diamètre, en fraction du rayon de l'aura
    }

    private Puff[]? _puffs;
    private float _time;
    private float _radius = 1f;
    private Color _tint = Color.white;
    private float _alpha = 0.1f;

    /// <summary>Nombre de bouffées vivantes — observable pour les vérifications.</summary>
    public int PuffCount => _puffs?.Length ?? 0;

    /// <summary>Rayon couvert, en pixels — observable pour les vérifications.</summary>
    public float RadiusPx => _radius;

    /// <summary>
    /// Installe (ou reconfigure) le nuage.
    /// </summary>
    /// <param name="radiusPx">Rayon de l'aura, en pixels du monde.</param>
    /// <param name="tint">Teinte des bouffées.</param>
    /// <param name="alpha">
    /// Opacité d'<b>une</b> bouffée. ⚠ À lire comme une contribution, jamais comme un résultat : le
    /// nuage est additif et ses bouffées se recouvrent largement — dix superposées à 0,10 saturent
    /// au blanc bien avant qu'une seule ne devienne visible isolément.
    /// </param>
    /// <param name="count">Nombre de bouffées. Au-delà d'une quinzaine, le nuage devient un disque.</param>
    /// <param name="seed">
    /// Graine du tirage de mise en place. ⚠ <b>Jamais <c>UnityEngine.Random</c></b> : il partage son
    /// état avec le jeu, et une campagne de banc à graine fixe verrait ses tirages se décaler selon
    /// le nombre d'auras dessinées.
    /// </param>
    public void Configure(float radiusPx, Color tint, float alpha, int count, int seed)
    {
        _radius = Mathf.Max(1f, radiusPx);
        _tint = tint;
        _alpha = alpha;

        if (_puffs != null && _puffs.Length == count) { ApplyTint(); return; }

        Clear();

        var rng = new System.Random(seed);
        _puffs = new Puff[Mathf.Max(1, count)];

        for (int i = 0; i < _puffs.Length; i++)
        {
            var go = new GameObject($"Bouffee{i}", typeof(SpriteRenderer));
            go.transform.SetParent(transform, false);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = VfxPrimitives.Glow;               // disque à dégradé : aucun bord franc
            sr.sharedMaterial = VfxPrimitives.Additive;
            sr.sortingOrder = VfxPrimitives.OrderGround;  // SOUS les entités : une brume ne les masque pas

            // Réparties en spirale plutôt qu'au hasard pur : un tirage uniforme laisse des trous et
            // des grumeaux, et un nuage à trous se lit comme une poignée de taches.
            float t = (i + 0.5f) / _puffs.Length;

            _puffs[i] = new Puff
            {
                Renderer = sr,
                Angle    = t * Mathf.PI * 2f * 2.4f + (float)rng.NextDouble(),
                Radius   = Mathf.Sqrt(t) * 0.82f,        // racine : densité constante par surface
                Spin     = 0.18f + (float)rng.NextDouble() * 0.22f,
                Breathe  = 0.5f + (float)rng.NextDouble() * 0.9f,
                Phase    = (float)rng.NextDouble() * 6.28f,
                Size     = 0.42f + (float)rng.NextDouble() * 0.34f,
            };
        }

        ApplyTint();
    }

    /// <summary>Retire toutes les bouffées — l'arme a disparu, ou son rayon a changé.</summary>
    public void Clear()
    {
        if (_puffs == null) return;

        foreach (var puff in _puffs)
            if (puff.Renderer != null) Destroy(puff.Renderer.gameObject);

        _puffs = null;
    }

    private void ApplyTint()
    {
        if (_puffs == null) return;

        foreach (var puff in _puffs)
            if (puff.Renderer != null)
                puff.Renderer.color = new Color(_tint.r, _tint.g, _tint.b, _alpha);
    }

    private void Update()
    {
        if (_puffs == null) return;

        _time += Time.deltaTime;

        // ⚠ Le nuage vit en espace MONDE mais reste enfant du porteur : il faut donc annuler
        // l'échelle héritée, sinon un porteur rendu à 1,5 traînerait une brume une fois et demie
        // trop large — la confusion « facteur contre taille » qui a déjà produit trois défauts.
        float scale = Mathf.Abs(transform.lossyScale.x);
        float k = scale > 0.001f ? 1f / scale : 1f;
        float sprite = Mathf.Max(0.001f, VfxPrimitives.Glow.bounds.size.x);

        for (int i = 0; i < _puffs.Length; i++)
        {
            ref var puff = ref _puffs[i];
            if (puff.Renderer == null) continue;

            puff.Angle += puff.Spin * Time.deltaTime;

            // Le rayon RESPIRE : sans cela, les bouffées tournent en rond sur des orbites fixes et
            // le nuage redevient un anneau — exactement la forme qu'on cherche à quitter.
            float breath = 1f + 0.12f * Mathf.Sin(_time * puff.Breathe + puff.Phase);
            float r = puff.Radius * breath * _radius * k;

            puff.Renderer.transform.localPosition =
                new Vector3(Mathf.Cos(puff.Angle) * r, Mathf.Sin(puff.Angle) * r, 0f);

            float size = puff.Size * _radius * breath * k;
            puff.Renderer.transform.localScale = Vector3.one * (size / sprite);

            // Les bouffées les plus extérieures sont les plus pâles : c'est ce dégradé qui donne au
            // nuage un bord FLOU au lieu d'une découpe, sans qu'aucun contour ne soit dessiné.
            float fade = Mathf.Clamp01(1f - puff.Radius * 0.75f);
            float twinkle = 0.78f + 0.22f * Mathf.Sin(_time * puff.Breathe * 1.7f + puff.Phase);

            puff.Renderer.color = new Color(_tint.r, _tint.g, _tint.b, _alpha * fade * twinkle);
        }
    }
}
