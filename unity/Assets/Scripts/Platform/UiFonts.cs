using UnityEngine;

/// <summary>
/// Polices de l'interface (lot de parité visuelle).
///
/// <para><b>Share Tech Mono</b> est la police du jeu — anti-crénelage activé, corps 16 de référence.
/// Elle porte une part réelle de l'identité visuelle : une interface rendue dans la police par défaut
/// d'Unity ne « ressemble » plus au jeu, même si tout le reste est juste.</para>
///
/// <para><b>Chargée depuis <c>Resources/</c>, pas depuis les ressources intégrées d'Unity.</b> Une
/// police posée ailleurs dans le projet n'existe à l'exécution que si une scène la référence — c'est
/// le piège qui avait rendu 40 jeux d'animations introuvables, et il vaut pour tous les assets.</para>
///
/// <para>⚠ Le repli sur la police intégrée n'est pas décoratif : sans lui, une police absente donnerait
/// une interface <b>sans aucun texte</b>, ce qui se lit comme un écran cassé plutôt que comme un asset
/// manquant.</para>
/// </summary>
public static class UiFonts
{
    private static Font? _main;

    /// <summary>Police principale de l'interface.</summary>
    public static Font Main
    {
        get
        {
            if (_main != null) return _main;

            _main = Resources.Load<Font>("Fonts/ShareTechMono");
            if (_main == null)
            {
                Debug.LogError("[UiFonts] ShareTechMono introuvable — repli sur la police integree.");
                _main = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return _main;
        }
    }

    /// <summary>Oublie la police chargée — réservé aux bancs.</summary>
    public static void Reset() => _main = null;
}
