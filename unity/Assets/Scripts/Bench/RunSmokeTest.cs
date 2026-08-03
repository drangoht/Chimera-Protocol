using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Vérifie le <b>cœur de run</b> porté au Lot 2, en conditions réelles et headless : bouger, faire
/// apparaître, tirer, tuer, créditer l'XP, monter de niveau, encaisser.
///
/// <para><b>C'est le critère de sortie du Lot 2</b> (docs/UNITY_MIGRATION_PLAN.md §6). Il assemble
/// la scène <b>par code</b> : à ce stade aucun prefab n'est encore authoré, et le but est de valider
/// la <i>logique</i> du portage, pas un travail d'éditeur.</para>
///
/// <para>Le joueur est piloté par un déplacement circulaire simple — assez pour que la séparation
/// joueur↔ennemis et les dégâts de contact travaillent vraiment, sans dépendre d'une entrée
/// clavier absente en batchmode.</para>
/// </summary>
public sealed class RunSmokeTest : MonoBehaviour
{
    private readonly List<string> _results = new();
    private int _failures;

    private void Check(string name, bool ok, string detail = "")
    {
        if (!ok) _failures++;
        _results.Add($"{(ok ? "  OK  " : " ECHEC")} {name}{(detail.Length > 0 ? " — " + detail : "")}");
    }

    private IEnumerator Start()
    {
        Application.targetFrameRate = -1;
        QualitySettings.vSyncCount = 0;
        Gd.Seed(2026);   // run reproductible

        // ─── Assemblage ───────────────────────────────────────────────────────
        var systems = new GameObject("[Systems]");
        var xp = systems.AddComponent<XpSystem>();
        var gm = systems.AddComponent<GameManager>();

        var playerGo = new GameObject("Player");
        var player = playerGo.AddComponent<Player>();

        // Les VRAIS prefabs, chargés par le même chemin logique que le code de jeu : c'est la
        // chaîne réelle qu'on veut valider, pas des gabarits fabriqués pour le test.
        var bulletPrefab = Spawner.Load("res://scenes/entities/Bullet.tscn");
        var enemyPrefab  = Spawner.Load("res://scenes/entities/Enemy.tscn");
        var orbPrefab    = Spawner.Load("res://scenes/entities/XpOrb.tscn");

        Check("prefabs : charges depuis Resources par chemin Godot",
              bulletPrefab != null && enemyPrefab != null && orbPrefab != null);
        if (bulletPrefab == null || enemyPrefab == null || orbPrefab == null)
        {
            Report();
            yield break;
        }

        var cannon = playerGo.AddComponent<ImpulseCannon>();
        cannon.BaseDamage = 25f;
        cannon.BaseCooldown = 0.15f;
        cannon.Range = 500f;
        cannon.BulletPrefab = bulletPrefab;

        Check("arme : prefab de projectile assigne", cannon.BulletPrefab != null,
              cannon.BulletPrefab != null ? cannon.BulletPrefab.name : "NULL");

        var spawnerGo = new GameObject("Spawner");
        var spawner = spawnerGo.AddComponent<EnemySpawner>();
        spawner.EnemyPrefab = enemyPrefab;
        spawner.XpOrbPrefab = orbPrefab;
        spawner.SpawnRadius = 320f;   // resserré : le banc doit voir du combat vite

        gm.StartRun();

        int levelUps = 0;
        xp.LevelUp += _ => levelUps++;

        Check("assemblage : joueur, systemes et spawner en place",
              Player.Instance != null && XpSystem.Instance != null && GameManager.Instance != null);
        Check("joueur : PV initiaux", Mathf.Approximately(player.Stats.CurrentHp, 100f),
              $"{player.Stats.CurrentHp}");

        // ─── Apparition ───────────────────────────────────────────────────────
        yield return new WaitForSeconds(2.0f);

        Check("spawner : des ennemis apparaissent", spawner.TotalSpawned > 0,
              $"{spawner.TotalSpawned} crees, {EnemyBase.Active.Count} vivants");

        // ─── Combat : l'arme doit tuer, les orbes doivent tomber puis être ramassés ──
        float t = 0f;
        int maxOrbsSeen = 0;
        int maxBulletsSeen = 0;
        float nearestSeen = float.MaxValue;

        while (t < 14f)
        {
            // Kite circulaire : le joueur bouge vraiment, donc il traverse les orbes tombés.
            float a = Time.time * 1.1f;
            playerGo.transform.position = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 260f;

            maxOrbsSeen = Mathf.Max(maxOrbsSeen, FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length);
            maxBulletsSeen = Mathf.Max(maxBulletsSeen, FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length);

            if (EnemyBase.Active.Count > 0)
            {
                float near = float.MaxValue;
                foreach (var e in EnemyBase.Active)
                    if (e != null) near = Mathf.Min(near, Vector2.Distance(e.transform.position, playerGo.transform.position));
                nearestSeen = Mathf.Min(nearestSeen, near);
            }

            t += Time.deltaTime;
            yield return null;
        }

        // Reproduction de la recherche de cible, pour distinguer « pas de cible » d'un défaut de
        // l'arme elle-même.
        int inRange = 0;
        foreach (var e in EnemyBase.Active)
            if (e != null && !e.IsDead &&
                Vector2.Distance(e.transform.position, playerGo.transform.position) < cannon.Range)
                inRange++;

        Check("arme : l'arme tourne", cannon.TicksRun > 0, $"{cannon.TicksRun} ticks");
        Check("arme : au moins une cible a portee", inRange > 0,
              $"portee={cannon.Range}, {inRange} cibles sur {EnemyBase.Active.Count} vivants");
        Check("arme : des projectiles sont tires", cannon.ShotsFired > 0,
              $"{cannon.ShotsFired} tirs, {maxBulletsSeen} en vol au max, " +
              $"ennemi le plus proche a {nearestSeen:F0}");

        Check("mort : des orbes d'XP tombent", maxOrbsSeen > 0, $"{maxOrbsSeen} orbes vus simultanement");

        // L'XP ne peut plus monter que par RAMASSAGE : si elle monte, toute la boucle
        // tuer → laisser tomber → attirer → ramasser fonctionne de bout en bout.
        Check("orbes : ramasses et credites", xp.CurrentXp > 0 || xp.CurrentLevel > 1,
              $"niveau={xp.CurrentLevel} xp={xp.CurrentXp}");
        Check("xp : au moins une montee de niveau", levelUps > 0, $"{levelUps} montees");
        Check("run : des victimes comptabilisees", gm.Kills > 0, $"{gm.Kills} elim.");

        // ─── Dégâts de contact et i-frames ─────────────────────────────────────
        // On colle un ennemi au joueur : un seul coup doit passer par fenêtre de 0,45 s.
        playerGo.transform.position = Vector3.zero;
        float hpBefore = player.Stats.CurrentHp;

        var glued = Instantiate(enemyPrefab, Vector3.zero, Quaternion.identity);
        glued.SetActive(true);   // le gabarit est inactif ; le spawner fait de même de son côté

        var gluedEnemy = glued.GetComponent<EnemyBase>();
        gluedEnemy.Damage = 7f;
        gluedEnemy.MaxHp = 100000f;   // il doit survivre au canon pendant la mesure
        gluedEnemy.ApplyScaling(100000f, 7f);

        yield return new WaitForSeconds(1.0f);

        float lost = hpBefore - player.Stats.CurrentHp;
        // En 1 s, la fenêtre de 0,45 s autorise 2 à 3 coups de 7 PV, jamais un par frame.
        Check("degats de contact : le joueur encaisse", lost > 0f, $"{lost:F1} PV perdus");
        Check("i-frames : la nuee ne tue pas en une frame", lost <= 7f * 4f,
              $"{lost:F1} PV en 1 s (plafond theorique {7f * 4f:F0})");

        if (gluedEnemy != null) Destroy(gluedEnemy.gameObject);

        // ─── Arsenal et fusions (critère de sortie du Lot 3) ──────────────────
        yield return RunFusionChecks(systems);

        // ─── Fin de run ───────────────────────────────────────────────────────
        gm.EndRun();
        Check("run : cloturee proprement", gm.RunEnded && gm.RunTime > 0f,
              $"{gm.RunTime:F1} s");

        Report();
    }

