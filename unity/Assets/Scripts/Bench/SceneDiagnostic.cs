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

        // Sans ce pilote, le relevé s'arrête à la première montée de niveau : la modale met le jeu en
        // pause et personne ne la ferme en headless. Tout ce qui vient après — l'arsenal qui se
        // construit, l'overtime, l'arrivée du boss — restait donc invérifiable hors session jouée.
        StartCoroutine(AutoPickCards());

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

        // ─── Progression sur 30 s : c'est la DUREE qui manquait au premier relevé ──
        sb.AppendLine();
        sb.AppendLine("--- progression (t, ennemis, elim., xp, PV, orbes, dist. ennemi le + proche) ---");

        float minContactDist = float.MaxValue;
        int damageEvents = 0;
        float lastHp = player != null ? player.Stats.CurrentHp : 0f;

        for (int step = 1; step <= 6; step++)
        {
            float until = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < until)
            {
                if (player != null)
                {
                    // Kite circulaire : immobile, le joueur meurt en ~15 s et le relevé s'arrête avant
                    // tout ce qu'on cherche à observer. Il ne s'agit pas de bien jouer, mais de rester
                    // en vie assez longtemps pour que la run existe.
                    float a = Time.realtimeSinceStartup * 1.1f;
                    player.ExternalMoveOverride = new Vector2(Mathf.Cos(a), Mathf.Sin(a));

                    foreach (var e in EnemyBase.Active)
                        if (e != null)
                            minContactDist = Mathf.Min(minContactDist,
                                Vector2.Distance(e.transform.position, player.transform.position));

                    if (player.Stats.CurrentHp < lastHp) { damageEvents++; lastHp = player.Stats.CurrentHp; }
                }
                yield return null;
            }

            sb.AppendLine($"t={step * 5,2}s  ennemis={EnemyBase.Active.Count,3}  " +
                          $"elim.={(gm != null ? gm.Kills : 0),3}  " +
                          $"xp={(xp != null ? xp.CurrentXp : 0),3}/niv {(xp != null ? xp.CurrentLevel : 0)}  " +
                          $"PV={(player != null ? player.Stats.CurrentHp : 0),6:F1}  " +
                          $"orbes={FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length,3}  " +
                          $"dmin={minContactDist,6:F0}");
        }

        sb.AppendLine();
        sb.AppendLine($"coups encaisses      : {damageEvents}");
        sb.AppendLine($"distance mini vue    : {minContactDist:F1} (rayon de contact = 24)");

        // ─── Ce que la run a réellement produit ───────────────────────────────
        var inv = InventorySystem.Instance;
        sb.AppendLine($"cartes choisies      : {_cardsPicked}");
        sb.AppendLine($"armes portees        : {(inv != null ? inv.WeaponCount : 0)} " +
                      $"(objets sur le joueur : {(player != null ? player.GetComponentsInChildren<WeaponBase>().Length : 0)})");
        sb.AppendLine($"overtime             : {(gm != null && gm.Overtime ? "OUI" : "non")} " +
                      $"(impartis {(gm != null ? gm.RunDurationSeconds : 0)} s)");
        sb.AppendLine($"boss vu              : {(_bossSeen ? "OUI" : "NON")}");
        sb.AppendLine($"boss vaincu          : {(gm != null && gm.BossDefeated ? "oui" : "non")}");

        Debug.Log(sb.ToString());
        Application.Quit(0);
    }

    private int _cardsPicked;
    private bool _bossSeen;

    /// <summary>
    /// Choisit la première carte dès qu'un écran de montée de niveau s'ouvre — l'équivalent headless
    /// d'un joueur qui clique. Le choix n'est pas éclairé, et n'a pas à l'être : ce qu'on vérifie est
    /// que la run <b>continue</b> et que l'arsenal se construit vraiment.
    /// </summary>
    private IEnumerator AutoPickCards()
    {
        while (true)
        {
            // En temps réel : la modale met le jeu en pause (timeScale 0), donc une attente asservie
            // au temps de jeu ne se réveillerait jamais.
            yield return new WaitForSecondsRealtime(0.2f);

            foreach (var e in EnemyBase.Active)
                if (e is RustedCore) { _bossSeen = true; break; }

            var screen = FindFirstObjectByType<LevelUpScreen>();
            if (screen == null || !screen.IsVisible) continue;

            foreach (var button in screen.GetComponentsInChildren<UnityEngine.UI.Button>())
            {
                button.onClick.Invoke();
                _cardsPicked++;
                break;
            }
        }
    }
}
