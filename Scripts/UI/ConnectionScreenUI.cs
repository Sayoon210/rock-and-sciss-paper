using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>방 만들기 / 참가, and the host's 매치 시작. Connection events arrive through
/// EventBus, which is where NetworkManager re-emits them; the match start arrives through
/// GameState.MatchStarted, which fires on both sides, so both screens leave together off the
/// same signal rather than one of them guessing.</summary>
public partial class ConnectionScreenUI : Control
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";
    private const string MATCH_SCENE_PATH = "res://Scenes/Screens/MatchScreen.tscn";

    private Button _createRoomButton = null!;
    private LineEdit _addressField = null!;
    private Button _joinButton = null!;
    private Button _startMatchButton = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        _createRoomButton = GetNode<Button>("CenterContainer/Panel/CreateRoomButton");
        _addressField = GetNode<LineEdit>("CenterContainer/Panel/AddressRow/AddressField");
        _joinButton = GetNode<Button>("CenterContainer/Panel/AddressRow/JoinButton");
        _startMatchButton = GetNode<Button>("CenterContainer/Panel/StartMatchButton");
        _statusLabel = GetNode<Label>("CenterContainer/Panel/StatusLabel");

        _addressField.Text = DEFAULT_ADDRESS;

        // 매치 시작 only ever appears on the side that made the room, and only becomes
        // pressable once someone is actually there to play.
        _startMatchButton.Visible = false;
        _startMatchButton.Disabled = true;

        _createRoomButton.Pressed += OnCreateRoomPressed;
        _joinButton.Pressed += OnJoinPressed;
        _startMatchButton.Pressed += OnStartMatchPressed;

        EventBus.Instance!.PeerConnected += OnPeerConnected;
        EventBus.Instance.ConnectionFailed += OnConnectionFailed;
        EventBus.Instance.ServerDisconnected += OnServerDisconnected;

        GameState.Instance!.MatchStarted += OnMatchStarted;
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PeerConnected -= OnPeerConnected;
            EventBus.Instance.ConnectionFailed -= OnConnectionFailed;
            EventBus.Instance.ServerDisconnected -= OnServerDisconnected;
        }

        if (GameState.Instance != null)
        {
            GameState.Instance.MatchStarted -= OnMatchStarted;
        }
    }

    private void OnCreateRoomPressed()
    {
        NetworkManager.Instance!.StartHost();
        if (Multiplayer.MultiplayerPeer == null)
        {
            _statusLabel.Text = "Could not create the room.";
            return;
        }

        LockConnectionControls();
        _startMatchButton.Visible = true;
        _statusLabel.Text = "Waiting for an opponent...";
    }

    private void OnJoinPressed()
    {
        NetworkManager.Instance!.JoinHost(_addressField.Text);
        if (Multiplayer.MultiplayerPeer == null)
        {
            _statusLabel.Text = "Could not start connecting.";
            return;
        }

        LockConnectionControls();
        _statusLabel.Text = string.Format(Tr("Connecting to {0}..."), _addressField.Text);
    }

    private void OnStartMatchPressed()
    {
        GameState.Instance!.HostStartsMatch();
    }

    private void OnPeerConnected(long peerId)
    {
        _statusLabel.Text = "Connected to your opponent.";

        // Harmless on the client, where the button is invisible — which is why this needs no
        // host/client branch of its own.
        _startMatchButton.Disabled = false;
    }

    private void OnConnectionFailed()
    {
        _statusLabel.Text = "Could not connect.";
        UnlockConnectionControls();
    }

    private void OnServerDisconnected()
    {
        _statusLabel.Text = "The connection dropped.";
        UnlockConnectionControls();
    }

    /// <summary>Fires on both sides — the host emits it locally, the client gets it from
    /// MatchStartedRpc — so one handler moves both screens on.</summary>
    private void OnMatchStarted()
    {
        GetTree().ChangeSceneToFile(MATCH_SCENE_PATH);
    }

    private void LockConnectionControls()
    {
        _createRoomButton.Disabled = true;
        _joinButton.Disabled = true;
        _addressField.Editable = false;
    }

    private void UnlockConnectionControls()
    {
        _createRoomButton.Disabled = false;
        _joinButton.Disabled = false;
        _addressField.Editable = true;
        _startMatchButton.Visible = false;
        _startMatchButton.Disabled = true;
    }
}
