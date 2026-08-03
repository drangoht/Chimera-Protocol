using System;
using UnityEngine;

/// <summary>
/// Expérience et montées de niveau — port de <c>XpSystem</c> (Lot 2).
///
/// <para>La courbe elle-même vit dans <see cref="XpCurve"/> : logique pure, <b>partagée avec le
/// projet Godot</b> et couverte par la suite de tests. Le port ne recopie donc aucun seuil — ce qui
/// exclut par construction la classe de bugs la plus vicieuse d'une migration : une constante
/// d'équilibrage retranscrite de travers, qui produit un jeu « qui marche » mais n'est plus le même.
/// </para>
/// </summary>
public sealed class XpSystem : MonoBehaviour
{
    public static XpSystem? Instance { get; private set; }

    /// <summary>Plus de plafond de niveau depuis 2026-06-27 : la courbe régule d'elle-même.</summary>
    private const int MaxLevel = 9999;

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXp    { get; private set; }

    /// <summary>XP restant à gagner pour le niveau suivant.</summary>
    public int XpToNextLevel => XpCurve.Threshold(CurrentLevel);

    /// <summary>Émis à chaque passage de seuil, avec le nouveau niveau.</summary>
    public event Action<int>? LevelUp;

    /// <summary>Multiplicateur d'XP du biome (Friche d'Aether, etc.).</summary>
    public float BiomeXpMult { get; set; } = 1f;

    private void Awake() => Instance = this;

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>Ajoute de l'XP et enchaîne les montées de niveau en cascade si nécessaire.</summary>
    public void AddXp(int amount)
    {
        if (CurrentLevel >= MaxLevel || amount <= 0) return;

        if (!Mathf.Approximately(BiomeXpMult, 1f))
            amount = Mathf.Max(1, Mathf.RoundToInt(amount * BiomeXpMult));

        CurrentXp += amount;

        while (CurrentLevel < MaxLevel && CurrentXp >= XpCurve.Threshold(CurrentLevel))
        {
            CurrentXp -= XpCurve.Threshold(CurrentLevel);
            CurrentLevel++;
            LevelUp?.Invoke(CurrentLevel);
        }
    }

    /// <summary>Remet le système à zéro pour une nouvelle run.</summary>
    public void ResetForRun()
    {
        CurrentLevel = 1;
        CurrentXp = 0;
    }
}
