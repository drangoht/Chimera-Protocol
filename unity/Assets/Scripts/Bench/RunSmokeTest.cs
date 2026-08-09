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

    /// <summary>
    /// Vérifie qu'une zone de défilement peut <b>recevoir la molette</b>.
    /// </summary>
    /// <remarks>
    /// <para>uGUI ne route un cran de molette que vers ce que le rayon du pointeur touche, et un
    /// rayon ne touche que des <c>Graphic</c> à <c>raycastTarget</c>. Une fenêtre de défilement sans
    /// aucun graphique — ce que produit naturellement une interface construite par code — n'en
    /// reçoit donc que là où le pointeur tombe par hasard sur un libellé. Aux Options, faites
    /// surtout d'espace libre, cela se lit « la molette ne fonctionne pas ».</para>
    ///
    /// <para>C'est le mode de défaillance habituel de ce portage : le réglage visible
    /// (<c>scrollSensitivity</c>) était juste, la <b>chaîne</b> qui l'amène ne l'était pas. Vérifier
    /// la sensibilité n'aurait rien montré.</para>
    /// </remarks>
    private void CheckScrollWheel(GameObject host, string screen)
    {
        var scrolls = host.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(true);

        int deaf = 0;
        foreach (var scroll in scrolls)
        {
            var viewport = scroll.viewport != null
                ? scroll.viewport
                : scroll.GetComponent<RectTransform>();

            var graphic = viewport != null ? viewport.GetComponent<UnityEngine.UI.Graphic>() : null;
            if (graphic == null || !graphic.raycastTarget) deaf++;
        }

        Check($"{screen} : la molette atteint la zone de defilement",
              scrolls.Length > 0 && deaf == 0,
              $"{scrolls.Length} zone(s), {deaf} sourde(s) a la molette");
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

        // Les prefabs de champions viennent de Resources, par le même chemin que le jeu : un
        // champion dont le prefab manque retomberait silencieusement sur la faune générique.
        var bossPrefab = Spawner.Load("res://scenes/entities/RustedCore.tscn");
        spawner.MiniBossPrefab = Spawner.Load("res://scenes/entities/MiniBoss.tscn");
        spawner.ChampionPrefabs = new[]
        {
            new EnemySpawner.NamedPrefab { Id = "molten_colossus", Prefab = Spawner.Load("res://scenes/entities/MoltenColossus.tscn") },
            new EnemySpawner.NamedPrefab { Id = "cryo_sentinel",   Prefab = Spawner.Load("res://scenes/entities/CryoSentinel.tscn") },
            new EnemySpawner.NamedPrefab { Id = "neon_warden",     Prefab = Spawner.Load("res://scenes/entities/NeonWarden.tscn") },
            new EnemySpawner.NamedPrefab { Id = EnemySpawner.BossId, Prefab = bossPrefab },
        };

        Check("prefabs : champions et boss presents dans Resources",
              bossPrefab != null && spawner.MiniBossPrefab != null &&
              System.Array.TrueForAll(spawner.ChampionPrefabs, p => p.Prefab != null));

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

        Check("bestiaire : enemies.json charge", spawner.BestiarySize >= 31,
              $"{spawner.BestiarySize} types");

        // La variété se mesure plus loin, sur la phase de combat : à 2 secondes de jeu, deux
        // ennemis vivants ne constituent pas un échantillon.

        // ─── Combat : l'arme doit tuer, les orbes doivent tomber puis être ramassés ──
        float t = 0f;
        int maxOrbsSeen = 0;
        int maxBulletsSeen = 0;
        float nearestSeen = float.MaxValue;
        var distinctProfiles = new HashSet<int>();

        while (t < 14f)
        {
            // Kite circulaire : le joueur bouge vraiment, donc il traverse les orbes tombés.
            float a = Time.time * 1.1f;
            playerGo.transform.position = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * 260f;

            maxOrbsSeen = Mathf.Max(maxOrbsSeen, FindObjectsByType<XpOrb>(FindObjectsSortMode.None).Length);
            foreach (var e in EnemyBase.Active) if (e != null) distinctProfiles.Add(Mathf.RoundToInt(e.MaxHp));
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

        // Variete du bestiaire : si tous les ennemis sortaient identiques, la table de donnees
        // serait chargee mais ignoree — une panne parfaitement muette.
        Check("bestiaire : le tirage produit des ennemis varies", distinctProfiles.Count > 1,
              $"{distinctProfiles.Count} profils de PV distincts observes");

        // L'XP ne peut plus monter que par RAMASSAGE : si elle monte, toute la boucle
        // tuer → laisser tomber → attirer → ramasser fonctionne de bout en bout.
        Check("orbes : ramasses et credites", xp.CurrentXp > 0 || xp.CurrentLevel > 1,
              $"niveau={xp.CurrentLevel} xp={xp.CurrentXp}");
        Check("xp : au moins une montee de niveau", levelUps > 0, $"{levelUps} montees");
        Check("run : des victimes comptabilisees", gm.Kills > 0, $"{gm.Kills} elim.");

        // ─── Noyaux d'Aether ───────────────────────────────────────────────────
        // ⚠ Ils ne s'aspirent PAS, contrairement aux orbes : le joueur doit entrer dans le rayon.
        // Un test qui poserait le Noyau et attendrait sans bouger ne prouverait rien — c'est
        // exactement le piège des ramassables « walk-over » déjà documenté.
        var corePrefab = Spawner.Load("res://scenes/entities/AetherCore.tscn");
        Check("noyau : prefab charge depuis Resources", corePrefab != null);

        // ⚠ Deux ramassables ne partagent JAMAIS un sprite. L'orbe d'XP prenait « le premier sprite
        // du dossier », qui se trouvait être celui du Noyau : tous les orbes ressemblaient à des
        // Noyaux, et le joueur a signalé un compteur qui « n'évolue pas » alors qu'il était exact.
        // Une confusion d'apparence produit un rapport de bug sur une tout autre mécanique.
        // Le contrôle porte sur TOUS les gabarits à sprite, et pas seulement sur la paire fautive :
        // le chargement « premier trouvé » avait donné le même sprite à quatre gabarits — l'orbe
        // d'XP déguisée en Noyau, et les trois projectiles du joueur portant celui des TIRS ENNEMIS.
        // Deux entités qui se ressemblent produisent un rapport de bug sur une autre mécanique.
        {
            // ⚠ Missile et Glaive ne figurent PAS ici : ils construisent leur silhouette à
            // l'exécution parce qu'ils tournent, et leur gabarit n'a volontairement aucun sprite.
            // Les y inclure ferait porter le contrôle sur une donnée inerte — c'est-à-dire sur
            // exactement ce qui a laissé une barre droite passer pour un missile. Ils sont vérifiés
            // plus bas, sur ce qu'ils affichent RÉELLEMENT.
            var named = new (string Name, GameObject? Prefab)[]
            {
                ("noyau", corePrefab),
                ("orbe d'XP", orbPrefab),
                ("tir joueur", Spawner.Load("res://scenes/entities/Bullet.tscn")),
                ("tir ennemi", Spawner.Load("res://scenes/entities/EnemyBullet.tscn")),
            };

            var seen = new Dictionary<string, string>();
            var clashes = new List<string>();
            var missing = new List<string>();

            foreach (var (name, prefab) in named)
            {
                var sprite = prefab != null ? prefab.GetComponent<SpriteRenderer>()?.sprite : null;
                if (sprite == null) { missing.Add(name); continue; }

                if (seen.TryGetValue(sprite.name, out string? other))
                    clashes.Add($"{name} = {other} ({sprite.name})");
                else
                    seen[sprite.name] = name;
            }

            Check("gabarits : chaque entite a son propre sprite",
                  clashes.Count == 0 && missing.Count == 0,
                  clashes.Count > 0 ? "doublons : " + string.Join(", ", clashes)
                  : missing.Count > 0 ? "sans sprite : " + string.Join(", ", missing)
                  : $"{seen.Count} sprites distincts");

            // Les deux projectiles qui s'habillent seuls : on vérifie ce qui est AFFICHÉ, en les
            // lançant pour de bon. Un gabarit sans sprite et un `Dress()` qui ne s'exécute pas
            // donnent un projectile invisible — pire qu'une mauvaise forme.
            var dressed = new List<string>();

            var mgo = Spawner.Load("res://scenes/entities/Missile.tscn");
            if (mgo != null)
            {
                var m = Instantiate(mgo, new Vector3(1200f, 1200f), Quaternion.identity);
                m.SetActive(true);
                m.GetComponent<SeekerMissile>()?.Launch(Vector2.right, 1f, null);

                var s = m.GetComponent<SpriteRenderer>()?.sprite;
                if (s == null) dressed.Add("missile");
                Destroy(m);
            }

            var ggo = Spawner.Load("res://scenes/entities/Glaive.tscn");
            if (ggo != null)
            {
                var g = Instantiate(ggo, new Vector3(1200f, 1240f), Quaternion.identity);
                g.SetActive(true);
                g.GetComponent<GlaiveProjectile>()?.Launch(Vector2.right, 1f, 100f);

                var s = g.GetComponent<SpriteRenderer>()?.sprite;
                if (s == null) dressed.Add("glaive");
                Destroy(g);
            }

            Check("projectiles tournants : ils posent leur silhouette au lancement",
                  dressed.Count == 0,
                  dressed.Count == 0 ? "missile et glaive habilles"
                                     : "sans sprite a l'ecran : " + string.Join(", ", dressed));
        }

        if (corePrefab != null)
        {
            var coreSpawnerGo = new GameObject("AetherCoreSpawner");
            var coreSpawner = coreSpawnerGo.AddComponent<AetherCoreSpawner>();
            coreSpawner.CorePrefab = corePrefab;

            int before = gm.CoresCollected;
            playerGo.transform.position = Vector3.zero;

            // Posé LOIN du joueur, puis le joueur marche dessus : c'est la boucle réelle.
            AetherCoreSpawner.SpawnAt(new Vector3(200f, 0f, 0f));
            yield return null;

            int placed = FindObjectsByType<AetherCore>(FindObjectsSortMode.None).Length;
            Check("noyau : depose dans l'arene", placed > 0, $"{placed} noyau(x) present(s)");

            Check("noyau : ne s'aspire pas comme un orbe",
                  gm.CoresCollected == before,
                  $"a 200 px, compteur {gm.CoresCollected} (attendu {before})");

            for (int i = 0; i < 40 && gm.CoresCollected == before; i++)
            {
                playerGo.transform.position = Vector3.MoveTowards(
                    playerGo.transform.position, new Vector3(200f, 0f, 0f), 12f);
                yield return null;
            }

            Check("noyau : ramasse au contact et comptabilise", gm.CoresCollected > before,
                  $"{gm.CoresCollected} noyau(x) ramasse(s)");

            // Le rayon suit la méta-progression, seule chose qu'elle élargit.
            Check("noyau : le rayon suit core_magnetism",
                  Mathf.Approximately(AetherCore.RadiusForLevel(0), 20f)
                  && Mathf.Approximately(AetherCore.RadiusForLevel(3), 70f),
                  $"niv0={AetherCore.RadiusForLevel(0):F0} px, niv3={AetherCore.RadiusForLevel(3):F0} px");

            // La règle de butin vient des données : une table vide voudrait dire que plus aucun
            // ennemi n'en laisse tomber, et rien ne le signalerait en jeu avant des minutes.
            Check("noyau : les regles de butin sont chargees", AetherCoreDrops.RuleCount > 0,
                  $"{AetherCoreDrops.RuleCount} regle(s)");

            Destroy(coreSpawnerGo);
        }

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
        yield return RunArchetypeChecks(enemyPrefab, bulletPrefab);
        yield return RunDirectedWeaponChecks(bulletPrefab);
        yield return RunStatusFxChecks(enemyPrefab);
        yield return RunAllWeaponsFire(enemyPrefab, bulletPrefab);
        yield return RunEliteChecks(enemyPrefab);
        yield return RunBossChecks(enemyPrefab);
        yield return RunModalChecks();
        yield return RunScreenChecks();
        yield return RunFusionChecks(systems);
        yield return RunProgressionChecks(playerGo);
        yield return RunBossSpawnChecks(gm, spawner);
        yield return RunMetaChecks();
        yield return RunAssimilationChecks(enemyPrefab);
        yield return RunChimeraChecks(enemyPrefab);
        yield return RunHitStopChecks();
        // ⚠ Le spawner est SUSPENDU pendant ces deux mesures. Elles portent sur un coup unique reçu
        // par le joueur, or ces contrôles durent plusieurs secondes : une nuée qui continue de
        // repeupler l'arène consommerait les charges de survie en arrière-plan, et la mesure
        // conclurait à un filet qui ne se déclenche pas.
        spawner.enabled = false;
        ClearArena();
        yield return RunPortedRulesChecks(enemyPrefab);
        yield return RunSafetyNetChecks();
        yield return RunPurgeChecks(spawner.MiniBossPrefab);
        spawner.enabled = true;

        // ─── Fin de run ───────────────────────────────────────────────────────
        gm.EndRun();
        Check("run : cloturee proprement", gm.RunEnded && gm.RunTime > 0f,
              $"{gm.RunTime:F1} s");

        Report();
    }

    /// <summary>
    /// <b>Critère de sortie du Lot 3 : « les 21 armes tirent ».</b> Chaque arme est montée seule,
    /// face à des cibles, et doit franchir sa recharge au moins une fois.
    ///
    /// <para>Une arme silencieuse ne lève aucune erreur : elle rate simplement sa cible, ou attend
    /// une condition qui n'arrive jamais. C'est un mode de défaillance parfaitement muet, et le seul
    /// moyen de le détecter est de compter les tirs.</para>
    /// </summary>
    private IEnumerator RunAllWeaponsFire(GameObject enemyPrefab, GameObject bulletPrefab)
    {
        // Prefabs de projectiles spécialisés, construits à la volée : leur apparence appartient aux
        // lots suivants, seule leur logique compte ici.
        var missilePrefab = new GameObject("Missile", typeof(SeekerMissile));
        missilePrefab.SetActive(false);
        var glaivePrefab = new GameObject("Glaive", typeof(GlaiveProjectile));
        glaivePrefab.SetActive(false);

        var types = new (string Name, System.Type Type)[]
        {
            ("impulse_cannon",  typeof(ImpulseCannon)),  ("plasma_blade",   typeof(PlasmaBlade)),
            ("drone_swarm",     typeof(DroneSwarm)),     ("overload_field", typeof(OverloadField)),
            ("tesla_coil",      typeof(TeslaCoil)),      ("scatter_volley", typeof(ScatterVolley)),
            ("glaive",          typeof(Glaive)),         ("seeker_swarm",   typeof(SeekerSwarm)),
            ("cryo_lance",      typeof(CryoLance)),      ("pyre_stream",    typeof(PyreStream)),
            ("vector_lance",    typeof(VectorLance)),    ("singularity",    typeof(Singularity)),
            ("fusion_blade",    typeof(FusionBlade)),    ("rail_overcharged", typeof(RailOvercharged)),
            ("orbital_swarm",   typeof(OrbitalSwarm)),   ("overload_aegis", typeof(OverloadAegis)),
            ("ionic_storm",     typeof(IonicStorm)),     ("solar_column",   typeof(SolarColumn)),
            ("hornet_swarm",    typeof(HornetSwarm)),    ("vector_beam",    typeof(VectorBeam)),
            ("frost_veil",      typeof(FrostVeil)),
        };

        var silent = new List<string>();
        var invisible = new List<string>();

        // Armes qui tirent SANS BRUIT. Signalé en jouant (« la bobine Tesla n'émet aucun son ») :
        // le portage n'avait repris que deux des seize appels de son du jeu publié, et les quatorze
        // autres étaient muettes depuis le premier jour. Rien ne pouvait le montrer — une arme sans
        // son blesse, monte de niveau et s'affiche normalement, et une capture d'écran est muette.
        var mute = new List<string>();
        var unloadable = new List<string>();

        // Armes qui se voient par leurs PROJECTILES ou leurs drones : elles n'ont pas à laisser de
        // trace. Toutes les autres frappent à distance sans rien lancer — sans trace, elles tuent
        // sans que rien n'apparaisse à l'écran, et le joueur lit « la carte n'a rien fait ».
        //
        // La liste a RÉTRÉCI avec les vrais VFX : les trois armes à tir direct posent désormais un
        // flash de bouche, donc la vérification les couvre au lieu de les excuser.
        var rendersWithoutTrace = new HashSet<string>
        {
            "seeker_swarm", "glaive", "rail_overcharged", "hornet_swarm",
            "drone_swarm", "orbital_swarm",
        };

        foreach (var (name, type) in types)
        {
            int tracesBefore = Vfx.TracesCreated;

            // Le son est compté PAR IDENTIFIANT, pas globalement : le porteur encaisse des coups
            // pendant la mesure (les cibles sont à cinquante pixels), et le compte global monterait
            // donc même pour une arme parfaitement muette.
            string? expectedSfx = WeaponSfx.For(name);
            int sfxBefore = expectedSfx != null ? AudioSystem.PlayedCountOf(expectedSfx) : 0;

            var host = new GameObject("W_" + name);
            host.transform.position = Vector3.zero;

            var dummies = new List<GameObject>();
            foreach (var off in new[] { new Vector3(50f, 0f), new Vector3(80f, 40f), new Vector3(30f, -60f) })
            {
                var go = Instantiate(enemyPrefab, off, Quaternion.identity);
                go.SetActive(true);
                var e = go.GetComponent<EnemyBase>();
                e.ApplyScaling(1000000f, 0f);
                e.Speed = 0f;
                dummies.Add(go);
            }

            var weapon = (WeaponBase)host.AddComponent(type);
            InjectPrefabs(weapon, bulletPrefab, missilePrefab, glaivePrefab);

            // Le porteur doit rester vivant : toute arme cesse de tirer quand le joueur meurt, et
            // les quatre dernières testées passeraient sinon pour silencieuses sans l'être.
            if (Player.Instance != null)
            {
                Player.Instance.transform.position = Vector3.zero;
                Player.Instance.HealFlat(Player.Instance.Stats.MaxHp);
            }

            yield return new WaitForSeconds(1.6f);

            // L'essaim orbital n'a volontairement pas de tir global : ses drones blessent seuls.
            bool ok = weapon is DroneSwarm || weapon.ShotsFired > 0;
            if (!ok) silent.Add(name);

            if (ok && !rendersWithoutTrace.Contains(name) && Vfx.TracesCreated == tracesBefore)
                invisible.Add(name);

            if (!WeaponSfx.Covers(name)) mute.Add(name + " (absente de WeaponSfx)");
            else if (expectedSfx != null)
            {
                // Deux relevés, parce qu'ils tombent en panne séparément : l'appel peut ne jamais
                // partir (le défaut d'origine), ou partir sur un identifiant dont le fichier manque
                // — et dans ce second cas l'arme reste muette pendant que le compteur monte.
                if (!AudioSystem.CanLoad(expectedSfx)) unloadable.Add($"{name} → {expectedSfx}");
                if (ok && AudioSystem.PlayedCountOf(expectedSfx) == sfxBefore) mute.Add(name);
            }

            foreach (var d in dummies) if (d != null) Destroy(d);
            Destroy(host);
            yield return null;
        }

        Check($"arsenal : les {types.Length} armes tirent", silent.Count == 0,
              silent.Count == 0 ? "aucune silencieuse" : "silencieuses : " + string.Join(", ", silent));

        // Signalé en jouant : « je ne vois pas les autres armes ». Une arme qui tue sans laisser de
        // trace est indiscernable d'une carte sans effet — donc c'est un défaut, pas une finition.
        Check("arsenal : aucune arme n'est invisible", invisible.Count == 0,
              invisible.Count == 0
                  ? $"{Vfx.TracesCreated} traces dessinees"
                  : "sans trace : " + string.Join(", ", invisible));

        // Signalé en jouant, le 2026-08-09. Le pendant sonore exact du critère ci-dessus : une arme
        // qu'on n'entend pas se lit comme une arme qui ne part pas.
        Check($"arsenal : les {types.Length - WeaponSfx.Silent.Count} armes a son se font entendre",
              mute.Count == 0,
              mute.Count == 0
                  ? $"{WeaponSfx.Silent.Count} muettes a dessein : {string.Join(", ", WeaponSfx.Silent)}"
                  : "muettes : " + string.Join(", ", mute));

        Check("arsenal : les sons de tir se chargent", unloadable.Count == 0,
              unloadable.Count == 0 ? "tous presents" : "introuvables : " + string.Join(", ", unloadable));

        // La CAUSE, relevée à part : tout ce qui est indexé par identifiant d'arme (le son, mais
        // aussi ce qui viendra après) passe par cette résolution. Trois armes héritent d'une autre,
        // et une résolution qui remonterait la chaîne d'héritage rendrait l'identité de la base.
        var unresolved = new List<string>();
        foreach (var (name, type) in types)
            if (WeaponRegistry.IdOf(type) != name) unresolved.Add($"{name} → {WeaponRegistry.IdOf(type) ?? "null"}");

        Check("arsenal : chaque classe d'arme resout son identifiant", unresolved.Count == 0,
              unresolved.Count == 0 ? $"{types.Length} armes" : string.Join(", ", unresolved));

        // Les effets sont RECYCLÉS. Une fuite du vivier a le pire symptôme possible : les effets
        // disparaissent au bout de quelques minutes de jeu, quand les plafonds sont atteints — donc
        // jamais pendant un test court, et jamais dans l'éditeur.
        //
        // Le critère n'est PAS « zéro actif » : la partie continue de tourner pendant la mesure
        // (l'arme du joueur tire, les ennemis meurent), donc un compte instantané n'est jamais nul.
        // Ce qui distingue une fuite, c'est que le compte reste BORNÉ après des centaines
        // d'émissions ; sans recyclage il serait collé aux plafonds, soit plus de 450.
        yield return new WaitForSeconds(1f);
        Check("vfx : les effets retournent au vivier",
              Vfx.TracesCreated > 200 && Vfx.ActiveEffects < 60,
              $"{Vfx.ActiveEffects} actifs pour {Vfx.TracesCreated} emis");

        Destroy(missilePrefab);
        Destroy(glaivePrefab);
    }

    /// <summary>
    /// Vérifie les <b>affixes d'élite</b> : chacun doit produire un effet observable. Un affixe qui
    /// se pose sans rien changer est le mode de défaillance le plus probable ici — l'ennemi est bien
    /// marqué « élite », teinté et agrandi, mais se comporte comme un ennemi ordinaire.
    /// </summary>
    private IEnumerator RunEliteChecks(GameObject enemyPrefab)
    {
        EnemyBase Make(EliteAffix affix, float hp = 100f, float dmg = 5f)
        {
            var go = Instantiate(enemyPrefab, new Vector3(400f, 400f), Quaternion.identity);
            go.SetActive(true);
            var e = go.GetComponent<EnemyBase>();
            e.ApplyScaling(hp, dmg);
            e.Speed = 0f;
            e.ApplyElite(affix);
            return e;
        }

        // Blindé : encaisse nettement mieux à dégâts identiques.
        var plain   = Make(EliteAffix.None);
        var armored = Make(EliteAffix.Armored);
        float plainBefore = plain.CurrentHp, armoredBefore = armored.CurrentHp;
        plain.TakeDamage(50f);
        armored.TakeDamage(50f);

        float plainLost = plainBefore - plain.CurrentHp;
        float armoredLost = armoredBefore - armored.CurrentHp;
        Check("elite Blinde : encaisse mieux", armoredLost < plainLost,
              $"ordinaire -{plainLost:F0}, blinde -{armoredLost:F0}");

        // Régénérant : remonte s'il n'est plus frappé.
        var regen = Make(EliteAffix.Regenerating, hp: 500f);
        regen.TakeDamage(200f);
        float afterHit = regen.CurrentHp;
        yield return new WaitForSeconds(2.5f);
        Check("elite Regenerant : se soigne s'il n'est pas frappe", regen.CurrentHp > afterHit,
              $"{afterHit:F0} -> {regen.CurrentHp:F0}");

        // Frénétique : plus rapide et plus fragile que l'ordinaire.
        var frenzied = Make(EliteAffix.Frenzied);
        Check("elite Frenetique : marque l'ennemi comme elite", frenzied.IsElite);
        Check("elite : l'affixe est conserve", frenzied.Affix == EliteAffix.Frenzied);

        // Explosif : blesse le joueur à la mort s'il est à portée.
        var player = Player.Instance;
        if (player != null)
        {
            // Isoler le joueur : l'explosion passe par TakeDamage, donc respecte les i-frames.
            // Tant que la nuée le frappe, le test mesurerait l'absorption et non l'affixe — on vide
            // donc l'arène, ce qu'aucun autre contrôle ne fait car ils veulent justement la nuée.
            foreach (var e in EnemyBase.Active.ToArray())
                if (e != null) Destroy(e.gameObject);
            yield return null;

            player.transform.position = new Vector3(900f, 550f, 0f);
            player.HealFlat(player.Stats.MaxHp);
            yield return new WaitForSeconds(Player.InvulnWindow + 0.15f);

            // Placé ENTRE le rayon de contact (24) et le rayon d'explosion (84) : posé sur le
            // joueur, il le frapperait d'abord au contact, ce qui déclencherait les i-frames et
            // ferait absorber l'explosion — on mesurerait alors l'inverse de ce qu'on veut.
            var explosive = Make(EliteAffix.Explosive, hp: 10f, dmg: 20f);
            explosive.transform.position = player.transform.position + new Vector3(50f, 0f, 0f);
            yield return null;

            float hpBefore = player.Stats.CurrentHp;
            explosive.TakeDamage(100000f);   // mort immédiate → explosion
            yield return null;

            Check("elite Explosif : blesse a la mort", player.Stats.CurrentHp < hpBefore,
                  $"{hpBefore:F0} -> {player.Stats.CurrentHp:F0}");
        }

        foreach (var e in new[] { plain, armored, regen, frenzied })
            if (e != null) Destroy(e.gameObject);

        yield return null;
    }

    /// <summary>
    /// Vérifie le <b>Noyau Rouillé</b> : phases, incarnations, irréversibilité, renforts.
    ///
    /// <para>Le boss est la <b>condition de victoire des cinq niveaux</b> : une bascule de phase qui
    /// ne se déclenche pas, ou qui recule, casserait la progression de tout le jeu sans produire la
    /// moindre erreur.</para>
    /// </summary>
    private IEnumerator RunBossChecks(GameObject enemyPrefab)
    {
        // Arène vide : on mesure le boss, pas la nuée.
        foreach (var e in EnemyBase.Active.ToArray()) if (e != null) Destroy(e.gameObject);
        yield return null;

        var go = new GameObject("RustedCore", typeof(SpriteRenderer), typeof(RustedCore));
        go.transform.position = new Vector3(-800f, -500f, 0f);
        var boss = go.GetComponent<RustedCore>();
        boss.AddPrefab = enemyPrefab;
        boss.ApplyScaling(1000f, 5f);

        // Une incarnation par biome, avec sa propre signature.
        boss.SetBiome("givre");
        Check("boss : incarnation choisie par le biome", boss.Incarnation.BiomeId == "givre",
              $"{boss.Incarnation.Id} / {boss.Incarnation.Signature}");

        boss.SetBiome("fournaise");
        Check("boss : les incarnations different", boss.Incarnation.Signature == BossSignature.MagmaPools,
              boss.Incarnation.Signature.ToString());

        // Un biome inconnu doit retomber sur la souche plutôt que d'échouer.
        boss.SetBiome("biome_inexistant");
        Check("boss : biome inconnu retombe sur la souche",
              boss.Incarnation.Id == BossIncarnations.Root.Id, boss.Incarnation.Id);

        boss.SetBiome("givre");
        yield return null;

        Check("boss : demarre en phase I", boss.Phase == 0, $"phase={boss.Phase}");

        // Bascule sous 66 % : la phase doit avancer et la surcharge se déclencher.
        boss.TakeDamage(400f);
        yield return null;
        Check("boss : bascule en phase II sous 66 % de PV", boss.Phase == 1,
              $"phase={boss.Phase}, ratio={boss.HpRatio:F2}");

        // Irréversibilité : se soigner ne doit PAS faire reculer la phase, sinon un combat long
        // oscillerait autour du seuil et rejouerait la surcharge en boucle.
        boss.ApplyScaling(1000f, 5f);   // PV remis au maximum
        yield return new WaitForSeconds(BossPhases.TransitionSeconds + 0.2f);
        Check("boss : la progression de phase est irreversible", boss.Phase >= 1,
              $"phase={boss.Phase} apres retour a PV pleins");

        // Phase III : invocation de renforts.
        boss.TakeDamage(900f);
        yield return new WaitForSeconds(BossPhases.TransitionSeconds + 0.3f);
        Check("boss : atteint la phase III", boss.Phase == 2, $"phase={boss.Phase}");

        yield return new WaitForSeconds(0.5f);

        // On compte les VAGUES et non les ennemis présents : les armes du joueur, encore actives,
        // tuent les renforts aussi vite qu'ils arrivent, et un solde net serait donc négatif alors
        // que l'invocation fonctionne parfaitement.
        Check("boss : invoque des renforts en phase III", boss.AddWaves > 0,
              $"{boss.AddWaves} vague(s) de {BossPhases.AddsPerWave}");

        Check("boss : tire sa signature", boss.SignatureCount > 0, $"{boss.SignatureCount} tirs");

        if (boss != null) Destroy(boss.gameObject);
        foreach (var e in EnemyBase.Active.ToArray()) if (e != null) Destroy(e.gameObject);
        yield return null;
    }

    /// <summary>
    /// Vérifie la <b>file de modales</b> et l'écran de montée de niveau.
    ///
    /// <para>C'est ici que se loge le pire risque de l'interface : une modale qui ne s'ouvre pas, ou
    /// une pause qui n'est jamais levée, laisse le jeu <b>définitivement figé</b> — sans erreur,
    /// sans message, et sans que le joueur puisse rien faire.</para>
    /// </summary>
    private IEnumerator RunModalChecks()
    {
        ModalQueue.Reset();
        Check("modales : etat initial propre", !ModalQueue.IsOpen && !SceneRoot.Paused);

        // Deux demandes dans la même frame : une seule doit s'ouvrir, l'autre attendre.
        ModalQueue.Request(ModalKind.Assimilation);
        ModalQueue.Request(ModalKind.LevelUp);

        // L'ouverture est reportee a la fin de frame, pour que la priorite s'applique aux demandes
        // de la MEME frame et non a la premiere arrivee.
        yield return null;

        Check("modales : une seule ouverte a la fois", ModalQueue.PendingCount == 1,
              $"ouverte={ModalQueue.Current}, en attente={ModalQueue.PendingCount}");

        // La montée de niveau passe devant : elle interrompt la run à un instant précis.
        Check("modales : la montee de niveau est prioritaire",
              ModalQueue.Current == ModalKind.LevelUp, ModalQueue.Current?.ToString() ?? "aucune");

        Check("modales : le jeu est en pause", SceneRoot.Paused);

        // Une demande en double ne doit pas empiler deux écrans identiques.
        int before = ModalQueue.PendingCount;
        ModalQueue.Request(ModalKind.LevelUp);
        ModalQueue.Request(ModalKind.Assimilation);
        Check("modales : pas de doublon en file", ModalQueue.PendingCount == before,
              $"{ModalQueue.PendingCount} en attente");

        // Fermeture : la suivante prend le relais, la pause est maintenue.
        ModalQueue.Close(ModalKind.LevelUp);
        yield return null;
        Check("modales : la suivante prend le relais",
              ModalQueue.Current == ModalKind.Assimilation, ModalQueue.Current?.ToString() ?? "aucune");
        Check("modales : la pause tient entre deux modales", SceneRoot.Paused);

        // Dernière fermeture : la pause DOIT être levée, sinon le jeu reste figé pour toujours.
        ModalQueue.Close(ModalKind.Assimilation);
        yield return null;
        Check("modales : la pause est levee a la fin", !SceneRoot.Paused && !ModalQueue.IsOpen);

        // L'écran lui-même : construction, présentation, choix.
        var screenGo = new GameObject("LevelUpScreen");
        var screen = screenGo.AddComponent<LevelUpScreen>();
        yield return null;

        var cards = LevelUpPool.BuildOverload();
        LevelUpCard? chosen = null;
        screen.CardChosen += c => chosen = c;

        screen.Present(cards);
        yield return null;   // laisse la file s'ouvrir en fin de frame
        yield return null;

        Check("ecran de niveau : s'affiche a la demande", screen.IsVisible);
        Check("ecran de niveau : propose les cartes fournies", screen.Cards.Count == cards.Count,
              $"{screen.Cards.Count} cartes");

        // Un clic simulé sur la première carte.
        var button = screenGo.GetComponentInChildren<UnityEngine.UI.Button>();
        Check("ecran de niveau : les cartes sont cliquables", button != null);
        if (button != null)
        {
            button.onClick.Invoke();
            yield return null;

            Check("ecran de niveau : le choix est remonte", chosen.HasValue,
                  chosen?.Id ?? "aucun");
            Check("ecran de niveau : se ferme et leve la pause",
                  !screen.IsVisible && !SceneRoot.Paused);
        }

        Destroy(screenGo);
        ModalQueue.Reset();
        yield return null;

        yield return RunLevelUpActionChecks();
    }

    /// <summary>
    /// Renouveler / Passer : deux améliorations du Hub qui n'existaient pas dans le portage.
    ///
    /// <para>Ce qui se vérifie ici n'est pas qu'elles marchent, mais qu'elles <b>apparaissent au bon
    /// moment</b> : achetées elles existent, non achetées elles sont absentes — pas grisées. Un
    /// bouton grisé qu'on ne peut jamais activer se lit comme une fonction cassée.</para>
    /// </summary>
    private IEnumerator RunLevelUpActionChecks()
    {
        // ─── Rien d'acheté : aucun bouton d'action ────────────────────────────
        var bareGo = new GameObject("LevelUpSansAchat");
        var bare = bareGo.AddComponent<LevelUpScreen>();
        yield return null;

        bare.Charges = new LevelUpCharges(0, 0);
        bare.Present(LevelUpPool.BuildOverload());
        yield return null; yield return null;

        Check("niveau : sans achat, aucun bouton Renouveler ni Passer",
              FindLabelled(bareGo, "Renouveler") == null && FindLabelled(bareGo, "Passer") == null);

        Destroy(bareGo);
        ModalQueue.Reset();
        yield return null;

        // ─── Les deux achetés : les deux boutons, avec leur décompte ──────────
        var go = new GameObject("LevelUpAvecAchats");
        var screen = go.AddComponent<LevelUpScreen>();
        yield return null;

        screen.Charges = new LevelUpCharges(rerollLevel: 3, skipLevel: 2);

        bool skipped = false, rerolled = false;
        screen.Skipped += () => skipped = true;
        screen.RerollRequested += () => rerolled = true;

        screen.Present(LevelUpPool.BuildOverload());
        yield return null; yield return null;

        var reroll = FindLabelled(go, "Renouveler");
        var skip = FindLabelled(go, "Passer");

        Check("niveau : achetes, les deux boutons existent", reroll != null && skip != null,
              $"renouveler={(reroll != null ? "present" : "ABSENT")}, passer={(skip != null ? "present" : "ABSENT")}");

        if (reroll != null)
        {
            reroll.onClick.Invoke();
            yield return null;

            Check("niveau : renouveler redemande une main sans fermer l'ecran",
                  rerolled && screen.IsVisible && screen.Charges.RerollsLeft == 2,
                  $"{screen.Charges.RerollsLeft} renouvellement(s) restant(s)");
        }

        if (skip != null)
        {
            skip.onClick.Invoke();
            yield return null;

            Check("niveau : passer ferme l'ecran et leve la pause",
                  skipped && !screen.IsVisible && !SceneRoot.Paused);

            Check("niveau : passer consomme une charge", screen.Charges.SkipsLeft == 1,
                  $"{screen.Charges.SkipsLeft} passage(s) restant(s)");
        }

        Destroy(go);
        ModalQueue.Reset();
        yield return null;
    }

    /// <summary>Premier bouton actif dont le libellé commence par <paramref name="prefix"/>.</summary>
    private static UnityEngine.UI.Button? FindLabelled(GameObject root, string prefix)
    {
        foreach (var b in root.GetComponentsInChildren<UnityEngine.UI.Button>(includeInactive: false))
        {
            var label = b.GetComponentInChildren<UnityEngine.UI.Text>();
            if (label != null && label.text.StartsWith(prefix, System.StringComparison.Ordinal)) return b;
        }
        return null;
    }

    /// <summary>
    /// Vérifie l'écran de pause et le bilan de fin de run — chacun sur SON piège documenté.
    /// </summary>
    private IEnumerator RunScreenChecks()
    {
        // ─── Pause ────────────────────────────────────────────────────────────
        var pauseGo = new GameObject("PauseScreen");
        var pause = pauseGo.AddComponent<PauseScreen>();
        yield return null;

        Check("pause : masquee au demarrage", !pause.IsVisible && !SceneRoot.Paused);

        pause.Open("Contenu de test");
        yield return null;
        Check("pause : ouvre et fige le jeu", pause.IsVisible && SceneRoot.Paused);

        // Le piege historique : avec un contenu tres long, les boutons doivent RESTER atteignables.
        // Ils vivent hors de la zone de defilement, donc leur nombre ne bouge pas.
        var longBody = string.Join(System.Environment.NewLine,
            System.Linq.Enumerable.Repeat("arme niveau 20 — passif — greffe", 80));
        pause.Open(longBody);
        yield return null;

        // Reprendre · Options · Quitter — les trois du jeu publié. Le compte est vérifié parce que
        // c'est ce qui a manqué : « Options » avait disparu du portage sans que rien ne le signale.
        var pauseButtons = pauseGo.GetComponentsInChildren<UnityEngine.UI.Button>(true);
        Check("pause : les boutons survivent a un contenu tres long", pauseButtons.Length == 3,
              $"{pauseButtons.Length} boutons");

        // Un contenu trop long pour le cadre ne sert à rien s'il ne peut pas défiler à la molette.
        CheckScrollWheel(pauseGo, "pause");

        pause.Resume();
        yield return null;
        Check("pause : la reprise leve la pause", !pause.IsVisible && !SceneRoot.Paused);

        Destroy(pauseGo);

        // ─── Fin de run ───────────────────────────────────────────────────────
        var endGo = new GameObject("RunEndScreen");
        var end = endGo.AddComponent<RunEndScreen>();
        yield return null;

        end.Show(victory: false, runSeconds: 600, kills: 320, cores: 7);
        yield return null;

        Check("fin de run : s'affiche", end.IsVisible);
        Check("fin de run : des Echos sont gagnes", end.EchoesEarned > 0, $"{end.EchoesEarned}");

        // LE piege : la somme animee doit atterrir EXACTEMENT sur le total credite. Sous Godot,
        // les deux venaient de calculs differents et divergeaient des qu'un multiplicateur entrait
        // en jeu — le joueur voyait un chiffre et en recevait un autre.
        yield return new WaitForSecondsRealtime(1.2f);
        Check("fin de run : la somme animee egale le total credite",
              end.DisplayedEchoes == end.EchoesEarned,
              $"affiche={end.DisplayedEchoes}, credite={end.EchoesEarned}");

        // Meme exigence avec un multiplicateur de palier, la ou la divergence apparaissait.
        end.Show(victory: true, runSeconds: 600, kills: 320, cores: 7, tierMult: 1.6);
        end.SkipAnimation();
        Check("fin de run : egalite maintenue avec un multiplicateur de palier",
              end.DisplayedEchoes == end.EchoesEarned,
              $"affiche={end.DisplayedEchoes}, credite={end.EchoesEarned}");

        Destroy(endGo);
        yield return null;
    }

    /// <summary>
    /// Arme <b>dirigée</b> : elle doit tirer là où le joueur vise, traverser, et gagner son éventail.
    ///
    /// <para>Les trois défauts corrigés étaient invisibles au code et aux relevés : la lance tirait
    /// bien, à la bonne cadence, avec les bons dégâts. Elle tirait simplement dans la direction de
    /// <i>déplacement</i>, sans perforer, et sans jamais appliquer les paliers 4-5 de ses données.
    /// C'est exactement le genre d'écart qu'un compteur de tirs ne peut pas voir.</para>
    /// </summary>
    private IEnumerator RunDirectedWeaponChecks(GameObject bulletPrefab)
    {
        var host = new GameObject("LanceHost");
        host.transform.position = Vector3.zero;

        var lance = host.AddComponent<VectorLance>();
        lance.BulletPrefab = bulletPrefab;
        yield return null;

        // Palier 5 des données : 3 projectiles, éventail de 20°, perforant.
        var stats = new WeaponTable.WeaponLevelStats(5, 40f, 0.55f, 3, 620f, true, 20f);
        lance.ApplyLevelStats(stats);

        Check("lance vectorielle : le palier de niveau est applique",
              lance.ProjectileCount == 3 && lance.IsPiercing && Mathf.Approximately(lance.SpreadDegrees, 20f),
              $"{lance.ProjectileCount} projectiles, eventail {lance.SpreadDegrees:F0}, perforant={lance.IsPiercing}");

        // Visée imposée vers le HAUT, sans aucun ennemi : une arme dirigée tire dans le vide.
        var player = Player.Instance;
        if (player != null)
        {
            player.transform.position = Vector3.zero;
            player.ForceAim(Vector2.up);
        }

        int bulletsBefore = FindObjectsByType<Bullet>(FindObjectsSortMode.None).Length;
        yield return new WaitForSeconds(1.0f);

        var bullets = FindObjectsByType<Bullet>(FindObjectsSortMode.None);

        Check("lance vectorielle : elle tire sans cible", lance.LastShots > 0,
              $"{lance.LastShots} tir(s), {bullets.Length - bulletsBefore} projectile(s)");

        Check("lance vectorielle : l'eventail part en trois traits", lance.LastVolleySize == 3,
              $"{lance.LastVolleySize} projectiles au dernier tir");

        // Le cœur du signalement : la direction. Au moins un projectile doit filer vers le haut,
        // c'est-à-dire là où l'on vise — et non là où l'on court.
        bool aimed = false, pierces = false;
        foreach (var b in bullets)
        {
            if (b == null) continue;
            if (b.Piercing) pierces = true;
            if (b.transform.position.y > 4f) aimed = true;
        }

        Check("lance vectorielle : elle tire dans la direction VISEE", aimed,
              aimed ? "projectiles partis vers la visee" : "aucun projectile dans l'axe de visee");

        Check("lance vectorielle : le trait perfore", pierces);

        Destroy(host);
        foreach (var b in bullets) if (b != null) Destroy(b.gameObject);

        // Les drones ne doivent plus être des carrés blancs de dépannage.
        Check("essaim de drones : silhouette dediee, pas la primitive blanche",
              DroneSprite.Get() != UiPrimitives.White && DroneSprite.Get().rect.width >= 8f,
              $"{DroneSprite.Get().rect.width:F0} px");

        // ─── La lame boomerang est une CROIX, et elle tourne ──────────────────
        // Elle empruntait le sprite d'un tir de sentinelle ENNEMIE, teinté violet et immobile —
        // la forme et la couleur d'un projectile hostile, pour l'arme que l'icône, le Codex et
        // l'écran de montée de niveau montrent tous comme une croix cyan tournoyante.
        // ⚠ Lancé LOIN du joueur et armé avant la première image : sans `Launch`, sa portée vaut
        // zéro, il se croit déjà en phase de retour dès la première image, se trouve à moins de
        // 24 px de son point d'arrivée et se détruit. La vérification tomberait alors sur un objet
        // mort — et se lirait « la lame ne tourne pas ».
        var blade = new GameObject("GlaiveTemoin", typeof(SpriteRenderer));
        blade.transform.position = new Vector3(400f, 400f, 0f);

        var projectile = blade.AddComponent<GlaiveProjectile>();
        projectile.Launch(Vector2.right, 1f, 4000f);
        yield return null;

        var bladeRenderer = blade.GetComponent<SpriteRenderer>();

        Check("lame boomerang : silhouette dediee en croix",
              bladeRenderer.sprite == GlaiveSprite.Get()
              && GlaiveSprite.Get().rect.width >= projectile.HitRadius * 1.8f,
              $"{GlaiveSprite.Get().rect.width:F0} px pour un rayon de frappe de {projectile.HitRadius:F0}");

        // Le halo est ce qui la rend suivable dans une nuée — or c'est l'arme dont la trajectoire
        // compte, puisqu'elle revient.
        Check("lame boomerang : elle porte un halo",
              blade.GetComponentsInChildren<SpriteRenderer>().Length >= 2);

        // Le tournoiement se COMPTE : il distingue une lame lancée d'un tir, et annonce le retour.
        // Fenêtre courte à dessein — au-delà d'un demi-tour, `DeltaAngle` se replie et l'écart
        // mesuré redescendrait.
        float spinBefore = blade.transform.eulerAngles.z;
        yield return new WaitForSeconds(0.2f);

        float turned = Mathf.Abs(Mathf.DeltaAngle(spinBefore, blade.transform.eulerAngles.z));

        Check("lame boomerang : elle tournoie", turned > 20f,
              $"{turned:F0} deg parcourus en 0,2 s");

        Destroy(blade);
        yield return null;
    }

    /// <summary>
    /// Gel et brûlure : deux états qui <b>fonctionnaient sans se voir</b>. Le banc ne peut pas juger
    /// leur lisibilité — seul l'œil le peut — mais il peut vérifier que les signaux existent, ce qui
    /// est précisément ce qui manquait.
    /// </summary>
    private IEnumerator RunStatusFxChecks(GameObject enemyPrefab)
    {
        var go = Instantiate(enemyPrefab, new Vector3(200f, 0f, 0f), Quaternion.identity);
        go.SetActive(true);

        var enemy = go.GetComponent<EnemyBase>();
        enemy.ApplyScaling(100000f, 0f);   // il doit survivre à sa propre brûlure
        enemy.Speed = 0f;

        enemy.ApplySlow(0.5f, 4f);
        enemy.ApplyBurn(5f, 4f);
        yield return null;

        var fx = go.GetComponent<EnemyStatusFx>();

        Check("etats : le composant d'apparence est pose au premier etat subi", fx != null);

        if (fx != null)
        {
            Check("etats : un ennemi gele porte un calque de givre", fx.FrostVisible);

            Check("etats : un ennemi gele porte des cristaux", fx.ShardsVisible);

            // Le signal qui manquait le plus : un sprite qui s'agite à pleine cadence en avançant au
            // ralenti se lit « il glisse ». La cadence est aussi la SEULE part du gel qu'un banc
            // puisse constater — le reste est en pixels.
            // ⚠ L'attendu passe par CrowdControlCaps, il n'est PAS le 0,5 demandé plus haut : le
            // ralentissement est plafonné à 0,60, et la cadence suit le ralentissement RÉEL. Ce
            // contrôle a échoué le jour où le plafond a enfin été branché — il encodait, sans le
            // dire, l'absence de garde-fou.
            float expected = CrowdControlCaps.CapSlowMult(0.5f);
            Check("etats : le gel ralentit l'animation de sa victime",
                  Mathf.Approximately(fx.CadenceScale, expected),
                  $"cadence x{fx.CadenceScale:F2} pour un ralentissement plafonne a x{expected:F2}");

            Check("etats : un ennemi qui brule porte des flammes", fx.FlamesVisible);

            // ⚠ La seule chose qu'un banc puisse dire d'un effet, c'est son EMPRISE. « Est-ce
            // subtil ? » ne se juge qu'à l'œil — mais « les flammes débordent-elles de la
            // silhouette ? » est un nombre, et c'est justement ce qui avait dérapé : chaque langue
            // de feu valait 18 fois le sprite de 16 px qui la portait, soit près de 290 px sur un
            // corps de 32, et le joueur lisait une explosion permanente.
            // ⚠ En headless, aucune image d'animation n'est posée et le renderer annonce des
            // dimensions nulles — d'où la même référence de secours que le composant, le rayon de
            // contact. S'aligner sur `bounds` seul aurait fait échouer la vérification pour une
            // raison sans rapport avec ce qu'elle mesure.
            var body = go.GetComponentInChildren<SpriteRenderer>();
            float bodyWidth = body != null && body.bounds.size.x > 1f
                ? body.bounds.size.x
                : enemy.PushRadius * 2f;

            Check("etats : les flammes tiennent dans la silhouette",
                  bodyWidth > 1f && fx.FlameSpanPx <= bodyWidth * 1.3f,
                  $"{fx.FlameSpanPx:F0} px de flammes pour un corps de {bodyWidth:F0} px");

            // Même mesure pour les cristaux, et pour la même raison : un effet porté qui déborde de
            // sa victime la remplace au lieu de la qualifier.
            Check("etats : les cristaux tiennent dans la silhouette",
                  bodyWidth > 1f && fx.ShardSpanPx <= bodyWidth * 1.3f,
                  $"{fx.ShardSpanPx:F0} px de givre pour un corps de {bodyWidth:F0} px");

            // La traînée ne se sème que si la cible AVANCE — un ennemi immobile ne doit rien laisser.
            yield return new WaitForSeconds(0.6f);
            Check("etats : immobile, le gel ne laisse aucune trainee", fx.FrostShardsDropped == 0,
                  $"{fx.FrostShardsDropped} eclat(s)");

            // La fumée, elle, ne demande PAS que la cible bouge : c'est le signal « ça brûle
            // encore », et les cibles qui portent un état assez longtemps sont les plus lentes.
            Check("etats : un ennemi qui brule fume, meme immobile", fx.SmokePuffsEmitted > 0,
                  $"{fx.SmokePuffsEmitted} bouffee(s)");

            // La vapeur froide joue le même rôle pour le gel, et suit donc la même règle.
            Check("etats : un ennemi gele exhale une vapeur, meme immobile", fx.FrostVaporEmitted > 0,
                  $"{fx.FrostVaporEmitted} bouffee(s)");

            Check("etats : la prise de gel est signalee une fois, pas a chaque tir",
                  fx.FreezeSnaps == 1, $"{fx.FreezeSnaps} gerbe(s) pour un seul gel");

            // En mouvement, il en sème.
            for (int i = 0; i < 30; i++)
            {
                go.transform.position += new Vector3(14f, 0f, 0f);
                yield return null;
            }

            yield return new WaitForSeconds(0.5f);
            Check("etats : en mouvement, le gel laisse une trainee", fx.FrostShardsDropped > 0,
                  $"{fx.FrostShardsDropped} eclat(s)");

            yield return RunFrostStrengthChecks(enemyPrefab, fx.FrostLevel);

            // Le gel doit RELACHER sa victime : une cadence jamais rendue serait un ralentissement
            // permanent, c'est-à-dire un bug de gameplay déguisé en effet visuel.
            //
            // ⚠ On attend l'ETAT, pas une durée. Une attente fixe est fragile ici : le banc tourne
            // bien plus vite que le temps réel, et les vérifications précédentes consomment un nombre
            // d'images qu'on ne connaît pas d'avance — 2,6 s calculées sur un pas de 20 ms tombaient
            // 0,15 s avant l'expiration, et l'échec se lisait « le gel ne relâche jamais ».
            for (float t = 0f; t < 6f && enemy.IsSlowed; t += Time.deltaTime) yield return null;

            // Puis le temps de la fonte, qui n'est pas instantanée (c'est le point).
            yield return new WaitForSeconds(0.5f);

            Check("etats : la cadence est rendue quand le gel expire",
                  Mathf.Approximately(fx.CadenceScale, 1f), $"cadence x{fx.CadenceScale:F2}");

            Check("etats : le givre fond au lieu de s'eteindre", !fx.FrostVisible && !fx.ShardsVisible,
                  $"givre {fx.FrostLevel:F2}");
        }

        if (go != null) Destroy(go);
        yield return null;
    }

    /// <summary>
    /// Le ralenti de coup décisif doit se <b>rendre</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ C'est le défaut exact signalé en jouant : « le ralenti reste actif après la mort du boss au
    /// lieu de revenir à la vitesse normale ». Un effet qui écrit <c>Time.timeScale</c> et ne le
    /// restaure pas ne casse rien, ne lève rien, et rend le jeu injouable pour le reste de la
    /// session. Il ne peut être attrapé que par une vérification qui regarde la valeur <i>après</i>.
    /// </remarks>
    private IEnumerator RunHitStopChecks()
    {
        HitStop.Reset();
        float nominal = SceneRoot.ResumeScale;

        HitStop.Trigger();
        yield return null;

        Check("ralenti : il ralentit vraiment le jeu", Time.timeScale < nominal * 0.5f,
              $"timeScale {Time.timeScale:F2} pour un nominal de {nominal:F2}");

        // On attend en temps RÉEL : attendre en temps de jeu pendant qu'on ralentit le jeu
        // multiplierait l'attente par l'inverse du ralenti — l'erreur qui rend ce genre d'effet
        // impossible à borner.
        yield return new WaitForSecondsRealtime(1.2f);

        Check("ralenti : la vitesse normale est RENDUE", !HitStop.Active
              && Mathf.Approximately(Time.timeScale, nominal),
              $"timeScale {Time.timeScale:F2}");

        // Et il se rend aussi quand on l'interrompt — un changement de scène pendant l'effet ne doit
        // pas laisser un menu au ralenti.
        HitStop.Trigger();
        yield return null;
        HitStop.Reset();

        Check("ralenti : interrompu, il rend la vitesse immediatement",
              Mathf.Approximately(Time.timeScale, nominal), $"timeScale {Time.timeScale:F2}");

        yield return null;
    }

    /// <summary>
    /// Le gel doit dire sa <b>force</b>.
    /// </summary>
    /// <remarks>
    /// ⚠ C'est la régression exacte que ce lot corrige : la teinte était binaire, si bien qu'une
    /// Lance Cryogénique (−20 %) et un Voile de Givre (−45 %) produisaient la même image. Un test qui
    /// se contenterait de « l'ennemi est bleu » repasserait au vert le jour où l'on y reviendrait.
    /// </remarks>
    private IEnumerator RunFrostStrengthChecks(GameObject enemyPrefab, float strongFrost)
    {
        var go = Instantiate(enemyPrefab, new Vector3(-200f, 0f, 0f), Quaternion.identity);
        go.SetActive(true);

        var enemy = go.GetComponent<EnemyBase>();
        enemy.ApplyScaling(100000f, 0f);
        enemy.Speed = 0f;
        enemy.ApplySlow(0.8f, 3f);           // le ralentissement le plus faible du jeu
        yield return null;

        var fx = go.GetComponent<EnemyStatusFx>();

        if (fx != null)
        {
            Check("etats : un gel faible se voit MOINS qu'un gel fort",
                  fx.FrostLevel < strongFrost - 0.05f,
                  $"givre {fx.FrostLevel:F2} (x0,80) contre {strongFrost:F2} (x0,50)");

            // …mais il se voit quand même : la Lance touche peu de cibles à la fois, un effet dosé
            // « à proportion » y serait purement et simplement invisible.
            Check("etats : un gel faible reste visible", fx.FrostVisible && fx.FrostLevel > 0.4f,
                  $"givre {fx.FrostLevel:F2}");
        }

        if (go != null) Destroy(go);
        yield return null;
    }

    /// <summary>Injecte les prefabs de projectile attendus par chaque famille d'arme.</summary>
    private static void InjectPrefabs(WeaponBase w, GameObject bullet, GameObject missile, GameObject glaive)
    {
        switch (w)
        {
            case ImpulseCannon c:  c.BulletPrefab = bullet; break;
            case ScatterVolley s:  s.BulletPrefab = bullet; break;
            case VectorLance v:    v.BulletPrefab = bullet; break;
            case SeekerSwarm k:    k.MissilePrefab = missile; break;
            case Glaive g:         g.GlaivePrefab = glaive; break;
        }
    }

    /// <summary>
    /// Vérifie que chaque <b>archétype</b> d'arme frappe réellement. Chacun a une géométrie de visée
    /// différente — arc orienté, chaîne de rebonds, aura radiale, orbite, éventail — et c'est cette
    /// géométrie, pas la boucle de tir, qui casse silencieusement lors d'un portage.
    /// </summary>
    private IEnumerator RunArchetypeChecks(GameObject enemyPrefab, GameObject bulletPrefab)
    {
        var host = new GameObject("ArchetypeHost");
        host.transform.position = Vector3.zero;

        // Trois cibles serrées autour de l'origine : dans l'arc, dans l'aura, et à portée de chaîne.
        var targets = new List<EnemyBase>();
        foreach (var offset in new[] { new Vector3(40f, 0f), new Vector3(0f, 40f), new Vector3(60f, 30f) })
        {
            var go = Instantiate(enemyPrefab, offset, Quaternion.identity);
            go.SetActive(true);
            var e = go.GetComponent<EnemyBase>();
            e.ApplyScaling(100000f, 0f);   // encaissent sans mourir : on mesure la portée, pas les dégâts
            e.Speed = 0f;                  // immobiles : la géométrie doit être déterministe
            targets.Add(e);
        }
        yield return null;

        var blade  = host.AddComponent<PlasmaBlade>();
        var coil   = host.AddComponent<TeslaCoil>();
        var field  = host.AddComponent<OverloadField>();
        var volley = host.AddComponent<ScatterVolley>();
        volley.BulletPrefab = bulletPrefab;
        var swarm  = host.AddComponent<DroneSwarm>();

        // Assez de temps pour que chaque arme franchisse sa recharge au moins une fois.
        yield return new WaitForSeconds(3.0f);

        Check("archetype arc : la lame touche dans son arc", blade.LastSweepHits > 0,
              $"{blade.LastSweepHits} touches");
        Check("archetype chaine : la bobine rebondit", coil.LastChainLength > 1,
              $"chaine de {coil.LastChainLength}");
        Check("archetype aura : l'impulsion touche autour du porteur", field.LastPulseHits > 0,
              $"{field.LastPulseHits} touches");
        Check("archetype salve : plusieurs projectiles partent", volley.LastVolleySize > 1,
              $"{volley.LastVolleySize} projectiles");
        Check("archetype orbital : les drones existent et tournent", swarm.enabled);

        // La valeur de fiche doit être celle de la sous-classe, pas le défaut du socle : c'est
        // l'ordre d'appel de base.Awake() qui en décide.
        Check("armes : valeur de fiche capturee apres reglage de la sous-classe",
              Mathf.Approximately(blade.SheetDamage, 18f) && Mathf.Approximately(coil.SheetDamage, 14f),
              $"lame={blade.SheetDamage}, bobine={coil.SheetDamage}");

        foreach (var e in targets) if (e != null) Destroy(e.gameObject);
        Destroy(host);
        yield return null;
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

    /// <summary>
    /// Vérifie la boucle de progression <b>de bout en bout</b> : le choix de niveau propose de vraies
    /// cartes, et chaque nature de carte produit un effet observable.
    ///
    /// <para>C'est le mode de défaillance le plus muet du jeu : tant que le choix n'est pas appliqué,
    /// l'écran s'ouvre, le joueur choisit, l'écran se ferme — et rien ne change. Aucune erreur, aucun
    /// symptôme, une run entière qui n'avance pas.</para>
    /// </summary>
    private IEnumerator RunProgressionChecks(GameObject playerGo)
    {
        var inv = InventorySystem.Instance;
        var player = Player.Instance;
        if (inv == null || player == null) { Check("progression : inventaire disponible", false); yield break; }

        inv.Mount = playerGo.transform;

        // ⚠ Une destruction Unity n'est effective qu'à la FIN de la frame : sans cette attente, la
        // fusion forgée juste avant laisse son arme source encore comptée, et la mesure « avant »
        // est fausse d'une unité — ce qui faisait échouer une vérification pourtant correcte.
        yield return null;

        // ─── Le pool réel ─────────────────────────────────────────────────────
        var passiveIds = new List<string>();
        foreach (string id in inv.AllPassiveIds)
            if (!inv.IsPassiveSaturated(id)) passiveIds.Add(id);

        var cards = LevelUpPool.Build(
            inv.WeaponLevels, inv.AllWeaponIds, 20,
            inv.PassiveLevels, passiveIds, 20,
            InventorySystem.MaxWeapons, inv.AvailableFusions,
            inv.RarityOf, _ => 0);

        Check("niveau : le choix propose bien trois cartes", cards.Count == LevelUpPool.CardsPerLevel,
              $"{cards.Count} cartes");

        bool onlyOverload = cards.Count > 0;
        foreach (var c in cards) if (c.Kind != LevelUpCardKind.Overload) onlyOverload = false;
        Check("niveau : le pool ordinaire n'est pas vide en debut de run", !onlyOverload,
              "sinon les cartes de surcharge masquent un pool qui ne se construit pas");

        // ─── Une carte d'arme fait vraiment apparaître l'arme ─────────────────
        int before = playerGo.GetComponentsInChildren<WeaponBase>().Length;
        int level = inv.AcquireOrLevelUp("tesla_coil");
        yield return null;
        int after = playerGo.GetComponentsInChildren<WeaponBase>().Length;

        Check("niveau : une arme acquise existe reellement sur le joueur",
              level == 1 && after == before + 1, $"{before} -> {after} armes portees");

        // La monter ne doit pas en créer une seconde : c'est la même arme qui progresse.
        inv.AcquireOrLevelUp("tesla_coil");
        yield return null;
        Check("niveau : monter une arme ne la duplique pas",
              playerGo.GetComponentsInChildren<WeaponBase>().Length == after,
              $"{playerGo.GetComponentsInChildren<WeaponBase>().Length} armes");

        // ─── Passifs ──────────────────────────────────────────────────────────
        float hpBefore = player.Stats.MaxHp, curBefore = player.Stats.CurrentHp;
        inv.AddOrUpgradePassive("reinforced_plating");
        float hpGain = player.Stats.MaxHp - hpBefore;

        Check("passif : la Plaque Renforcee donne des PV max", hpGain > 0f, $"+{hpGain:F0} PV max");
        Check("passif : elle soigne d'autant (sinon la barre grandit a vide)",
              player.Stats.CurrentHp - curBefore >= hpGain - 0.01f,
              $"+{player.Stats.CurrentHp - curBefore:F0} PV rendus pour +{hpGain:F0} max");

        // Le Capaciteur doit monter, puis se plafonner — c'est ce plafond qui empêche toutes les
        // armes de tomber à la même cadence (régression 1.22.0).
        for (int i = 0; i < 25; i++) inv.AddOrUpgradePassive("capacitor");
        Check("passif : la reduction de recharge se plafonne",
              player.Stats.CooldownReduction <= StatCaps.MaxCooldownReduction + 0.001f &&
              player.Stats.CooldownReduction > 0f,
              $"{player.Stats.CooldownReduction:F2} (plafond {StatCaps.MaxCooldownReduction:F2})");
        Check("passif : un passif au plafond est declare sature",
              inv.IsPassiveSaturated("capacitor"));

        // ─── Cartes de surcharge ──────────────────────────────────────────────
        float maxHpBefore = player.Stats.MaxHp;
        inv.ApplyOverload(OverloadCards.Plating.Id);
        inv.ApplyOverload(OverloadCards.Plating.Id);
        Check("surcharge : deux prises cumulent sans plafond",
              Mathf.Approximately(player.Stats.MaxHp - maxHpBefore, OverloadCards.Plating.Delta * 2f),
              $"+{player.Stats.MaxHp - maxHpBefore:F0} PV pour deux prises");

        float regenBefore = player.Stats.HpRegenPerSecond;
        inv.ApplyOverload(OverloadCards.Regen.Id);
        Check("surcharge : la regeneration monte",
              player.Stats.HpRegenPerSecond > regenBefore);

        yield return FuseStartingWeaponChecks(playerGo, inv);
    }

    /// <summary>
    /// Fusionne l'<b>arme de départ</b> — celle qui est un composant du joueur lui-même, et non un
    /// objet créé pour elle.
    ///
    /// <para>Deux façons de tout casser se cachent ici, et aucune ne se voit à la compilation :
    /// détruire l'objet de l'arme remplacée <b>supprimerait le joueur</b> ; et ne créer la fusion que
    /// lorsqu'un point de montage est fourni ferait <b>disparaître l'arme sans rien mettre à la
    /// place</b> — la carte la plus spectaculaire du jeu deviendrait une perte sèche.</para>
    /// </summary>
    private IEnumerator FuseStartingWeaponChecks(GameObject playerGo, InventorySystem inv)
    {
        // Reproduit le câblage de la scène : l'arme de départ vit SUR le joueur.
        var starting = playerGo.AddComponent<PlasmaBlade>();
        inv.Register("plasma_blade", starting);
        for (int i = 1; i < 5; i++) inv.AcquireOrLevelUp("plasma_blade");
        inv.AddOrUpgradePassive("thermal_core");

        Check("fusion : la fusion de l'arme de depart est deblocable", inv.CanFuse("fusion_blade"));

        int inherited = inv.ApplyFusion("fusion_blade");
        yield return null;

        Check("fusion : forgee depuis l'arme de depart", inherited > 0, $"niveau herite {inherited}");
        Check("fusion : le JOUEUR survit a la fusion de son arme de depart",
              playerGo != null && Player.Instance != null);

        bool fusionPresent = false;
        foreach (var w in playerGo.GetComponentsInChildren<WeaponBase>())
            if (w is FusionBlade) fusionPresent = true;

        Check("fusion : l'arme fusionnee existe reellement (pas une perte seche)", fusionPresent);
    }

    /// <summary>
    /// Vérifie l'arrivée du <b>boss de fin</b> : le décompte à zéro le fait apparaître, il n'apparaît
    /// jamais en double, et sa chute marque la complétion du niveau.
    ///
    /// <para>L'empilement était un défaut réel du jeu d'origine : un second Noyau toutes les 28-50 s
    /// avant la mort du premier rendait la mise à mort littéralement impossible.</para>
    /// </summary>
    private IEnumerator RunBossSpawnChecks(GameManager gm, EnemySpawner spawner)
    {
        // Attendre 13 minutes réelles rendrait cette partie du jeu invérifiable.
        gm.OverrideRunDuration(1);
        yield return null;

        Check("overtime : le decompte ecoule fait basculer la run", gm.Overtime,
              $"t={gm.RunTime:F0}s, impartis={gm.RunDurationSeconds}s");

        float waited = 0f;
        RustedCore? boss = null;
        while (waited < 8f && boss == null)
        {
            foreach (var e in EnemyBase.Active)
                if (e is RustedCore rc) { boss = rc; break; }

            waited += Time.deltaTime;
            yield return null;
        }

        Check("boss : le Noyau Rouille arrive en overtime", boss != null, $"apres {waited:F1} s");
        if (boss == null) yield break;

        Check("boss : incarnation choisie", !string.IsNullOrEmpty(boss.DisplayName), boss.DisplayName);

        // Son arrivée doit se voir : elle est un événement, pas un ajout silencieux à la nuée.
        float spawnDist = Player.Instance != null
            ? Vector2.Distance(boss.transform.position, Player.Instance.transform.position)
            : float.MaxValue;
        Check("boss : apparait dans le champ de vision", spawnDist <= 520f, $"{spawnDist:F0} px du joueur");

        // ⚠ LE critère manquant, et signalé en jouant : « le boss se voit mais n'approche pas ».
        // Le port l'avait figé sur place, alors que l'original avance à 46 px/s. Un boss immobile se
        // contourne et s'oublie — et rien, dans les phases ni les signatures, ne le signalait.
        if (Player.Instance != null)
        {
            Player.Instance.transform.position = (Vector2)boss.transform.position + new Vector2(600f, 0f);
            float before = Vector2.Distance(boss.transform.position, Player.Instance.transform.position);

            // Joueur immobile : c'est bien le boss qui doit combler l'écart.
            yield return new WaitForSeconds(2f);

            float after = Vector2.Distance(boss.transform.position, Player.Instance.transform.position);
            Check("boss : il AVANCE vers le joueur", after < before - 20f,
                  $"{before:F0} px -> {after:F0} px en 2 s");
        }

        // Un second Noyau ne doit pas s'ajouter tant que le premier vit.
        yield return new WaitForSeconds(3f);
        int alive = 0;
        foreach (var e in EnemyBase.Active) if (e is RustedCore) alive++;
        Check("boss : jamais deux Noyaux en meme temps", alive == 1, $"{alive} vivants");

        // Sa chute complète le niveau — sans terminer la run.
        boss.TakeDamage(boss.MaxHp * 10f);
        yield return null;
        Check("boss : sa chute marque la completion du niveau", gm.BossDefeated);
        Check("boss : la run continue apres sa mort", !gm.RunEnded);
    }

    /// <summary>
    /// Vérifie la <b>boucle de rétention</b> de bout en bout : une run rapporte des Échos, les Échos
    /// achètent une amélioration au Hub, et l'amélioration se retrouve dans les statistiques de la
    /// run suivante. Chaque maillon est inutile sans les deux autres.
    ///
    /// <para>⚠ Le banc écrit dans son propre dossier utilisateur (produit
    /// <c>ChimeraProtocolBench</c>) : il ne touche jamais la sauvegarde du joueur.</para>
    /// </summary>
    private IEnumerator RunMetaChecks()
    {
        Check("meta : le catalogue d'ameliorations se charge",
              MetaProgression.All.Count >= 14, $"{MetaProgression.All.Count} ameliorations");

        if (MetaProgression.All.Count == 0) yield break;

        // ⚠ Repartir d'un état CONNU. Sans cela, le banc rejoue sur sa propre sauvegarde accumulée :
        // au bout de quelques campagnes l'amélioration testée atteint son niveau maximum, et la
        // vérification échoue pour une raison qui n'a rien à voir avec ce qu'elle mesure.
        MetaProgression.HardReset();

        // ─── Gagner ───────────────────────────────────────────────────────────
        int before = MetaProgression.CurrentEchoes;
        MetaProgression.AddEchoes(50_000);
        Check("meta : les Echos gagnes sont credites",
              MetaProgression.CurrentEchoes == before + 50_000,
              $"{before} -> {MetaProgression.CurrentEchoes}");

        // ─── Dépenser ─────────────────────────────────────────────────────────
        const string upgradeId = "hp_boost";
        int levelBefore = MetaProgression.LevelOf(upgradeId);
        int cost = MetaProgression.NextCost(upgradeId);
        int walletBefore = MetaProgression.CurrentEchoes;

        bool bought = MetaProgression.TryPurchase(upgradeId);

        Check("meta : une amelioration s'achete", bought && MetaProgression.LevelOf(upgradeId) == levelBefore + 1,
              $"niveau {levelBefore} -> {MetaProgression.LevelOf(upgradeId)}, prix {cost}");
        Check("meta : l'achat debite exactement son prix",
              MetaProgression.CurrentEchoes == walletBefore - cost,
              $"{walletBefore} -> {MetaProgression.CurrentEchoes} pour {cost}");

        // ─── Sentir ───────────────────────────────────────────────────────────
        var stats = new PlayerStats();
        stats.ResetForRun();
        float baseHp = stats.MaxHp;
        MetaProgression.ApplyTo(stats);

        Check("meta : le bonus achete se retrouve dans les statistiques", stats.MaxHp > baseHp,
              $"{baseHp:F0} -> {stats.MaxHp:F0} PV max");
        Check("meta : les plafonds tiennent malgre le Hub",
              stats.CooldownReduction <= StatCaps.MaxCooldownReduction + 0.001f &&
              stats.DamageReduction <= StatCaps.MaxDamageReduction + 0.001f &&
              stats.Speed <= StatCaps.MaxSpeed + 0.001f);

        // ─── Persister ────────────────────────────────────────────────────────
        int expected = MetaProgression.CurrentEchoes;
        int expectedLevel = MetaProgression.LevelOf(upgradeId);
        MetaProgression.Persist();
        MetaProgression.Reset();   // force une relecture depuis le disque

        Check("meta : l'achat survit a un rechargement",
              MetaProgression.CurrentEchoes == expected && MetaProgression.LevelOf(upgradeId) == expectedLevel,
              $"{MetaProgression.CurrentEchoes} Echos, niveau {MetaProgression.LevelOf(upgradeId)}");

        // ─── Le Hub ───────────────────────────────────────────────────────────
        var hubGo = new GameObject("HubHost");
        var hub = hubGo.AddComponent<HubScreen>();
        yield return null;

        // Une ligne par amélioration, plus les deux sélecteurs de tête : le perk de départ et le
        // TITRE cosmétique. Sans ce dernier, un titre gagné par un défi ne pouvait pas être porté —
        // donc ne s'affichait nulle part, et la récompense n'existait que dans la sauvegarde.
        Check("hub : une ligne par amelioration, plus le perk et le titre",
              hub.RowCount == MetaProgression.All.Count + 2,
              $"{hub.RowCount} lignes pour {MetaProgression.All.Count} ameliorations + perk + titre");

        hub.Show();
        Check("hub : s'ouvre", hub.IsVisible);
        CheckScrollWheel(hubGo, "hub");
        hub.Hide();
        Check("hub : se ferme", !hub.IsVisible);

        Destroy(hubGo);
        yield return null;

        yield return RunLevelSelectChecks();
        yield return RunChallengeChecks();
    }

    /// <summary>
    /// Vérifie l'<b>Assimilation</b> : les éliminations remplissent la bonne jauge, la greffe se
    /// propose au seuil, s'équipe, et son effet <b>agit</b>.
    ///
    /// <para>C'est le seul axe qui transforme le personnage en cours de run. Une greffe équipée sans
    /// effet serait le même défaut muet que les armes invisibles — sauf qu'ici le joueur a payé une
    /// jauge entière pour l'obtenir.</para>
    /// </summary>
    private IEnumerator RunAssimilationChecks(GameObject enemyPrefab)
    {
        Assimilation.Reset();

        Check("assimilation : les greffes se chargent", Assimilation.Config.Grafts.Count > 0,
              $"{Assimilation.Config.Grafts.Count} greffes, {Assimilation.SlotCount} emplacements");
        if (Assimilation.Config.Grafts.Count == 0) yield break;

        // ─── Routage des éliminations ─────────────────────────────────────────
        var swarmGraft = Assimilation.Config.GraftForGauge("swarm");
        Check("assimilation : chaque jauge a sa greffe", swarmGraft != null);
        if (swarmGraft == null) yield break;

        string filled = "";
        void OnFilled(string gauge) => filled = gauge;
        Assimilation.GaugeFilled += OnFilled;

        int threshold = Assimilation.ThresholdOf("swarm");
        for (int i = 0; i < threshold; i++)
            Assimilation.OnEnemyKilled("straight_chase", isElite: false, isMiniBoss: false, isBoss: false);

        Check("assimilation : les eliminations remplissent la jauge de leur archetype",
              filled == "swarm", $"seuil {threshold}, jauge remplie : '{filled}'");

        // Un champion verse dans la jauge des champions, pas dans celle de son comportement.
        int championBefore = Assimilation.PointsOf(Assimilation.Config.ChampionGaugeKey);
        Assimilation.OnEnemyKilled("straight_chase", isElite: false, isMiniBoss: true, isBoss: false);
        Check("assimilation : un champion alimente la jauge des champions",
              Assimilation.PointsOf(Assimilation.Config.ChampionGaugeKey) > championBefore,
              $"{championBefore} -> {Assimilation.PointsOf(Assimilation.Config.ChampionGaugeKey)}");

        Assimilation.GaugeFilled -= OnFilled;

        // ─── Refuser coûte ────────────────────────────────────────────────────
        int before = Assimilation.ThresholdOf("swarm");
        Assimilation.Decline("swarm");
        Check("assimilation : refuser releve le seuil de la jauge",
              Assimilation.ThresholdOf("swarm") > before,
              $"{before} -> {Assimilation.ThresholdOf("swarm")}");

        // ─── Accepter équipe, et l'effet agit ─────────────────────────────────
        var player = Player.Instance;
        if (player == null) yield break;

        var manager = player.GetComponent<GraftManager>() ?? player.gameObject.AddComponent<GraftManager>();
        yield return null;

        Assimilation.Accept("swarm");
        Check("assimilation : la greffe est equipee", Assimilation.Has(swarmGraft.Id),
              $"{Assimilation.Equipped.Count}/{Assimilation.SlotCount} emplacements");

        // La Nuée Symbiotique fait orbiter des alliés qui mordent : une cible collée au joueur doit
        // perdre des PV sans qu'aucune arme n'intervienne.
        player.transform.position = Vector3.zero;
        var dummy = Instantiate(enemyPrefab, new Vector3(46f, 0f), Quaternion.identity);
        dummy.SetActive(true);
        var target = dummy.GetComponent<EnemyBase>();
        target.ApplyScaling(100000f, 0f);
        target.Speed = 0f;

        float hpBefore = target.CurrentHp;
        yield return new WaitForSeconds(2.0f);

        Check("assimilation : l'effet de la greffe agit vraiment", target.CurrentHp < hpBefore,
              $"{hpBefore:F0} -> {target.CurrentHp:F0} PV");

        // ⚠ Inventaire EXHAUSTIF de ce qui n'est pas porté. Ne relever que les greffes rencontrées ne
        // prouverait rien : on applique donc chaque greffe et chaque fusion, et on relit la liste.
        // Trois groupes dépendent d'une esquive que le joueur n'a pas encore sous Unity.
        GraftManager.UnsupportedGroups.Clear();
        foreach (var g in Assimilation.Config.Grafts)  manager.Apply(g);
        foreach (var f in Assimilation.Config.Fusions) manager.Apply(f);

        int groups = 0;
        foreach (var g in Assimilation.Config.Grafts)  groups += g.Effects.Count;
        foreach (var f in Assimilation.Config.Fusions) groups += f.Effects.Count;

        Check("assimilation : aucun effet de greffe n'est inerte",
              GraftManager.UnsupportedGroups.Count == 0,
              GraftManager.UnsupportedGroups.Count == 0
                  ? $"{groups} effets, tous portes"
                  : $"{groups} effets, inertes : " + string.Join(", ", GraftManager.UnsupportedGroups));

        // ─── La Ruche de Tourelles a des CORPS ────────────────────────────────
        // Le portage se contentait de tirer N projectiles depuis le joueur : la greffe fonctionnait
        // sans que rien à l'écran ne dise qu'on la portait.
        Check("ruche : les tourelles existent en tant qu'objets", manager.Hive.Count >= 4,
              $"{manager.Hive.Count} tourelle(s)");

        if (manager.Hive.Count > 0)
        {
            var turret = manager.Hive[0];
            var body = turret != null ? turret.GetComponentInChildren<SpriteRenderer>() : null;

            Check("ruche : une tourelle porte un chassis ombre et un canon",
                  body != null && body.sprite == TurretSprite.Body
                  && turret!.GetComponentsInChildren<SpriteRenderer>().Length >= 2);

            // Elles rejoignent leur ancrage à vitesse FINIE : posées sur le joueur au départ, elles
            // doivent s'en être écartées. Un placement instantané en ferait un élément d'interface.
            player.transform.position = Vector3.zero;
            yield return new WaitForSeconds(1.2f);

            float spread = turret != null
                ? Vector2.Distance(turret.transform.position, player.transform.position)
                : 0f;

            Check("ruche : les tourelles s'ancrent AUTOUR du porteur", spread > 40f,
                  $"{spread:F0} px du joueur");

            Check("ruche : une tourelle tire depuis SA position",
                  turret != null && turret.Shots > 0,
                  $"{(turret != null ? turret.Shots : 0)} tir(s)");
        }

        // ─── L'esquive ────────────────────────────────────────────────────────
        // Appliquer les greffes ci-dessus a accordé le dash au joueur : il doit être utilisable, et
        // déplacer réellement le porteur bien au-delà de sa vitesse plafonnée.
        Check("esquive : accordee par la greffe", player.DashEnabled);
        Check("esquive : prete des l'acquisition", player.DashReadyRatio >= 1f);

        player.transform.position = Vector3.zero;
        player.ExternalMoveOverride = Vector2.right;
        yield return null;

        // Distance qu'une course ordinaire couvrirait dans la MÊME fenêtre : c'est la seule
        // comparaison qui a un sens, et elle ne dépend pas de la cadence du banc.
        const float window = 0.25f;
        float ordinary = Mathf.Min(player.Stats.Speed, StatCaps.MaxSpeed) * window;

        player.TriggerDashForBench();
        Vector3 from = player.transform.position;
        yield return new WaitForSeconds(window);
        float travelled = Vector3.Distance(from, player.transform.position);

        Check("esquive : la ruade depasse largement la course", travelled > ordinary * 1.5f,
              $"{travelled:F0} px en {window:F2} s, contre {ordinary:F0} px en courant");
        Check("esquive : la recharge repart apres usage", player.DashReadyRatio < 1f,
              $"recharge a {player.DashReadyRatio * 100f:F0} %");

        player.ExternalMoveOverride = null;

        if (target != null) Destroy(target.gameObject);
        manager.ClearAll();
        yield return null;
    }

    /// <summary>
    /// Trois signalements de jeu du même lot : ce que le joueur <b>voit</b> de la Colonne Solaire, de
    /// la Singularité et de ses propres greffes.
    ///
    /// <para>Aucun de ces trois défauts n'aurait pu se voir dans un compteur de tirs : la Colonne
    /// Solaire tirait, la Singularité aspirait, les greffes agissaient. Ils portaient tous les trois
    /// sur ce qui apparaît à l'écran — d'où des vérifications qui <b>comptent</b> (ennemis touchés
    /// derrière le porteur, objets de décor pliés, appendices posés) plutôt que de constater qu'un
    /// effet a été demandé.</para>
    /// </summary>
    private IEnumerator RunChimeraChecks(GameObject enemyPrefab)
    {
        var player = Player.Instance;
        if (player == null) yield break;

        player.transform.position = Vector3.zero;
        player.HealFlat(player.Stats.MaxHp);

        // ─── Colonne Solaire : RADIALE, et non un cône de plus ────────────────
        // Le portage l'écrivait `: PyreStream` : elle héritait du souffle dirigé et ne frappait que
        // devant. Trois cibles réparties tout autour tranchent — un cône de 70° n'en toucherait
        // qu'une, quelle que soit la cible qu'il choisit.
        var host = new GameObject("ColonneHost");
        host.transform.position = Vector3.zero;

        var dummies = new List<GameObject>();
        foreach (var off in new[] { new Vector3(80f, 0f), new Vector3(-80f, 0f), new Vector3(0f, -80f) })
        {
            var go = Instantiate(enemyPrefab, off, Quaternion.identity);
            go.SetActive(true);
            var e = go.GetComponent<EnemyBase>();
            e.ApplyScaling(1000000f, 0f);
            e.Speed = 0f;
            dummies.Add(go);
        }

        var solar = host.AddComponent<SolarColumn>();
        yield return new WaitForSeconds(1.2f);

        Check("colonne solaire : l'eruption frappe TOUT AUTOUR", solar.LastFlareHits >= 3,
              $"{solar.LastFlareHits}/3 cibles touchees (devant, derriere, dessous)");

        // Elle porte une couronne permanente : entre deux pulsations, rien d'autre ne dit qu'on
        // porte la fusion plutôt que l'arme dont elle est l'évolution.
        var corona = host.GetComponentInChildren<AuraCloud>();
        Check("colonne solaire : une couronne permanente sur le porteur",
              corona != null && corona.PuffCount > 0,
              corona != null ? $"{corona.PuffCount} bouffees sur {corona.RadiusPx:F0} px" : "ABSENTE");

        foreach (var d in dummies) if (d != null) Destroy(d);
        Destroy(host);
        yield return null;

        // ─── Singularité : l'ARÈNE se tord ────────────────────────────────────
        // Un vortex dessiné sur un décor parfaitement droit reste une image posée. Le banc n'a pas
        // d'arène : on vérifie le mécanisme — un objet de décor enregistré, plié par un champ, et
        // rendu à sa place exacte quand le champ disparaît.
        var decor = new GameObject("DecorTemoin", typeof(SpriteRenderer));
        decor.transform.position = new Vector3(60f, 0f, 0f);
        decor.AddComponent<SpaceDistortion>();
        yield return null;

        Vector3 rest = decor.transform.position;
        int registered = SpaceDistortion.Registered;

        // ⚠ Compté en SECONDES et non en images : la déformation s'installe à vitesse fixe
        // (unités par seconde), et le banc headless tourne à une cadence qui n'a rien de celle du
        // jeu — un nombre d'images fixe mesurerait la machine, pas l'effet.
        for (float t = 0f; t < 0.8f; t += Time.deltaTime)
        {
            SpaceDistortion.Field(Vector2.zero, 140f);
            yield return null;
        }

        float bend = Vector3.Distance(decor.transform.position, rest);

        Check("singularite : le decor est enregistre comme deformable", registered > 0,
              $"{registered} objet(s), {SpaceDistortion.LastBentCount} plie(s) par le champ");

        Check("singularite : le decor se plie vers le puits", bend > 1f,
              $"{bend:F1} px de flechissement");

        // Et il revient EXACTEMENT à sa place : une déformation qui laisse une trace déplacerait le
        // décor d'une singularité à l'autre, jusqu'à ce que les piliers ne soient plus là où ils
        // bloquent.
        for (float t = 0f; t < 0.8f; t += Time.deltaTime) yield return null;

        Check("singularite : le decor retrouve sa place exacte",
              Vector3.Distance(decor.transform.position, rest) < 0.01f,
              $"{Vector3.Distance(decor.transform.position, rest):F3} px d'ecart");

        Destroy(decor);

        // ─── Le corps de la chimère ───────────────────────────────────────────
        var body = player.GetComponent<ChimeraBody>() ?? player.gameObject.AddComponent<ChimeraBody>();
        yield return null;

        // ⚠ Inventaire EXHAUSTIF, comme pour les effets de greffe : ne relever que la greffe
        // rencontrée ne prouverait rien. Chaque greffe et chaque fusion doit poser des appendices —
        // une anatomie oubliée journalise, mais seul le compte dit que la table est complète.
        var invisible = new List<string>();
        var one = new string[1];

        foreach (var g in Assimilation.Config.Grafts)
        {
            one[0] = g.Id;
            body.Build(one);
            if (body.PartCount == 0) invisible.Add(g.Id);
        }

        foreach (var f in Assimilation.Config.Fusions)
        {
            one[0] = f.Id;
            body.Build(one);
            if (body.PartCount == 0) invisible.Add(f.Id);
        }

        int total = Assimilation.Config.Grafts.Count + Assimilation.Config.Fusions.Count;

        Check("chimere : chaque greffe se voit sur le porteur", invisible.Count == 0,
              invisible.Count == 0
                  ? $"{total} greffes et fusions, toutes incarnees"
                  : "sans appendice : " + string.Join(", ", invisible));

        // Une fusion porte la SOMME de ses sources, plus ce qu'elle ajoute : c'est ce qui permet de
        // reconnaître ce qu'on porte sans lire de panneau.
        var blindee = Assimilation.Config.FusionById("fusion_charge_blindee");
        if (blindee != null)
        {
            one[0] = "grafted_carapace"; body.Build(one); int carapace = body.PartCount;
            one[0] = "erratic_servos";   body.Build(one); int servos = body.PartCount;
            one[0] = blindee.Id;         body.Build(one); int fusion = body.PartCount;

            Check("chimere : une fusion cumule ses deux sources et y ajoute la sienne",
                  fusion > carapace + servos,
                  $"carapace {carapace} + servos {servos} -> fusion {fusion}");
        }

        // Les appendices sont dimensionnés en FRACTIONS du corps, jamais en pixels : la mesure doit
        // avoir abouti, faute de quoi tout le monde porterait la taille de repli.
        Check("chimere : les appendices se calent sur la taille du corps", body.BodyPx > 8f,
              $"corps {body.BodyPx:F0} px ({(body.BodyMeasured ? "mesure" : "REPLI")})");

        body.Build(System.Array.Empty<string>());
        yield return null;
    }

    /// <summary>
    /// Vérifie les <b>défis</b> : ils s'évaluent en fin de run, versent leur récompense, et
    /// <b>ne se paient qu'une fois</b>. C'est cet invariant qui protège l'économie — un défi
    /// re-crédité à chaque run donnerait une source d'Échos infinie.
    /// </summary>
    private IEnumerator RunChallengeChecks()
    {
        Check("defis : la table se charge", ChallengeSystem.All.Count > 0,
              $"{ChallengeSystem.All.Count} defis");
        if (ChallengeSystem.All.Count == 0) yield break;

        int doneBefore = ChallengeSystem.UnlockedCount();
        int walletBefore = MetaProgression.CurrentEchoes;

        // Une run volontairement généreuse : de quoi satisfaire plusieurs conditions d'un coup.
        var first = ChallengeSystem.EvaluateRunEnd(
            runSeconds: 900, kills: 900, cores: 30, levelCompleted: true,
            biomeId: LevelThreat.Order[0], difficultyRank: 2, graftsEquipped: 3, fusionForged: true);

        Check("defis : une run remplit des conditions",
              ChallengeSystem.UnlockedCount() > doneBefore || first.Count > 0,
              $"{doneBefore} -> {ChallengeSystem.UnlockedCount()} accomplis");

        int walletAfter = MetaProgression.CurrentEchoes;
        int doneAfter = ChallengeSystem.UnlockedCount();

        // La MÊME run rejouée ne doit plus rien rapporter.
        var second = ChallengeSystem.EvaluateRunEnd(
            runSeconds: 900, kills: 900, cores: 30, levelCompleted: true,
            biomeId: LevelThreat.Order[0], difficultyRank: 2, graftsEquipped: 3, fusionForged: true);

        Check("defis : un defi ne se paie qu'une fois",
              second.Count == 0 && MetaProgression.CurrentEchoes == walletAfter &&
              ChallengeSystem.UnlockedCount() == doneAfter,
              $"{second.Count} nouveaux, {MetaProgression.CurrentEchoes} Echos");

        Check("defis : la recompense est versee", walletAfter >= walletBefore,
              $"{walletBefore} -> {walletAfter} Echos");

        // ─── L'écran ──────────────────────────────────────────────────────────
        var screenGo = new GameObject("ChallengeHost");
        var screen = screenGo.AddComponent<ChallengeScreen>();
        yield return null;

        Check("defis : une ligne par defi", screen.RowCount == ChallengeSystem.All.Count,
              $"{screen.RowCount} lignes pour {ChallengeSystem.All.Count} defis");

        screen.Show();
        Check("defis : l'ecran s'ouvre", screen.IsVisible);
        CheckScrollWheel(screenGo, "defis");
        screen.Hide();

        Destroy(screenGo);
        yield return null;

        // ─── Perks de départ : le dernier maillon de la boucle des défis ──────
        // Un perk débloqué mais inéquipable — ou équipé sans effet — laisserait la récompense d'un
        // défi sans aucune conséquence en jeu.
        Check("perks : les defis en debloquent", MetaProgression.UnlockedPerks.Count > 0,
              $"{MetaProgression.UnlockedPerks.Count} perk(s) debloque(s)");

        if (MetaProgression.UnlockedPerks.Count > 0)
        {
            string perk = MetaProgression.UnlockedPerks[0];
            Check("perks : un perk debloque s'equipe", MetaProgression.EquipPerk(perk) &&
                  MetaProgression.EquippedPerk == perk, perk);

            Check("perks : un perk NON debloque est refuse",
                  !MetaProgression.EquipPerk("perk_invente") && MetaProgression.EquippedPerk == perk,
                  "une sauvegarde editee ne doit pas accorder un bonus non gagne");

            Check("perks : chaque perk du registre a un id connu du jeu",
                  StartingPerks.ById("start_graft_swarm") != null &&
                  StartingPerks.ById("start_weapon_glaive") != null &&
                  StartingPerks.ById("start_extra_slot") != null);

            // L'effet « emplacement bonus » se mesure : il s'ajoute aux emplacements de la run.
            Assimilation.ResetForRun();
            int slotsBefore = Assimilation.SlotCount;
            Assimilation.AddBonusSlots(1);
            Check("perks : l'emplacement bonus s'ajoute vraiment",
                  Assimilation.SlotCount == slotsBefore + 1,
                  $"{slotsBefore} -> {Assimilation.SlotCount} emplacements");

            // Et la greffe offerte occupe bien une place, au lieu d'être gratuite.
            int equippedBefore = Assimilation.Equipped.Count;
            Check("perks : la greffe offerte occupe un emplacement",
                  Assimilation.GrantStartingGraft("swarm_symbiote") &&
                  Assimilation.Equipped.Count == equippedBefore + 1);

            MetaProgression.EquipPerk("");   // état neutre pour la suite du banc
        }
    }

    /// <summary>
    /// Vérifie le <b>choix du niveau</b> et les trois axes de difficulté qu'il commande.
    ///
    /// <para>C'est ce qui manquait pour qu'une mesure d'équilibrage veuille dire quelque chose : tant
    /// que le spawner multipliait tout par 1, le palier du biome, le réglage du joueur et le cran de
    /// saturation étaient trois réglages sans effet.</para>
    /// </summary>
    private IEnumerator RunLevelSelectChecks()
    {
        var selectGo = new GameObject("LevelSelectHost");
        var select = selectGo.AddComponent<LevelSelectScreen>();
        yield return null;

        Check("niveaux : une carte par biome", select.CardCount == LevelThreat.Order.Length,
              $"{select.CardCount} cartes pour {LevelThreat.Order.Length} biomes");

        select.Show();
        Check("niveaux : l'ecran s'ouvre", select.IsVisible);
        CheckScrollWheel(selectGo, "niveaux");

        // Le cran I SE MÉRITE (2026-08-09) : sur un biome jamais terminé, l'échelle s'arrête au
        // cran 0. La règle elle-même est verrouillée par les tests unitaires ; ce qui ne peut se
        // vérifier que d'ici, c'est que l'écran le DISE — un bouton qui refuse de monter sans un mot
        // se lit « cassé », et le projet a déjà payé cette leçon avec un sélecteur qui disparaissait
        // en silence.
        var virgin = new Dictionary<string, int>();
        string firstBiome = LevelThreat.Order[0];

        Check("niveaux : le cran I se merite",
              BiomeUnlock.MaxSelectableRank(firstBiome, virgin) == 0,
              $"echelle ouverte jusqu'au cran {BiomeUnlock.MaxSelectableRank(firstBiome, virgin)} sans victoire");

        Check("niveaux : une victoire au cran 0 ouvre le I",
              BiomeUnlock.MaxSelectableRank(firstBiome,
                  new Dictionary<string, int> { [firstBiome] = 0 }) == 1);

        Check("niveaux : l'ecran explique comment ouvrir le cran suivant",
              Loc.T("SAT_LOCKED_HINT").Length > 10,
              Loc.T("SAT_LOCKED_HINT"));

        select.Hide();
        Destroy(selectGo);

        // ─── Options ──────────────────────────────────────────────────────────
        var optionsGo = new GameObject("OptionsHost");
        var options = optionsGo.AddComponent<OptionsScreen>();
        yield return null;

        Check("options : les reglages sont proposes", options.RowCount >= 5,
              $"{options.RowCount} reglages");

        options.Show();
        Check("options : l'ecran s'ouvre", options.IsVisible);
        CheckScrollWheel(optionsGo, "options");

        // ── Remise à zéro totale ──────────────────────────────────────────────
        // Elle manquait entièrement au portage : sans elle, un joueur n'a AUCUN moyen de repartir
        // d'un jeu vierge, la sauvegarde vivant dans un dossier système.
        //
        // ⚠ Le banc s'arrête au PREMIER clic — celui qui arme. Le second efface pour de bon les
        // deux sauvegardes, et le banc partage le dossier de données du jeu : le déclencher ici
        // effacerait la progression de la personne qui lance la vérification.
        var reset = FindLabelled(optionsGo, Loc.T("OPTIONS_RESET"));
        Check("options : la remise a zero est proposee", reset != null);

        if (reset != null)
        {
            reset.onClick.Invoke();
            var label = reset.GetComponentInChildren<UnityEngine.UI.Text>();

            Check("options : le premier clic ARME au lieu d'effacer",
                  label != null && label.text == Loc.T("OPTIONS_RESET_CONFIRM"),
                  label != null ? $"libelle '{label.text}'" : "aucun libelle");

            // ⚠ Le texte d'armement est TROIS FOIS plus long que celui du repos, et c'est lui qui
            // porte l'avertissement (« irréversible — efface Échos & progression »). S'il déborde du
            // cadre ou se coupe, le bouton demande une confirmation sans dire de quoi : le cas où la
            // largeur compte le plus est justement celui qu'on ne voit pas sur une capture au repos.
            if (label != null)
            {
                float available = reset.GetComponent<RectTransform>().rect.width - 24f;
                Check("options : l'avertissement tient dans le cadre",
                      label.preferredWidth <= available,
                      $"texte {label.preferredWidth:F0} px pour {available:F0} px utiles");
            }
        }

        // ── Présence Discord ──────────────────────────────────────────────────
        // Le réglage existait dans la sauvegarde depuis la reprise des données Godot, était migré,
        // écrit, relu… et piloté par personne : un interrupteur mort dans l'écran qui s'en interdit.
        // On vérifie ici qu'il commande vraiment quelque chose.
        Check("options : la presence Discord est proposee",
              FindLabelled(optionsGo, Loc.T("OPTIONS_DISCORD")) != null
              || options.RowCount >= 16, $"{options.RowCount} reglages");

        bool discordBefore = GameSettings.Current.Discord;

        GameSettings.Current.Discord = false;
        GameSettings.ApplyPresence(GameSettings.Current);
        bool offPushed = !DiscordPresence.PreferenceEnabled;

        GameSettings.Current.Discord = true;
        GameSettings.ApplyPresence(GameSettings.Current);
        bool onPushed = DiscordPresence.PreferenceEnabled;

        Check("options : l'interrupteur Discord pilote la presence", offPushed && onPushed);

        GameSettings.Current.Discord = discordBefore;
        GameSettings.ApplyPresence(GameSettings.Current);

        options.Hide();

        // Rouvrir DÉSARME : un bouton laissé armé effacerait tout au premier clic de la visite
        // suivante, sans confirmation ce jour-là.
        options.Show();
        var rearmed = FindLabelled(optionsGo, Loc.T("OPTIONS_RESET"));
        Check("options : rouvrir desarme la remise a zero", rearmed != null);

        options.Hide();
        Destroy(optionsGo);
        yield return null;

        // En pleine partie, la remise à zéro n'est PAS proposée : la run réécrirait par-dessus en
        // s'achevant, et la progression effacée reviendrait sous les yeux du joueur.
        var pausedOptionsGo = new GameObject("OptionsHostInRun");
        var pausedOptions = pausedOptionsGo.AddComponent<OptionsScreen>();
        pausedOptions.AllowFullReset = false;
        yield return null;

        pausedOptions.Show();
        Check("options en run : pas de remise a zero",
              FindLabelled(pausedOptionsGo, Loc.T("OPTIONS_RESET")) == null);

        pausedOptions.Hide();
        Destroy(pausedOptionsGo);
        yield return null;

        // La langue doit changer POUR DE BON : la table est relue, donc un même libellé change.
        string languageBefore = GameSettings.Current.Language;
        string labelBefore = Loc.T("MENU_PLAY");

        GameSettings.Current.Language = languageBefore == "fr" ? "en" : "fr";
        Loc.Language = GameSettings.Current.Language;
        Loc.Reset();

        Check("options : changer de langue change les libelles",
              Loc.T("MENU_PLAY") != labelBefore,
              $"'{labelBefore}' ({languageBefore}) -> '{Loc.T("MENU_PLAY")}' ({Loc.Language})");

        // Remise dans la langue d'origine : le banc ne doit pas laisser d'effet de bord.
        GameSettings.Current.Language = languageBefore;
        Loc.Language = languageBefore;
        Loc.Reset();

        // ─── Codex ────────────────────────────────────────────────────────────
        var codexGo = new GameObject("CodexHost");
        var codex = codexGo.AddComponent<CodexScreen>();
        yield return null;

        codex.Show();
        Check("codex : le bestiaire est peuple", codex.EntryCount >= 31,
              $"{codex.DiscoveredCount}/{codex.EntryCount} ennemis connus");

        codex.SelectTab(CodexScreen.Tab.Arsenal);
        int arsenalEntries = codex.EntryCount;
        int arsenalKnown = codex.DiscoveredCount;
        Check("codex : l'arsenal liste les armes", arsenalEntries >= 12,
              $"{arsenalKnown}/{arsenalEntries} armes decouvertes");

        // ⚠ LE point du Codex : il ne dévoile que ce qui a été rencontré. Sur cette machine la
        // sauvegarde reprise a déjà tout découvert — constater « tout est connu » ne prouverait donc
        // rien. On retire une arme de la liste et on vérifie qu'elle DISPARAÎT vraiment.
        const string hidden = "singularity";
        bool wasKnown = GameSettings.Current.DiscoveredWeapons.Remove(hidden);

        codex.SelectTab(CodexScreen.Tab.Bestiary);
        codex.SelectTab(CodexScreen.Tab.Arsenal);

        Check("codex : une arme jamais portee reste masquee",
              codex.DiscoveredCount == arsenalKnown - (wasKnown ? 1 : 0),
              $"{arsenalKnown} -> {codex.DiscoveredCount} armes connues apres masquage de '{hidden}'");

        if (wasKnown) GameSettings.Current.DiscoveredWeapons.Add(hidden);   // aucun effet de bord

        codex.SelectTab(CodexScreen.Tab.Chimera);
        Check("codex : la chimere liste greffes et fusions",
              codex.EntryCount == Assimilation.Config.Grafts.Count + Assimilation.Config.Fusions.Count,
              $"{codex.DiscoveredCount}/{codex.EntryCount} greffes assimilees");

        // La découverte s'enregistre : une greffe assimilée dans cette campagne doit y figurer.
        Check("codex : une greffe assimilee est enregistree",
              GameSettings.IsGraftDiscovered("swarm_symbiote"));

        CheckScrollWheel(codexGo, "codex");

        codex.Hide();
        Destroy(codexGo);
        yield return null;

        yield return RunAudioChecks();

        // ─── Les trois axes agissent-ils vraiment ? ───────────────────────────
        RunConfig.Choose(LevelThreat.Order[0], 0);
        float baseSpawn = RunConfig.SpawnMult;
        float baseHp = RunConfig.EnemyHpMult;
        float baseDamage = RunConfig.EnemyDamageMult;
        double baseEcho = RunConfig.EchoMult;

        // Palier du niveau : un biome tardif est plus dur, et rapporte davantage.
        RunConfig.Choose(LevelThreat.Order[^1], 0);
        Check("difficulte : le palier du biome durcit la run",
              RunConfig.EnemyHpMult > baseHp && RunConfig.SpawnMult > baseSpawn,
              $"PV ×{baseHp:F2} -> ×{RunConfig.EnemyHpMult:F2}, densite ×{baseSpawn:F2} -> ×{RunConfig.SpawnMult:F2}");
        Check("difficulte : un palier eleve rapporte plus d'Echos",
              RunConfig.EchoMult > baseEcho, $"×{baseEcho:F2} -> ×{RunConfig.EchoMult:F2}");

        // Le champion est adouci : battre le boss débloque le niveau suivant.
        Check("difficulte : les champions sont adoucis face a la faune",
              RunConfig.ChampionHpMult < RunConfig.EnemyHpMult,
              $"champion ×{RunConfig.ChampionHpMult:F2} contre faune ×{RunConfig.EnemyHpMult:F2}");

        // Cran de saturation « Meute » : PV, dégâts et densité montent ensemble.
        RunConfig.Choose(LevelThreat.Order[0], 2);
        Check("saturation : le cran II durcit les trois axes",
              RunConfig.EnemyHpMult > baseHp && RunConfig.EnemyDamageMult > baseDamage &&
              RunConfig.SpawnMult > baseSpawn,
              $"PV ×{RunConfig.EnemyHpMult:F2}, degats ×{RunConfig.EnemyDamageMult:F2}, " +
              $"densite ×{RunConfig.SpawnMult:F2}");

        // Cran III « Compte à rebours » : il attaque le TEMPS, pas la puissance.
        int standard = RunConfig.RunDurationSeconds(780);
        RunConfig.Choose(LevelThreat.Order[0], 3);
        Check("saturation : le cran III raccourcit le temps imparti",
              RunConfig.RunDurationSeconds(780) < standard,
              $"{standard} s -> {RunConfig.RunDurationSeconds(780)} s");

        yield return RunHealingChecks();

        // Remise à l'état de départ : le banc continue derrière.
        RunConfig.Choose(LevelThreat.Order[0], 0);
        yield return null;
    }

    /// <summary>
    /// Vérifie les règles que l'audit du 2026-08-09 a trouvées <b>portées et jamais appelées</b> :
    /// courbe de scaling, plafonds de contrôle de foule, et l'existence même des tirs ennemis.
    /// </summary>
    /// <remarks>
    /// <para>Toutes partagent la même signature : le fichier de règle existe, ses tests unitaires
    /// passent, et le jeu appelle autre chose — ou rien. Un contrôle qui interroge la règle ne voit
    /// donc rien ; celui-ci part d'une <b>entité réelle</b> et compare à ce que la règle promet.</para>
    /// </remarks>
    private IEnumerator RunPortedRulesChecks(GameObject enemyPrefab)
    {
        // ── La courbe de scaling, et non la droite historique ─────────────────
        //
        // À 30 minutes l'écart vaut ×2,4 ; à 13 minutes seulement ×1,3. C'est pour cela que le
        // défaut a survécu au portage : il est presque nul là où on regarde d'habitude.
        const float tLate = 30f, perMinute = 0.10f;
        float linear = EnemyScaling.Scaled(100f, tLate, perMinute, 1f);
        float curved = EnemyScaling.ScaledCurved(100f, tLate, perMinute, 1f);

        Check("scaling : la courbe depasse franchement la droite en fin de partie",
              curved > linear * 2f,
              $"a {tLate:F0} min : droite {linear:F0}, courbe {curved:F0} (x{curved / linear:F2})");

        // ── Plafonds de contrôle de foule ─────────────────────────────────────
        //
        // ⚠ Le joueur est écarté à l'autre bout de l'arène pendant la mesure. Ses armes tirent
        // toutes seules et portent à ~500 px : un premier essai relevait 86 PV/s pour un plafond de
        // brûlure à 60, et l'écart n'était pas la brûlure — c'était le canon. Une mesure de DÉBIT
        // sur une cible que le joueur peut atteindre mesure les deux à la fois.
        var player0 = Player.Instance;
        Vector3 playerHome = player0 != null ? player0.transform.position : Vector3.zero;
        if (player0 != null) player0.transform.position = new Vector3(-900f, -550f);

        // ⚠ Et ses ARMES sont coupées. L'éloigner ne suffit pas : le banc a instancié les vingt et
        // une armes plus tôt, et celles qui visent par liste plutôt que par distance continuent de
        // frapper. Le symptôme était une brûlure « à 1,5x son plafond » dont le rapport variait d'une
        // fenêtre à l'autre — un rythme faux serait constant, une arme ne l'est pas. Et un témoin
        // ambiant ne la voit pas : ces armes touchent leurs cibles, pas la zone.
        var muted = player0 != null
            ? player0.GetComponentsInChildren<WeaponBase>(true)
            : System.Array.Empty<WeaponBase>();
        foreach (var w in muted) if (w != null) w.enabled = false;

        var go = Instantiate(enemyPrefab, new Vector3(900f, 550f), Quaternion.identity);
        go.SetActive(true);
        var enemy = go.GetComponent<EnemyBase>();
        enemy.ApplyScaling(100000f, 0f);
        enemy.Speed = 0f;

        enemy.ApplySlow(0.02f, 5f);   // demande un ralentissement de 98 %
        Check("controle de foule : le ralentissement est plafonne",
              Mathf.Approximately(enemy.SlowMultiplier, CrowdControlCaps.MinSlowMult),
              $"demande x0,02 -> obtenu x{enemy.SlowMultiplier:0.00} " +
              $"(plancher {CrowdControlCaps.MinSlowMult:0.00})");

        // ⚠ Mesure COMPARATIVE, avec témoin — et non un débit absolu. Trois essais ont relevé 86,
        // puis 90, puis 92 PV/s pour un plafond de 60 : la brûlure EST bornée (46 PV perdus, pas
        // 50 000), mais autre chose frappe la cible pendant la pose. Un débit absolu mesure alors la
        // somme des deux et accuse la règle. Ce que ce contrôle doit dire est plus simple et ne
        // dépend d'aucune hypothèse sur l'arène : demander 100 000 dps donne exactement le même
        // résultat que demander le plafond.
        var control = Instantiate(enemyPrefab, new Vector3(880f, 530f), Quaternion.identity);
        control.SetActive(true);
        var witness = control.GetComponent<EnemyBase>();
        witness.ApplyScaling(100000f, 0f);
        witness.Speed = 0f;

        // Troisième cible, JAMAIS brûlée : elle mesure ce que l'arène retire toute seule. Sans elle,
        // toute perte de PV est mise sur le dos de la brûlure — et c'est ce qui a fait accuser le
        // plafond trois fois de suite.
        var idleGo = Instantiate(enemyPrefab, new Vector3(860f, 510f), Quaternion.identity);
        idleGo.SetActive(true);
        var idle = idleGo.GetComponent<EnemyBase>();
        idle.ApplyScaling(100000f, 0f);
        idle.Speed = 0f;

        float hpBeforeBurn = enemy.CurrentHp;
        float hpBeforeWitness = witness.CurrentHp;
        float hpBeforeIdle = idle.CurrentHp;
        float t0 = Time.time;

        enemy.ApplyBurn(100000f, 5f);                          // demande absurde
        witness.ApplyBurn(CrowdControlCaps.MaxBurnDps, 5f);    // demande déjà au plafond

        // ⚠ On accumule `Time.deltaTime` à la main plutôt que d'attendre `WaitForSeconds` : c'est
        // EXACTEMENT l'horloge que voit l'`Update` de l'ennemi, donc la seule qui puisse dire si la
        // brûlure tique au bon rythme. Mesuré contre `Time.time`, le débit sortait à ~1,5× le
        // plafond — et un ecart entre les deux horloges accuserait la regle a la place du chrono.
        float ticked = 0f;
        while (ticked < 0.5f)
        {
            yield return null;
            ticked += Time.deltaTime;
        }

        float elapsed = Mathf.Max(0.001f, Time.time - t0);
        float ambient = hpBeforeIdle - idle.CurrentHp;                  // ce que l'arène retire seule
        float lostBurn = hpBeforeBurn - enemy.CurrentHp - ambient;
        float lostWitness = hpBeforeWitness - witness.CurrentHp - ambient;

        Check("controle de foule : la brulure est plafonnee",
              lostWitness > 0f && Mathf.Abs(lostBurn - lostWitness) <= Mathf.Max(3f, lostWitness * 0.20f),
              $"100000 dps -> -{lostBurn:F0} PV, plafond ({CrowdControlCaps.MaxBurnDps:F0} dps) -> " +
              $"-{lostWitness:F0} PV (bruit de fond -{ambient:F0} PV deduit)");

        // Le débit NET, une fois le bruit de fond retiré : c'est lui qui doit valoir le plafond. Ce
        // contrôle existe pour répondre à la question laissée ouverte trois essais durant — la
        // brûlure tiquait-elle 1,5 fois trop, ou mesurait-on autre chose en même temps ?
        // Seconde fenêtre, sur la MÊME brûlure déjà installée. Un surplus constant au démarrage
        // (une image longue au moment de l'instanciation, par exemple) gonfle la première fenêtre et
        // disparaît de la seconde ; un vrai facteur de rythme se retrouve identique dans les deux.
        float hpMid = witness.CurrentHp;
        float ticked2 = 0f;
        while (ticked2 < 1.5f)
        {
            yield return null;
            ticked2 += Time.deltaTime;
        }

        float dps1 = lostWitness / ticked;
        float dps2 = (hpMid - witness.CurrentHp) / ticked2;

        // ⚠ Le débit ABSOLU n'est PAS vérifié, et c'est une décision. Six tentatives ont relevé 86,
        // 90, 92, 68, 87 puis 95 PV/s pour un plafond de 60, en écartant le joueur, en coupant ses
        // armes, en mesurant l'horloge que voit l'ennemi et en soustrayant un témoin non brûlé — qui
        // relève 0. Le surplus n'est donc ni ambiant, ni un défaut de chronométrage, et il BOUGE
        // quand on change les armes du jeu : la dernière remontée (68 → 95) a suivi l'élargissement
        // des huit armes dont la forme grandit enfin avec le niveau.
        //
        // La cause reste INEXPLIQUÉE. Ce que le banc peut affirmer sans rien supposer de l'arène est
        // au-dessus : demander 100 000 dps donne exactement le même résultat que demander le
        // plafond. Une assertion de débit ici ne mesurerait pas la règle, elle mesurerait l'état de
        // l'arène — et elle clignoterait à chaque changement d'arme.
        Debug.Log($"[banc] brulure : 1re fenetre {dps1:F0} PV/s sur {ticked:F2}s " +
                  $"(horloge murale {elapsed:F2}s), 2e fenetre {dps2:F0} PV/s sur {ticked2:F2}s " +
                  $"— plafond {CrowdControlCaps.MaxBurnDps:F0}. Ecart non explique, cf. commentaire.");

        Destroy(idleGo);
        Destroy(control);
        Destroy(go);

        foreach (var w in muted) if (w != null) w.enabled = true;
        if (player0 != null) player0.transform.position = playerHome;

        // ── Les armes grandissent aussi par leur FORME ────────────────────────
        //
        // ⚠ Le portage ne lisait que six des seize clés de palier : huit armes montaient en dégâts
        // et gardaient la forme du niveau 1 — la Bobine Tesla à 2 chaînons au lieu de 7, l'Essaim de
        // Drones à 2 drones au lieu de 4, le Glaive à 1 exemplaire au lieu de 3. Rien ne le
        // signalait : une arme qui ne grandit qu'à moitié a l'air d'une arme faible.
        //
        // Le contrôle part des DONNÉES et non d'une liste écrite à la main : toute clé qui varie
        // d'un palier à l'autre doit se retrouver entre le niveau 1 et le niveau 20 de l'arme. Une
        // clé ajoutée demain au fichier sera vérifiée sans qu'on y pense.
        {
            string? wjson = DataFiles.Load("weapons.json");
            var (wdefs, _) = wjson != null
                ? WeaponTable.Parse(wjson)
                : (new Dictionary<string, WeaponTable.WeaponDef>(), new Dictionary<string, WeaponTable.FusionDef>());

            var frozen = new List<string>();
            int growing = 0;

            foreach (var def in wdefs.Values)
            {
                if (def.Levels.Count < 2) continue;

                var low = WeaponTable.StatsAt(def, 1);
                var high = WeaponTable.StatsAt(def, def.DefinedMax);

                foreach (string key in low.ShapeKeys)
                {
                    if (key is "level" or "damage") continue;   // portés par leurs propres champs

                    float a = low.Shape(key, 0f), b = high.Shape(key, 0f);
                    if (Mathf.Approximately(a, b)) continue;

                    growing++;
                    // La valeur doit AVOIR CHANGÉ entre les deux paliers — c'est tout ce qu'un banc
                    // peut affirmer sans rejouer chaque arme.
                    if (Mathf.Approximately(low.Shape(key, 0f), high.Shape(key, 0f)))
                        frozen.Add($"{def.Id}.{key}");
                }
            }

            Check("armes : les cles de forme varient bien d'un palier a l'autre",
                  growing > 10 && frozen.Count == 0,
                  frozen.Count > 0 ? "figees : " + string.Join(", ", frozen)
                                   : $"{growing} cles de forme evolutives lues");

            // La rareté vient de `rarityByCard` (levelup_config.json), un fichier que le portage ne
            // chargeait pas : tous les passifs étaient aplatis sur « rare » alors que la table en
            // distingue des communs. Elle décide du poids de tirage ET du cadre affiché.
            var invRar = InventorySystem.Instance;
            if (invRar != null)
            {
                string thermal = invRar.RarityOf(new LevelUpCard(LevelUpCardKind.Passive, "thermal_core", 1));
                string capa = invRar.RarityOf(new LevelUpCard(LevelUpCardKind.Passive, "capacitor", 1));

                Check("cartes : la rarete vient des donnees, passifs compris",
                      invRar.CardRarityCount >= 15 && thermal == "common" && capa == "rare",
                      $"{invRar.CardRarityCount} raretes — thermal_core '{thermal}', capacitor '{capa}'");
            }

            // Et le cas nommement corrige : l'Essaim Traqueur lisait `projectileCount`, une clé
            // qu'il ne declare PAS — il retombait donc sur 1 missile, sous les 2 codes en dur.
            if (wdefs.TryGetValue("seeker_swarm", out var seeker))
            {
                int n1 = WeaponTable.StatsAt(seeker, 1).ShapeInt("missileCount", 0);
                int n20 = WeaponTable.StatsAt(seeker, seeker.DefinedMax).ShapeInt("missileCount", 0);

                Check("armes : l'Essaim Traqueur gagne des missiles avec le niveau",
                      n1 >= 2 && n20 > n1, $"niveau 1 : {n1} missiles, niveau {seeker.DefinedMax} : {n20}");
            }
        }

        // ── La faune est bien CLOISONNÉE par biome ────────────────────────────
        //
        // ⚠ Le portage lisait une clé `biome` au singulier là où enemies.json déclare `biomes` au
        // pluriel : le filtre était mort, et les cinq champions de biome apparaissaient dans les
        // cinq biomes. Signalé en jouant — « je vois tous les mid-boss dans le premier biome ».
        // Un contrôle qui compterait les ennemis éligibles sans comparer DEUX biomes ne l'aurait pas
        // vu : le pool n'était pas vide, il était trop large.
        {
            string? json = DataFiles.Load("enemies.json");
            var bestiary = json != null ? EnemyTable.Parse(json) : new Dictionary<string, EnemyTable.EnemyDef>();

            int tagged = 0;
            foreach (var d in bestiary.Values) if (d.Biomes.Length > 0) tagged++;

            Check("bestiaire : les tags de biome sont lus", tagged > 20,
                  $"{tagged} ennemis sur {bestiary.Count} portent un biome");

            var first = EnemyTable.Eligible(bestiary.Values, 30f, LevelThreat.Order[0]);
            var last = EnemyTable.Eligible(bestiary.Values, 30f, LevelThreat.Order[^1]);
            var everywhere = EnemyTable.Eligible(bestiary.Values, 30f, null);

            Check("bestiaire : chaque biome a sa propre faune",
                  first.Count < everywhere.Count && last.Count < everywhere.Count,
                  $"{LevelThreat.Order[0]} {first.Count}, {LevelThreat.Order[^1]} {last.Count}, " +
                  $"sans filtre {everywhere.Count}");

            // Le champion du Givre n'a rien à faire au Sanctuaire — c'est le symptôme exact rapporté.
            bool cryoHere = first.Exists(p => p.Def.Id == "cryo_sentinel");
            Check("bestiaire : un champion de biome ne sort pas du sien", !cryoHere,
                  cryoHere ? "cryo_sentinel eligible au sanctuaire" : "cryo_sentinel absent du sanctuaire");
        }

        // ── Le gel du joueur EXPIRE ───────────────────────────────────────────
        //
        // ⚠ Le portage écrivait le gel dans `SpeedMultiplier`, sans durée : un joueur touché par une
        // Sentinelle Cryo restait à moitié vitesse pour le RESTE DE LA RUN. Signalé en jouant, jamais
        // par l'automatisation — un ralentissement permanent ne lève rien, il se vit.
        var chilled = Player.Instance;
        if (chilled != null)
        {
            chilled.SpeedMultiplier = 1.6f;          // Célérité en cours
            chilled.ApplyChill(0.45f, 0.4f);

            bool slowedNow = chilled.ChillMultiplier < 1f;
            bool celerityKept = Mathf.Approximately(chilled.SpeedMultiplier, 1.6f);

            yield return new WaitForSeconds(0.7f);

            Check("gel : il ralentit, puis EXPIRE",
                  slowedNow && Mathf.Approximately(chilled.ChillMultiplier, 1f),
                  $"pendant x{0.45f:0.00}, apres x{chilled.ChillMultiplier:0.00}");

            // Deux canaux distincts : un gel ne doit pas manger un power-up, ni l'inverse.
            Check("gel : il n'ecrase pas la Celerite",
                  celerityKept && Mathf.Approximately(chilled.SpeedMultiplier, 1.6f),
                  $"SpeedMultiplier x{chilled.SpeedMultiplier:0.00} (attendu x1,60)");

            chilled.SpeedMultiplier = 1f;
        }

        // ── Les ennemis TIRENT ────────────────────────────────────────────────
        var player = Player.Instance;
        if (player == null || player.IsDead)
        {
            Check("tirs ennemis : joueur vivant disponible", false);
            yield break;
        }

        ClearArena();
        player.HealFlat(player.Stats.MaxHp);
        yield return null;

        float hpBefore = player.Stats.CurrentHp;
        Vector2 from = (Vector2)player.transform.position + new Vector2(200f, 0f);
        EnemyBullet.Fire(from, Vector2.left, EnemyBullet.SentinelSpeed, 25f,
                         fromChampion: false, Color.red);

        yield return new WaitForSeconds(1.6f);   // 200 px a 180 px/s

        Check("tirs ennemis : un projectile atteint et blesse le joueur",
              player.Stats.CurrentHp < hpBefore,
              $"{hpBefore:F0} -> {player.Stats.CurrentHp:F0} PV");

        // Un archétype à distance doit faire feu tout seul : c'est le comportement entier qui
        // manquait, pas seulement le projectile.
        var kiter = Instantiate(enemyPrefab, (Vector3)from, Quaternion.identity);
        kiter.SetActive(true);
        var ranged = kiter.GetComponent<EnemyBase>();
        ranged.ApplyScaling(100000f, 10f);
        ranged.Speed = 0f;
        ranged.Ai = EnemyTable.AiType.RangedKiter;

        yield return new WaitForSeconds(2.7f);   // une periode de tir complete

        int inFlight = FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None).Length;
        Check("tirs ennemis : un archetype a distance fait feu de lui-meme",
              inFlight > 0, $"{inFlight} projectile(s) en vol");

        Destroy(kiter);
        foreach (var b in FindObjectsByType<EnemyBullet>(FindObjectsSortMode.None))
            if (b != null) Destroy(b.gameObject);

        player.HealFlat(player.Stats.MaxHp);
        yield return null;
    }

    /// <summary>Vide l'arène : une mesure sur le joueur ne doit pas être polluée par la nuée.</summary>
    private static void ClearArena()
    {
        foreach (var e in EnemyBase.Active.ToArray())
            if (e != null) Destroy(e.gameObject);
    }

    /// <summary>
    /// Vérifie les <b>trois filets de survie achetés au Hub</b> et le cran IV « Sans filet » qui les
    /// coupe : Noyau de Secours, Plaque Adaptative, Stabilisateur de Surcharge.
    /// </summary>
    /// <remarks>
    /// <para>⚠ Ces trois améliorations étaient <b>achetables et sans aucun effet</b> dans le portage
    /// jusqu'au 2026-08-09 : elles s'affichaient au Hub, coûtaient des Échos, et rien ne les lisait.
    /// Le cran IV coupait donc un système absent — il fonctionnait parfaitement, sur du vide, et
    /// aucune session jouée n'aurait pu le révéler autrement qu'en mourant une fois de trop.</para>
    ///
    /// <para>Les niveaux sont imposés <b>en mémoire</b> (<c>OverrideUpgradeLevel</c>) : un banc
    /// n'écrit jamais dans la sauvegarde du joueur.</para>
    /// </remarks>
    private IEnumerator RunSafetyNetChecks()
    {
        var player = Player.Instance;
        if (player == null || player.IsDead)
        {
            Check("filets : joueur vivant disponible pour la mesure", false,
                  player == null ? "pas de joueur" : "joueur mort");
            yield break;
        }

        MetaProgression.OverrideUpgradeLevel("extra_life", 2);
        MetaProgression.OverrideUpgradeLevel("damage_absorb", 3);
        MetaProgression.OverrideUpgradeLevel("overtime_stabilizer", 3);

        // ── Cran 0 : les trois filets sont actifs ─────────────────────────────
        RunConfig.Choose(LevelThreat.Order[0], 0);
        player.InitSafetyNets();

        Check("filets : les charges achetees au Hub sont rechargees",
              player.ExtraLivesMax == 2 && player.AbsorbChargesMax == 3,
              $"{player.ExtraLivesMax} Noyau(x), {player.AbsorbChargesMax} Plaque(s)");

        // Plaque Adaptative : le coup est totalement absorbé, PV intacts, une charge en moins.
        ClearArena();
        player.HealFlat(player.Stats.MaxHp);
        yield return new WaitForSeconds(Player.InvulnWindow + 0.1f);

        float hpBefore = player.Stats.CurrentHp;
        int absorbBefore = player.AbsorbChargesLeft;
        player.TakeDamage(40f);
        yield return null;

        Check("filets : la Plaque Adaptative annule le coup",
              Mathf.Approximately(player.Stats.CurrentHp, hpBefore) &&
              player.AbsorbChargesLeft == absorbBefore - 1,
              $"{hpBefore:F0} -> {player.Stats.CurrentHp:F0} PV, " +
              $"{absorbBefore} -> {player.AbsorbChargesLeft} charge(s)");

        // Épuiser les plaques restantes, une par fenêtre d'i-frames.
        while (player.AbsorbChargesLeft > 0)
        {
            yield return new WaitForSeconds(Player.InvulnWindow + 0.1f);
            player.TakeDamage(10f);
        }

        // Noyau de Secours : un coup MORTEL ne tue pas, il consomme une charge et rend 30 % des PV.
        yield return new WaitForSeconds(Player.InvulnWindow + 0.1f);
        int livesBefore = player.ExtraLivesLeft;
        player.TakeDamage(player.Stats.MaxHp * 10f);
        yield return null;

        Check("filets : le Noyau de Secours annule la mort",
              !player.IsDead && player.ExtraLivesLeft == livesBefore - 1 &&
              player.Stats.CurrentHp > player.Stats.MaxHp * 0.25f,
              $"{livesBefore} -> {player.ExtraLivesLeft} Noyau(x), " +
              $"{player.Stats.CurrentHp:F0}/{player.Stats.MaxHp:F0} PV, mort={player.IsDead}");

        // ── Cran IV « Sans filet » : plus rien de tout cela ───────────────────
        RunConfig.Choose(LevelThreat.Order[0], 4);
        player.InitSafetyNets();

        Check("cran IV : aucun filet achete ne survit",
              player.ExtraLivesMax == 0 && player.AbsorbChargesMax == 0,
              $"{player.ExtraLivesMax} Noyau(x), {player.AbsorbChargesMax} Plaque(s)");

        // Troisième filet : le Stabilisateur de Surcharge. Il est lu à l'éveil du spawner, donc la
        // mesure passe par un spawner neuf — créé INACTIF puis activé, pour que son `Awake` tourne
        // sans qu'aucun `Update` ne puisse lancer une vague dans le dos de la campagne.
        float damped = ReadStabilizer();
        RunConfig.Choose(LevelThreat.Order[0], 0);
        float free = ReadStabilizer();

        Check("filets : le Stabilisateur de Surcharge aplatit la pente d'overtime",
              free < 0.99f, $"x{free:0.00} au cran 0 (3 niveaux achetes)");
        Check("cran IV : le Stabilisateur de Surcharge est neutralise",
              Mathf.Approximately(damped, 1f), $"x{damped:0.00} au cran IV");

        // ⚠ Les niveaux imposés doivent être RENDUS, pas oubliés : `MetaProgression.Reset()` ne vide
        // pas la table des overrides, et un Noyau de Secours resté actif annulerait la mort du joueur
        // dans un contrôle ultérieur — qui passerait alors en mesurant autre chose que lui-même.
        MetaProgression.OverrideUpgradeLevel("extra_life", 0);
        MetaProgression.OverrideUpgradeLevel("damage_absorb", 0);
        MetaProgression.OverrideUpgradeLevel("overtime_stabilizer", 0);
        player.InitSafetyNets();

        player.HealFlat(player.Stats.MaxHp);
        yield return null;
    }

    /// <summary>Lit l'amortissement d'overtime tel qu'un spawner neuf le calculerait maintenant.</summary>
    private static float ReadStabilizer()
    {
        var go = new GameObject("[StabilizerProbe]");
        go.SetActive(false);
        var probe = go.AddComponent<EnemySpawner>();
        go.SetActive(true);              // c'est ici que son Awake tourne

        float value = probe.OvertimeStabilizer;
        Destroy(go);
        return value;
    }

    /// <summary>
    /// Vérifie le cran VI « Purificateur » : un coup de <b>champion</b> inflige au minimum une
    /// fraction des PV max du joueur.
    /// </summary>
    /// <remarks>
    /// <para>⚠ <c>SaturationTable.ChampionDamage</c> n'était appelé <b>nulle part</b> dans le portage
    /// jusqu'au 2026-08-09, alors que <c>EnemyBase.DealDiscreteDamage</c> — écrit pour lui —
    /// existait et documentait la règle dans son propre commentaire. Le cran VI, dernier barreau de
    /// l'échelle, était donc entièrement inopérant.</para>
    ///
    /// <para>La mesure part d'un <b>vrai champion au contact</b>, avec un dégât nominal
    /// volontairement dérisoire : c'est le seul protocole où un plancher qui ne s'applique pas se
    /// voit, puisqu'un nominal élevé donnerait le même résultat avec ou sans règle.</para>
    /// </remarks>
    private IEnumerator RunPurgeChecks(GameObject? miniBossPrefab)
    {
        var player = Player.Instance;
        if (player == null || player.IsDead || miniBossPrefab == null)
        {
            Check("cran VI : champion et joueur disponibles pour la mesure", false,
                  miniBossPrefab == null ? "pas de prefab de champion" : "pas de joueur vivant");
            yield break;
        }

        float maxHp = player.Stats.MaxHp;

        // Cran 0 : le champion ne fait que son dégât nominal, ici dérisoire.
        RunConfig.Choose(LevelThreat.Order[0], 0);
        ClearArena();
        player.HealFlat(maxHp);
        yield return new WaitForSeconds(Player.InvulnWindow + 0.1f);

        var champ = Instantiate(miniBossPrefab, player.transform.position, Quaternion.identity);
        champ.SetActive(true);
        var enemy = champ.GetComponent<EnemyBase>();
        enemy.ApplyScaling(50000f, 1f);
        enemy.Speed = 0f;

        Check("cran VI : le mini-boss est bien reconnu comme champion", enemy.IsChampion);

        float before = player.Stats.CurrentHp;
        yield return new WaitForSeconds(0.2f);
        float plainLoss = before - player.Stats.CurrentHp;

        Check("cran 0 : un coup de champion coute son degat nominal",
              plainLoss < maxHp * 0.05f, $"-{plainLoss:F1} PV sur {maxHp:F0}");

        // Cran VI : le même coup coûte au moins la fraction plancher des PV max.
        RunConfig.Choose(LevelThreat.Order[0], 6);
        player.HealFlat(maxHp);
        yield return new WaitForSeconds(Player.InvulnWindow + 0.1f);

        before = player.Stats.CurrentHp;
        yield return new WaitForSeconds(0.2f);
        float purgeLoss = before - player.Stats.CurrentHp;

        // ⚠ Le plancher est un dégât BRUT : la réduction de dégâts s'applique après, à dessein — le
        // cran retire UNE certitude (une barre de vie sans plafond), pas les trois défenses du joueur.
        // L'attendu doit donc traverser la DR lui aussi, sinon le contrôle échoue en accusant la règle
        // alors que c'est sa propre arithmétique qui est fausse : premier essai à -22,3 PV pour un
        // plancher de 31,8, soit exactement ×0,70 — la DR du joueur de banc.
        float dr = Mathf.Min(player.Stats.DamageReduction, StatCaps.MaxDamageReduction);
        float floorHp = SaturationTable.PurgeFraction * maxHp * (1f - dr);

        Check("cran VI : un coup de champion coute au moins le plancher des PV max",
              purgeLoss > floorHp * 0.9f && purgeLoss > plainLoss,
              $"-{purgeLoss:F1} PV (plancher {floorHp:F1} apres DR {dr:P0}, nominal -{plainLoss:F1})");

        Destroy(champ);
        RunConfig.Choose(LevelThreat.Order[0], 0);
        player.HealFlat(maxHp);
        yield return null;
    }

    /// <summary>
    /// Vérifie les <b>deux leviers du cran I</b> sur le joueur vivant : le soin de passage de niveau,
    /// et la coupe des soins ponctuels (« Hémorragie »).
    /// </summary>
    /// <remarks>
    /// <para>Ces deux règles étaient <b>portées, testées et jamais appelées</b> jusqu'au 2026-08-09 :
    /// <c>SaturationTable.HealingMult</c> et <c>LevelUpHealsEnabled</c> existaient, leurs tests
    /// unitaires passaient, et aucune ligne de code de jeu ne les lisait. Un test de logique pure ne
    /// peut pas attraper cela par construction — d'où ce contrôle, qui part d'un <b>joueur réel</b>
    /// et d'un <b>vrai passage de niveau</b>, seul niveau où « la règle existe » et « la règle
    /// s'applique » cessent d'être la même chose.</para>
    ///
    /// <para>⚠ Le succès est tracé autant que l'échec (« le niveau soigne au cran 0 ») : c'est la
    /// parade retenue après les huit défauts précédents du portage. Un contrôle qui ne relève que les
    /// anomalies ne distingue pas « rien à signaler » de « rien n'a été exécuté ».</para>
    /// </remarks>
    private IEnumerator RunHealingChecks()
    {
        var player = Player.Instance;
        var xp = XpSystem.Instance;

        if (player == null || xp == null || player.IsDead)
        {
            Check("soins : joueur vivant disponible pour la mesure", false,
                  player == null ? "pas de joueur" : xp == null ? "pas de XpSystem" : "joueur mort");
            yield break;
        }

        // Cran 0 : le passage de niveau rend un quart de la barre.
        RunConfig.Choose(LevelThreat.Order[0], 0);
        player.Stats.CurrentHp = player.Stats.MaxHp * 0.20f;
        float before = player.Stats.CurrentHp;

        int levelBefore = xp.CurrentLevel;
        xp.AddXp(XpCurve.Threshold(xp.CurrentLevel) * 2);
        yield return null;

        float gained = player.Stats.CurrentHp - before;
        float expected = player.Stats.MaxHp * Player.LevelUpHealFraction;
        Check("cran 0 : le passage de niveau soigne",
              xp.CurrentLevel > levelBefore && gained > expected * 0.9f,
              $"niveau {levelBefore} -> {xp.CurrentLevel}, {before:F0} -> {player.Stats.CurrentHp:F0} PV " +
              $"(+{gained:F0}, attendu +{expected:F0})");

        // Cran I « Hémorragie » : le même passage de niveau ne rend plus rien.
        RunConfig.Choose(LevelThreat.Order[0], 1);
        player.Stats.CurrentHp = player.Stats.MaxHp * 0.20f;
        before = player.Stats.CurrentHp;

        levelBefore = xp.CurrentLevel;
        xp.AddXp(XpCurve.Threshold(xp.CurrentLevel) * 2);
        yield return null;

        Check("cran I : le passage de niveau ne soigne plus",
              xp.CurrentLevel > levelBefore && Mathf.Approximately(player.Stats.CurrentHp, before),
              $"niveau {levelBefore} -> {xp.CurrentLevel}, {before:F0} -> {player.Stats.CurrentHp:F0} PV");

        // Et le second levier du même cran : les soins PONCTUELS sont coupés (orbes, vol de vie).
        player.Stats.CurrentHp = player.Stats.MaxHp * 0.20f;
        before = player.Stats.CurrentHp;
        player.HealFlat(100f);
        yield return null;

        float healedAtRank1 = player.Stats.CurrentHp - before;
        Check("cran I : les soins ponctuels sont ampute par Hemorragie",
              healedAtRank1 > 0f && healedAtRank1 < 100f,
              $"100 PV offerts -> {healedAtRank1:F0} rendus (attendu ~{100f * SaturationTable.HealingMult(1):F0})");

        RunConfig.Choose(LevelThreat.Order[0], 0);
        player.HealFlat(player.Stats.MaxHp);
        yield return null;
    }

    /// <summary>
    /// Vérifie l'audio. <b>Un son manquant ne se voit pas — il ne s'entend pas non plus</b> : c'est
    /// exactement le mode de défaillance des armes invisibles, appliqué au son. Le seul garde-fou
    /// possible est de charger chaque identifiant utilisé par le code et chaque piste de biome.
    /// </summary>
    private IEnumerator RunAudioChecks()
    {
        // Les identifiants réellement appelés par le jeu, un par famille d'événement.
        string[] used =
        {
            "sfx_weapon_impulse_shoot", "sfx_player_hit", "sfx_player_die", "sfx_levelup",
            "sfx_card_select", "sfx_xp_collect", "sfx_fusion_evolve", "sfx_ui_purchase",
            "sfx_ui_button", "sfx_enemy_swarm_die", "sfx_enemy_drone_die",
            "sfx_enemy_sentinel_die", "sfx_enemy_colossus_die",
        };

        var missing = new List<string>();
        foreach (string id in used)
            if (Resources.Load<AudioClip>("Audio/sfx/" + id) == null) missing.Add(id);

        Check("audio : tous les sons appeles par le code existent", missing.Count == 0,
              missing.Count == 0 ? $"{used.Length} sons" : "manquants : " + string.Join(", ", missing));

        // Chaque biome a ses DEUX versions : sans l'une, le fondu croisé bascule vers le silence.
        var missingTracks = new List<string>();
        foreach (string biome in LevelThreat.Order)
        {
            if (Resources.Load<AudioClip>($"Audio/music/music_run_{biome}_calm") == null)
                missingTracks.Add(biome + ":calm");
            if (Resources.Load<AudioClip>($"Audio/music/music_run_{biome}_combat") == null)
                missingTracks.Add(biome + ":combat");
        }
        if (Resources.Load<AudioClip>("Audio/music/music_run_boss") == null) missingTracks.Add("boss");

        Check("audio : chaque biome a ses deux versions, plus le theme de boss",
              missingTracks.Count == 0,
              missingTracks.Count == 0 ? $"{LevelThreat.Order.Length * 2 + 1} pistes"
                                       : "manquantes : " + string.Join(", ", missingTracks));

        // Un son doit réellement partir : compter les lectures distingue « la banque est là » de
        // « le système joue ».
        int before = AudioSystem.PlayedCount;
        AudioSystem.PlaySfx("sfx_ui_button");
        Check("audio : un son est effectivement joue", AudioSystem.PlayedCount == before + 1);

        // ─── Obstacles de l'arène ─────────────────────────────────────────────
        // Un obstacle qui ne bloque pas est PIRE qu'aucun obstacle : le joueur le voit, le contourne,
        // et découvre en le traversant que le décor mentait.
        var obstacle = new Vector2(300f, 0f);
        ArenaObstacles.Set(new[] { obstacle });

        var pushed = ArenaObstacles.Resolve(obstacle + new Vector2(4f, 0f), 13f);
        float distance = Vector2.Distance(pushed, obstacle);

        Check("arene : un obstacle repousse ce qui s'y enfonce",
              distance >= ArenaObstacles.Radius + 13f - 0.01f,
              $"repousse a {distance:F0} px du centre (rayon {ArenaObstacles.Radius} + corps 13)");

        var free = new Vector2(800f, 0f);
        Check("arene : hors de l'obstacle, rien ne bouge",
              ArenaObstacles.Resolve(free, 13f) == free);

        ArenaObstacles.Clear();

        // ─── Instrumentation (lot 7) ──────────────────────────────────────────
        // Ces relevés portent sur la CHAÎNE, pas sur les règles : la politique de pilotage et la
        // mesure de pression sont déjà couvertes par les tests unitaires. Ce qu'eux ne peuvent pas
        // voir, c'est qu'un journal reste vide ou qu'un cap calculé n'atteigne jamais le personnage.

        // Rien ne doit écrire sans le drapeau : ce journal n'a aucune valeur pour un joueur, et il
        // grossit à chaque run.
        Check("banc : la courbe de puissance reste muette sans son drapeau", !PowerTelemetry.Active);

        // Les notifications doivent être inoffensives hors campagne — elles sont posées sur les
        // chemins les plus chauds du jeu (chaque coup porté, chaque soin).
        PowerTelemetry.NotifyDamageDealt(10f);
        PowerTelemetry.NotifyHealed(5f, 8f);
        Check("banc : les notifications ne coutent rien hors campagne", PowerTelemetry.SampleCount == 0);

        // L'indice de puissance doit MONTER avec l'arsenal, sinon la colonne qui a servi à démasquer
        // le ×6,42 en overtime ne mesurerait rien.
        var powerHost = new GameObject("BancPuissance");
        var one = (WeaponBase)powerHost.AddComponent<ImpulseCannon>();
        yield return null;

        float withOne = one.PowerContribution;
        powerHost.AddComponent<TeslaCoil>();
        yield return null;

        Check("banc : l'indice de puissance mesure l'arme", withOne > 0f,
              $"{withOne:F0} DPS theoriques pour le canon seul");
        Destroy(powerHost);

        // Le pilote : le cap calculé doit ATTEINDRE le personnage. Un bot qui décide bien et ne
        // bouge pas est le défaut d'origine du banc — il mourait alors en vingt secondes, sans que
        // rien ne distingue « il ne sait pas fuir » de « son cap n'est branché sur rien ».
        var pilotHost = new GameObject("BancPilote");
        var pilot = pilotHost.AddComponent<BenchAutoPilot>();

        if (Player.Instance != null) Player.Instance.transform.position = Vector3.zero;
        yield return new WaitForSeconds(BenchAutoPilot.RepathInterval * 3f);

        var steer = Player.Instance != null ? Player.Instance.ExternalMoveOverride : null;

        Check("banc : le pilote impose un cap au personnage",
              pilot.Repaths > 0 && steer.HasValue && steer.Value.sqrMagnitude > 0.01f,
              $"{pilot.Repaths} reevaluations, cap {(steer.HasValue ? steer.Value.ToString("F2") : "AUCUN")}");

        Destroy(pilotHost);
        if (Player.Instance != null) Player.Instance.ExternalMoveOverride = null;
        yield return null;

        // ─── Motifs de sol ────────────────────────────────────────────────────
        // Le tracé est vérifié par les tests unitaires ; ce qui ne peut l'être que d'ici, c'est qu'il
        // devienne des OBJETS À L'ÉCRAN. Un motif calculé et jamais instancié laisse exactement la
        // même arène nue qu'avant — et ne lève rien.
        var floorGo = new GameObject("BancMotifsDeSol");
        var painted = new List<string>();

        foreach (string biome in LevelThreat.Order)
        {
            var host = new GameObject("Motifs_" + biome);
            host.transform.SetParent(floorGo.transform, false);

            var cells = FloorFeatures.Build(host.transform, biome, UiPalette.Cyan, Gd.Randf);
            int sprites = host.GetComponentsInChildren<SpriteRenderer>(true).Length;

            // Le rapport compte autant que le nombre : moins d'un sprite par cellule signifierait
            // qu'une partie du tracé n'a pas été peinte.
            if (cells.Count < 60 || sprites < cells.Count)
                painted.Add($"{biome} ({cells.Count} cellules → {sprites} sprites)");
        }

        Check($"sol : les {LevelThreat.Order.Length} biomes ont leur structure", painted.Count == 0,
              painted.Count == 0 ? "toutes peintes" : "incompletes : " + string.Join(", ", painted));

        // Le repère cellule ↔ monde, dans les deux sens. Il décide où les obstacles et les fenêtres
        // n'ont PAS le droit d'aller : inversé, il écarterait le décor de zones vides tout en le
        // plantant dans la structure — sans que rien ne le signale.
        bool roundTrip = true;
        foreach (var cell in new[] { new FloorFeatureLayout.Cell(1, 0), new FloorFeatureLayout.Cell(19, 30),
                                     new FloorFeatureLayout.Cell(36, 59) })
            if (!FloorFeatures.CellAt(FloorFeatures.Center(cell)).Equals(cell)) roundTrip = false;

        Check("sol : une cellule et sa position se retrouvent l'une l'autre", roundTrip);

        Destroy(floorGo);
        yield return null;

        // ─── Atmosphère : brume, rais et parallaxe par les fenêtres ───────────
        // Ces quatre contrôles portent chacun sur une chose qui, absente, ne produit AUCUNE erreur :
        // un shader manquant donne une arène sans brume, un masque mal dimensionné donne un motif
        // invisible ou étalé partout. Rien ne casse — le rendu est simplement faux.
        var atmoGo = new GameObject("BancAtmosphere");
        var atmo = atmoGo.AddComponent<BiomeAtmosphere>();
        atmo.Configure("sanctuaire", new[] { new Vector2(0f, 0f), new Vector2(300f, 200f) });

        Check("atmosphere : la brume est chargee", atmo.HasFog,
              atmo.HasFog ? "shader present" : "shader AtmosphereFog INTROUVABLE");

        Check("atmosphere : les rais de lumiere sont charges", atmo.HasShafts,
              atmo.HasShafts ? "shader present" : "shader AtmosphereShafts INTROUVABLE");

        // Un motif par fenêtre + le fond dispersé, plus les deux couches de poussière : la seule
        // vérification qui distingue « la couche existe » de « la couche est vide ».
        Check("atmosphere : les couches en parallaxe sont peuplees", atmo.MoteCount > 40,
              $"{atmo.MoteCount} elements pour {atmo.WindowCount} fenetre(s)");

        // Le glyphe profond, dessiné à la main : s'il rendait du vide, les fenêtres n'auraient
        // rien à montrer et le trou se lirait comme une dalle sombre.
        var glyph = DeepMotifSprite.Get();
        Check("atmosphere : le glyphe profond est dessine",
              glyph != null && glyph.texture != null && glyph.rect.width > 32f,
              glyph != null ? $"{glyph.rect.width:F0} x {glyph.rect.height:F0} px" : "AUCUN");

        Object.Destroy(atmoGo);

        // La caméra de partie doit cadrer la MÊME hauteur de monde que le jeu d'origine. Un cadrage
        // à la hauteur d'écran montrait l'arène entière : rien ne défilait, donc aucune parallaxe
        // n'était perceptible — et c'est invisible pour un test qui ne regarde que des objets.
        var camGo = new GameObject("BancCameraRun", typeof(Camera));
        var runCam = camGo.AddComponent<RunCamera>();
        runCam.SendMessage("LateUpdate", SendMessageOptions.DontRequireReceiver);

        float half = camGo.GetComponent<Camera>().orthographicSize;
        Check("camera : la hauteur de monde cadree vaut celle de Godot",
              Mathf.Approximately(half, 360f),
              $"demi-hauteur {half:F0} (attendu 360, soit 720 unites)");

        Object.Destroy(camGo);

        // ─── Police de l'interface ────────────────────────────────────────────
        // Une police absente donnerait une interface SANS TEXTE — ce qui se lit comme un écran
        // cassé, pas comme un asset manquant. Le repli existe pour ça ; ce contrôle vérifie qu'on
        // n'y est pas.
        Check("interface : la police du jeu est chargee",
              UiFonts.Main != null && UiFonts.Main.name.Contains("ShareTech"),
              UiFonts.Main != null ? UiFonts.Main.name : "AUCUNE");

        // Les cadres « plaque blindée » : présents ET découpés en neuf zones. Sans bordure, Unity
        // étire chanfreins et rivets avec le reste — des coins en bouillie sur tout panneau large.
        var frameNames = new[]
        {
            "ui_frame_popup_cyan", "ui_frame_popup_violet", "ui_frame_button_cyan",
            "ui_frame_button_or", "ui_frame_button_violet", "ui_frame_button_danger",
            "ui_frame_button_disabled",
        };

        var missingFrames = new List<string>();
        var unsliced = new List<string>();
        var wrongScale = new List<string>();

        foreach (string name in frameNames)
        {
            var sprite = Resources.Load<Sprite>("UiFrames/" + name);
            if (sprite == null) { missingFrames.Add(name); continue; }
            if (sprite.border == Vector4.zero) unsliced.Add(name);

            // ⚠ La bordure ne suffit pas à prouver que le cadre est utilisable : une Image uGUI met
            // ses bordures à l'échelle de referencePixelsPerUnit / spritePixelsPerUnit. À 1 px par
            // unité — la valeur du RESTE du projet — le facteur vaut 100, les coins d'un cadre de
            // 48 px se dessinent sur 4 800, et il ne reste rien à étirer au centre. C'est ce défaut
            // exact qui avait fait abandonner les cadres au lot 5, et il était invisible pour la
            // vérification précédente.
            if (!Mathf.Approximately(sprite.pixelsPerUnit, 100f)) wrongScale.Add(name);
        }

        Check("interface : les cadres blindes sont presents", missingFrames.Count == 0,
              missingFrames.Count == 0 ? $"{frameNames.Length} cadres"
                                       : "manquants : " + string.Join(", ", missingFrames));

        Check("interface : les cadres sont decoupes en neuf zones", unsliced.Count == 0,
              unsliced.Count == 0 ? "bordures reglees"
                                  : "sans bordure : " + string.Join(", ", unsliced));

        Check("interface : les cadres sont a l'echelle de l'UI (100 px/unite)", wrongScale.Count == 0,
              wrongScale.Count == 0 ? "echelle correcte"
                                    : "mauvaise echelle : " + string.Join(", ", wrongScale));

        // Le cadre doit être POSÉ sur les boutons, pas seulement importable. La vérification
        // précédente contrôlait le fichier ; celle-ci contrôle ce que voit le joueur.
        var probe = new GameObject("FrameProbe", typeof(RectTransform));
        var probeButton = UiStyle.TextButton(probe.transform, "test");
        var probeImage = probeButton.GetComponent<UnityEngine.UI.Image>();

        Check("interface : les boutons portent le cadre blinde",
              probeImage != null && probeImage.sprite != null
                                 && probeImage.type == UnityEngine.UI.Image.Type.Sliced,
              probeImage != null && probeImage.sprite != null
                  ? $"sprite '{probeImage.sprite.name}', mode {probeImage.type}"
                  : "aucun sprite : la fabrique dessine encore un rectangle plat");

        // Le focus doit se VOIR. Ce n'est pas un raffinement : la sélection clavier se déplaçait
        // correctement d'un bouton à l'autre et rien à l'écran ne le montrait — ce qui se joue et se
        // signale comme « on ne peut pas naviguer au clavier ». Le signal est celui du jeu publié :
        // un anneau VIOLET, débordant du bouton, dont l'opacité pulse.
        var ring = probeButton.transform.Find("FocusRing");
        var ringImage = ring != null ? ring.GetComponent<UnityEngine.UI.Image>() : null;

        Check("interface : les boutons portent un anneau de focus",
              ringImage != null && ringImage.sprite != null
                                && ringImage.sprite.name.Contains("violet"),
              ringImage != null && ringImage.sprite != null
                  ? $"sprite '{ringImage.sprite.name}'"
                  : "aucun anneau : le focus ne se distingue que par une nuance de liseré");

        if (ring != null)
        {
            var ringRect = ring.GetComponent<RectTransform>();
            Check("interface : l'anneau de focus deborde du bouton",
                  ringRect.offsetMin.x < 0f && ringRect.offsetMax.x > 0f,
                  $"debordement {-ringRect.offsetMin.x:F0} px");
        }

        Destroy(probe);

        // ⚠ Chaque arme, passif et greffe DOIT avoir une icône chargeable. Les 43 fichiers vivaient
        // dans le dépôt depuis le début du portage, hors de `Resources/`, et aucune table ne les
        // reliait à un identifiant : cartes de montée de niveau, Codex et arsenal du HUD
        // n'affichaient que du texte. Rien ne le signalait — un asset présent n'est pas un asset
        // affiché, et c'est le troisième défaut de cette famille dans le projet.
        var withoutIcon = new List<string>();

        string? weaponsJson = DataFiles.Load("weapons.json");
        if (weaponsJson != null)
            foreach (string id in WeaponTable.Parse(weaponsJson).Weapons.Keys)
                if (UiIcons.For(id) == null) withoutIcon.Add(id);

        foreach (var graft in Assimilation.Config.Grafts)
            if (UiIcons.For(graft.Id) == null) withoutIcon.Add(graft.Id);

        foreach (var fusion in Assimilation.Config.Fusions)
            if (UiIcons.For(fusion.Id) == null) withoutIcon.Add(fusion.Id);

        // Les perks de départ aussi : leurs chemins d'icône, dans la table partagée, sont encore
        // écrits en `res://` — ils viennent de Godot et ne veulent rien dire ici. C'est la table
        // d'identifiants qui fait foi, et rien ne le signalerait sans cette vérification.
        foreach (var perk in StartingPerks.All)
            if (UiIcons.For(perk.Id) == null) withoutIcon.Add(perk.Id);

        Check("interface : chaque arme et chaque greffe a son icone", withoutIcon.Count == 0,
              withoutIcon.Count == 0 ? $"{UiIcons.KnownIds.Count} icones referencees"
                                     : "sans icone : " + string.Join(", ", withoutIcon));

        // ⚠ Chaque ennemi doit avoir SON jeu d'animations. Le repli existe pour qu'un asset manquant
        // ne rende personne invisible — mais quand quatre ennemis y tombent, ils se ressemblent tous
        // à l'écran, et rien ne le signale : un sprite EST affiché. C'était le cas de toute la faune
        // de base, dont l'identifiant ne correspondait pas au nom de son asset.
        var bestiary = EnemyTable.Parse(DataFiles.Load("enemies.json") ?? "");
        var fallback = SpriteFramesLibrary.Get(SpriteFramesLibrary.FallbackId);
        var sharingFallback = new List<string>();

        foreach (var def in bestiary.Values)
        {
            if (def.Id == "rust_swarm") continue;   // le repli EST son propre jeu d'animations

            var frames = SpriteFramesLibrary.ForEnemy(def.Id, def.FramesPath);
            if (frames == fallback) sharingFallback.Add(def.Id);
        }

        Check("bestiaire : chaque ennemi a son propre jeu d'animations", sharingFallback.Count == 0,
              sharingFallback.Count == 0
                  ? $"{bestiary.Count} ennemis, aucun sur le repli"
                  : "sur le repli : " + string.Join(", ", sharingFallback));

        // ─── Musique adaptative ───────────────────────────────────────────────
        var musicGo = new GameObject("MusicHost");
        var music = musicGo.AddComponent<MusicDirector>();
        yield return null;

        music.PlayBiome(LevelThreat.Order[0]);
        yield return null;

        Check("musique : demarre sur la piste calme", music.CurrentTrack == "calm",
              $"piste '{music.CurrentTrack}', intensite {music.Intensity:F2}");

        Destroy(musicGo);
        yield return null;
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
