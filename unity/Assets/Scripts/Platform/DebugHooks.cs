using System;
using System.Globalization;

/// <summary>
/// Drapeaux de banc lus une fois sur la ligne de commande (Lot 7).
///
/// <para><b>Aucun effet en jeu normal</b> : tout est gardé derrière la présence explicite d'un
/// drapeau, et rien ici n'écrit dans la sauvegarde du joueur. Un banc qui laisserait une trace dans
/// <c>save.json</c> ou <c>settings.cfg</c> transformerait une mesure en modification.</para>
///
/// <para><b>Pourquoi un point unique.</b> Le portage a commencé par lire ses drapeaux là où ils
/// servaient — l'un dans le démarrage de run, l'autre dans la configuration, un troisième dans un
/// outil de capture. Cela marche jusqu'au premier drapeau dont deux systèmes ont besoin, et jusqu'au
/// premier lu <b>trop tard</b> : l'ordre des <c>Start</c> n'est pas garanti, et un réglage appliqué
/// après celui qui le consulte donne un résultat juste une fois sur deux. Un accès statique paresseux
/// répond toujours, quel que soit l'appelant et quel que soit le moment.</para>
///
/// <para>La valeur est mise en cache au premier accès : ces propriétés sont lues dans des boucles de
/// jeu (un test par coup porté pour <see cref="PowerCurve"/>).</para>
/// </summary>
public static class DebugHooks
{
    // ─── Mesure ───────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>--power-curve</c> : journalise toute la run (DPS, dégâts subis, population, build complet)
    /// dans <c>power_curve.log</c>.
    /// </summary>
    /// <remarks>
    /// Contrairement au journal de boss — un bloc par combat — celui-ci écrit en continu et n'a
    /// aucune valeur pour un joueur : d'où le drapeau, et un coût nul sans lui (un test booléen).
    /// </remarks>
    public static bool PowerCurve => _powerCurve ??= HasFlag("--power-curve");
    private static bool? _powerCurve;

    /// <summary>
    /// <c>--show-fps</c> : force le compteur d'images, sans toucher aux réglages du joueur.
    /// </summary>
    /// <remarks>
    /// <para>Le compteur existait déjà, derrière un interrupteur des options — donc atteignable
    /// seulement en naviguant dans les menus, ce qui suffisait tant qu'on jouait au clavier sur sa
    /// propre machine.</para>
    ///
    /// <para><b>Le portage web l'a rendu nécessaire.</b> La cadence y est le premier risque — un
    /// survivor tient 200 à 300 entités, et WebGL n'a ni fils d'exécution ni compilation Burst — et
    /// c'est précisément la plateforme où l'instrumenter de l'extérieur est impossible : tant que le
    /// canevas tourne, le navigateur ne laisse plus injecter le moindre script. Le seul instrument
    /// utilisable est donc <b>dans le jeu</b>, et il doit s'activer depuis l'adresse
    /// (<c>?show-fps</c>) puisqu'il n'y a pas de ligne de commande.</para>
    ///
    /// <para>⚠ Comme tous les drapeaux, il <b>n'écrit rien</b> dans les réglages : une mesure ne doit
    /// pas laisser sa mise en scène dans la sauvegarde — le projet a déjà vu un outil de captures
    /// calibrer son propre banc de cette façon.</para>
    /// </remarks>
    public static bool ShowFps => _showFps ??= HasFlag("--show-fps");
    private static bool? _showFps;

    /// <summary>
    /// <c>--seed=&lt;n&gt;</c> : graine du générateur global. Rend une run <b>reproductible</b> —
    /// mêmes vagues, mêmes tirages de cartes, mêmes affixes.
    /// </summary>
    /// <remarks>
    /// C'est ce qui permet la <b>comparaison appariée</b> : rejouer une campagne sur les mêmes graines
    /// après un changement annule le bruit de tirage dans la différence. Quelques paires tranchent ce
    /// que trente runs libres laisseraient indécis.
    /// </remarks>
    public static ulong? Seed => _seedRead ? _seed : ReadSeed();
    private static bool _seedRead;
    private static ulong? _seed;

    private static ulong? ReadSeed()
    {
        string? raw = ValueFlag("--seed=");
        _seed = raw != null && ulong.TryParse(raw, out ulong v) ? v : null;
        _seedRead = true;
        return _seed;
    }

