using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Prototype de banc — Lot 1 de la migration (docs/UNITY_MIGRATION_PLAN.md §13).
/// Il n'est PAS du code de jeu : il existe pour répondre à deux questions qui peuvent invalider
/// la méthode de migration entière, et il sera supprimé une fois qu'elles seront tranchées.
///
/// <para><b>R2 — le banc tourne-t-il headless plus vite que le temps réel ?</b>
/// Toute la méthodologie de mesure du projet (campagnes appariées, test des signes) suppose qu'on
/// puisse simuler des minutes de jeu en moins de temps qu'elles n'en durent. Sous Godot, le constat
/// mesuré était ×1,0 en nuée (CPU-bound). On mesure ici l'équivalent Unity.</para>
///
/// <para><b>R3 — 300 entités tiennent-elles la cadence ?</b>
/// La cible du projet est 200-300 entités simultanées. On reproduit la charge RÉELLE du jeu,
/// telle qu'établie en lisant le code Godot :
/// <list type="bullet">
///   <item>déplacement de chaque ennemi vers le joueur — O(n) ;</item>
///   <item>séparation joueur↔ennemi uniquement — O(n), et NON ennemi↔ennemi en O(n²)
///         (<c>Player.PushEnemiesAside</c>) ;</item>
///   <item>dégâts de contact par DISTANCE et non par collision (<c>EnemyBase</c>) ;</item>
///   <item>collision physique avec les seuls obstacles statiques
///         (<c>EnemyBase.CollisionMask = 2</c> — les ennemis se traversent entre eux et
///         traversent le joueur).</item>
/// </list>
/// C'est ce dernier point qui rend la charge légère : il n'y a aucune physique dynamique à n corps.
/// </para>
///
/// <para>Le pas de simulation est FIXE et indépendant de <c>Time.deltaTime</c> : on mesure un débit
/// (secondes simulées par seconde de horloge), exactement comme le fait <c>--timescale</c> côté
/// Godot. Piloter la simulation par <c>deltaTime</c> mesurerait la cadence d'affichage, pas la
/// capacité de calcul.</para>
/// </summary>
public sealed class BenchProto : MonoBehaviour
{
    // ─── Paramètres (surchargés par la ligne de commande) ─────────────────────
    private int   _entityCount    = 300;
    private float _simSeconds     = 60f;
    private bool  _render         = false;

    private const float FixedDt        = 1f / 60f;   // pas de simulation fixe
    private const float EnemySpeed     = 90f;
    private const float PlayerSpeed    = 220f;
    private const float PlayerRadius   = 14f;
    private const float ContactRadius  = 24f;
    private const int   ObstacleCount  = 24;
    private const float ObstacleRadius = 46f;
    private const float ArenaHalf      = 900f;

    // ─── État de simulation (tableaux plats : pas d'allocation par frame) ─────
    private Vector2[]   _pos;
    private Vector2[]   _vel;
    private Transform[] _tr;
    private Vector2[]   _obstacles;

    private Vector2 _playerPos;
    private float   _simTime;
    private long    _steps;
    private int     _contactHits;

    private Stopwatch _clock;
    private bool      _done;

    // Coût CPU pur de la simulation, hors rendu et hors reste de la boucle Unity.
    private double _simTicks;