    /// <summary>
    /// Vérifie l'arsenal <b>de bout en bout</b>, en particulier l'héritage de niveau des fusions.
    /// La règle est déjà couverte en logique pure par <c>WeaponFusionTests</c> ; ici on s'assure
    /// que le chemin réel — données lues depuis <c>StreamingAssets</c>, arme montée, passif acquis,
    /// fusion forgée — l'applique vraiment. C'est la différence entre « la règle est juste » et
    /// « le jeu l'utilise ».
    /// </summary>
    private IEnumerator RunFusionChecks(GameObject systems)
    {
        var inv = systems.AddComponent<InventorySystem>();
        yield return null;   // laisse Awake charger les données

        Check("arsenal : weapons.json charge depuis StreamingAssets",
              inv.AcquireOrLevelUp("impulse_cannon") > 0);

        // Monte l'arme jusqu'au niveau requis par la fusion.
        for (int i = 1; i < 5; i++) inv.AcquireOrLevelUp("impulse_cannon");
        int weaponLevel = inv.LevelOf("impulse_cannon");
        Check("arsenal : une arme monte en niveau", weaponLevel == 5, $"niveau={weaponLevel}");

        Check("fusion : verrouillee sans le passif requis", !inv.CanFuse("rail_overcharged"));

        inv.AddPassive("capacitor");
        Check("fusion : deblocable avec arme montee + passif", inv.CanFuse("rail_overcharged"));

        int inherited = inv.ApplyFusion("rail_overcharged");

        // LE critère : la fusion reprend le niveau investi, elle ne repart pas de 1.
        Check("fusion : herite du niveau de l'arme remplacee (regression 1.21.0)",
              inherited == weaponLevel, $"herite={inherited}, attendu={weaponLevel}");
        Check("fusion : l'arme source est retiree de l'arsenal", !inv.Has("impulse_cannon"));
        Check("fusion : enregistree comme forgee", inv.AppliedFusions.Count == 1);
        Check("fusion : non reforgeable", inv.ApplyFusion("rail_overcharged") == 0);
    }

    private void Report()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== VERIFICATION DU CŒUR DE RUN (LOT 2) ===");
        foreach (string r in _results) sb.AppendLine(r);
        sb.AppendLine(_failures == 0
            ? $"TOUT PASSE ({_results.Count} verifications)"
            : $"{_failures} ECHEC(S) sur {_results.Count}");
        Debug.Log(sb.ToString());

        Application.Quit(_failures == 0 ? 0 : 1);
    }
}
