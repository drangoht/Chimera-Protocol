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

    protected override void HandleContactDamage(Player player, float dt)
    {
        base.HandleContactDamage(player, dt);

        // Le sillage brûle sans passer par les coups discrets : c'est un dégât CONTINU.
        float dist = Vector2.Distance(transform.position, player.transform.position);
        if (dist < ChampionContactRadius * 1.8f) player.TakeDamage(TrailDps * dt);
    }
}
