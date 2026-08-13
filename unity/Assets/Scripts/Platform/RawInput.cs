using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Entrées <b>non remappables</b> — Échap, « n'importe quelle touche », clic, curseur, stick droit.
/// Les actions de jeu, elles, passent par <see cref="InputRemap"/>.
///
/// <para><b>Pourquoi ce fichier existe.</b> Le portage vers le paquet Input System (2026-08-13, en
/// remplacement de l'Input Manager marqué pour dépréciation) a introduit un mode d'échec que
/// l'ancienne API ne pouvait pas avoir : <c>Keyboard.current</c>, <c>Mouse.current</c> et
/// <c>Gamepad.current</c> sont <b>nuls</b> quand le périphérique n'est pas là — au banc headless,
/// dans un build lancé sans souris, ou pendant la frame qui suit un débranchement.
/// <c>Input.GetKeyDown</c> rendait simplement <c>false</c> ; ici, l'oubli d'un test se paie en
/// <c>NullReferenceException</c> <b>à chaque frame</b>, dans un <c>Update</c>, ce qui interrompt
/// silencieusement le reste de la méthode appelante.</para>
///
/// <para>Regrouper ces lectures ici donne un <b>seul endroit à auditer</b> : si un écran teste une
/// touche sans passer par cette classe, c'est un candidat au défaut ci-dessus. C'est aussi la
/// contrepartie de la leçon du portage — une entrée lue à dix endroits est une règle qu'on ne peut
/// pas vérifier.</para>
/// </summary>
public static class RawInput
{
    /// <summary>Échap vient-il d'être pressé ? Ferme un écran, annule un remappage.</summary>
    public static bool EscapePressedThisFrame()
        => Keyboard.current?.escapeKey.wasPressedThisFrame == true;

    /// <summary>
    /// Une entrée « quelconque » vient-elle d'arriver — clavier, clic ou manette ?
    ///
    /// <para>Sert à passer la cinématique : un joueur qui veut sauter ne doit pas chercher LA
    /// touche. Les trois périphériques sont couverts parce que l'ancien <c>Input.anyKeyDown</c> les
    /// couvrait tous les trois ; n'en garder qu'un rendrait l'intro impassable à la manette sans
    /// qu'aucune erreur ne le signale.</para>
    /// </summary>
    public static bool AnyInputThisFrame()
    {
        if (Keyboard.current?.anyKey.wasPressedThisFrame == true) return true;
        if (PrimaryClickThisFrame()) return true;

        var pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame ||
                               pad.buttonEast.wasPressedThisFrame ||
                               pad.startButton.wasPressedThisFrame);
    }

    /// <summary>Le bouton gauche de la souris vient-il d'être pressé ?</summary>
    public static bool PrimaryClickThisFrame()
        => Mouse.current?.leftButton.wasPressedThisFrame == true;

    /// <summary>
    /// Position du curseur en pixels écran, ou <c>null</c> sans souris — un <c>Vector2.zero</c> de
    /// repli serait pris pour un curseur posé dans le coin de l'écran, et ferait viser là.
    /// </summary>
    public static Vector2? PointerPosition()
        => Mouse.current?.position.ReadValue();

    /// <summary>Stick droit de la manette, ou <see cref="Vector2.zero"/> s'il n'y en a pas.</summary>
    public static Vector2 RightStick()
        => Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;

    /// <summary>
    /// Première touche pressée cette frame, pour la capture d'un remappage — <c>Key.None</c> si
    /// aucune.
    ///
    /// <para>Balayer <c>allKeys</c> plutôt que l'énumération complète règle au passage un détail que
    /// l'ancien code devait traiter à la main : les boutons de souris ne partagent plus
    /// l'énumération des touches, il n'y a donc plus de <c>Mouse0..Mouse6</c> à écarter pour éviter
    /// de lier une action au clic qui vient de valider le bouton.</para>
    /// </summary>
    public static Key FirstKeyPressedThisFrame()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return Key.None;

        foreach (var key in keyboard.allKeys)
            if (key.wasPressedThisFrame) return key.keyCode;

        return Key.None;
    }
}
