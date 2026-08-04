/// <summary>
/// Noms des scènes du jeu — <b>source unique</b> (Lot 5).
///
/// <para>Une scène désignée par une chaîne littérale disséminée dans le code est une erreur qui ne
/// se voit qu'à l'exécution : Unity ne vérifie ni l'existence de la scène, ni son inscription dans
/// les réglages de build. Le symptôme est un écran noir sans message.</para>
///
/// <para>Les regrouper ici permet aussi au script de build de déclarer <b>exactement</b> les mêmes
/// scènes, donc d'éviter le cas classique « ça marche dans l'éditeur, pas dans le build ».</para>
/// </summary>
public static class GameScenes
{
    /// <summary>Menu principal — point d'entrée du jeu.</summary>
    public const string MainMenu = "MainMenu";

    /// <summary>Scène de jeu — une run.</summary>
    public const string Game = "Game";

    /// <summary>Toutes les scènes à embarquer dans le build, dans l'ordre.</summary>
    public static readonly string[] All = { MainMenu, Game };

    /// <summary>Chemin d'asset d'une scène, tel qu'attendu par le pipeline de build.</summary>
    public static string PathOf(string sceneName) => $"Assets/Scenes/{sceneName}.unity";
}
