using UnityEngine;

/// <summary>
/// Rend <b>visibles</b> les deux états qu'un ennemi peut subir : le <b>gel</b> et la <b>brûlure</b>.
///
/// <para><b>Pourquoi ce composant existe.</b> Les deux effets étaient parfaitement fonctionnels et
/// parfaitement invisibles. Un ralentissement de 45 % ne se voit pas dans une nuée qui avance déjà
/// lentement, et une brûlure qui grignote 8 PV/s ne se distingue en rien d'un ennemi intact. Le
/// joueur n'avait donc aucun moyen de savoir laquelle de ses armes avait touché, ni sur quelles
/// cibles ses effets couraient encore — ce qui rend les deux archétypes indécidables au moment de
/// choisir une carte.</para>
///
/// <para>Chaque état porte <b>deux</b> signaux, parce qu'un seul se perd dans la masse : le gel
/// recolore la silhouette <i>et</i> sème des éclats derrière elle ; la brûlure pose des flammes
/// <i>et</i> fait pulser une lueur chaude. Le mouvement est ce qui accroche l'œil dans une mêlée à
/// 300 entités — une teinte fixe, non.</para>
/// </summary>
public sealed class EnemyStatusFx : MonoBehaviour
{
    /// <summary>Secondes entre deux éclats de givre semés derrière un ennemi gelé.</summary>
    private const float FrostTrailInterval = 0.22f;

    /// <summary>Déplacement minimal, en pixels, sous lequel aucune traînée n'est semée.</summary>
    private const float FrostTrailMinDistance = 6f;

    /// <summary>Nombre de langues de feu portées par un ennemi qui brûle.</summary>
    private const int FlameCount = 3;

    /// <summary>
    /// Plafond d'effets simultanés au-delà duquel la traînée s'abstient. Les états peuvent toucher
    /// des centaines d'entités à la fois : sans ce garde-fou, ils videraient le pool partagé et
    /// feraient disparaître les effets d'<b>armes</b>, autrement dit le retour dont le joueur a le
    /// plus besoin.
    /// </summary>
    private const int TrailBudget = 150;

    private static Shader? _frostShader;

    private SpriteRenderer? _sprite;
    private Material? _frostMaterial;
    private float _frostShown = -1f;

    private float _trailTimer;
    private Vector2 _lastTrailPosition;

    private Transform? _flameRoot;
    private SpriteRenderer[]? _flames;
    private float _flamePhase;

    /// <summary>Éclats de givre semés — observable pour les vérifications.</summary>
    public int FrostShardsDropped { get; private set; }

    /// <summary>Le givre est-il appliqué à l'instant ?</summary>
    public bool FrostVisible => _frostMaterial != null && _frostShown > 0.5f;

    /// <summary>Les flammes sont-elles visibles à l'instant ?</summary>
    public bool FlamesVisible => _flameRoot != null && _flameRoot.gameObject.activeSelf;

    private void Awake()
    {
        _sprite = GetComponentInChildren<SpriteRenderer>();
        _lastTrailPosition = transform.position;
    }

    /// <summary>
    /// Met à jour les deux états. Appelé par <see cref="EnemyBase"/> à chaque image, avec la vérité
    /// du modèle — ce composant ne décide de rien, il ne fait que le montrer.
    /// </summary>
    public void Render(bool frozen, bool burning, float dt)
    {
        RenderFrost(frozen, dt);
        RenderBurn(burning, dt);
    }

    // ─── Gel ──────────────────────────────────────────────────────────────────

    private void RenderFrost(bool frozen, float dt)
    {
        float target = frozen ? 1f : 0f;

        if (!Mathf.Approximately(_frostShown, target))
        {
            EnsureFrostMaterial();

            // Le paramètre n'est poussé qu'au CHANGEMENT : l'écrire à chaque image sur 300 entités
            // coûterait cher pour une valeur qui ne bouge que deux fois par gel.
            _frostMaterial?.SetFloat("_Frost", target);
            _frostShown = target;
        }

        if (!frozen)
        {
            _trailTimer = 0f;
            _lastTrailPosition = transform.position;
            return;
        }

        _trailTimer += dt;
        if (_trailTimer < FrostTrailInterval) return;

        _trailTimer = 0f;

        // Une traînée derrière un ennemi immobile serait un tas de givre, pas un sillage : elle ne
        // se sème que si la cible a réellement avancé depuis le dernier éclat.
        Vector2 here = transform.position;
        if (Vector2.Distance(here, _lastTrailPosition) < FrostTrailMinDistance) return;

        if (Vfx.ActiveEffects < TrailBudget)
        {
            Vfx.Dot(_lastTrailPosition, new Color(0.62f, 0.86f, 1f, 0.9f), size: 11f, life: 0.55f);
            FrostShardsDropped++;
        }

        _lastTrailPosition = here;
    }

