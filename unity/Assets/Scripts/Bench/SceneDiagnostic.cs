using System.Collections;
using System.Text;
using UnityEngine;

/// <summary>
/// Diagnostic de la <b>vraie scène de jeu</b>, activé par le drapeau <c>--diagnostic</c>.
///
/// <para>Écrit parce qu'un rapport de jeu (« le joueur ne bouge pas, aucune barre ne bouge ») ne
/// désigne pas une cause : plusieurs pannes très différentes produisent exactement ces symptômes —
/// temps figé, <c>Update</c> interrompu par une exception, statistique à zéro, singleton absent.
/// Ce composant relève l'état réel plutôt que de le supposer.</para>
///
/// <para>Il pilote le joueur par <see cref="Player.ExternalMoveOverride"/>, ce qui sépare deux
/// questions que le symptôme confond : « le déplacement fonctionne-t-il ? » et « l'entrée
/// clavier arrive-t-elle ? ».</para>
/// </summary>
public sealed class SceneDiagnostic : MonoBehaviour
{
    private void Start()
    {
        bool wanted = false;
        foreach (string a in System.Environment.GetCommandLineArgs())
            if (a == "--diagnostic") wanted = true;

        if (!wanted) { Destroy(this); return; }

        // Sans cela, le changement de scene detruit ce composant et sa coroutine
        // meurt avant d'avoir rien releve.
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);
        StartCoroutine(Run());
    }

    private IEnumerator Run()
    {
        Debug.Log("[DIAG] chargement de la scene de jeu…");
        SceneRoot.ChangeScene(GameScenes.Game);

        yield return new WaitForSecondsRealtime(3f);

        var sb = new StringBuilder();
        sb.AppendLine("=== DIAGNOSTIC DE LA SCENE DE JEU ===");
        sb.AppendLine($"timeScale            : {Time.timeScale}");
        sb.AppendLine($"deltaTime            : {Time.deltaTime:F5}");
        sb.AppendLine($"Player.Instance      : {(Player.Instance != null ? "present" : "ABSENT")}");
        sb.AppendLine($"XpSystem.Instance    : {(XpSystem.Instance != null ? "present" : "ABSENT")}");
        sb.AppendLine($"GameManager.Instance : {(GameManager.Instance != null ? "present" : "ABSENT")}");
        sb.AppendLine($"Inventory.Instance   : {(InventorySystem.Instance != null ? "present" : "ABSENT")}");
        sb.AppendLine($"ennemis vivants      : {EnemyBase.Active.Count}");

        var gm = GameManager.Instance;
        sb.AppendLine($"RunTime              : {(gm != null ? gm.RunTime.ToString("F2") : "n/a")}");
        sb.AppendLine($"RunEnded             : {(gm != null ? gm.RunEnded.ToString() : "n/a")}");
        sb.AppendLine($"Kills                : {(gm != null ? gm.Kills.ToString() : "n/a")}");

        var xp = XpSystem.Instance;
        sb.AppendLine($"XP / niveau          : {(xp != null ? $"{xp.CurrentXp} / niv {xp.CurrentLevel}" : "n/a")}");

        var player = Player.Instance;
        if (player != null)
        {
            sb.AppendLine($"PV                   : {player.Stats.CurrentHp:F1} / {player.Stats.MaxHp:F1}");
            sb.AppendLine($"Speed / multiplic.   : {player.Stats.Speed} × {player.SpeedMultiplier}");
            sb.AppendLine($"IsDead               : {player.IsDead}");

            // Sépare « le déplacement fonctionne » de « l'entrée arrive » : on force la direction.
            Vector3 before = player.transform.position;
            player.ExternalMoveOverride = Vector2.right;
            yield return new WaitForSecondsRealtime(1f);
            Vector3 after = player.transform.position;
            player.ExternalMoveOverride = null;

            float moved = Vector3.Distance(before, after);
            sb.AppendLine($"deplacement force    : {moved:F1} unites en 1 s " +
                          (moved > 10f ? "→ la logique de deplacement FONCTIONNE"
                                       : "→ la logique de deplacement EST EN PANNE"));
            sb.AppendLine($"position             : {before} → {after}");
        }

        int orbs = FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length;
        sb.AppendLine($"orbes presents       : {orbs}");

        Debug.Log(sb.ToString());
        Application.Quit(0);
    }
}
