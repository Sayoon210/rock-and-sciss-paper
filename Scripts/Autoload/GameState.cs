using System;
using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;
using RockAndScissPaper.Network;
// Godot.Side (an anchor/margin enum) collides with GameLogic's own Side (Player1/Player2) —
// disambiguate once here rather than fully qualifying every use below.
using Side = RockAndScissPaper.GameLogic.Side;

namespace RockAndScissPaper.Autoload;

/// <summary>Owns the current match and is the one entry point every player action goes
/// through, on either side of the connection (Scripts/Autoload/CLAUDE.md). Holds the
/// authoritative MatchSession on the host and leaves it null on a client — a client that
/// tried to touch match rules directly fails to compile, not just at runtime.
///
/// A round crosses the network twice now, not once: cards are submitted and revealed, and
/// if 교체 or 변화 was played the player who played it is prompted to choose before the
/// round can finish. Only the host ever knows whether a choice is still outstanding.
///
/// Reconnection is out of scope for this pass: any disconnect, on either side, ends the
/// match outright rather than trying to resume it.</summary>
public partial class GameState : Node
{
    // Sentinels for the two nullable pieces of round/match state that cross the network as
    // plain ints — WinLossResult and Side are both zero-based enums, so -1 is unambiguous.
    private const int NO_WIN_LOSS_RESULT_SENTINEL = -1;
    private const int NO_WINNER_SIDE_SENTINEL = -1;

    /// <summary>How long a player has to answer a choice prompt before the effect is
    /// skipped. A choice phase blocks the whole match, and by then the player has already
    /// committed a card and had it revealed, so idling cannot be allowed to freeze the
    /// game for the opponent. Applies to the host's own choice too — the host is not
    /// privileged in liveness, and a host who walks away wedges the match just as hard.</summary>
    private const double CHOICE_TIMEOUT_SECONDS = 30.0;

    public static GameState? Instance { get; private set; }

    // Host only: the authoritative match. Always null on a client.
    private MatchSession? _session;

    // Host only, connection-lifetime: which side each connected peer plays. Survives a
    // rematch (ResetMatch), cleared only when the connection itself ends (ResetConnection).
    private readonly Dictionary<long, Side> _sideByPeerId = new Dictionary<long, Side>();

    // Both sides. Host is always Player1 by design (no coin flip — see DevLogDoc), so this
    // default is already correct for the host at startup; a client overwrites it once
    // MatchStartedRpc assigns a side.
    private Side _mySide = Side.Player1;

    // Host only: armed while a choice is outstanding.
    private Timer? _choiceTimer;

    public MatchView View { get; private set; } = new MatchView();

    // UI Signals - dont need to distribute side, only my side
    [Signal] public delegate void RoundResolvedEventHandler();
    [Signal] public delegate void RequestRejectedEventHandler(string reason);
    [Signal] public delegate void MatchStartedEventHandler();
    [Signal] public delegate void MatchEndedEventHandler(bool didIWin);
    [Signal] public delegate void OpponentLeftEventHandler();

    /// <summary>Both cards are now public. Fires before any choice prompt, so the screen
    /// can show what was played before asking the player to pick against it — which is the
    /// whole reason the choice moved after the reveal.</summary>
    [Signal] public delegate void RoundRevealedEventHandler();

    /// <summary>I have to choose something before this round can finish. Read
    /// View.CardIMustChooseFor for which card. Only ever reaches the one player who owes
    /// the choice.</summary>
    [Signal] public delegate void ChoiceRequiredEventHandler();

    /// <summary>Fires whenever View.MyHand is replaced. Separate from RoundResolved because
    /// a client's hand arrives in its own targeted RPC — a UI that only redrew on
    /// RoundResolved would render the previous round's hand and never correct it.</summary>
    [Signal] public delegate void MyHandChangedEventHandler();

    public override void _EnterTree()
    {
        Instance = this;
    }

