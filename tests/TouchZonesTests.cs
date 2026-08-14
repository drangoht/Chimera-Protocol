using Xunit;

/// <summary>
/// Le découpage de l'écran tactile.
///
/// <para>Ces zones sont lues par deux couches qui ne se parlent pas — le dessin des boutons et la
/// lecture des doigts. Le mode d'échec propre au tactile est donc <b>un bouton qui se voit et ne
/// répond pas</b>, ou qui répond à côté : rien ne l'annonce, et il ne se reproduit souvent que sur
/// une taille d'écran précise. D'où des tests qui balaient les dalles réelles plutôt qu'une seule.
/// </para>
/// </summary>
public class TouchZonesTests
{
    /// <summary>
    /// Dalles réellement rencontrées, en paysage : téléphone compact, téléphone courant, téléphone
    /// large, tablette, portable. La première est celle où tout casse.
    /// </summary>
    public static TheoryData<float, float> Screens => new()
    {
        { 640f, 280f },     // petit téléphone, navigateur avec barre d'URL visible
        { 800f, 360f },     // téléphone courant en paysage
        { 915f, 412f },     // grand téléphone
        { 1024f, 768f },    // tablette
        { 1920f, 1080f },   // portable / bureau
    };

    // ─── Les boutons tiennent à l'écran ──────────────────────────────────────

    [Theory]
    [MemberData(nameof(Screens))]
    public void LesBoutonsSontEntierementDansLEcran(float w, float h)
    {
        var (dx, dy) = TouchZones.DashCenter(w, h);
        float dr = TouchZones.DashRadius(h);

        Assert.InRange(dx - dr, 0f, w);
        Assert.InRange(dx + dr, 0f, w);
        Assert.InRange(dy - dr, 0f, h);
        Assert.InRange(dy + dr, 0f, h);

        var (px, py) = TouchZones.PauseCenter(w, h);
        float pr = TouchZones.PauseRadius(h);

        Assert.InRange(px - pr, 0f, w);
        Assert.InRange(px + pr, 0f, w);
        Assert.InRange(py - pr, 0f, h);
        Assert.InRange(py + pr, 0f, h);
    }

    /// <summary>
    /// Une cible tactile plus petite qu'environ 9 mm se manque une fois sur trois. Le rayon plancher
    /// tient cet engagement même quand la dalle est très basse.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void AucuneCibleNePasseSousLeSeuilDuPouce(float w, float h)
    {
        _ = w;
        Assert.True(TouchZones.DashRadius(h) >= TouchZones.MinButtonRadiusPx);
        Assert.True(TouchZones.PauseRadius(h) >= TouchZones.MinButtonRadiusPx);
    }

    /// <summary>
    /// La pause en haut, l'esquive en bas : elles ne doivent jamais se toucher, y compris avec la
    /// marge de tolérance de l'esquive. Une pause déclenchée à la place d'une esquive pendant une
    /// nuée est une mort.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void PauseEtEsquiveNeSeRecouvrentJamais(float w, float h)
    {
        var (dx, dy) = TouchZones.DashCenter(w, h);
        var (px, py) = TouchZones.PauseCenter(w, h);

        float gap = (float)System.Math.Sqrt((dx - px) * (dx - px) + (dy - py) * (dy - py));
        float radii = TouchZones.DashRadius(h) * TouchZones.DashTouchSlop + TouchZones.PauseRadius(h);

        Assert.True(gap > radii, $"ecran {w}x{h} : {gap:0} px entre les centres pour {radii:0} px de rayons");
    }

    // ─── Le stick et les boutons ne se disputent pas un doigt ────────────────

    [Theory]
    [MemberData(nameof(Screens))]
    public void UnDoigtSurUnBoutonNeFaitPasNaitreLeStick(float w, float h)
    {
        var (dx, dy) = TouchZones.DashCenter(w, h);
        var (px, py) = TouchZones.PauseCenter(w, h);

        Assert.False(TouchZones.IsStickZone(dx, dy, w, h));
        Assert.False(TouchZones.IsStickZone(px, py, w, h));
    }

    /// <summary>
    /// La zone sensible de l'esquive ne doit pas déborder dans la moitié gauche : le pouce de
    /// déplacement s'y pose en permanence, et une esquive involontaire consomme la recharge au
    /// moment où le joueur en aurait besoin.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void LaToleranceDeLEsquiveNEmpieteJamaisSurLaMoitieGauche(float w, float h)
    {
        var (dx, _) = TouchZones.DashCenter(w, h);
        float sensitiveLeftEdge = dx - TouchZones.DashRadius(h) * TouchZones.DashTouchSlop;

        Assert.True(sensitiveLeftEdge > w * TouchZones.StickWidthFraction,
            $"ecran {w}x{h} : la zone d'esquive commence a {sensitiveLeftEdge:0} px, " +
            $"soit avant {w * TouchZones.StickWidthFraction:0}");
    }

    [Theory]
    [MemberData(nameof(Screens))]
    public void LeBasGaucheFaitNaitreLeStick(float w, float h)
    {
        Assert.True(TouchZones.IsStickZone(w * 0.05f, h * 0.1f, w, h));
        Assert.True(TouchZones.IsStickZone(w * 0.45f, h * 0.5f, w, h));
    }

