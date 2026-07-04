using Godot;

/// <summary>
/// Nettoyage des VFX/projectiles « monde » résiduels avant une transition de scène qui quitte une run.
///
/// Le projet parente systématiquement les entités éphémères de gameplay (balles, flammes, death
/// bursts, anneaux de choc, éclairs, explosions d'élite…) à la **racine** (`GetTree().Root`), pas à
/// la scène de jeu — si bien que `ChangeSceneToFile`, qui ne libère que `CurrentScene`, les laisse en
/// place. En temps normal ils s'auto-détruisent en une fraction de seconde ; mais **à la mort du
/// joueur l'arbre est mis en pause** (`RunStatsTracker`), ce qui gèle leurs timers/tweens : ils
/// restent, figés, comme enfants de la racine et réapparaissent par-dessus le menu/Hub après la
/// transition (bug « effets graphiques encore visibles sur le menu »).
///
/// Balayage sûr : tous les singletons AutoLoad du projet sont des `Node`/`CanvasLayer` (jamais
/// `Node2D`), donc les seuls `Node2D` enfants directs de la racine sont la scène courante (que
/// `ChangeSceneToFile` va elle-même libérer) et ces VFX résiduels. On libère donc tous les `Node2D`
/// enfants directs de la racine **sauf** `CurrentScene`.
/// </summary>
public static class SceneCleanup
{
    public static void ClearWorldVfx(SceneTree tree)
    {
        if (tree == null) return;
        var current = tree.CurrentScene;
        foreach (var child in tree.Root.GetChildren())
            if (child is Node2D && child != current)
                child.QueueFree();
    }
}
