/// <summary>
/// Noms et descriptions du <b>contenu</b> — armes, fusions, passifs, greffes, améliorations du Hub,
/// ennemis — traduits depuis <c>ui.csv</c>, avec repli sur le texte des données.
///
/// <para><b>Pourquoi cette classe existe.</b> Tout le contenu nommé du jeu vit dans les JSON de
/// <c>StreamingAssets/data</c>, et il y est écrit <b>en français</b>. Les écrans affichaient ce
/// texte tel quel : un joueur anglophone voyait un Hub dont l'en-tête était traduit et les
/// dix-neuf lignes ne l'étaient pas, un Codex entièrement français, des noms d'armes français dans
/// son HUD. Le jeu se dit localisé en trois langues.</para>
///
/// <para><b>Le principe.</b> La donnée reste la source des <i>identifiants</i> et des chiffres ;
/// <c>ui.csv</c> devient la source du <i>texte affiché</i>. Une clé se déduit mécaniquement de
/// l'identifiant — <c>tesla_coil</c> → <c>WEAPON_TESLA_COIL_NAME</c> — si bien qu'ajouter une arme
/// n'oblige pas à tenir une table de correspondance à jour.</para>
///
/// <para>⚠ <b>Le repli est volontaire, et c'est un piège.</b> Une clé absente rend le texte
/// français du JSON : à l'écran, cela ressemble à un jeu qui marche. C'est exactement ainsi que le
/// défaut a survécu au portage — les clés <c>GRAFT_</c> et <c>ENEMY_</c> étaient dans
/// <c>ui.csv</c>, traduites, et personne ne les lisait. La parade n'est donc pas dans le code mais
/// dans un contrôle : <c>tools/audit_loc_keys.py</c> déclare toute clé manquante comme une erreur,
/// et il connaît la même convention de nommage que ce fichier. <b>Les deux doivent bouger
/// ensemble.</b></para>
/// </summary>
public static class ContentText
{
    // ─── Armes, fusions, passifs (weapons.json) ──────────────────────────────
    //
    // ⚠ <b>Un seul préfixe pour les armes ET les fusions</b> : `WPN_`. Il est plus court que le mot
    // qu'il abrège, mais c'est celui que les cartes de montée de niveau lisaient déjà, avec ses
    // vingt-et-une entrées traduites en trois langues. Créer un `WEAPON_` « plus propre » à côté
    // aurait donné deux tables pour les mêmes douze armes — et la garantie qu'un jour l'une des deux
    // serait mise à jour seule.

    public static string WeaponName(string id, string fallback) => Get("WPN", id, "NAME", fallback);
    public static string WeaponDesc(string id, string fallback) => Get("WPN", id, "DESC", fallback);

    /// <summary>Une fusion se nomme sous le <b>même</b> préfixe qu'une arme : c'en est une.</summary>
    public static string FusionName(string id, string fallback) => Get("WPN", id, "NAME", fallback);

    public static string PassiveName(string id, string fallback) => Get("PAS", id, "NAME", fallback);
    public static string PassiveDesc(string id, string fallback) => Get("PAS", id, "DESC", fallback);

    // ─── Greffes et greffes de fusion (grafts.json) ──────────────────────────
    //
    // Un seul préfixe pour les deux : le Codex Chimère les affiche dans la même liste, et l'écran
    // d'Assimilation les propose de la même façon. Deux conventions pour un seul écran n'auraient
    // servi qu'à en oublier une.

    public static string GraftName(string id, string fallback) => Get("GRAFT", id, "NAME", fallback);
    public static string GraftDesc(string id, string fallback) => Get("GRAFT", id, "DESC", fallback);

    // ─── Améliorations permanentes du Hub (meta_upgrades.json) ───────────────

    public static string MetaName(string id, string fallback) => Get("META", id, "NAME", fallback);
    public static string MetaDesc(string id, string fallback) => Get("META", id, "DESC", fallback);

    // ─── Ennemis (enemies.json + enemies_biome_expansion.json) ───────────────

    public static string EnemyName(string id, string fallback) => Get("ENEMY", id, "NAME", fallback);
    public static string EnemyTag(string id, string fallback) => Get("ENEMY", id, "TAG", fallback);

    /// <summary>
    /// ⚠ <b>Aucun écran n'appelle encore cette méthode</b>, alors que <c>ui.csv</c> porte
    /// <b>31 descriptions d'ennemis</b> écrites et traduites en trois langues (<c>ENEMY_*_DESC</c>).
    /// Le bestiaire du Codex n'affiche que le rôle et les statistiques : ce texte existe, il est
    /// contrôlé par <c>tools/audit_loc_keys.py</c>… et le joueur ne l'a jamais vu. Même famille que
    /// le défaut corrigé en 2.0.1, à ceci près que le repli ne se voit même pas à l'écran.
    /// </summary>
    public static string EnemyDesc(string id, string fallback) => Get("ENEMY", id, "DESC", fallback);

    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>(&quot;WEAPON&quot;, &quot;tesla_coil&quot;, &quot;NAME&quot;)</c> →
    /// <c>WEAPON_TESLA_COIL_NAME</c>, ou <paramref name="fallback"/> si la clé manque.
    /// </summary>
    /// <remarks>
    /// <see cref="Loc.T"/> rend la <b>clé elle-même</b> quand elle est absente : c'est ce que teste
    /// la comparaison. Un identifiant vide rend directement le repli — le jeu manipule des
    /// identifiants vides en toute légitimité (un emplacement d'arme libre, par exemple), et
    /// interroger la table pour <c>WEAPON__NAME</c> n'aurait aucun sens.
    /// </remarks>
    private static string Get(string prefix, string id, string field, string fallback)
    {
        if (string.IsNullOrEmpty(id)) return fallback;

        string key = Key(prefix, id, field);
        string translated = Loc.T(key);

        return translated != key ? translated : fallback;
    }

    /// <summary>
    /// Clé de traduction d'un contenu : <c>("WPN", "tesla_coil", "NAME")</c> →
    /// <c>WPN_TESLA_COIL_NAME</c>.
    /// </summary>
    /// <remarks>
    /// Exposée parce que l'écran de montée de niveau, lui, doit <b>essayer plusieurs préfixes</b>
    /// (une carte peut être une arme, un passif ou une carte d'écran) : il a besoin de la clé, pas
    /// du texte. Il la fabriquait donc à la main, ce qui donnait une <b>seconde</b> définition de la
    /// convention de nommage — celle-là même dont <c>tools/audit_loc_keys.py</c> est le contrôle.
    /// Deux conventions, un seul audit : le jour où l'une bouge, l'autre affiche des noms d'armes en
    /// français sans que rien ne le signale.
    /// </remarks>
    public static string Key(string prefix, string id, string field)
        => $"{prefix}_{id.ToUpperInvariant()}_{field}";
}
