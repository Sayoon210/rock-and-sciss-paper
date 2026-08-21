using System;
using System.Collections.Generic;
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
    private HBoxContainer _choiceRow = null!;
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
            GameState.Instance.RequestRejected -= OnRequestRejected;
            GameState.Instance.RoundRevealed -= OnRoundRevealed;
            GameState.Instance.ChoiceRequired -= OnChoiceRequired;
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
        GameState.Instance.RequestRejected += OnRequestRejected;
        GameState.Instance.RoundRevealed += OnRoundRevealed;
        GameState.Instance.ChoiceRequired += OnChoiceRequired;
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

        // Only Transform and Swap ever fill this — every other card is playable from the
        // hand row alone.
        _choiceRow = new HBoxContainer();
        root.AddChild(_choiceRow);

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

    private void OnCardPressed(ECardName card)
    {
        // Every card is a single click now, 교체 and 변화 included — their choice is asked
        // for after the reveal, by ChoiceRequired, not gathered before submitting.
        //
        // No judgment here — the harness sends the intent and lets the host rule on it
        // (Scripts/CLAUDE.md: "a node that receives input does not judge it").
        Log($"-> requesting {card}");
        GameState.Instance!.RequestCardPlay(card);
    }

    /// <summary>Neither picker filters by what the rules allow. The harness holds no rule
    /// knowledge on purpose, so an illegal pick (a Joker as the target, a card not in hand)
    /// is sent and rejected by the host — which is the path worth being able to exercise.</summary>
    private void ShowTransformChoice()
    {
        ClearChoiceRow();

        var label = new Label();
        label.Text = "Transform";
        _choiceRow.AddChild(label);

        var fromPicker = new OptionButton();
        foreach (ECardName card in GameState.Instance!.View.MyHand)
        {
            fromPicker.AddItem(DisplayNameOf(card), (int)card);
        }

        _choiceRow.AddChild(fromPicker);

        var intoLabel = new Label();
        intoLabel.Text = "into";
        _choiceRow.AddChild(intoLabel);

        var intoPicker = new OptionButton();
        foreach (ECardName card in AllKnownCards())
        {
            intoPicker.AddItem(DisplayNameOf(card), (int)card);
        }

        _choiceRow.AddChild(intoPicker);

        AddConfirmButtons(() =>
        {
            if (fromPicker.Selected < 0 || intoPicker.Selected < 0)
            {
                Log("pick both a card to change and what it becomes");
                return;
            }

            var from = (ECardName)fromPicker.GetItemId(fromPicker.Selected);
            var into = (ECardName)intoPicker.GetItemId(intoPicker.Selected);
            Log($"-> choosing Transform {from} -> {into}");
            GameState.Instance!.RequestChoice(CardChoice.Transforming(from, into));
            ClearChoiceRow();
        });
    }

    /// <summary>Lists the whole hand as the host offered it. The played 교체 card is no
    /// longer in that hand at all, which is the point — the old "do not return the card you
    /// just played" special case has nothing left to guard.</summary>
    private void ShowSwapChoice()
    {
        ClearChoiceRow();

        var label = new Label();
        label.Text = "Swap back:";
        _choiceRow.AddChild(label);

        var toggles = new List<Button>();
        var toggledCards = new List<ECardName>();

        foreach (ECardName card in GameState.Instance!.View.MyHand)
        {
            var toggle = new Button();
            toggle.Text = DisplayNameOf(card);
            toggle.ToggleMode = true;
            _choiceRow.AddChild(toggle);

            toggles.Add(toggle);
            toggledCards.Add(card);
        }

        AddConfirmButtons(() =>
        {
            var chosen = new List<ECardName>();
            for (int i = 0; i < toggles.Count; i++)
            {
                if (toggles[i].ButtonPressed)
                {
                    chosen.Add(toggledCards[i]);
                }
            }

            Log($"-> choosing Swap, returning [{string.Join(", ", chosen)}]");
            GameState.Instance!.RequestChoice(CardChoice.Swapping(chosen));
            ClearChoiceRow();
        });
    }

    private void AddConfirmButtons(Action onPlay)
    {
        var playButton = new Button();
        playButton.Text = "Play";
        playButton.Pressed += () => { onPlay(); };
        _choiceRow.AddChild(playButton);

        var cancelButton = new Button();
        cancelButton.Text = "Cancel";
        cancelButton.Pressed += ClearChoiceRow;
        _choiceRow.AddChild(cancelButton);
    }

    private void ClearChoiceRow()
    {
        // Removed as well as freed: QueueFree only takes effect at the end of the frame, so
        // a row rebuilt in the same frame would briefly show both sets of controls.
        foreach (Node child in _choiceRow.GetChildren())
        {
            _choiceRow.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static IReadOnlyCollection<ECardName> AllKnownCards()
    {
        if (CardDatabase.Instance == null)
        {
            return Array.Empty<ECardName>();
        }

        return CardDatabase.Instance.LoadedCardNames;
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

    private void OnRequestRejected(string reason)
    {
        Log($"rejected: {reason}");
    }

    /// <summary>Both cards are public now. On the side that owes a choice this arrives
    /// just before the prompt, so the player sees what they are choosing against.</summary>
    private void OnRoundRevealed()
    {
        MatchView view = GameState.Instance!.View;
        Log($"revealed — me {view.MyCard} vs opponent {view.OpponentCard}");
        RefreshStatus();
    }

    /// <summary>The host is asking me to choose. Nothing here decides what is legal; the
    /// pickers offer everything and let the host rule on it.</summary>
    private void OnChoiceRequired()
    {
        ECardName? card = GameState.Instance!.View.CardIMustChooseFor;
        if (card == ECardName.Transform)
        {
            Log("choose: Transform");
            ShowTransformChoice();
            return;
        }

        if (card == ECardName.Swap)
        {
            Log("choose: Swap");
            ShowSwapChoice();
        }
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
            + $"last round: {DescribeLastRound(view)}\n"
            + $"{DescribeChoiceState(view)}";
    }

    private void RefreshHand()
    {
        // A picker names cards from a specific hand, and this rebuild means that hand is
        // gone. The prompt that opened it will have been answered or timed out.
        ClearChoiceRow();

        foreach (Node child in _handRow.GetChildren())
        {
            _handRow.RemoveChild(child);
            child.QueueFree();
        }

        // Rebuilding the whole row is fine for a harness; the real UI is expected to keep
        // card nodes stable instead, since Transform and Swap need the clicked node
        // identified (Scripts/CLAUDE.md, "Card presentation").
        foreach (ECardName card in GameState.Instance!.View.MyHand)
        {
            ECardName cardToPlay = card;
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

    private static string DescribeChoiceState(MatchView view)
    {
        if (view.CardIMustChooseFor != null)
        {
            return $"** waiting on MY choice for {view.CardIMustChooseFor} **";
        }

        if (view.OpponentIsChoosing)
        {
            return "waiting for the opponent to choose...";
        }

        return $"swapped: me {view.MySwappedCardCount} / opponent {view.OpponentSwappedCardCount}"
            + $"   transformed: me {view.MyTransformApplied} / opponent {view.OpponentTransformApplied}";
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

    private static string DisplayNameOf(ECardName card)
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
