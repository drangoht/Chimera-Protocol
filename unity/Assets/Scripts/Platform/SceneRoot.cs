using System;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Remplace les usages de <c>GetTree()</c> : pause, minuteries, exécution différée et changement
/// de scène (112 sites d'appel — docs/UNITY_MIGRATION_PLAN.md §4.2, §4.5).
///
/// <para><b>La pause est le piège principal de ce shim.</b> Godot met en pause l'arbre
/// (<c>GetTree().Paused</c>) et chaque nœud décide de son sort via son <c>ProcessMode</c>. Unity
/// n'a que <c>Time.timeScale = 0</c>, qui gèle <i>tout</i> ce qui lit <c>Time.deltaTime</c> — donc
/// aussi l'écran de pause lui-même s'il s'anime. D'où la règle : toute animation ou minuterie
/// d'<b>interface</b> passe par les variantes « temps réel »
/// (<see cref="CreateUnscaledTimer"/>, <c>GTween.Create(…, ignoreTimeScale: true)</c>).</para>
///
/// <para>Le projet en dépend directement : <c>ModalQueue</c> coordonne l'écran de montée de niveau
/// et celui d'assimilation avec une seule pause, et l'écran de pause ouvre les options en
/// surcouche — tout cela doit rester animé pendant que le jeu est figé.</para>
/// </summary>
public static class SceneRoot
{
    /// <summary>
    /// Le jeu est-il en pause ? Équivalent de <c>GetTree().Paused</c>.
    /// </summary>
    public static bool Paused
    {
        get => Mathf.Approximately(Time.timeScale, 0f);
        set => Time.timeScale = value ? 0f : _resumeScale;
    }

    private static float _resumeScale = 1f;

    /// <summary>
    /// Vitesse <b>nominale</b> du jeu, indépendamment d'une pause ou d'un ralenti en cours.
    /// </summary>
    /// <remarks>
    /// ⚠ C'est elle qu'un effet temporaire doit restaurer, jamais <c>1,0</c> : une campagne de banc
    /// tourne en temps accéléré (<c>--timescale</c>), et rendre 1,0 annulerait ce réglage
    /// silencieusement — la mesure continuerait, simplement vingt fois plus lente que prévu.
    /// </remarks>
    public static float ResumeScale => _resumeScale;

    /// <summary>
    /// Échelle de temps à restaurer en sortie de pause. Distincte de <see cref="Paused"/> parce que
    /// le banc tourne en temps accéléré (<c>--timescale</c>) : sortir de pause en forçant 1,0
    /// annulerait silencieusement ce réglage au premier menu ouvert.
    /// </summary>
    public static float TimeScale
    {
        get => Time.timeScale;
        set { _resumeScale = value; if (!Paused) Time.timeScale = value; }
    }

    /// <summary>
    /// Minuterie soumise à la pause — équivalent de <c>GetTree().CreateTimer(x).Timeout += …</c>.
    /// Pour le gameplay.
    /// </summary>
    public static int CreateTimer(double seconds, Action onTimeout, bool repeat = false)
        => PlatformHost.Instance.ScaledTimers.Add(seconds, onTimeout, repeat);

    /// <summary>
    /// Minuterie en temps réel, insensible à la pause. Pour l'interface — sinon un menu ouvert
    /// pendant la pause voit ses minuteries figées.
    /// </summary>
    public static int CreateUnscaledTimer(double seconds, Action onTimeout, bool repeat = false)
        => PlatformHost.Instance.UnscaledTimers.Add(seconds, onTimeout, repeat);

    /// <summary>Annule une minuterie, quelle que soit son échelle de temps.</summary>
    public static void CancelTimer(int id)
    {
        var host = PlatformHost.Instance;
        if (!host.ScaledTimers.Cancel(id)) host.UnscaledTimers.Cancel(id);
    }

    /// <summary>Reporte une action à la fin de la frame — équivalent de <c>CallDeferred</c>.</summary>
    public static void CallDeferred(Action action) => PlatformHost.Instance.Deferred.Enqueue(action);

    /// <summary>
    /// Change de scène — équivalent de <c>ChangeSceneToFile</c>. Les actions différées en attente
    /// sont abandonnées : elles visent des objets de la scène qui disparaît.
    /// </summary>
    public static void ChangeScene(string sceneName)
    {
        var host = PlatformHost.Instance;
        host.Deferred.Clear();
        host.ScaledTimers.Clear();
        host.UnscaledTimers.Clear();

        // Ni la pause ni un ralenti ne survivent à un changement de scène : rester à timeScale = 0
        // après un retour au menu produirait un écran définitivement figé, et un ralenti abandonné
        // en cours donnerait un menu au ralenti — dans les deux cas sans cause visible.
        HitStop.Reset();
        Paused = false;

        SceneManager.LoadScene(sceneName);
    }

    /// <summary>Quitte le jeu — équivalent de <c>GetTree().Quit()</c>.</summary>
    public static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
