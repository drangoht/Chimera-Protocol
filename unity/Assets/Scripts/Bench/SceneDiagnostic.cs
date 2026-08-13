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
        // ⚠ Via LaunchArgs, et non `Environment.GetCommandLineArgs()` : ce composant vit dans la
        // scène du MENU, donc il démarre aussi dans le build web — où l'appel direct peut lever, et
        // où l'exception escamoterait silencieusement la suite de ce Start.
        bool wanted = LaunchArgs.Has("--diagnostic");

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

        // ⚠ Sans AudioListener, Unity ne restitue AUCUN son : les clips se chargent, les sources
        // jouent, les compteurs montent, et le jeu reste muet — sans la moindre erreur. Le relever
        // ici est le seul moyen de distinguer « pas de son » de « son inaudible ».
        int listeners = FindObjectsByType<AudioListener>(FindObjectsSortMode.None).Length;
        sb.AppendLine($"AudioListener        : {(listeners == 1 ? "present" : listeners == 0 ? "ABSENT — LE JEU SERA MUET" : $"{listeners} (un seul attendu)")}");
        sb.AppendLine($"sources audio        : {FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Length}");
        sb.AppendLine($"obstacles d'arene    : {ArenaObstacles.Count}");
        sb.AppendLine($"sons joues           : {AudioSystem.PlayedCount}");
        sb.AppendLine($"musique              : {(MusicDirector.Instance != null ? MusicDirector.Instance.CurrentTrack : "AUCUN MusicDirector")}");


        yield return AuditWeapons(sb);

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

            // Suivi du boss : « sa barre ne baisse pas » peut vouloir dire deux choses très
            // différentes — la barre est cassée, ou le boss encaisse trop pour un build de ce niveau.
            RustedCore? boss = null;
            foreach (var e in EnemyBase.Active)
                if (e is RustedCore rc && !rc.IsDead) { boss = rc; break; }

            string bossInfo = boss != null
                ? $"  BOSS {boss.CurrentHp,7:F0}/{boss.MaxHp,7:F0} ({boss.HpRatio * 100f,5:F1} %)"
                : "";

            var status = EnemyBase.StatusCounts();

            sb.AppendLine($"t={step * 5,2}s  ennemis={EnemyBase.Active.Count,3}  " +
                          $"geles={status.Slowed,3}/{EnemyBase.SlowsApplied,4}  " +
                          $"brulent={status.Burning,3}/{EnemyBase.BurnsApplied,4}  " +
                          $"elim.={(gm != null ? gm.Kills : 0),3}  " +
                          $"xp={(xp != null ? xp.CurrentXp : 0),3}/niv {(xp != null ? xp.CurrentLevel : 0)}  " +
                          $"PV={(player != null ? player.Stats.CurrentHp : 0),6:F1}  " +
                          $"orbes={FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length,3}  " +
                          $"dmin={minContactDist,6:F0}{bossInfo}");
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

        AppendFireSounds(sb);

        Debug.Log(sb.ToString());
        Application.Quit(0);
    }

    /// <summary>
    /// Sons de tir, <b>arme par arme</b> et dans la scène réelle.
    ///
    /// <para>Signalé en jouant le 2026-08-09 : « la bobine Tesla n'émet aucun son ». Elle n'était pas
    /// seule — le portage n'avait repris que deux des seize appels du jeu publié, et quatorze armes
    /// tiraient en silence depuis le premier jour.</para>
    ///
    /// <para><b>Pourquoi le total global ne suffisait pas.</b> La ligne « sons joués » monte avec les
    /// ramassages d'XP, les coups encaissés et les morts d'ennemis : elle affichait des centaines de
    /// sons pendant que l'arsenal entier était muet. Un compte <b>par identifiant</b>, mis en face de
    /// l'arme qui devrait le produire, est le seul relevé qui sépare les deux.</para>
    ///
    /// <para>⚠ Relevé <b>en fin</b> de diagnostic, jamais au début : posé après l'inventaire des
    /// systèmes, il tombait à la troisième seconde de run — aucune arme n'avait encore franchi sa
    /// recharge, et il annonçait « 0 » pour tout le monde, y compris pour ce qui marchait.</para>
    /// </summary>
    private static void AppendFireSounds(StringBuilder sb)
    {
        sb.AppendLine("sons de tir          :");

        foreach (var weapon in FindObjectsByType<WeaponBase>(FindObjectsSortMode.None))
        {
            string id = weapon.WeaponId.Length > 0 ? weapon.WeaponId : weapon.GetType().Name + " (ID NON RESOLU)";
            string? sfx = WeaponSfx.For(weapon.WeaponId);

            if (sfx == null)
            {
                sb.AppendLine($"  {id,-18} muette a dessein ({weapon.ShotsFired} tirs)");
                continue;
            }

            int played = AudioSystem.PlayedCountOf(sfx);
            string verdict = weapon.ShotsFired > 0 && played == 0 ? "  ⚠ A TIRE SANS BRUIT" : "";
            if (!AudioSystem.CanLoad(sfx)) verdict = "  ⚠ CLIP INTROUVABLE";

            sb.AppendLine($"  {id,-18} {weapon.ShotsFired,4} tirs → {played,4} × {sfx}{verdict}");
        }
    }

    private int _cardsPicked;
    private bool _bossSeen;

    /// <summary>
    /// Acquiert quelques armes <b>dans la scène réelle</b> et relève, pour chacune : existe-t-elle,
    /// tourne-t-elle, tire-t-elle, et <b>se voit-elle</b> ?
    ///
    /// <para>Signalé en jouant : « je ne vois pas les autres armes, ni leurs projectiles ». Trois
    /// pannes très différentes donnent ce même symptôme — l'arme n'est pas créée, elle est créée mais
    /// ne tire pas, ou elle tire sans rien d'affichable. Ce relevé les sépare.</para>
    /// </summary>
    private IEnumerator AuditWeapons(StringBuilder sb)
    {
        var inv = InventorySystem.Instance;
        var player = Player.Instance;
        if (inv == null || player == null) yield break;

        string[] tested = { "scatter_volley", "glaive", "seeker_swarm", "plasma_blade", "tesla_coil" };
        foreach (string id in tested) inv.AcquireOrLevelUp(id);

        yield return new WaitForSecondsRealtime(4f);

        sb.AppendLine();
        sb.AppendLine("--- armes acquises en jeu (composant / ticks / tirs / sprite) ---");

        foreach (string id in tested)
        {
            WeaponBase? found = null;
            foreach (var w in player.GetComponentsInChildren<WeaponBase>())
                if (w.gameObject.name == "W_" + id) found = w;

            if (found == null) { sb.AppendLine($"{id,-16} : ABSENTE (aucun objet W_{id})"); continue; }

            var sprite = found.GetComponentInChildren<SpriteRenderer>();
            sb.AppendLine($"{id,-16} : niv {inv.LevelOf(id)}  ticks={found.TicksRun,5}  " +
                          $"tirs={found.ShotsFired,4}  degats={found.BaseDamage,6:F1}  " +
                          $"sprite={(sprite != null ? "oui" : "AUCUN")}");
        }

        sb.AppendLine($"projectiles en vol   : bullets={FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length}, " +
                      $"missiles={FindObjectsByType<SeekerMissile>(FindObjectsSortMode.None).Length}, " +
                      $"glaives={FindObjectsByType<GlaiveProjectile>(FindObjectsSortMode.None).Length}");
    }

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
