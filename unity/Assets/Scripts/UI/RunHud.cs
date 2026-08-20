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
    private AssimilationScreen? _assimilation;
    private FusionBanner? _fusionBanner;

    /// <summary>
    /// Une jauge d'Assimilation est pleine : proposer la greffe. L'écran passe par la file de
    /// modales, donc attend son tour si une montée de niveau est déjà ouverte.
    /// </summary>
    private void OnGaugeFilled(string gauge) => _assimilation?.Present(gauge);

    private void Start()
    {
        _pause   = gameObject.AddComponent<PauseScreen>();
        _levelUp = gameObject.AddComponent<LevelUpScreen>();
        _runEnd  = gameObject.AddComponent<RunEndScreen>();
        _assimilation = gameObject.AddComponent<AssimilationScreen>();
        _fusionBanner = gameObject.AddComponent<FusionBanner>();

        // Les contrôles tactiles. Ajoutés ici plutôt que posés dans la scène par l'éditeur : la
        // scène de jeu est écrite par un script d'éditeur, et un composant qui n'y vit que par une
        // reconstruction manquerait à tout build dont on a oublié de la relancer. Il ne se montre
        // que si un doigt touche la dalle — sur Windows, il ne coûte qu'un objet vide.
        gameObject.AddComponent<TouchHud>();

        Assimilation.GaugeFilled += OnGaugeFilled;

        // ⚠ L'annonce et la fanfare sont branchées ICI, et non dans `InventorySystem.ApplyFusion`.
        // L'inventaire appartient à `Gameplay`, qui ne référence pas `UI` : y appeler un écran serait
        // une dépendance à contresens. Il émet donc l'événement, et c'est ce composant — dont le rôle
        // est précisément de relier les écrans au jeu — qui décide de ce qu'on en montre.
        var inventory = InventorySystem.Instance;
        if (inventory != null) inventory.FusionApplied += OnFusionApplied;

        _pause.QuitRequested += () => SceneRoot.ChangeScene(GameScenes.MainMenu);
        _runEnd.Dismissed    += () => SceneRoot.ChangeScene(GameScenes.MainMenu);
        _levelUp.CardChosen  += OnCardChosen;

        // Renouveler ne referme pas l'écran : on retire une main et on la remplace sur place.
        _levelUp.RerollRequested += () => _levelUp.Replace(DrawCards());

        if (GameManager.Instance != null)
            GameManager.Instance.RunFinished += OnRunFinished;

        if (XpSystem.Instance != null)
            XpSystem.Instance.LevelUp += OnLevelUp;
    }

    private void OnDestroy()
    {
        if (GameManager.Instance != null) GameManager.Instance.RunFinished -= OnRunFinished;
        if (XpSystem.Instance != null)    XpSystem.Instance.LevelUp -= OnLevelUp;
        Assimilation.GaugeFilled -= OnGaugeFilled;

        var inventory = InventorySystem.Instance;
        if (inventory != null) inventory.FusionApplied -= OnFusionApplied;
    }

    /// <summary>
    /// Une fusion vient d'être forgée : la faire <b>exister</b> à l'écran.
    /// </summary>
    /// <remarks>
    /// <para>Jusqu'ici, forger une fusion produisait un son et une ligne de journal. Le reste était
    /// muet : l'écran de montée de niveau se refermait, la run reprenait, et une arme avait changé de
    /// forme quelque part dans la mêlée. C'est pourtant la carte la plus difficile à obtenir du jeu —
    /// elle exige une arme au niveau requis <i>et</i> le passif associé — et le moment où le joueur
    /// mérite de savoir qu'il a réussi quelque chose.</para>
    ///
    /// <para>Deux canaux, parce qu'ils ne s'adressent pas au même regard : la <b>fanfare</b> se joue
    /// dans l'arène, là où le joueur a les yeux ; l'<b>annonce</b> nomme ce qu'il vient d'obtenir,
    /// pour qu'il puisse le retrouver dans son arsenal.</para>
    /// </remarks>
    private void OnFusionApplied(string fusionId, int inheritedLevel)
    {
        _fusionBanner?.Show(fusionId);

        var player = Player.Instance;
        if (player != null) FusionFanfare.Play(player.transform.position, player);
    }

    private void Update()
    {
        // Échap — ou le bouton tactile, seule pause possible sur mobile — ouvre la pause, sauf si une
        // modale est déjà ouverte : deux pauses imbriquées se disputeraient la reprise, et l'une
        // d'elles laisserait le jeu figé.
        if (RawInput.PauseRequestedThisFrame() && !ModalQueue.IsOpen)
            _pause?.Toggle();
    }

    private void OnLevelUp(int level)
    {
        if (_levelUp == null) return;

        _levelUp.Charges = EnsureCharges();

        AudioSystem.PlaySfx("sfx_levelup");
        _levelUp.Present(DrawCards());
    }

    private LevelUpCharges? _charges;

    /// <summary>
    /// Charges de Renouveler / Passer de la run, lues au <b>premier</b> passage de niveau.
    /// </summary>
    /// <remarks>
    /// <para>Ces deux améliorations du Hub étaient achetables, coûtaient des Échos, et ne faisaient
    /// <i>rien</i> : le portage n'avait tout simplement pas ces boutons. Une amélioration payée sans
    /// effet est indiscernable d'un équilibrage raté — le joueur conclut qu'elle est faible, pas
    /// qu'elle est absente.</para>
    ///
    /// <para>⚠ <b>Surtout pas dans <c>Start</c>.</b> L'ordre d'appel des <c>Start</c> entre objets
    /// n'est pas garanti par Unity, et <c>RunBootstrap</c> — qui applique les améliorations imposées
    /// en ligne de commande — a le sien. Lues au démarrage, les charges valaient tantôt celles de la
    /// sauvegarde, tantôt celles du drapeau, <b>selon la frame</b>. Lues au premier passage de
    /// niveau, tous les <c>Start</c> sont derrière nous.</para>
    /// </remarks>
    private LevelUpCharges EnsureCharges()
        => _charges ??= new LevelUpCharges(MetaProgression.LevelOf("reroll"),
                                           MetaProgression.LevelOf("skip"));

    /// <summary>
    /// Tire une main de cartes. <b>Extrait pour être rejouable</b> : le renouvellement doit passer
    /// exactement par le même chemin que la montée de niveau, sinon les deux mains n'obéiraient pas
    /// aux mêmes règles d'exclusion (passifs saturés, arsenal plein, fusions disponibles).
    /// </summary>
    private static IReadOnlyList<LevelUpCard> DrawCards()
    {
        var inv = InventorySystem.Instance;

        // Sans inventaire, les cartes de surcharge restent le filet : une montée de niveau ne doit
        // JAMAIS proposer un choix vide, sous peine de bloquer la run derrière une modale sans bouton.
        if (inv == null) return LevelUpPool.BuildOverload();

        // Les passifs saturés sont retirés de la liste avant le tirage — proposer une carte qui ne
        // rapporte rien est un choix mort, indiscernable d'un bug par le joueur.
        var passiveIds = new List<string>();
        foreach (string id in inv.AllPassiveIds)
            if (!inv.IsPassiveSaturated(id)) passiveIds.Add(id);

        var weaponIds = inv.AllWeaponIds;
        int weaponMax = weaponIds.Count > 0 ? inv.WeaponMaxLevel(weaponIds[0]) : 20;
        int passiveMax = passiveIds.Count > 0 ? inv.PassiveMaxLevel(passiveIds[0]) : 20;

        // Le tirage est PONDÉRÉ par la rareté : commun 60, rare 30, épique 10. Une carte annoncée
        // épique qui tomberait aussi souvent qu'une commune vide son étiquette de son sens.
        return LevelUpPool.Build(
            inv.WeaponLevels, weaponIds, weaponMax,
            inv.PassiveLevels, passiveIds, passiveMax,
            InventorySystem.MaxWeapons,
            inv.AvailableFusions,
            inv.RarityOf,
            WeightedRoll);
    }

    /// <summary>Tirage pondéré : une valeur uniforme dans [0, somme des poids], puis l'index.</summary>
    private static int WeightedRoll(IReadOnlyList<float> weights)
    {
        float total = 0f;
        foreach (float w in weights) total += w;
        if (total <= 0f) return 0;

        return WeightedPicker.PickIndex(weights, Gd.Randf() * total);
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

        // Le multiplicateur d'Échos vient de la SOURCE UNIQUE (palier × cran) : l'écran anime les
        // composantes à partir du même total qu'il crédite. Deux calculs finiraient par diverger, et
        // la somme animée ne collerait plus au montant reçu.
        // ⚠ `cores` valait ZÉRO en dur : l'écran affichait « Noyaux d'Aether : 0 » quoi qu'il arrive,
        // et la part des Noyaux dans les Échos gagnés — jusqu'à 22 selon `EchoParams` — était
        // silencieusement perdue.
        // ⚠ Le record est lu AVANT ReportRun, qui va l'écraser avec le temps de cette run : le lire
        // après ferait comparer la run à elle-même, et « record battu » ne s'afficherait jamais.
        string biome = GameManager.Instance?.CurrentBiomeId ?? RunConfig.BiomeId;
        int previousRecord = GameSettings.SurvivalRecordFor(biome, RunConfig.Saturation);

        _runEnd?.Show(victory, runSeconds: seconds, kills: kills,
                      cores: GameManager.Instance?.CoresCollected ?? 0,
                      tierMult: RunConfig.EchoMult,
                      previousRecord: previousRecord);

        // ⚠ Le montant crédité est celui que l'écran AFFICHE, pas un second calcul : deux formules
        // pour un même total finissent toujours par diverger, et le joueur voit alors une somme qu'il
        // ne reçoit pas.
        if (_runEnd != null) MetaProgression.AddEchoes(_runEnd.EchoesEarned);

        MetaProgression.RegisterRun(kills);

        GameSettings.ReportRun(biome, seconds, victory, RunConfig.Saturation);

        // ⚠ Après RegisterRun et ReportRun : les défis cumulés (« 100 runs », « N biomes terminés »)
        // doivent voir la run qui vient de finir, sinon ils se déclenchent une partie trop tard.
        var inv = InventorySystem.Instance;
        // ⚠ Ces deux comptes étaient passés à ZÉRO en dur, faute de source à brancher. Conséquence
        // muette : « Moissonneur de Noyaux » et « Pleine Chimère » ne pouvaient <b>jamais</b>
        // s'accomplir — deux défis affichés, décrits, et inatteignables.
        var earned = ChallengeSystem.EvaluateRunEnd(
            runSeconds: seconds, kills: kills,
            cores: GameManager.Instance?.CoresCollected ?? 0,
            levelCompleted: victory, biomeId: biome,
            difficultyRank: RunConfig.Saturation,
            graftsEquipped: Assimilation.Equipped.Count,
            fusionForged: inv != null && inv.AppliedFusions.Count > 0);

        foreach (var def in earned)
            Debug.Log($"[RunHud] defi accompli : {def.Id}");
    }
}
