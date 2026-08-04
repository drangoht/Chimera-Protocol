using UnityEngine;

/// <summary>
/// Préférences et progression hors méta, chargées une fois pour toute la session (Lot 6).
///
/// <para><b>Statique, et non un composant.</b> Sous Godot c'était un AutoLoad ; ici, en faire un
/// <c>MonoBehaviour</c> le rendrait dépendant de l'ordre d'initialisation des scènes — or ces
/// réglages sont lus dès le menu principal, avant que la moindre scène de jeu n'existe. L'état vit
/// dans <see cref="SettingsData"/> (pur, partagé, testé) ; cette classe n'en est que l'accès.</para>
///
/// <para>⚠ <b>La reprise d'une installation Godot se joue ici, au premier accès.</b> Elle doit se
/// produire avant toute lecture : une seule lecture prématurée créerait un fichier vierge, et la
/// migration ne se déclencherait alors plus jamais — la progression du joueur serait perdue sans
/// qu'aucune erreur ne soit levée.</para>
/// </summary>
public static class GameSettings
{
    private static SettingsData? _current;

    /// <summary>Réglages courants. Les charge (et migre) au premier accès.</summary>
    public static SettingsData Current
    {
        get
        {
            if (_current != null) return _current;

            UserData.MigrateFromGodotIfNeeded();
            _current = UserData.LoadSettings();

            // La langue est POUSSÉE vers la couche de traduction : celle-ci ne peut pas dépendre du
            // Gameplay sans créer un cycle entre assemblies.
            Loc.Language = _current.Language;

            ApplyDisplay(_current);
            ApplyVolumes(_current);
            return _current;
        }
    }

    /// <summary>
    /// Applique les réglages d'affichage au démarrage. Sans cet appel, un joueur qui avait choisi le
    /// plein écran le retrouverait décoché au lancement suivant : la préférence serait bien
    /// enregistrée, mais jamais rejouée — l'un des rares défauts qu'un test ne voit pas et qu'un
    /// joueur remarque à chaque lancement.
    /// </summary>
    private static void ApplyDisplay(SettingsData settings)
    {
        if (Application.isBatchMode) return;   // rien à régler sans fenêtre

        Screen.fullScreenMode = settings.DisplayMode == 2
            ? FullScreenMode.FullScreenWindow
            : FullScreenMode.Windowed;

        QualitySettings.vSyncCount = settings.Vsync ? 1 : 0;
    }

    /// <summary>
    /// Pousse les volumes vers l'audio. Comme la langue, ils sont <b>poussés</b> et non tirés : la
    /// couche Platform ne peut pas dépendre du Gameplay sans créer un cycle entre assemblies.
    /// </summary>
    public static void ApplyVolumes(SettingsData settings)
    {
        AudioSystem.MasterVolume = settings.MasterVolume;
        AudioSystem.SfxVolume = settings.SfxVolume;

        if (MusicDirector.Instance != null) MusicDirector.Instance.MusicVolume = settings.MusicVolume;
    }

    /// <summary>Écrit les réglages sur disque.</summary>
    public static void Save()
    {
        if (_current == null) return;

        ApplyVolumes(_current);
        UserData.SaveSettings(_current);
    }

    /// <summary>
    /// Armes de signature des personnages : toujours considérées découvertes. Sans cette exception,
    /// le Codex s'ouvrirait entièrement vide pour un joueur qui vient de finir sa première run avec
    /// l'arme de départ — il aurait pourtant vu cette arme pendant treize minutes.
    /// </summary>
    public static readonly string[] SignatureWeapons =
        { "impulse_cannon", "drone_swarm", "plasma_blade", "vector_lance" };

    /// <summary>L'arme a-t-elle déjà été portée (ou est-elle une arme de signature) ?</summary>
    public static bool IsWeaponDiscovered(string weaponId)
        => System.Array.IndexOf(SignatureWeapons, weaponId) >= 0
        || Current.DiscoveredWeapons.Contains(weaponId);

    /// <summary>Marque une arme comme découverte, à sa première acquisition.</summary>
    public static void DiscoverWeapon(string weaponId)
    {
        if (weaponId.Length == 0 || Current.DiscoveredWeapons.Contains(weaponId)) return;

        Current.DiscoveredWeapons.Add(weaponId);
        Save();
    }

    /// <summary>La greffe a-t-elle déjà été assimilée au moins une fois ?</summary>
    public static bool IsGraftDiscovered(string graftId)
        => Current.DiscoveredGrafts.Contains(graftId);

    /// <summary>Marque une greffe comme découverte, à sa première assimilation.</summary>
    public static void DiscoverGraft(string graftId)
    {
        if (graftId.Length == 0 || Current.DiscoveredGrafts.Contains(graftId)) return;

        Current.DiscoveredGrafts.Add(graftId);
        Save();
    }

    /// <summary>Cran de saturation choisi pour un biome (0 si jamais réglé).</summary>
    public static int SaturationFor(string biomeId)
        => Current.SaturationByLevel.TryGetValue(biomeId, out int v) ? v : 0;

    /// <summary>Cran le plus haut battu sur ce biome — c'est lui qui débloque le suivant.</summary>
    public static int BeatenSaturationFor(string biomeId)
        => Current.SaturationBeatenByLevel.TryGetValue(biomeId, out int v) ? v : -1;

    /// <summary>Meilleur temps enregistré sur un biome, en secondes (0 si aucun).</summary>
    public static int HighScoreFor(string biomeId)
        => Current.HighScores.TryGetValue(biomeId, out int v) ? v : 0;

    /// <summary>
    /// Enregistre un résultat de run. Ne redescend jamais un record — un mauvais essai ne doit pas
    /// effacer une meilleure performance.
    /// </summary>
    public static void ReportRun(string biomeId, int seconds, bool bossDefeated, int saturation)
    {
        var s = Current;

        if (seconds > HighScoreFor(biomeId)) s.HighScores[biomeId] = seconds;

        if (bossDefeated)
        {
            s.Completions[biomeId] = Mathf.Max(s.Completions.TryGetValue(biomeId, out int c) ? c : 0, 1);

            if (saturation > BeatenSaturationFor(biomeId))
                s.SaturationBeatenByLevel[biomeId] = saturation;
        }

        Save();
    }

    /// <summary>Oublie l'état chargé — réservé aux bancs, qui rejouent plusieurs sessions d'affilée.</summary>
    public static void Reset() => _current = null;
}
