using System;
using UnityEngine;

/// <summary>
/// Noyau Rouillé — <b>condition de victoire des cinq niveaux</b> (Lot 4).
///
/// <para>Trois <b>phases</b> (100 → 66 → 33 → 0 % de PV) qui resserrent ses cadences, et une
/// <b>incarnation par biome</b> qui change sa signature d'attaque. Toute la table des phases et des
/// incarnations vit dans <see cref="BossPhases"/> et <see cref="BossIncarnations"/> — logique pure
/// partagée avec Godot, donc mêmes seuils et mêmes cadences par construction.</para>
///
/// <para><b>La progression de phase est irréversible.</b> Se soigner ne doit jamais faire reculer le
/// boss d'une phase : sinon un combat long pourrait osciller sans fin autour d'un seuil, et la
/// surcharge de bascule se rejouerait en boucle. <see cref="BossPhases.Advance"/> porte cette
/// garantie.</para>
///
/// <para>⚠ Le boss <b>avance</b> vers le joueur, mais lentement (46 px/s de fiche, ×1,18 en phase
/// III) : c'est un combat d'espace, pas de course — il laisse le temps de manœuvrer sans jamais
/// offrir de répit. Le port l'avait figé sur place, et un boss immobile se contourne et s'oublie.
/// Il ne s'arrête que pendant la <b>surcharge</b> de bascule de phase, qui est le télégraphe.</para>
/// </summary>
public sealed class RustedCore : EnemyBase
{
    /// <summary>Durée de la surcharge télégraphiée à chaque bascule de phase.</summary>
    private const float SurchargeSeconds = BossPhases.TransitionSeconds;

    /// <summary>Incarnation courante, choisie par le biome.</summary>
    public BossIncarnation Incarnation { get; private set; } = BossIncarnations.Root;

    /// <summary>Phase courante, de 0 à 2.</summary>
    public int Phase { get; private set; }

    /// <summary>Fraction de PV restante, entre 0 et 1 — lue par la barre de boss du HUD.</summary>
    public float HpRatio => MaxHp > 0f ? Mathf.Clamp01(CurrentHp / MaxHp) : 0f;

    /// <summary>Le boss est-il en surcharge (bascule de phase) ? Il n'attaque pas pendant ce temps.</summary>
    public bool IsSurcharging => _surchargeLeft > 0f;

    /// <summary>Nom affiché, dérivé de l'incarnation.</summary>
    public string DisplayName => Incarnation.NameKey;

    /// <summary>Émis à chaque bascule, avec la nouvelle phase.</summary>
    public event Action<int>? PhaseChanged;

    /// <summary>Émis quand le boss tire sa signature — l'effet visuel s'y branche.</summary>
    public event Action<BossSignature>? SignatureFired;

    /// <summary>Tirs de signature effectués — observable pour les tests et le HUD.</summary>
    public int SignatureCount { get; private set; }

    /// <summary>Vagues d'adds invoquées — observable pour les tests.</summary>
    public int AddWaves { get; private set; }

    private float _surchargeLeft;
    private float _signatureTimer;
    private float _addsTimer;

    /// <summary>Vitesse de fiche, avant le facteur de phase. Figée à la première apparition.</summary>
    private float _baseSpeed;

    /// <summary>
    /// Déplacement du boss : il avance vers le joueur à sa vitesse de phase, et <b>s'immobilise
    /// pendant la surcharge</b> — c'est ce qui rend la bascule lisible.
    /// </summary>
    protected override void UpdateMovement(Player player, float dt)
    {
        if (_baseSpeed <= 0f) _baseSpeed = Speed;

        if (IsSurcharging) return;

        Speed = _baseSpeed * BossPhases.SpeedMult(Phase);
        base.UpdateMovement(player, dt);
    }

    /// <summary>Prefab des renforts invoqués en phase III.</summary>
    public GameObject? AddPrefab;

    protected override float ContactRadius => 72f;

    protected override void Awake()
    {
        base.Awake();
        Ai = EnemyTable.AiType.BossCore;
    }

    /// <summary>Choisit l'incarnation correspondant au biome. À appeler avant l'apparition.</summary>
    public void SetBiome(string? biomeId)
    {
        Incarnation = BossIncarnations.For(biomeId);

        var sr = GetComponentInChildren<SpriteRenderer>();
        if (sr != null) sr.color = new Color(Incarnation.TintR, Incarnation.TintG, Incarnation.TintB);
    }

