using Godot;
using DiscordRPC;

/// <summary>
/// Intègre Discord Rich Presence : affiche « joue à Chimera Protocol » sur le statut
/// Discord des joueurs, avec icône, texte contextuel (menus / en run) et chrono de session.
/// Autoload. Tolérant à l'absence de Discord : si le client local n'est pas lancé,
/// l'initialisation échoue silencieusement et le jeu continue normalement (aucun crash).
///
/// Les clés d'images (<c>logo</c>, <c>chimera</c>) doivent correspondre aux Art Assets
/// uploadés dans le Discord Developer Portal (Rich Presence → Art Assets).
/// </summary>
public partial class DiscordPresence : Node
{
    public static DiscordPresence? Instance { get; private set; }

    private const string AppId = "1523258677715406990";
    private const string LargeImageKey = "chimera";
    private const string SmallImageKey = "chimera_small";

    private DiscordRpcClient? _client;
    private Timestamps? _sessionStart;

    public override void _Ready()
    {
        Instance = this;
        try
        {
            _client = new DiscordRpcClient(AppId);
            _client.Initialize();
            _sessionStart = Timestamps.Now;
            SetInMenus();
        }
        catch (System.Exception e)
        {
            // Discord absent / IPC indisponible : on désactive proprement, sans jamais bloquer le jeu.
            GD.Print($"[DiscordPresence] désactivé ({e.Message})");
            _client?.Dispose();
            _client = null;
        }
    }

    /// <summary>Présence « dans les menus » (état par défaut).</summary>
    public void SetInMenus() => Push("In the menus", null);

    /// <summary>Présence « en run » : personnage + biome courant.</summary>
    public void SetInRun(string characterName, string biomeName)
    {
        string details = characterName.Length > 0 ? $"In a run — {characterName}" : "In a run";
        string? state  = biomeName.Length > 0 ? biomeName : null;
        Push(details, state);
    }

    private void Push(string details, string? state)
    {
        if (_client == null) return;
        try
        {
            _client.SetPresence(new RichPresence
            {
                Details    = details,
                State      = state,
                Timestamps = _sessionStart,
                Assets = new Assets
                {
                    LargeImageKey  = LargeImageKey,
                    LargeImageText = $"Chimera Protocol {BuildInfo.Label}",
                    SmallImageKey  = SmallImageKey,
                },
            });
        }
        catch (System.Exception e)
        {
            GD.Print($"[DiscordPresence] SetPresence a échoué ({e.Message})");
        }
    }

    public override void _Notification(int what)
    {
        // Ferme proprement l'IPC à la fermeture de la fenêtre.
        if (what == NotificationWMCloseRequest || what == NotificationPredelete)
            Shutdown();
    }

    public override void _ExitTree() => Shutdown();

    private void Shutdown()
    {
        if (_client == null) return;
        try { _client.ClearPresence(); } catch { /* ignoré */ }
        try { _client.Dispose(); } catch { /* ignoré */ }
        _client = null;
    }
}