    public override void _Ready()
    {
        if (EventBus.Instance != null)
        {
            EventBus.Instance.PeerConnected += OnPeerConnected;
            EventBus.Instance.PeerDisconnected += OnPeerDisconnected;
            EventBus.Instance.ServerDisconnected += OnServerDisconnected;
        }

        _choiceTimer = new Timer();
        _choiceTimer.OneShot = true;
        _choiceTimer.WaitTime = CHOICE_TIMEOUT_SECONDS;
        _choiceTimer.Timeout += OnChoiceTimedOut;
        AddChild(_choiceTimer);
    }

    // keep connection, only reset match (session & view)
    public void ResetMatch()
    {
        _session = null;
        View = new MatchView();
        StopChoiceTimer();
    }

    // reset connection and match
    public void ResetConnection()
    {
        ResetMatch();
        _sideByPeerId.Clear();
        _mySide = Side.Player1;
    }

    /// <summary>Host only. Builds both decks, deals the mulligan, and tells the connected
    /// client its side and hand privately. Gated on exactly one connected client — this
    /// pass has no lobby flow, so there is nothing else to wait on.</summary>
    public void HostStartsMatch()
    {
        if (!Multiplayer.IsServer())
        {
            GD.PrintErr("GameState: HostStartsMatch was called on a non-host peer.");
            return;
        }

        long? clientPeerId = ClientPeerId();
        if (!clientPeerId.HasValue)
        {
            GD.PrintErr("GameState: HostStartsMatch requires exactly one connected client.");
            return;
        }

        ResetMatch();
        _mySide = Side.Player1;
        Side clientSide = _sideByPeerId[clientPeerId.Value];

        List<CardName> player1Deck = DeckAssembler.BuildDeck();
        List<CardName> player2Deck = DeckAssembler.BuildDeck();
        _session = new MatchSession(player1Deck, player2Deck, new Random(), GD.Print);

        RpcId(
            clientPeerId.Value,
            MethodName.MatchStartedRpc,
            (int)clientSide,
            EncodeHand(_session.HandOf(clientSide)),
            _session.DeckCountOf(clientSide),
            _session.DeckCountOf(_mySide),
            _session.HandOf(_mySide).Count);

        // Host fills its own View straight from the session it holds. Every field is
        // written before any signal goes out — a handler that does a full redraw must
        // never see a half-filled View.
        View.MyHand = new List<CardName>(_session.HandOf(_mySide));
        View.MyDeckCount = _session.DeckCountOf(_mySide);
        View.OpponentDeckCount = _session.DeckCountOf(clientSide);
        View.OpponentHandCount = _session.HandOf(clientSide).Count;
        View.RoundNumber = _session.RoundNumber;

        EmitSignalMyHandChanged();
        EmitSignalMatchStarted();
    }

    /// <summary>The one entry point for playing a card, identical on both sides.
    /// On the host it resolves locally;
    /// on a client it becomes an RPC to peer 1.
    /// The caller (a CardController) never branches on host vs. client.</summary>
    public void RequestCardPlay(CardName card)
    {
        if (Multiplayer.IsServer())
        {
            HandleSubmission(_mySide, card, null); // handle as local signal
        }
        else
        {
            RpcId(1, MethodName.SubmitCardRpc, (int)card);
        }
    }

    /// <summary>The one entry point for answering a choice prompt, shaped exactly like
    /// RequestCardPlay so the UI does not branch on host vs. client here either.</summary>
    public void RequestChoice(CardChoice choice)
    {
        if (Multiplayer.IsServer())
        {
            HandleChoice(_mySide, choice, null);
        }
        else
        {
            CardChoiceCodec.EncodedCardChoice encoded = CardChoiceCodec.Encode(choice);
            RpcId(
                1,
                MethodName.SubmitChoiceRpc,
                View.RoundNumber,
                encoded.CardToTransform,
                encoded.TransformInto,
                encoded.CardsToReturn);
        }
    }

