using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>A throwaway harness for driving a real match across two running instances —
/// the check root CLAUDE.md asks for ("test multiplayer RPC/authority behavior with two
/// running instances, not from memory"). Nothing here is meant to survive into the real UI.
///
/// Controls are built in code rather than in the scene file so none of this layout has to
/// be maintained in the editor while it is only ever used to watch log lines.
///
/// Reads GameState.View and never the session, on the host too — the rule from
/// Scripts/CLAUDE.md that keeps a host's own screen from rendering the opponent's hand.</summary>
public partial class MatchDebugUI : Control
{
    private const string DEFAULT_ADDRESS = "127.0.0.1";

    private LineEdit _addressField = null!;
    private Button _hostButton = null!;
    private Button _joinButton = null!;
    private Button _startMatchButton = null!;
    private Label _statusLabel = null!;
    private HBoxContainer _handRow = null!;
    private RichTextLabel _logView = null!;

    public override void _Ready()
    {
        BuildControls();
        ConnectToAutoloadSignals();
        RefreshDisplay();
        Log("ready — press Host on one instance, Join on the other");
    }

    /// <summary>A scene node that outlives its connections to a session-lifetime Autoload
    /// is the crash this prevents (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.MatchStarted -= OnMatchStarted;
            GameState.Instance.RoundResolved -= OnRoundResolved;
            GameState.Instance.SubmissionRejected -= OnSubmissionRejected;
            GameState.Instance.MatchEnded -= OnMatchEnded;
            GameState.Instance.OpponentLeft -= OnOpponentLeft;
            GameState.Instance.MyHandChanged -= OnMyHandChanged;
        }

        if (EventBus.Instance != null)
        {
            EventBus.Instance.PeerConnected -= OnPeerConnected;
            EventBus.Instance.PeerDisconnected -= OnPeerDisconnected;
            EventBus.Instance.ServerDisconnected -= OnServerDisconnected;
            EventBus.Instance.ConnectionFailed -= OnConnectionFailed;
        }
    }

    private void ConnectToAutoloadSignals()
    {
        GameState.Instance!.MatchStarted += OnMatchStarted;
        GameState.Instance.RoundResolved += OnRoundResolved;
        GameState.Instance.SubmissionRejected += OnSubmissionRejected;
        GameState.Instance.MatchEnded += OnMatchEnded;
        GameState.Instance.OpponentLeft += OnOpponentLeft;
        GameState.Instance.MyHandChanged += OnMyHandChanged;

        // Connection events are logged straight from EventBus: the whole point of this
        // screen is seeing which of them actually fire, on which side.
        EventBus.Instance!.PeerConnected += OnPeerConnected;
        EventBus.Instance.PeerDisconnected += OnPeerDisconnected;
        EventBus.Instance.ServerDisconnected += OnServerDisconnected;
        EventBus.Instance.ConnectionFailed += OnConnectionFailed;
    }

    private void BuildControls()
    {
        var root = new VBoxContainer();
        root.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(root);

        var connectionRow = new HBoxContainer();
        root.AddChild(connectionRow);

        _hostButton = new Button();
        _hostButton.Text = "Host";
        _hostButton.Pressed += OnHostPressed;
        connectionRow.AddChild(_hostButton);

        _addressField = new LineEdit();
        _addressField.Text = DEFAULT_ADDRESS;
        _addressField.CustomMinimumSize = new Vector2(160, 0);
        connectionRow.AddChild(_addressField);

        _joinButton = new Button();
        _joinButton.Text = "Join";
        _joinButton.Pressed += OnJoinPressed;
        connectionRow.AddChild(_joinButton);

        _startMatchButton = new Button();
        _startMatchButton.Text = "Start Match (host)";
        _startMatchButton.Pressed += OnStartMatchPressed;
        connectionRow.AddChild(_startMatchButton);

        _statusLabel = new Label();
        root.AddChild(_statusLabel);

        var handLabel = new Label();
        handLabel.Text = "My hand — click to play";
        root.AddChild(handLabel);

        _handRow = new HBoxContainer();
        root.AddChild(_handRow);

        _logView = new RichTextLabel();
        _logView.SizeFlagsVertical = SizeFlags.ExpandFill;
        _logView.ScrollFollowing = true;
        root.AddChild(_logView);
    }

    private void OnHostPressed()
    {
        NetworkManager.Instance!.StartHost();
        Log("started hosting");
        RefreshDisplay();
    }

    private void OnJoinPressed()
    {
        NetworkManager.Instance!.JoinHost(_addressField.Text);
        Log($"joining {_addressField.Text}");
        RefreshDisplay();
    }

    private void OnStartMatchPressed()
    {
        GameState.Instance!.HostStartsMatch();
    }

    private void OnCardPressed(CardName card)
    {
        // No judgment here — the harness sends the intent and lets the host rule on it
        // (Scripts/CLAUDE.md: "a node that receives input does not judge it"). Transform
        // and Swap need a choice this screen can't make, so playing one is expected to come
        // back rejected, which is itself worth watching work.
        Log($"-> requesting {card}");
        GameState.Instance!.RequestCardPlay(CardPlay.WithoutChoice(card));
    }

    private void OnMatchStarted()
    {
        Log("match started");
        RefreshDisplay();
    }

    private void OnRoundResolved()
    {
        Log("round resolved");
        RefreshDisplay();
    }

    private void OnSubmissionRejected(string reason)
    {
        Log($"submission rejected: {reason}");
    }

    private void OnMatchEnded(bool didIWin)
    {
        if (didIWin)
        {
            Log("match over — I won");
        }
        else
        {
            Log("match over — opponent won");
        }

        RefreshDisplay();
    }

    private void OnOpponentLeft()
    {
        Log("opponent left");
        RefreshDisplay();
    }

    /// <summary>On a client this is what actually shows the new hand — the public round
    /// broadcast arrives first and carries no hand contents at all.</summary>
    private void OnMyHandChanged()
    {
        RefreshHand();
    }

    private void OnPeerConnected(long peerId)
    {
        Log($"peer {peerId} connected");
        RefreshDisplay();
    }

    private void OnPeerDisconnected(long peerId)
    {
        Log($"peer {peerId} disconnected");
    }

    private void OnServerDisconnected()
    {
        Log("server disconnected");
    }

    private void OnConnectionFailed()
    {
        Log("connection failed");
    }

    private void RefreshDisplay()
    {
        RefreshStatus();
        RefreshHand();
    }

    private void RefreshStatus()
    {
        MatchView view = GameState.Instance!.View;

        _statusLabel.Text =
            $"[{DescribeRole()}]  round {view.RoundNumber}   score {view.MyScore}-{view.OpponentScore}\n"
            + $"my deck {view.MyDeckCount}   opponent deck {view.OpponentDeckCount}   opponent hand {view.OpponentHandCount}\n"
            + $"last round: {DescribeLastRound(view)}";
    }

    private void RefreshHand()
    {
        foreach (Node child in _handRow.GetChildren())
        {
            child.QueueFree();
        }

        // Rebuilding the whole row is fine for a harness; the real UI is expected to keep
        // card nodes stable instead, since Transform and Swap need the clicked node
        // identified (Scripts/CLAUDE.md, "Card presentation").
        foreach (CardName card in GameState.Instance!.View.MyHand)
        {
            CardName cardToPlay = card;
            var cardButton = new Button();
            cardButton.Text = DisplayNameOf(card);
            cardButton.Pressed += () => { OnCardPressed(cardToPlay); };
            _handRow.AddChild(cardButton);
        }
    }

    private string DescribeRole()
    {
        if (Multiplayer.MultiplayerPeer == null)
        {
            return "not connected";
        }

        if (Multiplayer.IsServer())
        {
            return "host";
        }

        return "client";
    }

    private static string DescribeLastRound(MatchView view)
    {
        if (view.MyCard == null)
        {
            return "none yet";
        }

        string outcome;
        if (view.LastRoundOutcome == null)
        {
            outcome = "no win/loss";
        }
        else
        {
            outcome = view.LastRoundOutcome.ToString()!;
        }

        return $"me {view.MyCard} ({view.MyCardFate}) vs opponent {view.OpponentCard} ({view.OpponentCardFate}) -> {outcome}";
    }

    private static string DisplayNameOf(CardName card)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(card);
        if (cardData == null)
        {
            return card.ToString();
        }

        return cardData.DisplayName;
    }

    private void Log(string message)
    {
        GD.Print($"[MatchDebugUI] {message}");
        _logView.AppendText($"{message}\n");
    }
}
