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

        var bulletPrefab = BuildBulletPrefab();
        var enemyPrefab  = BuildEnemyPrefab();

        var cannon = playerGo.AddComponent<ImpulseCannon>();
        cannon.BaseDamage = 25f;
        cannon.BaseCooldown = 0.15f;
        cannon.Range = 500f;
        cannon.BulletPrefab = bulletPrefab;

        var spawnerGo = new GameObject("Spawner");
        var spawner = spawnerGo.AddComponent<EnemySpawner>();
        spawner.EnemyPrefab = enemyPrefab;
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

        // ─── Combat : l'arme doit tuer, l'XP doit monter ───────────────────────
        float t = 0f;
        while (t < 12f)
        {
            // Kite circulaire : le joueur bouge vraiment.
            float a = Time.time * 1.1f;
            playerGo.transform.position = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 260f;
            t += Time.deltaTime;
            yield return null;
        }

        Check("arme : des ennemis sont tues", xp.CurrentXp > 0 || xp.CurrentLevel > 1,
              $"niveau={xp.CurrentLevel} xp={xp.CurrentXp}");
        Check("xp : au moins une montee de niveau", levelUps > 0, $"{levelUps} montees");

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

        // ─── Fin de run ───────────────────────────────────────────────────────
        gm.EndRun();
        Check("run : cloturee proprement", gm.RunEnded && gm.RunTime > 0f,
              $"{gm.RunTime:F1} s");

        var sb = new StringBuilder();
        sb.AppendLine("=== VERIFICATION DU CŒUR DE RUN (LOT 2) ===");
        foreach (string r in _results) sb.AppendLine(r);
        sb.AppendLine(_failures == 0
            ? $"TOUT PASSE ({_results.Count} verifications)"
            : $"{_failures} ECHEC(S) sur {_results.Count}");
        Debug.Log(sb.ToString());

        Application.Quit(_failures == 0 ? 0 : 1);
    }

    // ─── Prefabs construits à la volée ────────────────────────────────────────

    private static GameObject BuildEnemyPrefab()
    {
        var go = new GameObject("Enemy");
        var enemy = go.AddComponent<EnemyBase>();
        enemy.MaxHp = 20f;
        enemy.Speed = 120f;
        enemy.Damage = 5f;
        enemy.XpValue = 1;
        go.SetActive(false);   // sert de gabarit, ne vit pas dans la scène
        return go;
    }

    private static GameObject BuildBulletPrefab()
    {
        var go = new GameObject("Bullet");
        go.AddComponent<Bullet>();
        go.SetActive(false);
        return go;
    }
}
