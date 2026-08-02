using Godot;
using System.Collections.Generic;

/// <summary>
/// <b>Props de silhouette</b> de <see cref="GraftManager"/> — la partie <b>visuelle</b> des greffes,
/// séparée de leur logique de combat (classe partielle, mêmes champs, aucun changement de
/// comportement).
///
/// <para>Chaque greffe/fusion sans nœud visuel propre (le swarm et les tourelles en ont déjà) reçoit
/// un « prop » : un petit assemblage de <c>Polygon2D</c> ancré au corps du joueur et ombré en
/// pseudo-3D (lumière haut-gauche, cf. <c>docs/ART_BRIEF_PSEUDO3D.md</c> §1-2). Procédural — pas
/// d'asset PNG — donc cohérent avec les essaims et tourelles déjà procéduraux, et surtout
/// <b>indépendant du personnage</b> : le même prop marche pour les 4 corps jouables sans art par perso
/// ni par frame. C'est le choix « props attachés » plutôt que « couches par frame ».</para>
///
/// <para><b>Pourquoi ce fichier existe.</b> Ces ~290 lignes de géométrie ne touchent ni les stats, ni
/// les comportements, ni l'état des greffes : les garder dans le fichier principal noyait la logique
/// de combat sous du dessin. Voir <c>docs/DESIGN_ASSIMILATION.md</c> §14.</para>
/// </summary>
public partial class GraftManager : Node2D
{
    // Props actifs : carapace, servos, œil, résonateur, proue de charge, cœur de ruche, cœur de nova.
    private readonly List<GraftProp> _props = new();
    private float _propBob; // phase d'oscillation partagée (respiration des props)

    /// <summary>Un prop de silhouette ancré au joueur (espace local du GraftManager = espace joueur).</summary>
    private sealed class GraftProp
    {
        public Node2D Node = null!;
        public Node2D? Sub;                                   // sous-élément animé (pupille de l'œil, vents…)
        public Vector2 Anchor;                                // offset local depuis le centre du joueur
        public bool Mirror;                                   // miroir X selon le facing (props directionnels)
        public System.Action<GraftProp, float>? Update;       // animation par frame (bob/rotation/visée/pulse)
    }

    private void UpdateProps(float dt)
    {
        _propBob += dt;
        bool left = _player.FacingLeft;
        for (int i = 0; i < _props.Count; i++)
        {
            var p = _props[i];
            if (!IsInstanceValid(p.Node)) continue;
            float ax = p.Mirror && left ? -p.Anchor.X : p.Anchor.X;
            p.Node.Position = new Vector2(ax, p.Anchor.Y);
            if (p.Mirror) p.Node.Scale = new Vector2(left ? -1f : 1f, 1f);
            p.Update?.Invoke(p, dt);
        }
    }

    /// <summary>Construit le prop de silhouette d'une greffe/fusion (bespoke par id ; le swarm et les
    /// tourelles n'en ont pas — leurs nœuds servent déjà de silhouette).</summary>
    private void BuildPropFor(GraftTable.GraftDef def)
    {
        var b = BaseColorFromTint(def.Tint);
        // Accent de biome (§21) baké dans la couleur de matière → l'ombrage pseudo-3D en dérive et
        // les Update qui modulent le prop en préservent la teinte. Ignoré si affinité neutre (blanc).
        var acc = AffFor(def.Id).Accent;
        if (acc[0] < 0.99f || acc[1] < 0.99f || acc[2] < 0.99f)
            b = b.Lerp(new Color(acc[0], acc[1], acc[2]), 0.22f);
        switch (def.Id)
        {
            case "grafted_carapace":        BuildCarapaceProp(b); break;
            case "erratic_servos":          BuildServosProp(b);   break;
            case "aiming_eye":              BuildEyeProp(b);      break;
            case "stalker_wave":            BuildWaveProp(b);     break;
            case "fusion_charge_blindee":   BuildChargeProwProp(b); break;
            case "fusion_ruche_tourelles":  BuildHiveCoreProp(b);   break;
            case "fusion_nova_rodeur":      BuildNovaCoreProp(b);   break;
        }
    }

