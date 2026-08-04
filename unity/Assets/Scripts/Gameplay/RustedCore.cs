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
/// <para>⚠ Le boss ne poursuit pas le joueur (<c>ai.type = boss_core</c>) : il tient sa position et
/// attaque à distance. C'est ce qui fait de lui un combat d'espace, pas de course.</para>
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

    private void UpdatePhase()
    {
        int next = BossPhases.Advance(Phase, HpRatio);
        if (next == Phase) return;

        Phase = next;
        _surchargeLeft = SurchargeSeconds;
        PhaseChanged?.Invoke(Phase);
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

        switch (signature)
        {
            case BossSignature.Blink:
                // Translocation : réapparaît près du joueur, ce qui interdit le kiting pur.
                if (dist > 220f)
                {
                    Vector2 toward = ((Vector2)player.transform.position - (Vector2)transform.position).normalized;
                    transform.position = (Vector2)player.transform.position - toward * 180f;
                }
                break;

            case BossSignature.FrostNova:
                if (dist < 260f) { DealDiscreteDamage(player, Damage * 1.2f); player.SpeedMultiplier = 0.7f; }
                break;

            case BossSignature.DirectedFan:
            case BossSignature.RotatingBeams:
                if (dist < 320f) DealDiscreteDamage(player, Damage);
                break;

            case BossSignature.MagmaPools:
                // Zone au sol : ne touche que de près, mais persiste — reproduit ici par une frappe
                // de proximité, la zone visuelle appartenant au lot VFX.
                if (dist < 160f) DealDiscreteDamage(player, Damage * 0.8f);
                break;
        }
    }

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