    // ─── Conduite automatique ─────────────────────────────────────────────────

    /// <summary>
    /// <c>--auto-play</c> : les écrans qui attendent un choix se résolvent seuls, et le joueur est
    /// <b>piloté</b>.
    /// </summary>
    /// <remarks>
    /// <para>Sans lui, une run headless se fige au premier passage de niveau et rien n'est mesurable
    /// au-delà de la deuxième minute : le build d'un joueur au boss ne se simule pas par un
    /// équipement figé.</para>
    /// <para>⚠ Le bot tire ses cartes <b>au hasard</b> : son build n'est pas celui d'un humain, et
    /// aucun relevé sous ce drapeau ne dit ce qu'un joueur <i>choisirait</i>.</para>
    /// </remarks>
    public static bool AutoPlay => _autoPlay ??= HasFlag("--auto-play");
    private static bool? _autoPlay;

    /// <summary>
    /// <c>--invuln</c> : le joueur ne subit plus aucun dégât — pour observer un combat long jusqu'au
    /// bout.
    /// </summary>
    /// <remarks>
    /// ⚠ Sous ce drapeau, la colonne des dégâts subis <b>ne veut rien dire</b>, et la survie non plus.
    /// L'en-tête du journal le signale, sans quoi une campagne entière peut être lue à l'envers.
    /// </remarks>
    public static bool Invulnerable => _invulnerable ??= HasFlag("--invuln");
    private static bool? _invulnerable;

    // ─── Fenêtre de mesure ────────────────────────────────────────────────────

    /// <summary>
    /// <c>--timescale=&lt;x&gt;</c> : accélère le temps de jeu (0 = inactif).
    /// </summary>
    /// <remarks>
    /// ⚠ <b>Au-delà de ~4, les projectiles traversent leurs cibles</b> (deltas trop grands) et toute
    /// mesure de dégâts devient fausse. ⚠ Et le gain réel est bien moindre qu'annoncé : en nuée, le
    /// banc est limité par le processeur — une campagne mesurée à ×3 rendait ×1,0.
    /// </remarks>
    public static float TimeScale => _timeScaleRead ? _timeScale : ReadFloat("--timescale=", ref _timeScale, ref _timeScaleRead);
    private static bool _timeScaleRead;
    private static float _timeScale;

    /// <summary>
    /// <c>--start-at=&lt;minutes&gt;</c> : démarre la run à cette minute, montée en puissance des
    /// ennemis comprise.
    /// </summary>
    /// <remarks>
    /// <para>La fenêtre à instruire est l'<b>overtime</b>, où le bot n'arrive pas de lui-même.
    /// Combiné à <c>--saturate-arsenal</c>, ce drapeau <b>fixe l'état d'entrée</b> — et c'est là que
    /// logeait la variance qui empêchait de conclure (facteur 2,4 sur la survie entre deux sessions
    /// du même joueur, selon que l'arsenal sature à la 11ᵉ ou à la 13ᵉ minute).</para>
    /// <para>⚠ Ce que le drapeau ne dit pas : le personnage démarre <b>nu</b> quand un joueur arrive
    /// là avec des PV et des cartes accumulés. La mesure sert à <i>comparer</i> des réglages, jamais
    /// à prédire une durée de vie.</para>
    /// </remarks>
    public static float StartAtMinutes => _startAtRead ? _startAt : ReadFloat("--start-at=", ref _startAt, ref _startAtRead);
    private static bool _startAtRead;
    private static float _startAt;

    /// <summary>
    /// <c>--run-limit=&lt;secondes&gt;</c> : termine la run d'elle-même au-delà (0 = inactif).
    /// </summary>
    /// <remarks>
    /// ⚠ Une run interrompue par cette limite est <b>censurée</b> : sa survie n'est pas mesurée mais
    /// minorée. Une campagne où toutes les runs finissent ainsi a une variance écrasée, et annonce une
    /// précision qu'elle n'a pas — le journal marque donc l'issue pour que l'outil les écarte.
    /// </remarks>
    public static float RunLimit => _runLimitRead ? _runLimit : ReadFloat("--run-limit=", ref _runLimit, ref _runLimitRead);
    private static bool _runLimitRead;
    private static float _runLimit;

