using System;
using System.Collections.Generic;

/// <summary>
/// Un personnage jouable : le profil de départ d'une run.
/// </summary>
/// <remarks>
/// <para>Trois choses, et trois seulement. Les <b>PV</b> et la <b>vitesse</b> de départ, parce que
/// c'est le seul couple qui se ressent dès la première seconde de jeu ; et l'<b>arme de
/// signature</b>, qui décide de la façon dont on tue pendant les deux premières minutes — donc de ce
/// qu'on apprend du jeu.</para>
/// <para><b>Aucune teinte.</b> Le GDD §26 en mentionne une pour le Vecteur, héritée de l'ère Godot ;
/// elle n'a plus lieu d'être. Chaque personnage a son propre jeu de sprites, déjà colorés par
/// <c>tools/generate_character_sprites.py</c> (palettes <c>T_*</c>, <c>VG_*</c>, <c>VC_*</c>) :
/// appliquer une teinte par-dessus <i>recolorerait</i> un sprite déjà coloré, et le résultat ne
/// ressemblerait ni à l'un ni à l'autre.</para>
/// </remarks>
public sealed class CharacterDef
{
    public CharacterDef(string id, float maxHp, float moveSpeed, string weaponId, string framesId)
    {
        Id = id;
        MaxHp = maxHp;
        MoveSpeed = moveSpeed;
        WeaponId = weaponId;
        FramesId = framesId;
    }

    /// <summary>Identifiant stable — persisté dans la sauvegarde et attendu par <c>--character=</c>.</summary>
    public string Id { get; }

    /// <summary>PV de départ, <b>avant</b> les achats du Hub.</summary>
    public float MaxHp { get; }

    /// <summary>Vitesse de départ en unités/s, <b>avant</b> les achats du Hub.</summary>
    public float MoveSpeed { get; }

    /// <summary>Arme portée dès la première image. Doit figurer dans <c>GameSettings.SignatureWeapons</c>.</summary>
    public string WeaponId { get; }

    /// <summary>Jeu d'animations, dans <c>Resources/SpriteFrames/</c>.</summary>
    public string FramesId { get; }

    /// <summary>Clé de localisation du nom (<c>CHAR_TITAN_NAME</c>…).</summary>
    public string NameKey => $"CHAR_{Id.ToUpperInvariant()}_NAME";

    /// <summary>Clé de la ligne d'archétype (« Robot lourd — tank »).</summary>
    public string TagKey => $"CHAR_{Id.ToUpperInvariant()}_TAG";

    /// <summary>Clé de la description longue.</summary>
    public string DescKey => $"CHAR_{Id.ToUpperInvariant()}_DESC";
}