    /// <summary>
    /// Le bandeau du HUD ne fait pas naître de stick : un pouce qui s'y pose vient de manquer un
    /// bouton, il ne demande pas à courir.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void LeBandeauDuHudNeFaitPasNaitreLeStick(float w, float h)
    {
        Assert.False(TouchZones.IsStickZone(w * 0.2f, h * 0.95f, w, h));
    }

    // ─── La tolérance de l'esquive ───────────────────────────────────────────

    /// <summary>
    /// Un appui juste à côté du bouton dessiné compte quand même : le doigt masque ce qu'il touche,
    /// le joueur vise ce qu'il a vu.
    /// </summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void UnAppuiJusteACoteDeclencheQuandMemeLEsquive(float w, float h)
    {
        var (dx, dy) = TouchZones.DashCenter(w, h);
        float justOutside = TouchZones.DashRadius(h) * 1.2f;

        Assert.True(TouchZones.IsDashButton(dx - justOutside, dy, w, h));
        Assert.True(TouchZones.DashTouchSlop > 1f);
    }

    /// <summary>La pause, elle, n'a pas de tolérance — pour la raison exactement inverse.</summary>
    [Theory]
    [MemberData(nameof(Screens))]
    public void LaPauseNAPasDeTolerance(float w, float h)
    {
        var (px, py) = TouchZones.PauseCenter(w, h);
        float outside = TouchZones.PauseRadius(h) * 1.2f;

        Assert.False(TouchZones.IsPauseButton(px - outside, py, w, h));
        Assert.True(TouchZones.IsPauseButton(px, py, w, h));
    }

    // ─── Échelle de l'interface ──────────────────────────────────────────────

    private const float RefW = 1920f;
    private const float RefH = 1080f;

    /// <summary>Échelle finale de l'interface, telle que la calcule un CanvasScaler à mi-chemin.</summary>
    private static float UiScale(float w, float h)
    {
        float k = TouchZones.UiEnlargement(w, h, RefW, RefH);
        return (float)System.Math.Sqrt((w / (RefW / k)) * (h / (RefH / k)));
    }

    /// <summary>
    /// Le défaut visé : sur un téléphone, l'interface tombait à 0,37 et un bouton de 60 unités y
    /// mesurait 22 pixels — quatre millimètres, moins que le doigt qui le cherche.
    /// </summary>
    [Theory]
    [InlineData(800f, 360f)]     // téléphone courant en paysage
    [InlineData(915f, 412f)]     // grand téléphone
    [InlineData(667f, 331f)]     // petit téléphone, barre d'URL visible
    public void UnTelephoneGardeDesLignesDeMenuTouchables(float w, float h)
    {
        float row = 60f * UiScale(w, h);   // hauteur d'une ligne cliquable du jeu

        Assert.True(row >= 40f, $"ecran {w}x{h} : une ligne de menu ferait {row:0} px");
    }

    /// <summary>
    /// Sur la plus petite dalle imaginable, le plafond l'emporte sur la cible : on donne alors
    /// <b>tout</b> ce qu'on s'autorise, et pas moins. Ce test existe pour que baisser le plafond sans
    /// y penser se voie.
    /// </summary>
    [Fact]
    public void SurUneDalleMinusculeLeGrossissementEstSature()
    {
        Assert.Equal(TouchZones.MaxUiEnlargement,
                     TouchZones.UiEnlargement(640f, 280f, RefW, RefH), 4);
    }

    /// <summary>
    /// Un écran de bureau ne change pas d'un pixel : le grossissement ne doit toucher que là où le
    /// problème existe, sinon il devient une régression sur la plateforme qui marche.
    /// </summary>
    [Theory]
    [InlineData(1920f, 1080f)]
    [InlineData(2560f, 1440f)]
    [InlineData(1600f, 900f)]
    [InlineData(1280f, 720f)]
    [InlineData(1024f, 768f)]     // tablette : grande, donc pas concernée
    [InlineData(960f, 600f)]      // juste au seuil
    public void UnEcranDeBureauNEstJamaisGrossi(float w, float h)
    {
        Assert.Equal(1f, TouchZones.UiEnlargement(w, h, RefW, RefH), 4);
    }

    /// <summary>
    /// Le grossissement est borné : au-delà d'un facteur deux, les panneaux posés en unités absolues
    /// sortiraient de l'écran, et un panneau tronqué est pire que des boutons petits.
    /// </summary>
    [Theory]
    [InlineData(320f, 200f)]
    [InlineData(200f, 120f)]
    public void LeGrossissementResteBorne(float w, float h)
    {
        Assert.InRange(TouchZones.UiEnlargement(w, h, RefW, RefH), 1f, TouchZones.MaxUiEnlargement);
    }

    [Fact]
    public void UneFenetreDegenereeNeGrossitRien()
    {
        Assert.Equal(1f, TouchZones.UiEnlargement(0f, 0f, RefW, RefH), 4);
        Assert.Equal(1f, TouchZones.UiEnlargement(800f, 360f, 0f, 0f), 4);
    }

    // ─── Orientation ─────────────────────────────────────────────────────────

    [Fact]
    public void LePortraitSeReconnaitALaFormeDuCanevas_PasAuSysteme()
    {
        Assert.True(TouchZones.IsPortrait(360f, 800f));
        Assert.False(TouchZones.IsPortrait(800f, 360f));
        Assert.False(TouchZones.IsPortrait(500f, 500f));   // carré : pas portrait
    }
}
