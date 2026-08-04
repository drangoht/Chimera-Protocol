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
        if (inv == null) { _levelUp.Present(LevelUpPool.BuildOverload()); return; }

        // Le pool réel viendra du câblage complet de l'inventaire ; à ce stade, les cartes de
        // surcharge garantissent qu'une montée de niveau ne propose JAMAIS un choix vide.
        _levelUp.Present(LevelUpPool.BuildOverload());
    }

    private void OnRunFinished(float runTime, int kills)
    {
        _runEnd?.Show(victory: false, runSeconds: Mathf.RoundToInt(runTime), kills: kills, cores: 0);
    }
}