    private void OnPeerConnected(long peerId)
    {
        // Only the host assigns sides
        if (!Multiplayer.IsServer())
        {
            return;
        }

        // Host is always Player1 (decision already made, no coin flip)
        // Client is always side Player2
        _sideByPeerId[peerId] = Side.Player2;
    }

    private void OnPeerDisconnected(long peerId)
    {
        HandleDisconnect();
    }

    private void OnServerDisconnected()
    {
        HandleDisconnect();
    }

    private void HandleDisconnect()
    {
        // Reconnection is explicitly out of scope for this pass — any disconnect, on
        // either side, ends the match outright.
        ResetConnection();
        EmitSignalOpponentLeft();
    }

    // --- RPCs -----------------------------------------------------------------------

    /// <summary>Client to host: "I want to play this." AnyPeer because any connected
    /// client must be able to call it; the host is the only one who ever receives it,
    /// since a client only ever calls this via RpcId(1, ...).</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitCardRpc(int card)
    {
        long fromPeerId = Multiplayer.GetRemoteSenderId();
        if (!_sideByPeerId.TryGetValue(fromPeerId, out Side side))
        {
            RejectRequest(fromPeerId, "Unknown peer.");
            return;
        }

        HandleSubmission(side, (CardName)card, fromPeerId);
    }

    /// <summary>Client to host: "here is my choice." roundNumber stamps which round it was
    /// meant for, so an answer that arrives after its round already timed out is dropped
    /// instead of being applied to the next one.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitChoiceRpc(int roundNumber, int cardToTransform, int transformInto, int[] cardsToReturn)
    {
        long fromPeerId = Multiplayer.GetRemoteSenderId();
        if (!_sideByPeerId.TryGetValue(fromPeerId, out Side side))
        {
            RejectRequest(fromPeerId, "Unknown peer.");
            return;
        }

        if (_session == null)
        {
            RejectRequest(fromPeerId, "No match is running.");
            return;
        }

        if (roundNumber != _session.RoundNumber)
        {
            // Stale: that round is already over. Silently dropped — telling the client its
            // choice was rejected would be misleading, since it did nothing wrong.
            GD.Print($"GameState: dropped a choice for round {roundNumber}, now on {_session.RoundNumber}.");
            return;
        }

        CardName? promptedCard = _session.CardAwaitingChoiceFrom(side);
        if (!promptedCard.HasValue)
        {
            RejectRequest(fromPeerId, "You were not asked to choose this round.");
            return;
        }

        CardChoice? choice = CardChoiceCodec.Decode(
            promptedCard.Value, cardToTransform, transformInto, cardsToReturn);
        if (choice == null)
        {
            RejectRequest(fromPeerId, "That choice was incomplete.");
            return;
        }

        HandleChoice(side, choice, fromPeerId);
    }

    /// <summary>Host to one peer: that request was rejected, with a reason. Covers a
    /// rejected card and a rejected choice alike. Authority-only so a client can never
    /// spoof this at another client.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestRejectedRpc(string reason)
    {
        EmitSignalRequestRejected(reason);
    }

    /// <summary>Host to all: both played cards are now public, and whether each side still
    /// owes a choice. Sent on every round, including ones that finish immediately, so a
    /// round always has the same shape on screen. Carries no hidden information — which
    /// cards need a choice follows from the rules and the revealed cards alone.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RoundRevealedRpc(int player1Card, int player2Card, bool player1IsChoosing, bool player2IsChoosing)
    {
        ApplyRevealToView(
            (CardName)player1Card, (CardName)player2Card, player1IsChoosing, player2IsChoosing);
        EmitSignalRoundRevealed();
    }

