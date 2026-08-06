using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Ce que l'Assimilation fait au <b>corps</b> du porteur : chaque greffe équipée fait pousser ses
/// appendices sur la silhouette du joueur, et ils y restent tant qu'il la porte.
///
/// <para><b>Pourquoi c'est une pièce manquante et non une finition.</b> L'Assimilation est le troisième
/// axe de progression du jeu, et le seul dont la promesse tienne en un mot : <i>devenir une chimère</i>.
/// Or elle ne se voyait nulle part sur le personnage. Une carapace ajoutait des épines invisibles, des
/// servos une esquive sans jambes, une ruche des tourelles posées <i>à côté</i> — le joueur au centre
/// de l'écran restait exactement l'humain du début de partie. Les greffes se lisaient dans un panneau
/// de statistiques, c'est-à-dire au même endroit que n'importe quel bonus de Hub, alors que tout leur
/// propos est d'être d'une autre nature.</para>
///
/// <para><b>Reconstruit à chaque changement, jamais accumulé.</b> Une fusion <i>absorbe</i> ses deux
/// greffes sources (<see cref="Assimilation.Accept"/> les retire des équipées) et un remplacement en
/// retire une : ajouter les appendices au fil des équipements laisserait à l'écran ceux de greffes que
/// le joueur ne porte plus. La liste des équipées est la seule source de vérité, et elle est relue en
/// entier — sur trois à cinq entrées, cela ne coûte rien.</para>
///
/// <para>⚠ <b>Aucun effet de jeu ici.</b> Ni collision, ni dégât, ni portée : les appendices sont du
/// rendu. Les tourelles de la Ruche et les orbiteurs de la Nuée ont leur propre corps et leurs propres
/// règles dans <see cref="GraftManager"/> — ce composant montre ce que le porteur <i>est devenu</i>, pas
/// ce qu'il fait.</para>
/// </summary>
[RequireComponent(typeof(Player))]
public sealed class ChimeraBody : MonoBehaviour
{
    /// <summary>
    /// Un appendice en place : sa forme, son point d'attache et sa phase propre.
    /// </summary>
    private struct Part
    {
        public ChimeraParts.Kind Kind;
        public Transform Transform;
        public SpriteRenderer Renderer;

        /// <summary>Point d'attache, en <b>fractions du corps</b> — jamais en pixels.</summary>
        public Vector2 Anchor;

        /// <summary>Taille voulue, en fraction de la largeur du corps.</summary>
        public float Size;

        /// <summary>Déphasage, pour que deux appendices identiques ne battent pas à l'unisson.</summary>
        public float Phase;

        /// <summary>Passe-t-il derrière le corps ?</summary>
        public bool Behind;
    }

    /// <summary>
    /// Repli si le corps n'a pas encore de sprite : le diamètre du rayon de corps du joueur.
    /// </summary>
    /// <remarks>
    /// ⚠ Le repli n'est pas une constante devinée mais la mesure de gameplay déjà en place, et la
    /// mesure est <b>retentée</b> tant qu'elle a échoué : figée au premier appel, elle donnerait des
    /// appendices calibrés sur un corps qui n'existait pas encore.
    /// </remarks>
    private const float FallbackBodyPx = 26f;

    private readonly List<Part> _parts = new();

    private Player? _player;
    private SpriteRenderer? _body;
    private float _bodyPx;
    private float _time;

    /// <summary>Appendices portés — observable pour les vérifications.</summary>
    public int PartCount => _parts.Count;

    /// <summary>Largeur du corps retenue, en pixels — observable pour les vérifications.</summary>
    public float BodyPx => _bodyPx > 0f ? _bodyPx : FallbackBodyPx;

    /// <summary>
    /// La largeur vient-elle d'une <b>mesure</b> ou du repli ?
    /// </summary>
    /// <remarks>
    /// ⚠ Sans cette distinction, un relevé qui affiche « corps 26 px » ne dit pas si la mesure a
    /// abouti — et une vérification portant sur la valeur seule passerait alors qu'aucun corps n'a
    /// jamais été mesuré. Le défaut exact qu'a produit le relevé des états d'ennemis.
    /// </remarks>
    public bool BodyMeasured => _bodyPx > 0f;

    private void Awake()
    {
        _player = GetComponent<Player>();
        _body = GetComponent<SpriteRenderer>();

        Assimilation.GraftEquipped += OnGraftChanged;
    }

    private void OnDestroy() => Assimilation.GraftEquipped -= OnGraftChanged;