    /// <summary>
    /// Pose le matériau de givre au <b>premier</b> gel seulement.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Deux approches plus simples ont été essayées et <b>ne peuvent pas marcher</b>, pour
    /// la même raison : la faune est majoritairement rouge. Teinter <c>SpriteRenderer.color</c>
    /// multiplie, donc ne peut qu'assombrir — un ennemi rouge gelé reste rouge, en plus sombre, ce
    /// qui se lit « il est dans l'ombre ». Superposer un calque additif bleu ajoute du bleu au rouge
    /// et donne du <b>rose délavé</b>. Il faut <i>remplacer</i> la couleur, donc un shader — c'est
    /// exactement pourquoi le jeu d'origine en utilise un.</para>
    ///
    /// <para>Un matériau posé d'emblée sur chaque ennemi supprimerait le regroupement de rendu de
    /// toute la faune, alors que la plupart des entités ne sont jamais gelées. Il est donc créé à la
    /// demande — mais <b>par instance</b> : partager un matériau ferait givrer la nuée entière dès
    /// qu'un seul ennemi l'est.</para>
    /// </remarks>
    private void EnsureFrostMaterial()
    {
        if (_frostMaterial != null || _sprite == null) return;

        _frostShader ??= Resources.Load<Shader>("Shaders/EnemyFrost");
        if (_frostShader == null)
        {
            Debug.LogWarning("[EnemyStatusFx] shader de givre introuvable — ennemis gelés non teintés.");
            return;
        }

        _frostMaterial = new Material(_frostShader);
        _sprite.material = _frostMaterial;
    }

    // ─── Brûlure ──────────────────────────────────────────────────────────────

    private void RenderBurn(bool burning, float dt)
    {
        if (!burning)
        {
            if (_flameRoot != null) _flameRoot.gameObject.SetActive(false);
            return;
        }

        if (_flameRoot == null) BuildFlames();
        if (_flameRoot == null || _flames == null) return;

        _flameRoot.gameObject.SetActive(true);
        _flamePhase += dt * 6.5f;

        for (int i = 0; i < _flames.Length; i++)
        {
            var flame = _flames[i];
            if (flame == null) continue;

            // Chaque langue a sa propre phase : à l'unisson, trois flammes se lisent comme un seul
            // bloc qui clignote, et le feu perd exactement ce qui le rend reconnaissable.
            float phase = _flamePhase + i * 2.1f;
            float rise = Mathf.Repeat(phase * 0.34f, 1f);

            flame.transform.localPosition = new Vector3(
                (i - (FlameCount - 1) * 0.5f) * 8f + Mathf.Sin(phase * 1.7f) * 3f,
                -6f + rise * 22f, 0f);

            float fade = 1f - rise;
            flame.transform.localScale = Vector3.one * (0.55f + 0.45f * fade) * FlameSize;
            flame.color = Color.Lerp(new Color(1f, 0.42f, 0.10f, 0f),
                                     new Color(1f, 0.86f, 0.32f, 0.95f), fade);
        }
    }

    /// <summary>Côté d'une langue de feu, en pixels.</summary>
    private const float FlameSize = 18f;

    /// <summary>
    /// Trois petites lueurs additives portées par l'ennemi.
    /// </summary>
    /// <remarks>
    /// Des enfants <b>persistants</b>, et non des effets empruntés au pool partagé : une brûlure
    /// dure plusieurs secondes et peut courir sur des dizaines de cibles à la fois. Les tirer du
    /// pool le viderait en une seconde, au détriment des effets d'armes.
    /// </remarks>
    private void OnDestroy()
    {
        if (_frostMaterial != null) Destroy(_frostMaterial);
    }

    private void BuildFlames()
    {
        var root = new GameObject("Flammes");
        root.transform.SetParent(transform, false);

        // ⚠ Sur le CORPS, pas sur l'origine. Les sprites d'ennemis ne sont pas centrés sur le
        // transform de leur entité — les flammes se posaient sous les pieds, ce qui se lit « le sol
        // brûle » et non « l'ennemi brûle ».
        if (_sprite != null)
        {
            Vector3 center = transform.InverseTransformPoint(_sprite.bounds.center);
            root.transform.localPosition = new Vector3(center.x, center.y, 0f);
        }

        _flames = new SpriteRenderer[FlameCount];

        for (int i = 0; i < FlameCount; i++)
        {
            var go = new GameObject($"Flamme{i}", typeof(SpriteRenderer));
            go.transform.SetParent(root.transform, false);

            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = VfxPrimitives.Spark;
            sr.sharedMaterial = VfxPrimitives.AdditiveSpark;

            // Devant l'ennemi qui les porte, sinon un colosse mange ses propres flammes.
            sr.sortingOrder = 24;

            _flames[i] = sr;
        }

        _flameRoot = root.transform;
    }

}
