using System;

/// <summary>
/// <b>Les vagues de la Marée de Rouille</b> — la nappe pousse vers l'intérieur (logique pure, testable).
///
/// <para><b>Pourquoi ces vagues existent.</b> Le bord de la marée recule de 1,6 unité par seconde sur
/// les côtés et 1,0 sur le haut et le bas : à l'écran, <b>un pixel toutes les 0,7 à 0,9 seconde</b>.
/// C'est sous le seuil de perception du mouvement, et un mouvement continu mais sous-perceptible se
/// lit exactement comme un mouvement discret — « ça avance par à-coups », signalé en jouant le
/// 2026-08-21. Accélérer le bord était exclu : la date de fermeture <i>est</i> la garantie de fin de
/// partie (<see cref="RustTide.CloseMinutes"/>, GDD §38), et la régler pour un motif de lisibilité
/// aurait rejoué le défaut d'origine — une fin qui dépend d'un réglage.</para>
///
/// <para><b>D'où le découplage.</b> Ce qui se voit avancer n'est pas ce qui avance : les vagues
/// traversent la nappe à ~110 unités/s, près de <b>cent fois</b> le recul du bord. C'est le principe
/// d'une rivière — l'eau court bien plus vite que la berge ne s'érode, et l'œil lit « ça vient sur
/// moi » sans jamais confondre les deux. <b>Rien ici ne touche à la géométrie ni aux dégâts</b> : la
/// limite qui fait mal reste le liseré, que ces vagues ne franchissent jamais.</para>
///
/// <para><b>Ce qu'il reste de ce fichier depuis le passage au shader (2026-08-22).</b> La forme des
/// vagues, leur opacité et leur placement se calculent désormais par pixel dans
/// <c>Resources/Shaders/RustTide.shader</c> — un champ de distance les fait épouser le front rongé,
/// ce qu'une bande rectangulaire ne pouvait pas faire. <b>La phase, elle, ne peut pas descendre dans
/// le shader</b> : elle doit être <i>accumulée</i> (voir <see cref="AdvancePhase"/>), et un shader
/// n'a pas d'état — il ne saurait la recalculer que depuis son horloge, ce qui la remettrait à zéro à
/// chaque rechargement de scène en plus de rouvrir le piège ci-dessous.</para>
/// </summary>
public static class TideWaves
{
    /// <summary>
    /// Avance une phase de vague, à vitesse constante <i>en unités du monde</i>.
    ///
    /// <para>⚠ La phase s'<b>accumule</b> ; elle ne se recalcule jamais depuis l'horloge. Une forme
    /// <c>temps × vitesse ÷ espacement</c> paraît équivalente et ne l'est que tant que l'espacement ne
    /// bouge pas. Le premier rendu y divisait par la <i>profondeur de la nappe</i>, qui grandit à
    /// mesure que l'arène se referme : à la dixième minute, un centième d'unité de profondeur en plus
    /// déplaçait la phase d'un demi-cycle — on aurait corrigé un à-coup en en fabriquant un autre,
    /// bien pire. Le rendu au shader espace désormais les vagues d'une distance <b>constante</b>, ce
    /// qui retire au piège son carburant ; l'accumulation reste, parce qu'elle est ce qui rend le
    /// résultat indépendant de la grandeur qu'on met au dénominateur.</para>
    /// </summary>
    public static float AdvancePhase(float phase, float deltaSeconds, float speed, float spacing)
    {
        if (deltaSeconds <= 0f || spacing <= 0f) return Frac(phase);
        return Frac(phase + deltaSeconds * speed / spacing);
    }

    /// <summary>Partie fractionnaire, toujours dans [0,1) — y compris pour une entrée négative.</summary>
    public static float Frac(float v) => v - (float)Math.Floor(v);
}
