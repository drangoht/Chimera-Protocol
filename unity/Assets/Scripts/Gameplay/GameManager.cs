using System;
using UnityEngine;

/// <summary>
/// Cycle de vie d'une run — port du noyau de <c>GameManager</c> (Lot 2).
///
/// <para>Sous Godot, c'était un AutoLoad ; ici, un composant créé par le bootstrap dans un ordre
/// <b>explicite</b> (§4.6). Le contrat d'accès <c>GameManager.Instance</c> est conservé tel quel :
/// c'est ce qui permet aux systèmes portés de garder leurs appelants inchangés.</para>
/// </summary>
public sealed class GameManager : MonoBehaviour
{
    public static GameManager? Instance { get; private set; }

    /// <summary>Durée de la run en cours, en secondes.</summary>
    public float RunTime { get; private set; }

    /// <summary>Ennemis tués pendant la run.</summary>
    public int Kills { get; private set; }

    /// <summary>La run est-elle terminée (mort du joueur ou limite atteinte) ?</summary>
    public bool RunEnded { get; private set; }

    /// <summary>Multiplicateur d'XP du biome courant.</summary>
    public float BiomeXpMult { get; set; } = 1f;

    /// <summary>Émis à la fin de la run, avec sa durée et le nombre de victimes.</summary>
    public event Action<float, int>? RunFinished;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Démarre une run : remet à zéro tout ce qui est état de partie.</summary>
    public void StartRun()
    {
        RunTime = 0f;
        Kills = 0;
        RunEnded = false;

        XpSystem.Instance?.ResetForRun();
        Player.Instance?.Stats.ResetForRun();

        if (Player.Instance != null) Player.Instance.Died += OnPlayerDied;
    }

    /// <summary>Comptabilise une victime — appelé par les ennemis à leur mort.</summary>
    public void RegisterKill() => Kills++;

    private void Update()
    {
        if (RunEnded) return;
        RunTime += Time.deltaTime;
    }

    private void OnPlayerDied() => EndRun();

    /// <summary>Clôt la run. Idempotent : une double fin ne doit pas doubler les récompenses.</summary>
    public void EndRun()
    {
        if (RunEnded) return;
        RunEnded = true;
        RunFinished?.Invoke(RunTime, Kills);
    }
}
