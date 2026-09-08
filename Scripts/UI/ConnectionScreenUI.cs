using System;
using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>방 만들기 / 참가, and the host's 매치 시작. Connection events arrive through
/// EventBus, which is where NetworkManager re-emits them; the match start arrives through
/// GameState.MatchStarted, which fires on both sides, so both screens leave together off the
/// same signal rather than one of them guessing.
///
/// A panel inside TitleScreen.tscn rather than a scene of its own. It used to be
/// ConnectionScreen.tscn, reached with ChangeSceneToFile — which threw away the 3D table
/// behind the menu and built it again, for a step that is really just the next few lines of
/// the same menu. Now the title stays put and this takes the main list's place under it.
///
/// The one thing that arrangement adds is a way back, which a scene of its own never needed
/// (there was no back route out of it at all). Backing out has to undo the connection as well
/// as the panel: a room left open would still be sitting there the next time 방 만들기 is
/// pressed. That is the same teardown ScreenRouter.GoToTitleScreen does on its way out of the
/// match, minus the scene change — the connection is being abandoned either way.</summary>
public partial class ConnectionScreenUI : VBoxContainer
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";

    private const string CREATE_ROOM_BUTTON_PATH = "CreateRoomRow/CreateRoomButton";
    private const string JOIN_BUTTON_PATH = "JoinRow/JoinButton";
    private const string ADDRESS_FIELD_PATH = "JoinRow/AddressField";
    private const string START_MATCH_ROW_PATH = "StartMatchRow";
    private const string START_MATCH_BUTTON_PATH = "StartMatchRow/StartMatchButton";
    private const string BACK_BUTTON_PATH = "BackRow/BackButton";
    private const string STATUS_LABEL_PATH = "StatusLabel";

    /// <summary>Raised when the player backs out. Whoever put this panel on screen decides what
    /// goes back in its place — this only knows it is done.</summary>
    public event Action? Closed;

    private Button _createRoomButton = null!;
    private LineEdit _addressField = null!;
    private Button _joinButton = null!;
    private Control _startMatchRow = null!;
    private Button _startMatchButton = null!;
    private Label _statusLabel = null!;

    public override void _Ready()
    {
        _createRoomButton = GetNode<Button>(CREATE_ROOM_BUTTON_PATH);
        _addressField = GetNode<LineEdit>(ADDRESS_FIELD_PATH);
        _joinButton = GetNode<Button>(JOIN_BUTTON_PATH);
        _startMatchRow = GetNode<Control>(START_MATCH_ROW_PATH);
        _startMatchButton = GetNode<Button>(START_MATCH_BUTTON_PATH);
        _statusLabel = GetNode<Label>(STATUS_LABEL_PATH);

        _createRoomButton.Pressed += OnCreateRoomPressed;
        _joinButton.Pressed += OnJoinPressed;
        _startMatchButton.Pressed += OnStartMatchPressed;
        GetNode<Button>(BACK_BUTTON_PATH).Pressed += OnBackPressed;

        EventBus.Instance!.PeerConnected += OnPeerConnected;
        EventBus.Instance.ConnectionFailed += OnConnectionFailed;
        EventBus.Instance.ServerDisconnected += OnServerDisconnected;

        GameState.Instance!.MatchStarted += OnMatchStarted;

        Open();
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

    /// <summary>Puts the panel back to the state it was in the first time it was shown. Needed
    /// because it is now shown more than once in a screen's life: the node survives being
    /// closed, so anything the last attempt left behind — a locked address field, a stale
    /// status line — would still be there on the next one.</summary>
    public void Open()
    {
        _addressField.Text = DEFAULT_ADDRESS;
        _statusLabel.Text = string.Empty;
        UnlockConnectionControls();
    }

    private void OnCreateRoomPressed()
    {
        NetworkManager.Instance!.StartHost();
        if (Multiplayer.MultiplayerPeer == null)
        {
            _statusLabel.Text = "CONNECT_STATUS_ROOM_FAILED";
            return;
        }

        LockConnectionControls();
        _startMatchRow.Visible = true;
        _statusLabel.Text = "CONNECT_STATUS_WAITING";
    }

    private void OnJoinPressed()
    {
        NetworkManager.Instance!.JoinHost(_addressField.Text);
        if (Multiplayer.MultiplayerPeer == null)
        {
            _statusLabel.Text = "CONNECT_STATUS_START_FAILED";
            return;
        }

        LockConnectionControls();
        _statusLabel.Text = string.Format(Tr("CONNECT_STATUS_CONNECTING"), _addressField.Text);
    }

    private void OnStartMatchPressed()
    {
        GameState.Instance!.HostStartsMatch();
    }

    private void OnBackPressed()
    {
        NetworkManager.Instance?.Disconnect();
        GameState.Instance?.ResetConnection();
        Open();
        Closed?.Invoke();
    }

    private void OnPeerConnected(long peerId)
    {
        _statusLabel.Text = "CONNECT_STATUS_CONNECTED";

        // Harmless on the client, where the row is invisible — which is why this needs no
        // host/client branch of its own.
        _startMatchButton.Disabled = false;
    }

    private void OnConnectionFailed()
    {
        _statusLabel.Text = "CONNECT_STATUS_FAILED";
        UnlockConnectionControls();
    }

    private void OnServerDisconnected()
    {
        _statusLabel.Text = "CONNECT_STATUS_DROPPED";
        UnlockConnectionControls();
    }

    /// <summary>Fires on both sides — the host emits it locally, the client gets it from
    /// MatchStartedRpc — so one handler moves both screens on.</summary>
    private void OnMatchStarted()
    {
        ScreenRouter.GoToMatchWorld(this);
    }

    private void LockConnectionControls()
    {
        _createRoomButton.Disabled = true;
        _joinButton.Disabled = true;
        _addressField.Editable = false;
    }

    /// <summary>매치 시작 only ever appears on the side that made the room, and only becomes
    /// pressable once someone is actually there to play — so the row goes away again with
    /// everything else the room implied. Hiding the ROW rather than the button matters here:
    /// the button no longer sits alone, and a hidden button would leave its "&gt;" behind.</summary>
    private void UnlockConnectionControls()
    {
        _createRoomButton.Disabled = false;
        _joinButton.Disabled = false;
        _addressField.Editable = true;
        _startMatchRow.Visible = false;
        _startMatchButton.Disabled = true;
    }
}
