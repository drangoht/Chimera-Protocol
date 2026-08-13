using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Accès aux fichiers du joueur — sauvegarde, préférences — et <b>reprise d'une installation
/// Godot</b> (Lot 6).
///
/// <para>Ce composant ne décide de rien : toute la conversion vit dans <see cref="SaveMigration"/>
/// (logique pure, vérifiée sur une vraie sauvegarde). Ici il n'y a que des chemins et des octets.</para>
///
/// <para><b>Le risque que cette classe existe pour écarter</b> : <c>user://</c> de Godot vit dans
/// <c>%APPDATA%\Godot\app_userdata\Chimera Protocol\</c>, quand Unity écrit dans
/// <c>%USERPROFILE%\AppData\LocalLow\&lt;société&gt;\&lt;produit&gt;\</c>. Un joueur qui met à jour
/// via l'app itch changerait donc de dossier sans le savoir, et retrouverait un jeu vierge — Échos,
/// améliorations, défis, records et arsenal découvert compris.</para>
/// </summary>
public static class UserData
{
    private const string SaveFile     = "save.json";
    private const string SettingsFile = "settings.json";

    /// <summary>Nom du dossier utilisateur de la version Godot.</summary>
    private const string LegacyFolder = "Chimera Protocol";

    /// <summary>Dossier des données de cette version.</summary>
    public static string Root => Application.persistentDataPath;

    /// <summary>
    /// Dossier de la version Godot, s'il existe encore sur cette machine. <c>null</c> sinon — c'est
    /// le cas d'un nouveau joueur, et il ne doit produire aucun message.
    /// </summary>
    public static string? LegacyRoot
    {
        get
        {
            // Aucun joueur web ne vient d'une installation Godot : il n'y a pas de machine à
            // inspecter, seulement un espace de stockage propre à ce domaine. Chercher quand même
            // ferait interroger un système de fichiers émulé pour un dossier Windows — inoffensif,
            // mais c'est une question dont on connaît déjà la réponse.
            if (Application.platform == RuntimePlatform.WebGLPlayer) return null;

            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrEmpty(appData)) return null;

            string path = Path.Combine(appData, "Godot", "app_userdata", LegacyFolder);
            return Directory.Exists(path) ? path : null;
        }
    }

    /// <summary>La reprise a-t-elle eu lieu, et a-t-elle ramené une progression ?</summary>
    public static bool MigratedFromGodot { get; private set; }

    // ─── Sauvegarde ───────────────────────────────────────────────────────────

    public static SaveData LoadSave() => SaveMigration.ReadSave(ReadText(Path.Combine(Root, SaveFile)));

    public static void SaveGame(SaveData data)
        => WriteText(Path.Combine(Root, SaveFile), SaveMigration.WriteSave(data));

    public static SettingsData LoadSettings()
        => SaveMigration.ReadSettings(ReadText(Path.Combine(Root, SettingsFile)));

    public static void SaveSettings(SettingsData data)
        => WriteText(Path.Combine(Root, SettingsFile), SaveMigration.WriteSettings(data));

    // ─── Reprise ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Reprend la progression d'une installation Godot, <b>une seule fois</b>.
    ///
    /// <para>Condition d'exécution : aucune donnée Unity ne doit exister. C'est ce qui rend l'appel
    /// idempotent et, surtout, ce qui garantit qu'une partie Unity déjà entamée ne sera <b>jamais</b>
    /// écrasée par une vieille sauvegarde Godot — l'inverse exact du défaut qu'on corrige.</para>
    /// </summary>
    public static void MigrateFromGodotIfNeeded()
    {
        string savePath     = Path.Combine(Root, SaveFile);
        string settingsPath = Path.Combine(Root, SettingsFile);

        if (File.Exists(savePath) || File.Exists(settingsPath)) return;

        string? legacy = LegacyRoot;
        if (legacy == null) return;

        var save     = SaveMigration.ReadSave(ReadText(Path.Combine(legacy, SaveFile)));
        var settings = SaveMigration.FromLegacySettings(ReadText(Path.Combine(legacy, "settings.cfg")));

        // Un dossier Godot vide (ou d'un autre jeu) ne doit rien écrire : sans cela, on créerait une
        // sauvegarde neuve « migrée » et on perdrait la possibilité de réessayer plus tard.
        if (!SaveMigration.CarriesProgress(save, settings))
        {
            Debug.Log("[UserData] installation Godot trouvee, mais sans progression a reprendre.");
            return;
        }

        SaveGame(save);
        SaveSettings(settings);
        MigratedFromGodot = true;

        Debug.Log($"[UserData] progression Godot reprise : {save.Meta.CurrentEchoes} Echos, " +
                  $"{save.Meta.Upgrades.Count} ameliorations, {settings.Completions.Count} completions, " +
                  $"{settings.HighScores.Count} records.");
    }

    // ─── Fichiers ─────────────────────────────────────────────────────────────

    private static string? ReadText(string path)
    {
        try
        {
            return File.Exists(path) ? File.ReadAllText(path) : null;
        }
        catch (IOException e)
        {
            Debug.LogError($"[UserData] lecture impossible : {path} ({e.Message})");
            return null;
        }
    }

    private static void WriteText(string path, string content)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, content);

            Flush();
        }
        catch (IOException e)
        {
            // Une écriture qui échoue en silence, c'est une progression perdue à la fermeture du jeu.
            Debug.LogError($"[UserData] ecriture impossible : {path} ({e.Message})");
        }
    }

#if UNITY_WEBGL && !UNITY_EDITOR
    [System.Runtime.InteropServices.DllImport("__Internal")]
    private static extern void ChimeraSyncFilesystem();
#endif

    /// <summary>
    /// Pousse réellement sur le disque ce que <see cref="File.WriteAllText"/> vient d'écrire.
    /// </summary>
    /// <remarks>
    /// <para><b>Sans effet ailleurs que sur le web, et indispensable là.</b> Sur un vrai système de
    /// fichiers, écrire c'est enregistrer. Dans un navigateur, <c>persistentDataPath</c> désigne un
    /// système de fichiers émulé qui vit dans la mémoire de l'onglet : l'écriture réussit, la
    /// relecture rend bien le contenu, et pourtant rien n'a atteint le stockage du navigateur.
    /// Fermer l'onglet efface tout.</para>
    ///
    /// <para>⚠ Ce défaut ne se voit <b>jamais</b> pendant une session d'essai — c'est au lancement
    /// suivant que le joueur retrouve un compte vierge. C'est exactement le sinistre que
    /// <see cref="MigrateFromGodotIfNeeded"/> existe pour éviter, par un autre chemin.</para>
    /// </remarks>
    private static void Flush()
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        ChimeraSyncFilesystem();
#endif
    }
}
