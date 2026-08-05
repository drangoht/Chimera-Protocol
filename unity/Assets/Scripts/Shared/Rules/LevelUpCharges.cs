/// <summary>
/// Charges de <b>Renouveler</b> et <b>Passer</b> d'une run — portage de la part comptable de
/// <c>LevelUpSystem</c>.
///
/// <para>Les deux s'achètent au Hub (<c>reroll</c> et <c>skip</c> dans <c>meta_upgrades.json</c>) et
/// leur <b>niveau d'amélioration EST le nombre de charges</b> de la run : un joueur qui n'a rien
/// acheté n'a aucune charge, et le bouton correspondant ne doit alors pas exister — pas être grisé,
/// mais absent. Un bouton grisé qu'on ne peut jamais activer se lit comme une fonction cassée ;
/// l'absence se lit comme une fonction qu'on n'a pas encore débloquée, ce qui est la vérité.</para>
///
/// <para>Logique pure : ce type ne connaît ni l'écran, ni le tirage des cartes. C'est ce qui permet
/// de vérifier la règle — <i>une charge dépensée ne revient pas</i> — sans lancer le jeu.</para>
/// </summary>
public sealed class LevelUpCharges
{
    /// <summary>Renouvellements restants.</summary>
    public int RerollsLeft { get; private set; }

    /// <summary>Passages restants.</summary>
    public int SkipsLeft { get; private set; }

    /// <summary>Le joueur a-t-il acheté la relance ? Décide de l'existence du bouton.</summary>
    public bool RerollUnlocked { get; }

    /// <summary>Le joueur a-t-il acheté le passage ? Décide de l'existence du bouton.</summary>
    public bool SkipUnlocked { get; }

    /// <param name="rerollLevel">Niveau de l'amélioration <c>reroll</c> au Hub.</param>
    /// <param name="skipLevel">Niveau de l'amélioration <c>skip</c> au Hub.</param>
    public LevelUpCharges(int rerollLevel, int skipLevel)
    {
        RerollsLeft = rerollLevel > 0 ? rerollLevel : 0;
        SkipsLeft = skipLevel > 0 ? skipLevel : 0;

        // Déblocage figé à la construction : dépenser sa dernière charge ne doit pas faire
        // DISPARAÎTRE le bouton en cours de run — le joueur croirait avoir perdu son achat.
        RerollUnlocked = RerollsLeft > 0;
        SkipUnlocked = SkipsLeft > 0;
    }

    /// <summary>Consomme un renouvellement. Faux s'il n'en reste plus.</summary>
    public bool TryReroll()
    {
        if (RerollsLeft <= 0) return false;
        RerollsLeft--;
        return true;
    }

    /// <summary>Consomme un passage. Faux s'il n'en reste plus.</summary>
    public bool TrySkip()
    {
        if (SkipsLeft <= 0) return false;
        SkipsLeft--;
        return true;
    }
}