    /// <summary>
    /// Reconstruit à partir des greffes portées. Le paramètre est ignoré à dessein : ce qui compte
    /// n'est pas la greffe qui vient d'arriver mais l'état complet du porteur après son arrivée.
    /// </summary>
    private void OnGraftChanged(GraftTable.GraftDef _) => Rebuild();

    /// <summary>Reconstruit les appendices depuis <see cref="Assimilation.Equipped"/>.</summary>
    public void Rebuild() => Build(Assimilation.Equipped);

    /// <summary>
    /// Reconstruit les appendices pour une liste de greffes donnée.
    /// </summary>
    /// <remarks>
    /// Séparé de <see cref="Rebuild"/> pour que le banc puisse <b>compter</b> les appendices de
    /// chacune des huit greffes sans avoir à remplir huit jauges — une anatomie oubliée est
    /// journalisée, mais seul un compte dit que la table est complète.
    /// </remarks>
    public void Build(IReadOnlyList<string> graftIds)
    {
        foreach (var part in _parts)
            if (part.Transform != null) Destroy(part.Transform.gameObject);

        _parts.Clear();

        foreach (string id in graftIds)
        {
            var def = Assimilation.Config.GraftById(id) ?? Assimilation.Config.FusionById(id);
            var tint = def != null
                ? new Color(Mathf.Clamp01(def.Tint[0]), Mathf.Clamp01(def.Tint[1]), Mathf.Clamp01(def.Tint[2]))
                : Color.white;

            foreach (var (kind, anchor, size) in Anatomy(id))
                Add(kind, anchor, size, tint);
        }
    }

    /// <summary>
    /// <b>L'anatomie d'une chimère</b> : ce que chaque greffe fait pousser, et où.
    ///
    /// <para>Les fusions ne reçoivent pas une forme inédite mais la <b>somme</b> de leurs sources, plus
    /// une pièce qui dit ce qu'elles ajoutent : la Charge Blindée est la carapace <i>et</i> les servos
    /// <i>et</i> une corne. C'est la lecture juste de l'objet — une fusion lie deux greffes, elle n'en
    /// remplace pas le corps — et c'est ce qui permet de reconnaître ce qu'on porte sans lire de
    /// panneau.</para>
    /// </summary>
    /// <remarks>
    /// Les points d'attache sont en <b>fractions du corps</b> : le même code doit habiller le joueur
    /// quelle que soit la taille de son sprite. Un <c>x</c> positif est <i>devant</i> — du côté où le
    /// personnage regarde.
    /// </remarks>
    private static IEnumerable<(ChimeraParts.Kind Kind, Vector2 Anchor, float Size)> Anatomy(string graftId)
    {
        switch (graftId)
        {
            case "swarm_symbiote":
                foreach (var p in Nodules()) yield return p;
                break;

            case "erratic_servos":
                foreach (var p in Pistons()) yield return p;
                break;

            case "aiming_eye":
                yield return (ChimeraParts.Kind.Eye, new Vector2(0.30f, 0.40f), 0.32f);
                break;

            case "grafted_carapace":
                foreach (var p in Plates()) yield return p;
                break;

            case "stalker_wave":
                foreach (var p in Antennae()) yield return p;
                break;

            case "fusion_charge_blindee":
                foreach (var p in Plates()) yield return p;
                foreach (var p in Pistons()) yield return p;
                yield return (ChimeraParts.Kind.Horn, new Vector2(0.34f, 0.02f), 0.44f);
                break;

            case "fusion_ruche_tourelles":
                yield return (ChimeraParts.Kind.Pod, new Vector2(-0.36f, 0.26f), 0.34f);
                yield return (ChimeraParts.Kind.Pod, new Vector2(-0.36f, -0.14f), 0.34f);
                yield return (ChimeraParts.Kind.Eye, new Vector2(0.30f, 0.40f), 0.32f);
                foreach (var p in Nodules()) yield return p;
                break;

            case "fusion_nova_rodeur":
                foreach (var p in Antennae()) yield return p;
                foreach (var p in Pistons()) yield return p;
                break;

            default:
                // Une greffe inconnue ne doit pas passer inaperçue : elle s'équipe, occupe un
                // emplacement, et le corps n'en dirait rien — exactement le défaut qu'on corrige.
                Debug.LogWarning($"[ChimeraBody] aucune anatomie pour '{graftId}' — " +
                                 "la greffe est portee mais INVISIBLE sur le porteur.");
                break;
        }
    }

    private static IEnumerable<(ChimeraParts.Kind, Vector2, float)> Plates()
    {
        yield return (ChimeraParts.Kind.Plate, new Vector2(-0.34f, 0.22f), 0.40f);
        yield return (ChimeraParts.Kind.Plate, new Vector2(-0.40f, 0.00f), 0.44f);
        yield return (ChimeraParts.Kind.Plate, new Vector2(-0.34f, -0.22f), 0.40f);
    }