    protected override void Update()
    {
        base.Update();
        if (IsDead) return;

        float dt = Time.deltaTime;

        // La surcharge suspend les attaques : c'est le télégraphe qui rend la bascule lisible.
        if (_surchargeLeft > 0f) { _surchargeLeft -= dt; return; }

        UpdatePhase();
        UpdateSignature(dt);
        UpdateAdds(dt);
    }

    /// <summary>
    /// Encaisse un coup, et alimente le chronométrage du combat.
    /// </summary>
    /// <remarks>
    /// Le chrono ne démarre qu'ici, au <b>premier dégât</b> : le boss arrive à distance, et le temps
    /// d'approche n'appartient pas au combat. Le ratio de PV est relevé à chaque coup pour qu'une run
    /// interrompue dise quand même où en était le combat — un boss laissé à 40 % en dit plus long
    /// qu'une victoire.
    /// </remarks>
    public override void TakeDamage(float amount)
    {
        base.TakeDamage(amount);

        BossTelemetry.NotifyFirstDamage();
        BossTelemetry.NotifyHpRatio(HpRatio);
    }

    private void UpdatePhase()
    {
        int next = BossPhases.Advance(Phase, HpRatio);
        if (next == Phase) return;

        Phase = next;
        BossTelemetry.NotifyPhase(Phase, HpRatio);
        _surchargeLeft = SurchargeSeconds;
        PhaseChanged?.Invoke(Phase);

        // La bascule de phase est TÉLÉGRAPHIÉE : une seconde de surcharge pendant laquelle le boss
        // ne frappe pas. Sans onde ni secousse, cette seconde ne se distingue pas d'un temps mort,
        // et le joueur n'apprend jamais que le combat vient de changer de régime.
        var tint = new Color(Incarnation.TintR, Incarnation.TintG, Incarnation.TintB);
        Vfx.Shockwave(transform.position, 220f, SurchargeSeconds, tint);
        Vfx.Glow(transform.position, tint, 70f, 0.8f, SurchargeSeconds);
        ScreenShake.Shake(14f, 0.5f);
    }

    private void UpdateSignature(float dt)
    {
        _signatureTimer -= dt;
        if (_signatureTimer > 0f) return;

        // La cadence de signature se resserre avec la phase — même table que sous Godot.
        _signatureTimer = BossPhases.SignatureInterval(Phase, Incarnation.BaseIntervalSec);

        SignatureCount++;
        SignatureFired?.Invoke(Incarnation.Signature);
        FireSignature(Incarnation.Signature);
    }

