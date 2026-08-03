using System;
using UnityEngine;

/// <summary>
/// Équivalent Unity d'une ressource <c>SpriteFrames</c> de Godot : un jeu d'animations nommées,
/// chacune étant une suite d'images avec sa cadence (docs/UNITY_MIGRATION_PLAN.md §7.2).
///
/// <para><b>Pourquoi pas Mecanim.</b> Le jeu joue ses animations de façon <b>data-driven</b> —
/// <c>PlayAnim("attack")</c> sur un ennemi dont le sprite est désigné par <c>enemies.json</c>, avec
/// repli quand l'animation n'existe pas. Reproduire ce contrat avec un
/// <c>AnimatorController</c> par ennemi (40 au total) serait plus lourd, moins tolérant, et
/// remplacerait une donnée lisible par un graphe à maintenir à la main.</para>
///
/// <para>Ces assets sont <b>générés</b> depuis les manifestes produits par
/// <c>tools/unity/convert_spriteframes.py</c> — ils ne s'éditent pas à la main.</para>
/// </summary>
[CreateAssetMenu(fileName = "SpriteFrames", menuName = "Chimera/Sprite Frames")]
public sealed class SpriteFramesAsset : ScriptableObject
{
    [Serializable]
    public sealed class Animation
    {
        public string Name = "";

        [Tooltip("Images par seconde.")]
        public float Speed = 8f;

        public bool Loop = true;

        public Sprite[] Frames = Array.Empty<Sprite>();

        /// <summary>Durée totale, en secondes. Zéro si l'animation est vide ou figée.</summary>
        public float Duration => Speed > 0f && Frames.Length > 0 ? Frames.Length / Speed : 0f;
    }

    [Tooltip("Identifiant d'origine (nom du SpriteFrames Godot), pour tracer la provenance.")]
    public string Id = "";

    public Animation[] Animations = Array.Empty<Animation>();

    /// <summary>Cherche une animation par nom. Renvoie <c>null</c> si elle n'existe pas.</summary>
    public Animation? Find(string name)
    {
        foreach (var a in Animations)
            if (string.Equals(a.Name, name, StringComparison.Ordinal)) return a;
        return null;
    }

    /// <summary>Cette animation existe-t-elle ?</summary>
    public bool Has(string name) => Find(name) != null;
}
