using Godot;

/// <summary>
/// Raccourci de localisation. Toutes les chaînes d'UI passent par une clé traduite
/// via le TranslationServer de Godot (tables CSV importées, langue choisie au menu).
/// </summary>
public static class Loc
{
    /// <summary>Traduit une clé dans la langue courante.</summary>
    public static string T(string key) => TranslationServer.Translate(key).ToString();

    /// <summary>Traduit une clé contenant des emplacements {0}, {1}… et y injecte les arguments.</summary>
    public static string T(string key, params object[] args) => string.Format(T(key), args);
}
