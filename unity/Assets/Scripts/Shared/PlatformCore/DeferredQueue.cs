using System;
using System.Collections.Generic;

/// <summary>
/// File d'exécution différée reproduisant <c>CallDeferred</c> de Godot (57 sites d'appel dans le
/// projet — docs/UNITY_MIGRATION_PLAN.md §4.2).
///
/// <para><b>À quoi ça sert dans ce jeu.</b> <c>CallDeferred</c> y résout un problème récurrent :
/// modifier l'arbre de scène pendant qu'on le parcourt (mort d'un ennemi au milieu d'une boucle de
/// collision, ajout d'un nœud depuis un callback…). Reporter l'opération à la fin de la frame évite
/// d'invalider l'itération en cours. Unity n'offre pas d'équivalent direct : d'où cette file.</para>
///
/// <para><b>Sémantique retenue : on draine jusqu'à épuisement.</b> Une action ajoutée <i>pendant</i>
/// le drainage s'exécute donc dans le même passage, comme le fait la <c>MessageQueue</c> de Godot,
/// et non à la frame suivante. La différence n'est pas cosmétique : reporter à la frame suivante
/// introduirait une latence d'une image dans des chaînes du type « l'ennemi meurt → l'orbe
/// apparaît → le ramassage se déclenche ».</para>
///
/// <para><b>Garde-fou</b> : une action qui se réenfile indéfiniment produirait une boucle infinie
/// silencieuse — le pire mode de défaillance possible, puisqu'il gèle le jeu sans message. Le
/// drainage est donc borné (<see cref="MaxDrainPasses"/>) et signale bruyamment.</para>
///
/// <para>Logique pure : testable sans moteur.</para>
/// </summary>
public sealed class DeferredQueue
{
    /// <summary>
    /// Nombre maximal de passages de drainage avant de considérer qu'il y a une boucle. Généreux :
    /// les chaînes légitimes du jeu font 2 ou 3 niveaux, jamais 64.
    /// </summary>
    public const int MaxDrainPasses = 64;

    private readonly Queue<Action> _pending = new();
    private bool _draining;

    /// <summary>Actions en attente d'exécution.</summary>
    public int Count => _pending.Count;

    /// <summary>Vrai pendant <see cref="Flush"/> — utile pour diagnostiquer une réentrance.</summary>
    public bool IsDraining => _draining;

    /// <summary>
    /// Signalé quand le drainage est interrompu par <see cref="MaxDrainPasses"/>. Non géré, cela
    /// resterait un gel inexplicable ; l'hôte moteur y branche un log d'erreur.
    /// </summary>
    public event Action<int>? DrainLimitReached;

    /// <summary>Reporte <paramref name="action"/> au prochain <see cref="Flush"/>.</summary>
    public void Enqueue(Action action)
    {
        if (action is null) throw new ArgumentNullException(nameof(action));
        _pending.Enqueue(action);
    }

    /// <summary>
    /// Exécute les actions en attente, dans leur ordre d'ajout, en incluant celles qu'elles
    /// ajoutent au passage. Renvoie le nombre d'actions exécutées.
    /// </summary>
    /// <remarks>
    /// Un appel réentrant (<see cref="Flush"/> depuis une action différée) est ignoré et renvoie 0 :
    /// le drainage en cours prendra de toute façon en charge les nouvelles actions, et laisser deux
    /// drainages s'imbriquer casserait l'ordre d'exécution.
    /// </remarks>
    public int Flush()
    {
        if (_draining) return 0;

        _draining = true;
        int executed = 0;
        try
        {
            int passes = 0;
            while (_pending.Count > 0)
            {
                if (++passes > MaxDrainPasses)
                {
                    DrainLimitReached?.Invoke(_pending.Count);
                    _pending.Clear();
                    break;
                }

                // On fige le lot courant : les actions ajoutées pendant ce lot iront au suivant,
                // ce qui garde l'ordre d'ajout lisible et rend le compteur de passages parlant.
                int batch = _pending.Count;
                for (int i = 0; i < batch; i++)
                {
                    _pending.Dequeue().Invoke();
                    executed++;
                }
            }
        }
        finally { _draining = false; }

        return executed;
    }

    /// <summary>
    /// Abandonne les actions en attente sans les exécuter — pour un changement de scène, où les
    /// actions différées visent des objets qui n'existeront plus.
    /// </summary>
    public void Clear() => _pending.Clear();
}