    /// <summary>Host to one peer: you must choose, and here is the hand to choose from.
    ///
    /// This is the one message that would leak an entire hand if it were ever broadcast
    /// instead of targeted. It carries the recipient's own hand only.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ChoiceRequiredRpc(int card, int[] handToChooseFrom)
    {
        View.MyHand = DecodeHand(handToChooseFrom);
        View.CardIMustChooseFor = (CardName)card;

        EmitSignalMyHandChanged();
        EmitSignalChoiceRequired();
    }

    /// <summary>Host to all: the public part of a resolved round — fates, win/loss, deck
    /// counts, hand *counts* (never contents), and what each side's choice did as a count
    /// and a flag. The played cards themselves already went out with the reveal. Also
    /// carries match-end info piggybacked on the same message when the match just ended,
    /// rather than a separate round trip.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RoundResolvedRpc(
        int player1Fate,
        int player2Fate,
        int winLoss,
        int player1DeckCount,
        int player2DeckCount,
        int player1HandCount,
        int player2HandCount,
        int player1SwappedCardCount,
        int player2SwappedCardCount,
        bool player1TransformApplied,
        bool player2TransformApplied,
        int player1Score,
        int player2Score,
        int roundNumber,
        int winnerSide)
    {
        WinLossResult? winLossResult = null;
        if (winLoss != NO_WIN_LOSS_RESULT_SENTINEL)
        {
            winLossResult = (WinLossResult)winLoss;
        }

        Side? winner = null;
        if (winnerSide != NO_WINNER_SIDE_SENTINEL)
        {
            winner = (Side)winnerSide;
        }

        ApplyRoundResultToView(
            (CardFate)player1Fate,
            (CardFate)player2Fate,
            winLossResult,
            player1DeckCount,
            player2DeckCount,
            player1HandCount,
            player2HandCount,
            player1SwappedCardCount,
            player2SwappedCardCount,
            player1TransformApplied,
            player2TransformApplied,
            player1Score,
            player2Score,
            roundNumber,
            winner);

        EmitSignalRoundResolved();
        if (winner.HasValue)
        {
            EmitSignalMatchEnded(winner.Value == _mySide);
        }
    }

    /// <summary>Host to one peer: that peer's private post-round hand.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void PrivateHandRpc(int[] hand)
    {
        View.MyHand = DecodeHand(hand);
        EmitSignalMyHandChanged();
    }

    /// <summary>Host to one peer: the match has started — side assignment, mulligan hand,
    /// and both deck/hand counts. The only RPC where a side assignment reaches the client,
    /// matching the host-always-Player1 decision.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MatchStartedRpc(int side, int[] mulliganHand, int myDeckCount, int opponentDeckCount, int opponentHandCount)
    {
        View = new MatchView();
        _mySide = (Side)side;

        View.MyHand = DecodeHand(mulliganHand);
        View.MyDeckCount = myDeckCount;
        View.OpponentDeckCount = opponentDeckCount;
        View.OpponentHandCount = opponentHandCount;
        View.RoundNumber = 1;

        EmitSignalMyHandChanged();
        EmitSignalMatchStarted();
    }

    // --- Helpers ----------------------------------------------------------------------

    /// <summary>Runs a submitted card through the session and reacts to the outcome.
    /// remotePeerId is null for the host's own local play (a rejection becomes a local
    /// signal, not an RPC) and non-null for a client's play relayed here by SubmitCardRpc.
    /// MatchSession throws on an illegal or out-of-turn play; that is expected client
    /// behavior to guard against, not a bug, so it is caught here rather than left to
    /// crash the RPC handler.</summary>
    private void HandleSubmission(Side side, CardName card, long? remotePeerId)
    {
        if (_session == null)
        {
            RejectRequest(remotePeerId, "No match is running.");
            return;
        }

        RoundReveal? reveal;
        try
        {
            reveal = _session.SubmitCard(side, card);
        }
        catch (InvalidOperationException exception)
        {
            RejectRequest(remotePeerId, exception.Message);
            return;
        }
        catch (ArgumentException exception)
        {
            RejectRequest(remotePeerId, exception.Message);
            return;
        }

        if (reveal == null)
        {
            // Waiting on the other side's submission — nothing to reveal yet.
            return;
        }

        BroadcastReveal(reveal);

        if (reveal.Result != null)
        {
            BroadcastRoundResult(reveal.Result);
            return;
        }

        PromptChoosers();
    }

    /// <summary>Runs a choice through the session. A rejected choice deliberately leaves
    /// the side still awaited, so the player can simply pick again — their hand has not
    /// changed, so their open picker is still valid.</summary>
    private void HandleChoice(Side side, CardChoice choice, long? remotePeerId)
    {
        if (_session == null)
        {
            RejectRequest(remotePeerId, "No match is running.");
            return;
        }

        RoundResult? result;
        try
        {
            result = _session.SubmitChoice(side, choice);
        }
        catch (InvalidOperationException exception)
        {
            RejectRequest(remotePeerId, exception.Message);
            return;
        }
        catch (ArgumentException exception)
        {
            RejectRequest(remotePeerId, exception.Message);
            return;
        }

        if (result == null)
        {
            // The other side is still choosing.
            return;
        }

        BroadcastRoundResult(result);
    }

    /// <summary>Host only. Gives up on every outstanding choice and lets the round finish.
    /// Declining is not a penalty — for 교체 it is indistinguishable from swapping nothing,
    /// and for 변화 there is no neutral pick to make on the player's behalf.</summary>
    private void OnChoiceTimedOut()
    {
        if (_session == null)
        {
            return;
        }

        // Declining Player 1 only finishes the round if Player 2 owed nothing; otherwise
        // the second call is the one that settles it. Either is a no-op for a side that
        // was not awaited.
        RoundResult? result = _session.DeclineChoice(Side.Player1);
        if (result == null)
        {
            result = _session.DeclineChoice(Side.Player2);
        }

        if (result != null)
        {
            BroadcastRoundResult(result);
        }
    }

    private void StopChoiceTimer()
    {
        if (_choiceTimer != null)
        {
            _choiceTimer.Stop();
        }
    }

    private void RejectRequest(long? remotePeerId, string reason)
    {
        if (remotePeerId.HasValue)
        {
            RpcId(remotePeerId.Value, MethodName.RequestRejectedRpc, reason);
        }
        else
        {
            EmitSignalRequestRejected(reason);
        }
    }

    private void BroadcastReveal(RoundReveal reveal)
    {
        Rpc(
            MethodName.RoundRevealedRpc,
            (int)reveal.Player1Card,
            (int)reveal.Player2Card,
            reveal.Player1MustChoose,
            reveal.Player2MustChoose);

        ApplyRevealToView(
            reveal.Player1Card, reveal.Player2Card, reveal.Player1MustChoose, reveal.Player2MustChoose);
        EmitSignalRoundRevealed();
    }

    /// <summary>Host only. Sends each player who owes a choice their own hand to pick from,
    /// and arms the timeout. Both prompts go out before the timer starts, so neither player
    /// gets a head start on the other.</summary>
    private void PromptChoosers()
    {
        PromptOneChooser(Side.Player1);
        PromptOneChooser(Side.Player2);

        if (_choiceTimer != null)
        {
            _choiceTimer.Start();
        }
    }

    private void PromptOneChooser(Side side)
    {
        CardName? card = _session!.CardAwaitingChoiceFrom(side);
        if (!card.HasValue)
        {
            return;
        }

        if (side == _mySide)
        {
            // The host's own prompt never touches the network — same shape as its own
            // card play resolving in-process. MyHand is refreshed here for the same reason
            // ChoiceRequiredRpc refreshes it for a client: the played card is already gone
            // from the real hand (and 리셋 may have replaced it outright), and without this
            // the host would be offered a picker still showing the card it just played.
            View.MyHand = new List<CardName>(_session.HandOf(side));
            View.CardIMustChooseFor = card.Value;
            EmitSignalMyHandChanged();
            EmitSignalChoiceRequired();
            return;
        }

        long? clientPeerId = ClientPeerId();
        if (!clientPeerId.HasValue)
        {
            return;
        }

        RpcId(
            clientPeerId.Value,
            MethodName.ChoiceRequiredRpc,
            (int)card.Value,
            EncodeHand(_session.HandOf(side)));
    }

    private void BroadcastRoundResult(RoundResult result)
    {
        StopChoiceTimer();

        int winLoss = NO_WIN_LOSS_RESULT_SENTINEL;
        if (result.WinLoss.HasValue)
        {
            winLoss = (int)result.WinLoss.Value;
        }

        int winnerSide = NO_WINNER_SIDE_SENTINEL;
        if (_session!.Winner.HasValue)
        {
            winnerSide = (int)_session.Winner.Value;
        }

        int player1HandCount = result.Player1Hand.Count;
        int player2HandCount = result.Player2Hand.Count;

        // The client's hand is private — send it only to that one peer, never broadcast.
        // Sent before the public result so both sides see MyHandChanged before
        // RoundResolved; the host fills its own hand in the same order below.
        long? clientPeerId = ClientPeerId();
        if (clientPeerId.HasValue)
        {
            Side clientSide = _sideByPeerId[clientPeerId.Value];
            RpcId(clientPeerId.Value, MethodName.PrivateHandRpc, EncodeHand(_session.HandOf(clientSide)));
        }

        Rpc(
            MethodName.RoundResolvedRpc,
            (int)result.Player1CardFate,
            (int)result.Player2CardFate,
            winLoss,
            result.Player1DeckCount,
            result.Player2DeckCount,
            player1HandCount,
            player2HandCount,
            result.Player1SwappedCardCount,
            result.Player2SwappedCardCount,
            result.Player1TransformApplied,
            result.Player2TransformApplied,
            _session.Player1Score,
            _session.Player2Score,
            _session.RoundNumber,
            winnerSide);

        ApplyRoundResultToView(
            result.Player1CardFate,
            result.Player2CardFate,
            result.WinLoss,
            result.Player1DeckCount,
            result.Player2DeckCount,
            player1HandCount,
            player2HandCount,
            result.Player1SwappedCardCount,
            result.Player2SwappedCardCount,
            result.Player1TransformApplied,
            result.Player2TransformApplied,
            _session.Player1Score,
            _session.Player2Score,
            _session.RoundNumber,
            _session.Winner);

        // Host fills its own hand straight from the session
        View.MyHand = new List<CardName>(_session.HandOf(_mySide));
        EmitSignalMyHandChanged();

        EmitSignalRoundResolved();
        if (_session.Winner.HasValue)
        {
            EmitSignalMatchEnded(_session.Winner.Value == _mySide);
        }
    }

    /// <summary>Translates a reveal into me/opponent terms. Clears last round's animation
    /// facts, since this round has not produced any yet.</summary>
    private void ApplyRevealToView(
        CardName player1Card,
        CardName player2Card,
        bool player1IsChoosing,
        bool player2IsChoosing)
    {
        if (_mySide == Side.Player1)
        {
            View.MyCard = player1Card;
            View.OpponentCard = player2Card;
            View.OpponentIsChoosing = player2IsChoosing;
        }
        else
        {
            View.MyCard = player2Card;
            View.OpponentCard = player1Card;
            View.OpponentIsChoosing = player1IsChoosing;
        }

        View.MyCardFate = null;
        View.OpponentCardFate = null;
        View.LastRoundOutcome = null;
        View.CardIMustChooseFor = null;
        View.MySwappedCardCount = 0;
        View.OpponentSwappedCardCount = 0;
        View.MyTransformApplied = false;
        View.OpponentTransformApplied = false;
    }

    /// <summary>Fills every View field that is public information, translated from
    /// Player1/Player2 into me/opponent via _mySide. Shared by the host's direct path and
    /// the client's RoundResolvedRpc handler so the translation logic exists exactly once.
    /// Never touches MyHand — hand contents are private and travel separately.</summary>
    private void ApplyRoundResultToView(
        CardFate player1Fate,
        CardFate player2Fate,
        WinLossResult? winLoss,
        int player1DeckCount,
        int player2DeckCount,
        int player1HandCount,
        int player2HandCount,
        int player1SwappedCardCount,
        int player2SwappedCardCount,
        bool player1TransformApplied,
        bool player2TransformApplied,
        int player1Score,
        int player2Score,
        int roundNumber,
        Side? winner)
    {
        if (_mySide == Side.Player1)
        {
            View.MyCardFate = player1Fate;
            View.OpponentCardFate = player2Fate;
            View.MyDeckCount = player1DeckCount;
            View.OpponentDeckCount = player2DeckCount;
            View.OpponentHandCount = player2HandCount;
            View.MySwappedCardCount = player1SwappedCardCount;
            View.OpponentSwappedCardCount = player2SwappedCardCount;
            View.MyTransformApplied = player1TransformApplied;
            View.OpponentTransformApplied = player2TransformApplied;
            View.MyScore = player1Score;
            View.OpponentScore = player2Score;
        }
        else
        {
            View.MyCardFate = player2Fate;
            View.OpponentCardFate = player1Fate;
            View.MyDeckCount = player2DeckCount;
            View.OpponentDeckCount = player1DeckCount;
            View.OpponentHandCount = player1HandCount;
            View.MySwappedCardCount = player2SwappedCardCount;
            View.OpponentSwappedCardCount = player1SwappedCardCount;
            View.MyTransformApplied = player2TransformApplied;
            View.OpponentTransformApplied = player1TransformApplied;
            View.MyScore = player2Score;
            View.OpponentScore = player1Score;
        }

        View.LastRoundOutcome = TranslateOutcome(winLoss, _mySide);
        View.RoundNumber = roundNumber;
        View.CardIMustChooseFor = null;
        View.OpponentIsChoosing = false;

        if (winner.HasValue)
        {
            if (winner.Value == _mySide)
            {
                View.MatchResult = MatchOutcome.IWon;
            }
            else
            {
                View.MatchResult = MatchOutcome.OpponentWon;
            }
        }
    }

    private static RoundOutcome? TranslateOutcome(WinLossResult? winLoss, Side mySide)
    {
        if (winLoss == null)
        {
            return null;
        }

        if (winLoss == WinLossResult.Draw)
        {
            return RoundOutcome.Draw;
        }

        bool iWon = (winLoss == WinLossResult.Player1Win && mySide == Side.Player1)
            || (winLoss == WinLossResult.Player2Win && mySide == Side.Player2);

        if (iWon)
        {
            return RoundOutcome.MyWin;
        }

        return RoundOutcome.OpponentWin;
    }

    /// <summary>The single connected client's peer id, or null if none is connected yet.
    /// A linear scan over _sideByPeerId is fine — ENet's maxClients: 1 guarantees at most
    /// one entry.</summary>
    private long? ClientPeerId()
    {
        foreach (long peerId in _sideByPeerId.Keys)
        {
            return peerId;
        }

        return null;
    }

    private static int[] EncodeHand(IReadOnlyList<CardName> hand)
    {
        int[] encoded = new int[hand.Count];
        for (int i = 0; i < hand.Count; i++)
        {
            encoded[i] = (int)hand[i];
        }

        return encoded;
    }

    private static List<CardName> DecodeHand(int[] hand)
    {
        List<CardName> decoded = new List<CardName>();
        foreach (int value in hand)
        {
            decoded.Add((CardName)value);
        }

        return decoded;
    }
}
