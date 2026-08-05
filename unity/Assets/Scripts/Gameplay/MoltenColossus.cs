using UnityEngine;

/// <summary>
/// Colosse en Fusion — champion de la <b>Fournaise</b>. Charge en ligne droite en laissant un
/// sillage brûlant : la réponse est de <b>se décaler latéralement</b>, jamais de reculer.
/// </summary>
public sealed class MoltenColossus : MiniBoss
{
    [Tooltip("Dégâts par seconde du sillage laissé pendant la charge.")]
    public float TrailDps = 8f;

    [Tooltip("Durée de la brûlure appliquée au contact du sillage.")]
    public float TrailBurnDuration = 1.5f;

    protected override void Awake()
    {
        base.Awake();
        Ai = EnemyTable.AiType.ChargingBruiser;
        ChampionContactRadius = 40f;
    }

    private float _emberTimer;

    protected override void Update()
    {
        base.Update();
        if (IsDead) return;

        // Le sillage de magma était une zone de dégâts INVISIBLE : le joueur perdait des PV en
        // suivant le colosse sans jamais pouvoir apprendre qu'il fallait se décaler. Les braises
        // marquent la zone qui brûle, à sa taille réelle.
        _emberTimer -= Time.deltaTime;
        if (_emberTimer > 0f) return;
        _emberTimer = 0.12f;

        Vfx.Burst(transform.position, new Color(1f, 0.75f, 0.3f, 0.9f), new Color(1f, 0.25f, 0.05f, 0f),
                  6, 10f, 45f, 9f, 0.5f, 0f, 360f, VfxPrimitives.OrderGround);
    }

    protected override void HandleContactDamage(Player player, float dt)
    {
        base.HandleContactDamage(player, dt);

        // Le sillage brûle sans passer par les coups discrets : c'est un dégât CONTINU.
        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist < ChampionContactRadius * 1.8f) player.TakeDamage(TrailDps * dt);
    }
}