    /// <summary>
    /// Effet de la signature. Les dégâts passent par le chemin des coups <b>discrets</b>, donc par
    /// les i-frames du joueur : un boss qui les contournerait tuerait en une frame.
    /// </summary>
    private void FireSignature(BossSignature signature)
    {
        var player = Player.Instance;
        if (player == null || player.IsDead) return;

        float dist = Vector2.Distance(transform.position, player.transform.position);
        Vector2 self = transform.position;
        Vector2 toward = ((Vector2)player.transform.position - self).normalized;
        var tint = new Color(Incarnation.TintR, Incarnation.TintG, Incarnation.TintB);

        switch (signature)
        {
            case BossSignature.Blink:
                // Translocation : réapparaît près du joueur, ce qui interdit le kiting pur.
                if (dist > 220f)
                {
                    Vfx.Shockwave(self, 90f, 0.25f, tint);
                    transform.position = (Vector2)player.transform.position - toward * 180f;
                    Vfx.Shockwave(transform.position, 110f, 0.3f, tint);
                    Vfx.Glow(transform.position, tint, 40f, 0.8f, 0.3f);
                    ScreenShake.Shake(5f, 0.2f);
                }
                break;

            case BossSignature.FrostNova:
                Vfx.Shockwave(self, 260f, 0.4f, new Color(0.6f, 0.92f, 1f));
                Vfx.Burst(self, new Color(0.85f, 0.97f, 1f, 0.9f), new Color(0.5f, 0.8f, 1f, 0f),
                          40, 220f, 520f, 9f, 0.4f);
                ScreenShake.Shake(7f, 0.25f);
                if (dist < 260f) { DealDiscreteDamage(player, Damage * 1.2f); player.SpeedMultiplier = 0.7f; }
                break;

            case BossSignature.DirectedFan:
                Vfx.Cone(self, toward, 40f, 320f, tint, 0.3f, 4f);
                Vfx.Flame(self, toward, 80f, 200f);
                ScreenShake.Shake(6f, 0.25f);
                if (dist < 320f) DealDiscreteDamage(player, Damage);
                break;

            case BossSignature.RotatingBeams:
                // Quatre faisceaux en croix, décalés d'un tir à l'autre : c'est leur ROTATION qui
                // fait la signature, et un seul trait vers le joueur ne la montrerait pas.
                for (int i = 0; i < 4; i++)
                {
                    float a = (SignatureCount * 23f + i * 90f) * Mathf.Deg2Rad;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vfx.Beam(self, self + dir * 320f, tint, 5f, 0.3f);
                }
                ScreenShake.Shake(6f, 0.25f);
                if (dist < 320f) DealDiscreteDamage(player, Damage);
                break;

            case BossSignature.MagmaPools:
                // Zone au sol : ne touche que de près, mais persiste — reproduit ici par une frappe
                // de proximité. Les flaques marquent CE QUI BRÛLE ; sans elles, le joueur perd des
                // PV au corps à corps sans distinguer la flaque du contact du boss.
                for (int i = 0; i < 3; i++)
                {
                    float a = (SignatureCount * 47f + i * 120f) * Mathf.Deg2Rad;
                    var at = self + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 90f;
                    Vfx.Ring(at, 55f, new Color(1f, 0.45f, 0.15f), 4f, 1.2f);
                    Vfx.Burst(at, new Color(1f, 0.8f, 0.35f, 0.9f), new Color(1f, 0.25f, 0.05f, 0f),
                              14, 20f, 90f, 11f, 0.8f, 0f, 360f, VfxPrimitives.OrderGround);
                }
                ScreenShake.Shake(4f, 0.2f);
                if (dist < 160f) DealDiscreteDamage(player, Damage * 0.8f);
                break;
        }
    }

    /// <summary>
    /// Sa chute <b>complète le niveau</b> — elle ne met pas fin à la run, qui ne s'arrête qu'à la
    /// mort du joueur. C'est la seule condition de victoire des cinq biomes.
    /// </summary>
    protected override void Die()
    {
        if (IsDead) return;

        BossTelemetry.NotifyKill();
        GameManager.Instance?.RegisterBossDefeated();

        // Le seul coup du jeu qui conclut un niveau : il s'entend dans le TEMPS. Sans ce ralenti, le
        // boss disparaît exactement comme un ennemi de base — et la fin du niveau se déduit d'une
        // absence.
        HitStop.Trigger();
        ScreenShake.Shake(9f, 0.5f);

        // Trois Noyaux en couronne autour du cadavre, comme sous Godot. En couronne et non empilés :
        // le joueur les ramasse un par un, ce qui étire la récompense sur quelques secondes au lieu
        // de la donner d'un pas — et ces secondes-là se passent au milieu de l'arène, sans boss pour
        // la nettoyer.
        for (int i = 0; i < BossCoreDrop; i++)
        {
            float angle = 2f * Mathf.PI * i / BossCoreDrop;
            var offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * BossCoreSpread;
            AetherCoreSpawner.SpawnAt(transform.position + offset);
        }

        base.Die();
    }

    /// <summary>Noyaux laissés par le boss.</summary>
    private const int BossCoreDrop = 3;

    /// <summary>Rayon de la couronne de Noyaux, en pixels.</summary>
    private const float BossCoreSpread = 48f;

    private void UpdateAdds(float dt)
    {
        if (!BossPhases.SummonsAdds(Phase) || AddPrefab == null) return;

        _addsTimer -= dt;
        if (_addsTimer > 0f) return;

        _addsTimer = BossPhases.AddsIntervalSeconds;
        AddWaves++;

        for (int i = 0; i < BossPhases.AddsPerWave; i++)
        {
            float angle = i * Mathf.PI * 2f / BossPhases.AddsPerWave;
            Vector2 pos = (Vector2)transform.position
                        + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * 120f;

            var go = Instantiate(AddPrefab, pos, Quaternion.identity);
            go.SetActive(true);   // sémantique Godot
        }
    }
}