    private static IEnumerable<(ChimeraParts.Kind, Vector2, float)> Pistons()
    {
        yield return (ChimeraParts.Kind.Piston, new Vector2(0.28f, -0.30f), 0.42f);
        yield return (ChimeraParts.Kind.Piston, new Vector2(-0.26f, -0.34f), 0.42f);
    }

    private static IEnumerable<(ChimeraParts.Kind, Vector2, float)> Nodules()
    {
        yield return (ChimeraParts.Kind.Nodule, new Vector2(0.16f, 0.24f), 0.24f);
        yield return (ChimeraParts.Kind.Nodule, new Vector2(-0.06f, 0.06f), 0.20f);
        yield return (ChimeraParts.Kind.Nodule, new Vector2(0.22f, -0.16f), 0.22f);
    }

    private static IEnumerable<(ChimeraParts.Kind, Vector2, float)> Antennae()
    {
        yield return (ChimeraParts.Kind.Antenna, new Vector2(-0.12f, 0.30f), 0.46f);
        yield return (ChimeraParts.Kind.Antenna, new Vector2(0.10f, 0.34f), 0.40f);
    }

    private void Add(ChimeraParts.Kind kind, Vector2 anchor, float size, Color tint)
    {
        var go = new GameObject($"Greffe_{kind}_{_parts.Count}", typeof(SpriteRenderer));

        // Enfant du porteur, contrairement aux tourelles et aux orbiteurs : un appendice EST le corps,
        // il doit suivre chaque pixel de son déplacement sans un frame de retard.
        go.transform.SetParent(transform, false);

        var sr = go.GetComponent<SpriteRenderer>();
        sr.sprite = ChimeraParts.Get(kind);
        sr.color = tint;

        // Ce qui est dans le dos passe DERRIÈRE le corps, sinon la carapace recouvre le personnage
        // qu'elle est censée protéger — et l'on ne voit plus qui l'on joue.
        bool behind = anchor.x < 0f && kind != ChimeraParts.Kind.Piston;
        sr.sortingOrder = (_body != null ? _body.sortingOrder : 15) + (behind ? -1 : 1);

        _parts.Add(new Part
        {
            Kind = kind,
            Transform = go.transform,
            Renderer = sr,
            Anchor = anchor,
            Size = size,
            // Déphasage déterministe tiré du rang : deux plaques qui respirent ensemble se lisent
            // comme une seule pièce qui pulse.
            Phase = _parts.Count * 1.37f,
            Behind = behind,
        });
    }

    private void LateUpdate()
    {
        // ⚠ LateUpdate : l'animation image par image écrit le sprite du corps pendant Update, et le
        // retournement gauche/droite avec. Poser les appendices avant elle les ferait vaciller.
        if (_parts.Count == 0) return;

        _time += Time.deltaTime;
        Measure();

        float body = BodyPx;
        bool flip = _player != null && _player.FacingLeft;
        float mirror = flip ? -1f : 1f;

        // ⚠ La mesure du corps est en espace MONDE (elle tient déjà compte de l'échelle du porteur),
        // alors que ce qu'on pose est une échelle LOCALE. Sans cette division, un porteur rendu à 1,5
        // porterait des appendices une fois et demie trop grands — la confusion « facteur contre
        // taille » qui a déjà produit trois défauts dans ce portage.
        float parent = Mathf.Abs(transform.lossyScale.x);
        float k = parent > 0.001f ? 1f / parent : 1f;

        foreach (var part in _parts)
        {
            if (part.Transform == null) continue;

            // ⚠ La normalisation se fait sur le PLUS GRAND côté du sprite, jamais sur sa largeur
            // seule. Les formes ne sont pas carrées : l'antenne est dessinée dans un 6 × 16, et la
            // diviser par 6 la rendait 2,7 fois trop haute — deux tiges roses de 39 px plantées sur
            // un corps de 32, visibles au premier coup d'œil sur la capture et par rien d'autre.
            // Troisième variante du même piège : `localScale` est un facteur, pas une taille.
            var sprite = part.Renderer.sprite;
            float spritePx = sprite != null
                ? Mathf.Max(1f, Mathf.Max(sprite.rect.width, sprite.rect.height))
                : 1f;

            float scale = part.Size * body * k / spritePx;

            float wave = Mathf.Sin(_time * WaveSpeed(part.Kind) + part.Phase);

            // Même correction pour le point d'attache : il se calcule sur une mesure du monde et se
            // pose en coordonnées locales.
            Vector2 offset = (part.Anchor + Wobble(part.Kind, wave)) * (body * k);

            part.Transform.localPosition = new Vector3(offset.x * mirror, offset.y, 0f);

            // L'échelle porte le retournement : le corps est retourné par `SpriteRenderer.FlipX`, et
            // un appendice qui resterait dans son sens d'origine se retrouverait greffé du mauvais
            // côté dès que le joueur part à gauche.
            part.Transform.localScale = new Vector3(scale * mirror, scale * Breathe(part.Kind, wave), 1f);
            part.Transform.localRotation = Rotation(part.Kind, wave, flip);
        }
    }

