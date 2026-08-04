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
            return _current;
        }
    }

    /// <summary>Écrit les réglages sur disque.</summary>
    public static void Save()
    {
        if (_current == null) return;
        UserData.SaveSettings(_current);
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