    // ─── Contenu ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>--saturation=&lt;n&gt;</c> : cran de l'échelle de saturation imposé, ou <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Sans ce drapeau <b>aucun cran n'est mesurable</b> : la saturation se choisit sur la carte du
    /// biome, écran que le bot ne traverse jamais. Il ignore le déblocage — on mesure un cran avant de
    /// l'avoir gagné — et ne persiste rien.
    /// </remarks>
    public static int? Saturation => _saturationRead ? _saturation : ReadSaturation();
    private static bool _saturationRead;
    private static int? _saturation;

    private static int? ReadSaturation()
    {
        string? raw = ValueFlag("--saturation=");
        _saturation = raw != null && int.TryParse(raw, out int v) ? v : null;
        _saturationRead = true;
        return _saturation;
    }

    // ⚠ `--biome=<id>` n'est PAS lu ici : il est analysé par `RunConfig.FromCommandLine`, qui le
    // porte jusqu'au décor et au spawn. Une seconde lecture a existé ici, sans appelant — deux
    // analyseurs pour un même drapeau, dont un seul décidait quoi que ce soit.

    /// <summary><c>--saturate-arsenal</c> : armes et passifs au niveau maximum dès le départ.</summary>
    public static bool SaturateArsenal => _saturateArsenal ??= HasFlag("--saturate-arsenal");
    private static bool? _saturateArsenal;

    /// <summary><c>--force-elites</c> : chaque ennemi de base devient une élite.</summary>
    public static bool ForceElites => _forceElites ??= HasFlag("--force-elites");
    private static bool? _forceElites;

    // ─── Capture ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <c>--trailer</c> : masque ce qui n'a rien à faire dans une vidéo — tampon de version, invite
    /// « appuyer pour passer ».
    /// </summary>
    /// <remarks>
    /// Ces deux mentions sont utiles au joueur et <b>datent</b> la vidéo : un trailer qui affiche
    /// <c>v2.0.0-37c9d64</c> dans un coin devient faux à la version suivante, alors que rien du jeu
    /// n'a changé à l'image.
    /// </remarks>
    public static bool TrailerMode => _trailerMode ??= HasFlag("--trailer");
    private static bool? _trailerMode;

    /// <summary>
    /// <c>--lang=&lt;en|fr|es&gt;</c> : langue de la session, ou <c>null</c> pour celle du joueur.
    /// </summary>
    /// <remarks>
    /// <para>Tout le texte à l'écran en dépend : narration de la cinématique, bandeaux de biome,
    /// cartes de montée de niveau, menus. Sans ce drapeau, une capture prend la langue du <b>poste</b>
    /// — qui n'est pas forcément celle de la vidéo qu'on monte, et l'on ne s'en aperçoit qu'au
    /// montage, une fois les rushes tournés.</para>
    /// <para><b>Non persisté</b> : la préférence du joueur n'est pas touchée.</para>
    /// </remarks>
    public static string? Language => _languageRead ? _language : ReadLanguage();
    private static bool _languageRead;
    private static string? _language;

    private static string? ReadLanguage()
    {
        _language = ValueFlag("--lang=");
        _languageRead = true;
        return _language;
    }

    // ─── Lecture ──────────────────────────────────────────────────────────────

    private static float ReadFloat(string prefix, ref float slot, ref bool read)
    {
        string? raw = ValueFlag(prefix);

        // Culture INVARIANTE : sur un poste français, « 2.5 » lu avec la culture locale rend 25.
        slot = raw != null && float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
             ? v : 0f;

        read = true;
        return slot;
    }

    private static bool HasFlag(string flag) => LaunchArgs.Has(flag);

    private static string? ValueFlag(string prefix) => LaunchArgs.Value(prefix);

    /// <summary>Oublie tout ce qui a été lu — réservé aux tests, qui jouent plusieurs configurations.</summary>
    public static void Reset()
    {
        _powerCurve = _autoPlay = _invulnerable = _saturateArsenal = _forceElites = _trailerMode = null;
        _showFps = null;
        _seedRead = _timeScaleRead = _startAtRead = _runLimitRead = _saturationRead = false;
        _languageRead = false;
    }
}
