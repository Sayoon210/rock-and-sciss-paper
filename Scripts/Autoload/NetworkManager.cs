using Godot;

namespace RockAndScissPaper.Autoload;

/// <summary>Owns Multiplayer.MultiplayerPeer exclusively — nothing else creates or replaces
/// it (Scripts/Autoload/CLAUDE.md). Connection events are re-emitted through EventBus
/// rather than exposed as direct signals here, so GameState never has to reference this
/// Autoload to learn about them.</summary>
public partial class NetworkManager : Node
{
    // Fixed port for this 1:1 game — no lobby/port picker in this pass.
    private const int PORT = 7777;

    // A strict 1:1 game: refusing the third connection at the transport layer means no
    // application code ever has to reject an over-limit peer itself.
    private const int MAX_CLIENTS = 1;

    public static NetworkManager? Instance { get; private set; }

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        Multiplayer.PeerConnected += OnPeerConnected;
        Multiplayer.PeerDisconnected += OnPeerDisconnected;
        Multiplayer.ServerDisconnected += OnServerDisconnected;
        Multiplayer.ConnectionFailed += OnConnectionFailed;
    }

    public void StartHost()
    {
        ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
        Error error = peer.CreateServer(PORT, MAX_CLIENTS);
        if (error != Error.Ok)
        {
            GD.PrintErr($"NetworkManager: failed to start host on port {PORT} ({error}).");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
    }

    public void JoinHost(string address)
    {
        ENetMultiplayerPeer peer = new ENetMultiplayerPeer();
        Error error = peer.CreateClient(address, PORT);
        if (error != Error.Ok)
        {
            GD.PrintErr($"NetworkManager: failed to join {address}:{PORT} ({error}).");
            return;
        }

        Multiplayer.MultiplayerPeer = peer;
    }

    private void OnPeerConnected(long id)
    {
        // The generated EmitSignalX helpers are protected (only usable from inside the
        // declaring class), so relaying into another Autoload goes through the base
        // EmitSignal(StringName, ...) instead.
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PeerConnected, id);
    }

    private void OnPeerDisconnected(long id)
    {
        EventBus.Instance?.EmitSignal(EventBus.SignalName.PeerDisconnected, id);
    }

    private void OnServerDisconnected()
    {
        EventBus.Instance?.EmitSignal(EventBus.SignalName.ServerDisconnected);
    }

    private void OnConnectionFailed()
    {
        EventBus.Instance?.EmitSignal(EventBus.SignalName.ConnectionFailed);
    }
}
