using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Assemble l'interface d'une run et la relie au jeu (Lot 5).
///
/// <para>Ce composant existe pour que les écrans n'aient pas à se connaître entre eux : le HUD, la
/// pause, la montée de niveau et le bilan de fin sont indépendants, et c'est <b>ici</b> que leurs
/// branchements sont déclarés. Sans ce point unique, chaque écran finirait par référencer les
/// autres, et l'ordre de leurs initialisations deviendrait un piège.</para>
/// </summary>
[RequireComponent(typeof(HUD))]
public sealed class RunHud : MonoBehaviour
{
    private PauseScreen? _pause;
    private LevelUpScreen? _levelUp;
    private RunEndScreen? _runEnd;

    private void Start()
    {
        _pause   = gameObject.AddComponent<PauseScreen>();
        _levelUp = gameObject.AddComponent<LevelUpScreen>();
        _runEnd  = gameObject.AddComponent<RunEndScreen>();

        _pause.QuitRequested += () => SceneRoot.ChangeScene(GameScenes.MainMenu);
        _runEnd.Dismissed    += () => SceneRoot.ChangeScene(GameScenes.MainMenu);
        _levelUp.CardChosen  += OnCardChosen;

        if (GameManager.Instance != null)
            GameManager.Instance.RunFinished += OnRunFinished;

        if (XpSystem.Instance != null)
            XpSystem.Instance.LevelUp += OnLevelUp;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.RunFinished -= OnRunFinished;
        if (XpSystem.Instance != null)    XpSystem.Instance.LevelUp -= OnLevelUp;
    }

    private void Update()
    {
        // Échap ouvre la pause — sauf si une modale est déjà ouverte : deux pauses imbriquées se
        // disputeraient la reprise, et l'une d'elles laisserait le jeu figé.
        if (Input.GetKeyDown(KeyCode.Escape) && !ModalQueue.IsOpen)
            _pause?.Toggle();
    }

    private void OnLevelUp(int level)
    {
        if (_levelUp == null) return;

        var inv = InventorySystem.Instance;

        // Sans inventaire, les cartes de surcharge restent le filet : une montée de niveau ne doit
        // JAMAIS proposer un choix vide, sous peine de bloquer la run derrière une modale sans bouton.
        if (inv == null) { _levelUp.Present(LevelUpPool.BuildOverload()); return; }

        // Les passifs saturés sont retirés de la liste avant le tirage — proposer une carte qui ne
        // rapporte rien est un choix mort, indiscernable d'un bug par le joueur.
        var passiveIds = new List<string>();
        foreach (string id in inv.AllPassiveIds)
            if (!inv.IsPassiveSaturated(id)) passiveIds.Add(id);

        var weaponIds = inv.AllWeaponIds;
        int weaponMax = weaponIds.Count > 0 ? inv.WeaponMaxLevel(weaponIds[0]) : 20;
        int passiveMax = passiveIds.Count > 0 ? inv.PassiveMaxLevel(passiveIds[0]) : 20;

        var cards = LevelUpPool.Build(
            inv.WeaponLevels, weaponIds, weaponMax,
            inv.PassiveLevels, passiveIds, passiveMax,
            InventorySystem.MaxWeapons,
            inv.AvailableFusions,
            n => (int)(Gd.Randi() % (uint)Mathf.Max(1, n)));

        _levelUp.Present(cards);
    }

    /// <summary>
    /// Applique la carte choisie. <b>C'est ici que le choix devient un effet</b> : sans ce
    /// branchement, l'écran se ferme, la run reprend, et rien n'a changé — le mode de défaillance le
    /// plus muet du jeu, puisque tout continue de fonctionner.
    /// </summary>
    private void OnCardChosen(LevelUpCard card)
    {
        var inv = InventorySystem.Instance;
        if (inv == null) return;

        switch (card.Kind)
        {
            case LevelUpCardKind.NewWeapon:
            case LevelUpCardKind.WeaponUpgrade:
                inv.AcquireOrLevelUp(card.Id);
                break;

            case LevelUpCardKind.Passive:
                inv.AddOrUpgradePassive(card.Id);
                break;

            case LevelUpCardKind.Fusion:
                inv.ApplyFusion(card.Id);
                break;

            case LevelUpCardKind.Overload:
                inv.ApplyOverload(card.Id);
                break;
        }
    }

    private void OnRunFinished(float runTime, int kills)
    {
        // « Victoire » = le Noyau Rouillé est tombé. La run, elle, ne s'arrête qu'à la mort : battre
        // le boss marque la complétion du niveau, il ne met pas fin à la partie.
        bool victory = GameManager.Instance?.BossDefeated ?? false;
        int seconds = Mathf.RoundToInt(runTime);

        _runEnd?.Show(victory, runSeconds: seconds, kills: kills, cores: 0);

        // ⚠ Le montant crédité est celui que l'écran AFFICHE, pas un second calcul : deux formules
        // pour un même total finissent toujours par diverger, et le joueur voit alors une somme qu'il
        // ne reçoit pas.
        if (_runEnd != null) MetaProgression.AddEchoes(_runEnd.EchoesEarned);

        MetaProgression.RegisterRun(kills);

        string biome = GameManager.Instance?.CurrentBiomeId ?? "sanctuaire";
        GameSettings.ReportRun(biome, seconds, victory, GameSettings.SaturationFor(biome));
    }
}