    private void Awake()
    {
        ParseArgs();

        // Ne jamais laisser la boucle se faire brider : on mesure une capacité de calcul.
        QualitySettings.vSyncCount    = 0;
        Application.targetFrameRate   = -1;

        _pos       = new Vector2[_entityCount];
        _vel       = new Vector2[_entityCount];
        _obstacles = new Vector2[ObstacleCount];

        var rng = new System.Random(12345);   // déterministe : deux exécutions sont comparables
        for (int i = 0; i < _entityCount; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2.0;
            double r = 300.0 + rng.NextDouble() * 500.0;
            _pos[i] = new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
        }
        for (int i = 0; i < ObstacleCount; i++)
        {
            double a = rng.NextDouble() * Math.PI * 2.0;
            double r = 150.0 + rng.NextDouble() * 700.0;
            _obstacles[i] = new Vector2((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
        }

        if (_render) BuildVisuals();

        _clock = Stopwatch.StartNew();
        Log($"demarrage — entites={_entityCount} sim={_simSeconds}s rendu={_render} " +
            $"backend={Application.platform} il2cpp={IsIl2Cpp()}");
    }

    private void Update()
    {
        if (_done) return;

        long t0 = Stopwatch.GetTimestamp();
        Step(FixedDt);
        _simTicks += Stopwatch.GetTimestamp() - t0;

        if (_render) SyncVisuals();

        if (_simTime >= _simSeconds) Finish();
    }

    /// <summary>Un pas de simulation, calqué sur la charge réelle du jeu (voir en-tête).</summary>
    private void Step(float dt)
    {
        _simTime += dt;
        _steps++;

        // Le joueur kite en cercle : il bouge vraiment, donc la séparation travaille vraiment.
        float ang = _simTime * 0.6f;
        _playerPos = new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * 420f
                   + new Vector2(Mathf.Cos(ang * 3.1f), Mathf.Sin(ang * 2.7f)) * 90f;

        for (int i = 0; i < _entityCount; i++)
        {
            Vector2 p  = _pos[i];
            Vector2 to = _playerPos - p;
            float   d  = to.magnitude;
            if (d > 0.001f) _vel[i] = to / d * EnemySpeed;

            Vector2 next = p + _vel[i] * dt;

            // Collision avec les obstacles STATIQUES uniquement (EnemyBase.CollisionMask = 2).
            for (int o = 0; o < ObstacleCount; o++)
            {
                Vector2 off = next - _obstacles[o];
                float   od  = off.magnitude;
                if (od < ObstacleRadius && od > 0.001f)
                    next = _obstacles[o] + off / od * ObstacleRadius;
            }

            // Bornage d'arène.
            next.x = Mathf.Clamp(next.x, -ArenaHalf, ArenaHalf);
            next.y = Mathf.Clamp(next.y, -ArenaHalf, ArenaHalf);

            // Séparation joueur↔ennemi (Player.PushEnemiesAside) — O(n), pas O(n²).
            Vector2 sep  = next - _playerPos;
            float   sd   = sep.magnitude;
            float   minD = Mathf.Max(PlayerRadius, ContactRadius - 6f);
            if (sd < minD)
                next = _playerPos + (sd > 0.01f ? sep / sd : Vector2.right) * minD;

            // Dégâts de contact par DISTANCE (EnemyBase), pas par collision.
            if (sd < ContactRadius) _contactHits++;

            _pos[i] = next;
        }
    }

    private void Finish()
    {
        _done = true;
        _clock.Stop();

        double wall      = _clock.Elapsed.TotalSeconds;
        double simPerSec = _simSeconds / wall;
        double simMs     = _simTicks / (double)Stopwatch.Frequency * 1000.0;
        double msPerStep = simMs / Math.Max(1, _steps);

        var sb = new StringBuilder();
        sb.AppendLine("=== BENCH PROTO — resultat ===");
        sb.AppendLine($"entites            : {_entityCount}");
        sb.AppendLine($"rendu              : {_render}");
        sb.AppendLine($"IL2CPP             : {IsIl2Cpp()}");
        sb.AppendLine($"pas simules        : {_steps}");
        sb.AppendLine($"temps simule       : {_simSeconds:F1} s");
        sb.AppendLine($"temps horloge      : {wall:F2} s");
        sb.AppendLine($"DEBIT              : x{simPerSec:F2} temps reel");
        sb.AppendLine($"cout CPU simu      : {msPerStep:F4} ms/pas ({simMs:F0} ms cumules)");
        sb.AppendLine($"IPS equivalents    : {(msPerStep > 0 ? 1000.0 / msPerStep : 0):F0} (simu seule)");
        sb.AppendLine($"contacts (sanite)  : {_contactHits}");
        Log(sb.ToString());

        WriteReport(sb.ToString());
        Quit();
    }

    // ─── Rendu optionnel (R3) ─────────────────────────────────────────────────

    private void BuildVisuals()
    {
        var cam = new GameObject("Cam").AddComponent<Camera>();
        cam.orthographic     = true;
        cam.orthographicSize = 540f;
        cam.transform.position = new Vector3(0, 0, -10);
        cam.backgroundColor  = new Color(0.10f, 0.10f, 0.18f);
        cam.clearFlags       = CameraClearFlags.SolidColor;

        var tex = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var px  = new Color32[32 * 32];
        for (int y = 0; y < 32; y++)
        for (int x = 0; x < 32; x++)
        {
            float dx = x - 15.5f, dy = y - 15.5f;
            bool inside = dx * dx + dy * dy < 196f;
            px[y * 32 + x] = inside ? new Color32(170, 68, 255, 255) : new Color32(0, 0, 0, 0);
        }
        tex.SetPixels32(px);
        tex.Apply();
        var sprite = Sprite.Create(tex, new Rect(0, 0, 32, 32), new Vector2(0.5f, 0.5f), 1f);

        _tr = new Transform[_entityCount];
        for (int i = 0; i < _entityCount; i++)
        {
            var go = new GameObject("e" + i);
            go.AddComponent<SpriteRenderer>().sprite = sprite;
            _tr[i] = go.transform;
        }
    }

    private void SyncVisuals()
    {
        for (int i = 0; i < _entityCount; i++)
            _tr[i].position = new Vector3(_pos[i].x, _pos[i].y, 0f);
    }

    // ─── Ligne de commande / sortie ───────────────────────────────────────────

    private void ParseArgs()
    {
        foreach (string a in Environment.GetCommandLineArgs())
        {
            if (a.StartsWith("--entities=", StringComparison.Ordinal))
                int.TryParse(a.Substring(11), NumberStyles.Integer, CultureInfo.InvariantCulture, out _entityCount);
            else if (a.StartsWith("--sim-seconds=", StringComparison.Ordinal))
                float.TryParse(a.Substring(14), NumberStyles.Float, CultureInfo.InvariantCulture, out _simSeconds);
            else if (a == "--render")
                _render = true;
        }
        if (_entityCount < 1)  _entityCount = 300;
        if (_simSeconds < 1f)  _simSeconds  = 60f;
    }

    private static bool IsIl2Cpp()
    {
#if ENABLE_IL2CPP
        return true;
#else
        return false;
#endif
    }

    private static void Log(string s) => UnityEngine.Debug.Log("[BENCH] " + s);

    private void WriteReport(string body)
    {
        try
        {
            string dir = Path.Combine(Application.persistentDataPath, "bench");
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "proto_result.txt"), body);
            Log("rapport ecrit dans " + dir);
        }
        catch (Exception e) { Log("echec ecriture rapport : " + e.Message); }
    }

    private static void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