    // ─── Mouvement propre à chaque forme ──────────────────────────────────────

    /// <summary>
    /// Vitesse d'oscillation, en radians par seconde.
    /// </summary>
    /// <remarks>
    /// Ce qui distingue une greffe d'une autre n'est pas sa couleur mais sa <b>grammaire de
    /// mouvement</b> : les servos sont <i>erratiques</i> (c'est leur nom), la carapace est lourde et
    /// lente, la rouille vivante bat comme un organe, l'antenne ondule. Une seule vitesse pour tout
    /// donnerait cinq greffes qui respirent de la même façon.
    /// </remarks>
    private static float WaveSpeed(ChimeraParts.Kind kind) => kind switch
    {
        ChimeraParts.Kind.Piston  => 13.5f,
        ChimeraParts.Kind.Nodule  => 5.2f,
        ChimeraParts.Kind.Antenna => 2.4f,
        ChimeraParts.Kind.Plate   => 1.6f,
        ChimeraParts.Kind.Pod     => 1.9f,
        _                         => 3.0f,
    };

    /// <summary>Débattement du point d'attache, en fractions du corps.</summary>
    private static Vector2 Wobble(ChimeraParts.Kind kind, float wave) => kind switch
    {
        ChimeraParts.Kind.Piston => new Vector2(0f, wave * 0.028f),
        ChimeraParts.Kind.Plate  => new Vector2(wave * -0.012f, 0f),
        ChimeraParts.Kind.Pod    => new Vector2(0f, wave * 0.016f),
        _                        => new Vector2(0f, wave * 0.020f),
    };

    /// <summary>Battement de l'échelle verticale — ce qui fait qu'un nodule paraît vivant.</summary>
    private static float Breathe(ChimeraParts.Kind kind, float wave) => kind switch
    {
        ChimeraParts.Kind.Nodule => 1f + wave * 0.16f,
        ChimeraParts.Kind.Piston => 1f + wave * 0.09f,
        _                        => 1f,
    };

    /// <summary>
    /// Orientation. L'œil est le seul appendice qui <b>vise</b> : il suit la direction de tir, ce qui
    /// en fait le seul retour permanent sur l'endroit que le joueur désigne.
    /// </summary>
    /// <remarks>
    /// ⚠ Il n'est pas retourné avec le corps : sa forme tourne sur 360°, et lui appliquer en plus un
    /// miroir le ferait viser à l'opposé une fois sur deux. C'est aussi pour cela qu'il est dessiné
    /// <b>sans ombrage cuit</b> — une pièce qui pivote emporterait sa lumière avec elle.
    /// </remarks>
    private Quaternion Rotation(ChimeraParts.Kind kind, float wave, bool flip)
    {
        if (kind == ChimeraParts.Kind.Eye)
        {
            var aim = _player?.AimDirection ?? Vector2.right;
            float deg = Mathf.Atan2(aim.y, aim.x) * Mathf.Rad2Deg;

            // Le miroir de l'échelle s'applique aussi à l'œil : on l'annule ici, sinon l'angle est lu
            // dans un repère retourné.
            return Quaternion.Euler(0f, 0f, flip ? 180f - deg : deg);
        }

        return kind switch
        {
            ChimeraParts.Kind.Antenna => Quaternion.Euler(0f, 0f, wave * 15f),
            ChimeraParts.Kind.Horn    => Quaternion.Euler(0f, 0f, wave * 3f),
            _                         => Quaternion.identity,
        };
    }

    /// <summary>
    /// Mesure la largeur du corps.
    /// </summary>
    /// <remarks>
    /// ⚠ <c>bounds</c> est <b>muet</b> tant qu'aucune image d'animation n'est posée : il rend zéro.
    /// La mesure est donc retentée à chaque image tant qu'elle a échoué — et le repli est le rayon de
    /// corps du joueur, pas une constante devinée.
    /// </remarks>
    private void Measure()
    {
        if (_bodyPx > 0f || _body == null) return;

        float width = _body.bounds.size.x;
        if (width > 0.01f) _bodyPx = width;
    }
}
