using Godot;

namespace RockAndScissPaper.Autoload;

/// <summary>Pure signal relay for cross-autoload notifications — holds no state of its own.
/// Autoloads must not reference each other directly (Scripts/Autoload/CLAUDE.md), so this
/// is the one hub every other Autoload is allowed to know about: NetworkManager re-emits
/// Godot's connection events through here, and GameState listens here instead of holding a
/// reference to NetworkManager.</summary>
public partial class EventBus : Node
{
    public static EventBus? Instance { get; private set; }

    [Signal]
    public delegate void PeerConnectedEventHandler(long peerId);

    [Signal]
    public delegate void PeerDisconnectedEventHandler(long peerId);

    [Signal]
    public delegate void ServerDisconnectedEventHandler();

    [Signal]
    public delegate void ConnectionFailedEventHandler();

    public override void _EnterTree()
    {
        Instance = this;
    }
}