/// <summary>
/// <b>Les quatre personnages jouables.</b>
///
/// <para><b>⚠ Ce système a mis un an à exister, et c'est la douzième fois que ce projet trouve la
/// même chose.</b> L'écran de sélection n'a jamais été porté depuis Godot — mais tout le reste
/// l'avait été, silencieusement : les <b>douze clés</b> <c>CHAR_*</c> dorment dans <c>ui.csv</c>
/// <i>traduites en trois langues</i>, les quatre jeux d'animations sont générés et importés
/// (<c>Resources/SpriteFrames/{player,titan,vagabond,vecteur}.asset</c>), et
/// <c>GameSettings.SignatureWeapons</c> énumère les quatre armes de départ pour les marquer
/// « découvertes » au Codex. Trois systèmes complets au service d'un choix que personne ne pouvait
/// faire. <i>Déclaré n'est pas consommé</i> — et cette fois le déclaré était du contenu fini, prêt,
/// payé, jusque dans sa traduction espagnole.</para>
///
/// <para><b>⚠ Les chiffres du Titan, du Vagabond et de la Chimère sont des DÉCISIONS, pas des
/// retrouvailles.</b> Le code Godot a été supprimé du dépôt le 2026-08-10 : seul le Vecteur avait ses
/// valeurs consignées (GDD §26 — 90 PV, 210 de vitesse, « profil médian-fragile, entre Chimera et
/// Vagabond »). Les trois autres ont été reconstruits à partir de leurs <i>descriptions</i>, qui
/// existaient déjà en trois langues, et de cette phrase-là — qui borne le Vagabond des deux côtés.
/// Ils n'ont jamais été joués sous cette forme.</para>
///
/// <para><b>Pourquoi ces écarts et pas d'autres.</b> Les i-frames du joueur (0,45 s) plafonnent les
/// dégâts entrants à 2,2 coups par seconde <i>quelle que soit la vitesse</i> : être lent ne fait donc
/// pas encaisser plus de coups au contact, cela empêche de <b>rompre</b>. Un écart de PV se paie donc
/// bien plus cher qu'un écart de vitesse ne rapporte, et c'est pourquoi le Titan n'est pas à ×1,5 PV
/// pour ×0,8 de vitesse — il serait simplement le meilleur. Cf. <see cref="RustTide"/> : la marée
/// rongeant en <i>fraction</i> des PV max, aucun de ces profils n'y gagne ni n'y perd.</para>
/// </summary>
public static class Characters
{
    /// <summary>Le personnage par défaut — celui de toutes les runs jouées jusqu'ici.</summary>
    /// <remarks>
    /// Ses valeurs sont <b>exactement</b> celles que <c>PlayerStats.ResetForRun</c> posait en dur
    /// (100 PV, 200 de vitesse) et son arme celle que <c>RunBootstrap</c> déclarait. C'est
    /// volontaire : un joueur qui reprend sa sauvegarde ne doit rien sentir changer tant qu'il ne
    /// choisit pas autre chose.
    /// </remarks>
    public const string DefaultId = "chimera";

    /// <summary>
    /// Les quatre profils, dans l'ordre d'affichage.
    /// </summary>
    /// <remarks>
    /// La Chimère d'abord : sa description la donne comme « le bon point de départ pour apprendre la
    /// boucle », et un écran de sélection place son option recommandée sous le curseur, pas au bout
    /// d'une rangée. Les trois autres suivent par écart croissant à ce profil.
    /// </remarks>
    public static readonly IReadOnlyList<CharacterDef> All = new[]
    {
        //                id           PV     vitesse  arme               animations
        new CharacterDef("chimera",   100f,   200f,   "impulse_cannon",  "player"),
        new CharacterDef("titan",     140f,   170f,   "drone_swarm",     "titan"),
        new CharacterDef("vagabond",   80f,   240f,   "plasma_blade",    "vagabond"),
        new CharacterDef("vecteur",    90f,   210f,   "vector_lance",    "vecteur"),
    };

    /// <summary>
    /// Le personnage d'identifiant donné, ou <see cref="Default"/> si l'identifiant est inconnu.
    /// </summary>
    /// <remarks>
    /// ⚠ Le repli est <b>délibéré et silencieux</b>. Cette méthode lit une chaîne venue de la
    /// sauvegarde du joueur ou d'un drapeau de ligne de commande : une sauvegarde écrite par une
    /// version future, ou une faute de frappe dans <c>--character=</c>, ne doit pas empêcher de
    /// jouer. Le prix est qu'une faute de frappe est <i>muette</i> — d'où le journal côté moteur, qui
    /// nomme le personnage réellement appliqué.
    /// </remarks>
    public static CharacterDef Get(string? id)
    {
        if (!string.IsNullOrEmpty(id))
            foreach (var def in All)
                if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;

        return Default;
    }

    /// <summary>Le profil par défaut, garanti non nul.</summary>
    public static CharacterDef Default => Get2(DefaultId) ?? All[0];

    /// <summary>L'identifiant désigne-t-il un personnage connu ?</summary>
    public static bool IsKnown(string? id) => Get2(id) != null;

    /// <summary>Index d'affichage, ou 0 si inconnu — l'écran de sélection s'en sert pour son curseur.</summary>
    public static int IndexOf(string? id)
    {
        for (int i = 0; i < All.Count; i++)
            if (string.Equals(All[i].Id, id, StringComparison.Ordinal)) return i;

        return 0;
    }

    private static CharacterDef? Get2(string? id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        foreach (var def in All)
            if (string.Equals(def.Id, id, StringComparison.Ordinal)) return def;

        return null;
    }
}
