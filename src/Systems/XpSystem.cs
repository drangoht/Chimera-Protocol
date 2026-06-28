using Godot;

/// <summary>
/// Singleton AutoLoad gérant l'XP et les montées de niveau du joueur.
/// Signal LevelUp(newLevel) émis à chaque passage de seuil.
/// Formule seuil : XP_TOTAL(n) = 15 * n * n + 25 * n
/// </summary>
public partial class XpSystem : Node
{
    public static XpSystem Instance { get; private set; } = null!;

    [Signal] public delegate void LevelUpEventHandler(int newLevel);

    public int CurrentLevel { get; private set; } = 1;
    public int CurrentXp    { get; private set; } = 0;
    public int XpToNextLevel => XpThreshold(CurrentLevel);

    // Plus de cap de niveau joueur (2026-06-27) : valeur très haute = illimité en pratique.
    // La courbe XpThreshold (formule) continue de croître et régule naturellement.
    private const int MaxLevel = 9999;

    public override void _Ready()
    {
        Instance = this;
    }

    /// <summary>Ajoute de l'XP et déclenche les montées de niveau en cascade si nécessaire.</summary>
    public void AddXp(int amount)
    {
        if (CurrentLevel >= MaxLevel) return;

        // Effet de biome : multiplicateur d'XP (Friche d'Aether, etc.)
        float mult = GameManager.Instance?.BiomeXpMult ?? 1f;
        if (mult != 1f) amount = Mathf.Max(1, Mathf.RoundToInt(amount * mult));

        CurrentXp += amount;

        while (CurrentLevel < MaxLevel && CurrentXp >= XpThreshold(CurrentLevel))
        {
            CurrentXp -= XpThreshold(CurrentLevel);
            CurrentLevel++;
            GD.Print($"[XpSystem] Level up ! Niveau {CurrentLevel}");

            // SFX de montee de niveau
            AudioSystem.Instance?.PlaySfx("sfx_levelup");

            EmitSignal(SignalName.LevelUp, CurrentLevel);

            if (CurrentLevel >= MaxLevel) break;
        }
    }

    /// <summary>Réinitialise entre deux runs.</summary>
    public void Reset()
    {
        CurrentLevel = 1;
        CurrentXp    = 0;
    }

    /// <summary>
    /// XP nécessaire pour passer du niveau n au niveau n+1.
    /// Inspiré de Vampire Survivors : linéaire +10/niveau, L1=5 XP.
    /// Mur à L20 (×2) pour marquer la mi-run, puis +13/niveau pour l'endgame.
    /// Source : Module:Experience dataminé sur le wiki VS officiel.
    /// </summary>
    public static int XpThreshold(int level) => XpCurve.Threshold(level);
}
