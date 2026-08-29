using System;
using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;
using RockAndScissPaper.Network;

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
    // plain ints — EWinLossResult and ESide are both zero-based enums, so -1 is unambiguous.
    private const int NO_WIN_LOSS_RESULT_SENTINEL = -1;
    private const int NO_WINNER_SIDE_SENTINEL = -1;

    /// <summary>How long a player has to answer a choice prompt before the effect is
    /// skipped. A choice phase blocks the whole match, and by then the player has already
    /// committed a card and had it revealed, so idling cannot be allowed to freeze the
    /// game for the opponent. Applies to the host's own choice too — the host is not
    /// privileged in liveness, and a host who walks away wedges the match just as hard.</summary>
    /// <summary>How long a choice phase lasts before the host gives up on every outstanding
    /// choice and lets the round finish. Public because the screen draws a gauge of it and has
    /// to be counting down the same number — it runs its own clock rather than being sent
    /// ticks, so this constant is the only thing keeping the two in agreement.</summary>
    public const double CHOICE_TIMEOUT_SECONDS = 15.0;

    /// <summary>How long a round waits for cards before the host plays for whoever has not
    /// submitted. Longer than the choice limit because submitting is decided from the whole
    /// board — both healths, the field, and every card in hand — where a choice is made from
    /// one already-narrowed set. Public for the same reason CHOICE_TIMEOUT_SECONDS is: the
    /// screen counts the same number down on its own.</summary>
    public const double SUBMIT_TIMEOUT_SECONDS = 45.0;

    /// <summary>Dead time at the start of every round, before the submission clock begins.
    /// The screen fills it with its "Round N" splash (MatchWorldView), but the number lives
    /// here rather than there because it is not only presentation: submissions are not
    /// possible during it, so it is time the host must not count against a player. The submit
    /// timer below is armed for this plus SUBMIT_TIMEOUT_SECONDS for exactly that reason —
    /// a round's real clock is 45 seconds of playable time, not 45 including the splash.</summary>
    public const double ROUND_INTRO_SECONDS = 2.0;

    public static GameState? Instance { get; private set; }

    // Host only: the authoritative match. Always null on a client.
    private MatchSession? _session;

    // Host only, connection-lifetime: which side each connected peer plays. Survives a
    // rematch (ResetMatch), cleared only when the connection itself ends (ResetConnection).
    private readonly Dictionary<long, ESide> _sideByPeerId = new Dictionary<long, ESide>();

    // Both sides. Host is always Player1 by design (no coin flip — see DevLogDoc), so this
    // default is already correct for the host at startup; a client overwrites it once
    // MatchStartedRpc assigns a side.
    private ESide _mySide = ESide.Player1;

    // Host only: armed while a choice is outstanding.
    private Timer? _choiceTimer;
    private Timer? _submitTimer;

    public MatchView View { get; private set; } = new MatchView();

    /// <summary>This match's round-by-round record, filled on both sides from the same public
    /// data View is filled from. Read-only to everyone else; MatchLogPanel renders it.
    /// One instance for the session, Reset per match rather than replaced, so a screen holding
    /// a reference to it across a rematch keeps looking at the live log.</summary>
    public MatchLog Log { get; } = new MatchLog();

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

    /// <summary>The opponent's card is in; mine is not yet, so there is nothing to reveal.
    /// Carries no card identity — that a side has submitted is public the moment it happens,
    /// but which card only becomes public at RoundRevealed (Scripts/CLAUDE.md's hidden-
    /// information rule). Never fires for my own submission; the screen already knows about
    /// that from the drag that caused it.</summary>
    [Signal] public delegate void OpponentSubmittedEventHandler();

    /// <summary>I have to choose something before this round can finish. Read
    /// View.CardIMustChooseFor for which card. Only ever reaches the one player who owes
    /// the choice.</summary>
    [Signal] public delegate void ChoiceRequiredEventHandler();

    /// <summary>Fires whenever View.MyHand is replaced. Separate from RoundResolved because
    /// a client's hand arrives in its own targeted RPC — a UI that only redrew on
    /// RoundResolved would render the previous round's hand and never correct it.</summary>
    [Signal] public delegate void MyHandChangedEventHandler();

    /// <summary>The opponent's own head-look direction just changed. Purely cosmetic — not
    /// part of match state, never validated, and does not touch MatchView — so MatchWorldView
    /// (or whatever is showing the opponent's character) listens directly rather than this
    /// being folded into a round update. localDeltaInBoneSpace is relative to the SENDER's own
    /// rest head-bone frame, not a world-space rotation — see BoneLookRotator's doc comment for
    /// why: MySeat and OpponentSeat face 180 degrees apart, and a world-frame delta built from
    /// one character's own facing does not mean the same thing applied to the other.</summary>
    [Signal] public delegate void OpponentLookChangedEventHandler(Quaternion localDeltaInBoneSpace);

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

        _submitTimer = new Timer();
        _submitTimer.OneShot = true;
        // Intro plus play, not just play — see ROUND_INTRO_SECONDS. The splash is unskippable
        // and blocks the hand view, so counting it against the submission clock would quietly
        // shorten every round by that much.
        _submitTimer.WaitTime = ROUND_INTRO_SECONDS + SUBMIT_TIMEOUT_SECONDS;
        _submitTimer.Timeout += OnSubmitTimedOut;
        AddChild(_submitTimer);
    }

    // keep connection, only reset match (session & view)
    public void ResetMatch()
    {
        _session = null;
        View = new MatchView();
        Log.Reset();
        StopChoiceTimer();
        StopSubmitTimer();
    }

    // reset connection and match
    public void ResetConnection()
    {
        ResetMatch();
        _sideByPeerId.Clear();
        _mySide = ESide.Player1;
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
        _mySide = ESide.Player1;
        ESide clientSide = _sideByPeerId[clientPeerId.Value];

        List<ECardName> player1Deck = DeckAssembler.BuildDeck();
        List<ECardName> player2Deck = DeckAssembler.BuildDeck();
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
        View.MyHand = new List<ECardName>(_session.HandOf(_mySide));
        View.MyDeckCount = _session.DeckCountOf(_mySide);
        View.OpponentDeckCount = _session.DeckCountOf(clientSide);
        View.OpponentHandCount = _session.HandOf(clientSide).Count;
        View.RoundNumber = _session.RoundNumber;
        View.SubmissionPhaseActive = true;

        EmitSignalMyHandChanged();
        EmitSignalMatchStarted();

        StartSubmitTimer();
    }

    /// <summary>The one entry point for playing a card, identical on both sides.
    /// On the host it resolves locally;
    /// on a client it becomes an RPC to peer 1.
    /// The caller (a CardController) never branches on host vs. client.</summary>
    public void RequestCardPlay(ECardName card)
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

    /// <summary>Relays my own head-look direction to the opponent, so it shows on their
    /// screen too. Not a match action — no session involvement, no validation, works
    /// identically whether I am host or client, which RequestCardPlay/RequestChoice's
    /// server-resolves-locally shape does not fit. localDeltaInBoneSpace is relative to MY OWN
    /// rest head-bone frame (Scripts/Match3D/BoneLookRotator.cs), not a world-space rotation —
    /// the receiver composes it onto the OPPONENT's own rest head bone, which faces a different
    /// absolute direction than mine.</summary>
    public void SendMyLookDirection(Quaternion localDeltaInBoneSpace)
    {
        long? targetPeerId = Multiplayer.IsServer() ? ClientPeerId() : 1;
        if (!targetPeerId.HasValue)
        {
            return;
        }

        // Godot errors on an RPC to a peer that is not connected, and this one is sent from
        // _Process on a timer rather than in response to anything — so unlike the match RPCs,
        // which only fire inside a running match, it will keep firing at whatever is or is not
        // on the other end. That covers the scene run on its own with no peer at all, a client
        // whose connection dropped, and the window between MatchWorld loading and the peer
        // actually being up. Checking the live peer list rather than a status flag also catches
        // a _sideByPeerId entry left behind by a peer that has since gone.
        if (Array.IndexOf(Multiplayer.GetPeers(), (int)targetPeerId.Value) < 0)
        {
            return;
        }

        RpcId(targetPeerId.Value, MethodName.OpponentLookChangedRpc, localDeltaInBoneSpace);
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
        _sideByPeerId[peerId] = ESide.Player2;
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
        if (!_sideByPeerId.TryGetValue(fromPeerId, out ESide side))
        {
            RejectRequest(fromPeerId, "Unknown peer.");
            return;
        }

        HandleSubmission(side, (ECardName)card, fromPeerId);
    }

    /// <summary>Client to host: "here is my choice." roundNumber stamps which round it was
    /// meant for, so an answer that arrives after its round already timed out is dropped
    /// instead of being applied to the next one.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmitChoiceRpc(int roundNumber, int cardToTransform, int transformInto, int[] cardsToReturn)
    {
        long fromPeerId = Multiplayer.GetRemoteSenderId();
        if (!_sideByPeerId.TryGetValue(fromPeerId, out ESide side))
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

        ECardName? promptedCard = _session.CardAwaitingChoiceFrom(side);
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

    /// <summary>Either side to the other: my current head-look direction. AnyPeer because
    /// either side calls this on the other — host and client are symmetric here, unlike the
    /// match-rule RPCs above, which are always client-to-host. Unreliable — the next update is
    /// due within LOOK_SEND_INTERVAL_SECONDS regardless, so a dropped packet is not worth
    /// paying for guaranteed delivery.</summary>
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.UnreliableOrdered)]
    private void OpponentLookChangedRpc(Quaternion localDeltaInBoneSpace)
    {
        EmitSignalOpponentLookChanged(localDeltaInBoneSpace);
    }

    /// <summary>Host to one peer: that request was rejected, with a reason. Covers a
    /// rejected card and a rejected choice alike. Authority-only so a client can never
    /// spoof this at another client.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestRejectedRpc(string reason)
    {
        EmitSignalRequestRejected(reason);
    }

    /// <summary>Host to one peer: the other side's card is in. See NotifyOpponentSubmitted.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void OpponentSubmittedRpc()
    {
        EmitSignalOpponentSubmitted();
    }

    /// <summary>Host to all: both played cards are now public, and whether each side still
    /// owes a choice. Sent on every round, including ones that finish immediately, so a
    /// round always has the same shape on screen. Carries no hidden information — which
    /// cards need a choice follows from the rules and the revealed cards alone.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RoundRevealedRpc(int player1Card, int player2Card, bool player1IsChoosing, bool player2IsChoosing)
    {
        ApplyRevealToView(
            (ECardName)player1Card, (ECardName)player2Card, player1IsChoosing, player2IsChoosing);
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
        View.CardIMustChooseFor = (ECardName)card;

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
        bool resetApplied,
        int player1Health,
        int player2Health,
        int roundNumber,
        int winnerSide)
    {
        EWinLossResult? winLossResult = null;
        if (winLoss != NO_WIN_LOSS_RESULT_SENTINEL)
        {
            winLossResult = (EWinLossResult)winLoss;
        }

        ESide? winner = null;
        if (winnerSide != NO_WINNER_SIDE_SENTINEL)
        {
            winner = (ESide)winnerSide;
        }

        ApplyRoundResultToView(
            (ECardFate)player1Fate,
            (ECardFate)player2Fate,
            winLossResult,
            player1DeckCount,
            player2DeckCount,
            player1HandCount,
            player2HandCount,
            player1SwappedCardCount,
            player2SwappedCardCount,
            player1TransformApplied,
            player2TransformApplied,
            resetApplied,
            player1Health,
            player2Health,
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
        Log.Reset();
        _mySide = (ESide)side;

        View.MyHand = DecodeHand(mulliganHand);
        View.MyDeckCount = myDeckCount;
        View.OpponentDeckCount = opponentDeckCount;
        View.OpponentHandCount = opponentHandCount;
        View.RoundNumber = 1;
        View.SubmissionPhaseActive = true;

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
    private void HandleSubmission(ESide side, ECardName card, long? remotePeerId)
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
            // Waiting on the other side's submission — nothing to reveal yet, but the side
            // still deciding should know theirs is in.
            NotifyOpponentSubmitted(side);
            return;
        }

        HandleReveal(reveal);
    }

    /// <summary>Host only. Tells whichever side has not submitted yet that the other side's
    /// card is in, without saying what it is. Mirrors PromptOneChooser's shape: a local
    /// signal for the host's own screen, an RPC for the client's.</summary>
    private void NotifyOpponentSubmitted(ESide submittedSide)
    {
        ESide sideToNotify = submittedSide == ESide.Player1 ? ESide.Player2 : ESide.Player1;

        if (sideToNotify == _mySide)
        {
            EmitSignalOpponentSubmitted();
            return;
        }

        long? clientPeerId = ClientPeerId();
        if (clientPeerId.HasValue)
        {
            RpcId(clientPeerId.Value, MethodName.OpponentSubmittedRpc);
        }
    }

    /// <summary>What follows a completed round of submissions, however the cards got there —
    /// played by a person or filled in by OnSubmitTimedOut. Both must take exactly the same
    /// route out, or a timed-out round would resolve differently from a played one.</summary>
    private void HandleReveal(RoundReveal reveal)
    {
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
    private void HandleChoice(ESide side, CardChoice choice, long? remotePeerId)
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
        RoundResult? result = _session.DeclineChoice(ESide.Player1);
        if (result == null)
        {
            result = _session.DeclineChoice(ESide.Player2);
        }

        if (result != null)
        {
            BroadcastRoundResult(result);
        }
    }

    /// <summary>Host only. Plays for every side that let the clock run out. Not a penalty —
    /// the card is drawn at random from that player's own hand and the round carries on
    /// normally, the same shape as a declined choice simply not running its effect.</summary>
    private void OnSubmitTimedOut()
    {
        if (_session == null)
        {
            return;
        }

        RoundReveal? reveal = _session.SubmitRandomCardForIdleSides();
        if (reveal == null)
        {
            return;
        }

        HandleReveal(reveal);
    }

    private void StartSubmitTimer()
    {
        if (_submitTimer != null)
        {
            _submitTimer.Start();
        }
    }

    private void StopSubmitTimer()
    {
        if (_submitTimer != null)
        {
            _submitTimer.Stop();
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
        // Both cards are in, whatever brought them — the round is not taking any more.
        StopSubmitTimer();

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
        PromptOneChooser(ESide.Player1);
        PromptOneChooser(ESide.Player2);

        if (_choiceTimer != null)
        {
            _choiceTimer.Start();
        }
    }

    private void PromptOneChooser(ESide side)
    {
        ECardName? card = _session!.CardAwaitingChoiceFrom(side);
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
            View.MyHand = new List<ECardName>(_session.HandOf(side));
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

        // The next round opens the moment this one is broadcast, so its clock starts here.
        // A match that has just been won opens no round, and ApplyRoundResultToView says the
        // same thing to both screens through SubmissionPhaseActive.
        if (!_session!.Winner.HasValue)
        {
            StartSubmitTimer();
        }

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
            ESide clientSide = _sideByPeerId[clientPeerId.Value];
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
            result.ResetApplied,
            _session.Player1Health,
            _session.Player2Health,
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
            result.ResetApplied,
            _session.Player1Health,
            _session.Player2Health,
            _session.RoundNumber,
            _session.Winner);

        // Host fills its own hand straight from the session
        View.MyHand = new List<ECardName>(_session.HandOf(_mySide));
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
        ECardName player1Card,
        ECardName player2Card,
        bool player1IsChoosing,
        bool player2IsChoosing)
    {
        if (_mySide == ESide.Player1)
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
        View.SubmissionPhaseActive = false;
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
        ECardFate player1Fate,
        ECardFate player2Fate,
        EWinLossResult? winLoss,
        int player1DeckCount,
        int player2DeckCount,
        int player1HandCount,
        int player2HandCount,
        int player1SwappedCardCount,
        int player2SwappedCardCount,
        bool player1TransformApplied,
        bool player2TransformApplied,
        bool resetApplied,
        int player1Health,
        int player2Health,
        int roundNumber,
        ESide? winner)
    {
        if (_mySide == ESide.Player1)
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
            View.MyHealth = player1Health;
            View.OpponentHealth = player2Health;
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
            View.MyHealth = player2Health;
            View.OpponentHealth = player1Health;
        }

        View.ResetApplied = resetApplied;
        View.LastRoundOutcome = TranslateOutcome(winLoss, _mySide);
        View.RoundNumber = roundNumber;
        View.CardIMustChooseFor = null;
        View.OpponentIsChoosing = false;
        View.SubmissionPhaseActive = !winner.HasValue;

        if (winner.HasValue)
        {
            if (winner.Value == _mySide)
            {
                View.MatchResult = EMatchOutcome.IWon;
            }
            else
            {
                View.MatchResult = EMatchOutcome.OpponentWon;
            }
        }

        RecordRoundInLog();
    }

    /// <summary>Appends the round that just finished to Log. Called from the tail of
    /// ApplyRoundResultToView because that is the one place both sides pass through with the
    /// round fully applied — the host in-process, a client off RoundResolvedRpc — so neither
    /// gets a log the other does not.
    ///
    /// The round number is View.RoundNumber - 1: MatchSession increments its counter as the
    /// last step of recording a resolution, so the number that arrives here already names the
    /// NEXT round rather than the one being logged.</summary>
    private void RecordRoundInLog()
    {
        EMatchLogOutcome outcome;
        switch (View.LastRoundOutcome)
        {
            case ERoundOutcome.MyWin:
                outcome = EMatchLogOutcome.MyWin;
                break;

            case ERoundOutcome.OpponentWin:
                outcome = EMatchLogOutcome.OpponentWin;
                break;

            case ERoundOutcome.Draw:
                outcome = EMatchLogOutcome.Draw;
                break;

            default:
                outcome = EMatchLogOutcome.NoContest;
                break;
        }

        Log.RecordRound(
            View.RoundNumber - 1,
            View.MyCard,
            View.OpponentCard,
            outcome,
            View.MyHealth,
            View.OpponentHealth);

        if (View.MatchResult.HasValue)
        {
            Log.RecordMatchEnd(View.MatchResult.Value == EMatchOutcome.IWon);
        }
    }

    private static ERoundOutcome? TranslateOutcome(EWinLossResult? winLoss, ESide mySide)
    {
        if (winLoss == null)
        {
            return null;
        }

        if (winLoss == EWinLossResult.Draw)
        {
            return ERoundOutcome.Draw;
        }

        bool iWon = (winLoss == EWinLossResult.Player1Win && mySide == ESide.Player1)
            || (winLoss == EWinLossResult.Player2Win && mySide == ESide.Player2);

        if (iWon)
        {
            return ERoundOutcome.MyWin;
        }

        return ERoundOutcome.OpponentWin;
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

    private static int[] EncodeHand(IReadOnlyList<ECardName> hand)
    {
        int[] encoded = new int[hand.Count];
        for (int i = 0; i < hand.Count; i++)
        {
            encoded[i] = (int)hand[i];
        }

        return encoded;
    }

    private static List<ECardName> DecodeHand(int[] hand)
    {
        List<ECardName> decoded = new List<ECardName>();
        foreach (int value in hand)
        {
            decoded.Add((ECardName)value);
        }

        return decoded;
    }
}