    private void AddProp(Node2D node, Vector2 anchor, bool mirror, int z,
                         Node2D? sub = null, System.Action<GraftProp, float>? update = null)
    {
        node.ZIndex = z;
        node.Position = anchor;
        AddChild(node);
        _props.Add(new GraftProp { Node = node, Sub = sub, Anchor = anchor, Mirror = mirror, Update = update });
    }

    // ── Carapace Greffée : pauldrons blindés + plastron sur le haut du corps ──
    private void BuildCarapaceProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var node = new Node2D();
        // Plastron (bande d'armure sur les épaules) : base + reflet haut + ombre basse.
        node.AddChild(P(new[] { V(-8, -1), V(8, -1), V(7, 4), V(-7, 4) }, b));
        node.AddChild(P(new[] { V(-8, -1), V(8, -1), V(8, 0), V(-8, 0) }, hi));
        node.AddChild(P(new[] { V(-7, 3), V(7, 3), V(7, 4), V(-7, 4) }, sh));
        // Pauldron gauche (tourné vers la lumière) : base éclaircie.
        node.AddChild(P(new[] { V(-11, -4), V(-4, -5), V(-3, 0), V(-10, 1) }, b));
        node.AddChild(P(new[] { V(-11, -4), V(-4, -5), V(-4, -4), V(-10, -2) }, hi));
        // Pauldron droit (à l'ombre) : base assombrie.
        node.AddChild(P(new[] { V(4, -5), V(11, -4), V(10, 1), V(3, 0) }, sh));
        node.AddChild(P(new[] { V(4, -5), V(11, -4), V(11, -3), V(4, -4) }, b));
        AddProp(node, new Vector2(0, 1), mirror: false, z: 1);
    }

    // ── Servos Erratiques : deux tuyères sur les flancs bas (débordent la silhouette pour rester
    //    lisibles), vents lumineux qui pulsent — et s'embrasent pendant le dash ──
    private void BuildServosProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var vent = new Color(Mathf.Min(b.R * 1.7f, 1f), Mathf.Min(b.G * 1.7f, 1f), Mathf.Min(b.B * 1.8f, 1f));
        var node = new Node2D();
        // Tuyère gauche + droite (biseaux dépassant hors du corps, x jusqu'à ±11), pointant bas-dehors.
        node.AddChild(P(new[] { V(-6, -2), V(-11, 2), V(-9, 8), V(-4, 4) }, b));
        node.AddChild(P(new[] { V(-6, -2), V(-11, 2), V(-9, 3), V(-5, 0) }, hi));
        node.AddChild(P(new[] { V(6, -2), V(11, 2), V(4, 4), V(9, 8) }, sh));
        node.AddChild(P(new[] { V(6, -2), V(11, 2), V(9, 3), V(5, 0) }, hi));
        // Sous-nœud des vents (tips lumineux, pulsent/s'embrasent dans Update).
        var vents = new Node2D();
        vents.AddChild(P(new[] { V(-10, 5), V(-7, 5), V(-8, 9), V(-11, 8) }, vent));
        vents.AddChild(P(new[] { V(7, 5), V(10, 5), V(11, 8), V(8, 9) }, vent));
        node.AddChild(vents);
        AddProp(node, new Vector2(0, 5), mirror: false, z: 1, sub: vents, update: (p, dt) =>
        {
            float pulse = _player.IsDashing ? 1f : 0.5f + 0.3f * Mathf.Sin(_propBob * 6f);
            if (p.Sub != null) p.Sub.Modulate = new Color(1f, 1f, 1f, pulse);
        });
    }

    // ── Œil de Visée : orbe flottant au-dessus de la tête, pupille qui suit l'ennemi le plus proche ──
    private void BuildEyeProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var sclera = new Color(0.92f, 0.94f, 0.98f);
        var node = new Node2D();
        // Sclère (octogone pâle) → iris teinté → pupille sombre (sous-nœud mobile).
        node.AddChild(P(Octagon(6f), sh));            // contour/ombre
        node.AddChild(P(Octagon(5f), sclera));        // blanc de l'œil
        node.AddChild(P(Octagon(3.2f), b));           // iris
        node.AddChild(P(Octagon(3.2f, topHalf: true), hi)); // reflet haut de l'iris
        var pupil = new Node2D();
        pupil.AddChild(P(Octagon(1.6f), new Color(0.05f, 0.05f, 0.09f)));
        node.AddChild(pupil);
        AddProp(node, new Vector2(0, -15), mirror: false, z: 2, sub: pupil, update: (p, dt) =>
        {
            p.Node.Position += new Vector2(0, Mathf.Sin(_propBob * 3f) * 1.3f); // flottaison
            var target = NearestEnemyTo(_player.GlobalPosition, 420f);
            Vector2 dir = target != null
                ? (target.GlobalPosition - _player.GlobalPosition).Normalized()
                : Vector2.Zero;
            if (p.Sub != null)
                p.Sub.Position = p.Sub.Position.Lerp(dir * 1.6f, 0.25f);
        });
    }

    // ── Onde du Rôdeur : couronne-résonateur qui tourne et enfle juste avant chaque onde ──
    private void BuildWaveProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var node = new Node2D();
        // 3 nœuds-diapasons sur un anneau (radius ~13), reliés par de fins segments.
        const float r = 13f;
        var ring = new Line2D { Width = 1.4f, DefaultColor = new Color(b.R, b.G, b.B, 0.35f), Closed = true };
        var pts = new Godot.Collections.Array<Vector2>();
        for (int i = 0; i < 3; i++)
        {
            float a = i * Mathf.Tau / 3f;
            var pos = Vector2.Right.Rotated(a) * r;
            pts.Add(pos);
            var nub = new Node2D { Position = pos, Rotation = a };
            nub.AddChild(P(new[] { V(3, 0), V(-2, -2), V(-2, 2) }, b));
            nub.AddChild(P(new[] { V(3, 0), V(-2, -2), V(0, -1) }, hi));
            node.AddChild(nub);
        }
        ring.Points = pts.ToArray();
        node.AddChild(ring);
        AddProp(node, new Vector2(0, 1), mirror: false, z: -1, update: (p, dt) =>
        {
            p.Node.Rotation += dt * 0.9f;
            float ratio = _shockCd > 0.01f ? Mathf.Clamp(1f - _shockTimer / _shockCd, 0f, 1f) : 0f;
            float s = 1f + 0.22f * ratio; // enfle en anticipation de l'onde
            p.Node.Scale = new Vector2(s, s);
        });
    }

    // ── Fusion Charge Blindée : proue blindée orientée vers le facing, s'illumine à la charge ──
    private void BuildChargeProwProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var node = new Node2D();
        // Coque épaisse (héritage carapace) + proue en biseau pointant +x.
        node.AddChild(P(new[] { V(-7, -3), V(4, -3), V(4, 4), V(-7, 4) }, b));
        node.AddChild(P(new[] { V(-7, -3), V(4, -3), V(4, -2), V(-7, -2) }, hi));
        node.AddChild(P(new[] { V(-7, 3), V(4, 3), V(4, 4), V(-7, 4) }, sh));
        // Proue (sous-nœud, s'illumine au dash).
        var prow = new Node2D();
        prow.AddChild(P(new[] { V(4, -3), V(11, 0), V(4, 3) }, hi));
        prow.AddChild(P(new[] { V(4, 0), V(11, 0), V(4, 3) }, sh));
        node.AddChild(prow);
        AddProp(node, new Vector2(2, 1), mirror: true, z: 1, sub: prow, update: (p, dt) =>
        {
            if (p.Sub != null)
                p.Sub.Modulate = _player.IsDashing ? new Color(1.8f, 1.7f, 1.4f) : Colors.White;
        });
    }

    // ── Fusion Ruche de Tourelles : petit cœur de ruche (grappe d'alvéoles) dans le dos ──
    private void BuildHiveCoreProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var sh = Shade(b, Face.Shadow);
        var node = new Node2D();
        // 4 alvéoles hexagonales serrées, teinte des tourelles (cyan).
        var cells = new[] { V(-3, -4), V(3, -4), V(-4, 1), V(3, 1) };
        foreach (var c in cells)
        {
            node.AddChild(P(Hexagon(3.2f, c), sh));
            node.AddChild(P(Hexagon(2.4f, c), b));
            node.AddChild(P(Hexagon(2.4f, c, topHalf: true), hi));
        }
        AddProp(node, new Vector2(0, -3), mirror: false, z: 1, update: (p, dt) =>
        {
            float g = 0.85f + 0.15f * Mathf.Sin(_propBob * 4f); // léger battement
            p.Node.Modulate = new Color(g, g, g);
        });
    }

    // ── Fusion Frappe Nova : cœur d'étoile pulsant qui s'embrase au dash (annonce la nova) ──
    private void BuildNovaCoreProp(Color b)
    {
        var hi = Shade(b, Face.Highlight);
        var node = new Node2D();
        // Étoile à 4 branches (halo sombre + corps teinté), pivote lentement.
        var star = new[] { V(0, -7), V(2, -2), V(7, 0), V(2, 2), V(0, 7), V(-2, 2), V(-7, 0), V(-2, -2) };
        node.AddChild(P(NovaStar(1.3f), new Color(b.R * 0.3f, b.G * 0.2f, b.B * 0.3f, 0.6f)));
        node.AddChild(P(star, b));
        var core = new Node2D();
        core.AddChild(P(Octagon(2.6f), hi));
        node.AddChild(core);
        AddProp(node, new Vector2(0, 1), mirror: false, z: 1, sub: core, update: (p, dt) =>
        {
            p.Node.Rotation += dt * 1.3f;
            float flare = _player.IsDashing ? 2.0f : 1f + 0.25f * Mathf.Sin(_propBob * 5f);
            if (p.Sub != null) { p.Sub.Scale = new Vector2(flare, flare); p.Sub.Modulate = new Color(flare, flare, flare); }
        });
    }

    private static Vector2[] NovaStar(float s)
    {
        var pts = new[] { V(0, -7), V(2, -2), V(7, 0), V(2, 2), V(0, 7), V(-2, 2), V(-7, 0), V(-2, -2) };
        for (int i = 0; i < pts.Length; i++) pts[i] *= s;
        return pts;
    }

    // ── Primitives géométriques ──
    private static Polygon2D P(Vector2[] pts, Color c) => new() { Polygon = pts, Color = c };
    private static Vector2 V(float x, float y) => new(x, y);

    private static Vector2[] Octagon(float r, bool topHalf = false)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < 8; i++)
        {
            float a = Mathf.Pi / 8f + i * Mathf.Tau / 8f;
            var pt = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r * 0.9f); // légèrement aplati
            if (topHalf && pt.Y > 0f) pt.Y = 0f;
            list.Add(pt);
        }
        return list.ToArray();
    }

    private static Vector2[] Hexagon(float r, Vector2 center = default, bool topHalf = false)
    {
        var list = new List<Vector2>();
        for (int i = 0; i < 6; i++)
        {
            float a = Mathf.Pi / 6f + i * Mathf.Tau / 6f;
            var pt = center + new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            if (topHalf && pt.Y > center.Y) pt.Y = center.Y;
            list.Add(pt);
        }
        return list.ToArray();
    }

    // ── Ombrage pseudo-3D (lumière haut-gauche, ART_BRIEF_PSEUDO3D §2) répliqué en C# pour les
    //    props procéduraux (la lib PIL ne sert que les PNG pré-rendus). ──
    private enum Face { Highlight, Base, Shadow, Contact }

    private static Color Shade(Color b, Face face)
    {
        float h = b.H, s = b.S, v = b.V;
        switch (face)
        {
            case Face.Highlight: v = Mathf.Min(v * 1.35f, 1f); s *= 0.85f; break;
            case Face.Shadow:    v *= 0.55f; s = Mathf.Min(s * 1.10f, 1f); break;
            case Face.Contact:   v *= 0.35f; s = Mathf.Min(s * 1.15f, 1f); break;
        }
        var c = Color.FromHsv(h, s, v);
        return new Color(c.R, c.G, c.B, b.A);
    }

    /// <summary>Normalise une teinte-multiplicateur (canaux &gt; 1 possibles, ex. servos [0.6,0.85,1.3])
    /// en couleur de matière lisible (canal dominant ramené à ~0.85), en préservant la teinte.</summary>
    private static Color BaseColorFromTint(float[] tint)
    {
        float r = tint[0], g = tint[1], bl = tint[2];
        float max = Mathf.Max(r, Mathf.Max(g, bl));
        if (max <= 0.001f) return new Color(0.8f, 0.8f, 0.8f);
        float k = 0.85f / max;
        return new Color(r * k, g * k, bl * k);
    }
}
