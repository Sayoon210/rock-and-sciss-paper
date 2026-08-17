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
/// Reconnection is out of scope for this pass: any disconnect, on either side, ends the
/// match outright rather than trying to resume it.</summary>
public partial class GameState : Node
{
    // Sentinels for the two nullable pieces of round/match state that cross the network as
    // plain ints — WinLossResult and Side are both zero-based enums, so -1 is unambiguous.
    private const int NoWinLossSentinel = -1;
    private const int NoWinnerSentinel = -1;

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

    public MatchView View { get; private set; } = new MatchView();

    [Signal]
    public delegate void RoundResolvedEventHandler();

    [Signal]
    public delegate void SubmissionRejectedEventHandler(string reason);

    [Signal]
    public delegate void MatchStartedEventHandler();

    [Signal]
    public delegate void MatchEndedEventHandler(bool didIWin);

    [Signal]
    public delegate void OpponentLeftEventHandler();

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
    }

    /// <summary>Clears match-lifetime state only — scores, hands, the session itself.
    /// For starting a fresh match on a connection that is staying open.</summary>
    public void ResetMatch()
    {
        _session = null;
        View = new MatchView();
    }

    /// <summary>Clears match-lifetime state plus connection-lifetime state (peer/side
    /// assignments, my own side). For when the connection itself has ended.</summary>
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

        // Host fills its own View straight from the session it holds, never through a
        // network round trip to itself.
        View.MyHand = new List<CardName>(_session.HandOf(_mySide));
        View.MyDeckCount = _session.DeckCountOf(_mySide);
        View.OpponentDeckCount = _session.DeckCountOf(clientSide);
        View.OpponentHandCount = _session.HandOf(clientSide).Count;
        View.RoundNumber = _session.RoundNumber;
        View.MyScore = 0;
        View.OpponentScore = 0;
        View.MyCard = null;
        View.OpponentCard = null;
        View.MyCardFate = null;
        View.OpponentCardFate = null;
        View.LastRoundOutcome = null;
        View.MatchResult = null;

        EmitSignalMatchStarted();
    }

    /// <summary>The one entry point for playing a card, identical on both sides. On the
    /// host it resolves locally; on a client it becomes an RPC to peer 1. The caller (a
    /// CardController) never branches on host vs. client.</summary>
    public void RequestCardPlay(CardPlay play)
    {
        if (Multiplayer.IsServer())
        {
            HandleSubmission(_mySide, play, null);
        }
        else
        {
            CardPlayCodec.EncodedCardPlay encoded = CardPlayCodec.Encode(play);
            RpcId(
                1,
                MethodName.SubmitCardRpc,
                encoded.Card,
                encoded.CardToTransform,
                encoded.TransformInto,
                encoded.CardsToReturn);
        }
    }

    private void OnPeerConnected(long peerId)
    {
        // Only the host assigns sides — a client has nothing to record this into.
        if (!Multiplayer.IsServer())
        {
            return;
        }

        // Host is always Player1 (decision already made, no coin flip); the one
        // connecting client is always Player2.
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
    private void SubmitCardRpc(int card, int cardToTransform, int transformInto, int[] cardsToReturn)
    {
        long fromPeerId = Multiplayer.GetRemoteSenderId();
        if (!_sideByPeerId.TryGetValue(fromPeerId, out Side side))
        {
            RejectSubmission(fromPeerId, "Unknown peer.");
            return;
        }

        CardPlay play = CardPlayCodec.Decode(card, cardToTransform, transformInto, cardsToReturn);
        HandleSubmission(side, play, fromPeerId);
    }

    /// <summary>Host to one peer: that submission was rejected, with a reason.
    /// Authority-only so a client can never spoof this at another client.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void SubmissionRejectedRpc(string reason)
    {
        EmitSignalSubmissionRejected(reason);
    }

    /// <summary>Host to all: the public part of a resolved round — played cards, fates,
    /// win/loss, deck counts, and hand *counts* (never contents). Also carries match-end
    /// info piggybacked on the same message when the match just ended, rather than a
    /// separate round trip.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RoundResolvedRpc(
        int player1Card,
        int player2Card,
        int player1Fate,
        int player2Fate,
        int winLoss,
        int player1DeckCount,
        int player2DeckCount,
        int player1HandCount,
        int player2HandCount,
        int player1Score,
        int player2Score,
        int roundNumber,
        int winnerSide)
    {
        WinLossResult? winLossResult = null;
        if (winLoss != NoWinLossSentinel)
        {
            winLossResult = (WinLossResult)winLoss;
        }

        Side? winner = null;
        if (winnerSide != NoWinnerSentinel)
        {
            winner = (Side)winnerSide;
        }

        ApplyRoundResultToView(
            (CardName)player1Card,
            (CardName)player2Card,
            (CardFate)player1Fate,
            (CardFate)player2Fate,
            winLossResult,
            player1DeckCount,
            player2DeckCount,
            player1HandCount,
            player2HandCount,
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
    }

    /// <summary>Host to one peer: the match has started — side assignment, mulligan hand,
    /// and both deck/hand counts. The only RPC where a side assignment reaches the client,
    /// matching the host-always-Player1 decision.</summary>
    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void MatchStartedRpc(int side, int[] mulliganHand, int myDeckCount, int opponentDeckCount, int opponentHandCount)
    {
        _mySide = (Side)side;
        View.MyHand = DecodeHand(mulliganHand);
        View.MyDeckCount = myDeckCount;
        View.OpponentDeckCount = opponentDeckCount;
        View.OpponentHandCount = opponentHandCount;
        View.RoundNumber = 1;
        View.MyScore = 0;
        View.OpponentScore = 0;
        View.MyCard = null;
        View.OpponentCard = null;
        View.MyCardFate = null;
        View.OpponentCardFate = null;
        View.LastRoundOutcome = null;
        View.MatchResult = null;

        EmitSignalMatchStarted();
    }

    // --- Helpers ----------------------------------------------------------------------

    /// <summary>Runs a submission through the session and reacts to the outcome.
    /// remotePeerId is null for the host's own local play (a rejection becomes a local
    /// signal, not an RPC) and non-null for a client's play relayed here by SubmitCardRpc.
    /// MatchSession throws on an illegal or out-of-turn play; that is expected client
    /// behavior to guard against, not a bug, so it is caught here rather than left to
    /// crash the RPC handler.</summary>
    private void HandleSubmission(Side side, CardPlay play, long? remotePeerId)
    {
        if (_session == null)
        {
            RejectSubmission(remotePeerId, "No match is running.");
            return;
        }

        RoundResult? result;
        try
        {
            result = _session.SubmitCard(side, play);
        }
        catch (InvalidOperationException exception)
        {
            RejectSubmission(remotePeerId, exception.Message);
            return;
        }
        catch (ArgumentException exception)
        {
            RejectSubmission(remotePeerId, exception.Message);
            return;
        }

        if (result == null)
        {
            // Waiting on the other side's submission — nothing to broadcast yet.
            return;
        }

        BroadcastRoundResult(result);
    }

    private void RejectSubmission(long? remotePeerId, string reason)
    {
        if (remotePeerId.HasValue)
        {
            RpcId(remotePeerId.Value, MethodName.SubmissionRejectedRpc, reason);
        }
        else
        {
            EmitSignalSubmissionRejected(reason);
        }
    }

    private void BroadcastRoundResult(RoundResult result)
    {
        int winLoss = NoWinLossSentinel;
        if (result.WinLoss.HasValue)
        {
            winLoss = (int)result.WinLoss.Value;
        }

        int winnerSide = NoWinnerSentinel;
        if (_session!.Winner.HasValue)
        {
            winnerSide = (int)_session.Winner.Value;
        }

        int player1HandCount = result.Player1Hand.Count;
        int player2HandCount = result.Player2Hand.Count;

        Rpc(
            MethodName.RoundResolvedRpc,
            (int)result.Player1Card,
            (int)result.Player2Card,
            (int)result.Player1CardFate,
            (int)result.Player2CardFate,
            winLoss,
            result.Player1DeckCount,
            result.Player2DeckCount,
            player1HandCount,
            player2HandCount,
            _session.Player1Score,
            _session.Player2Score,
            _session.RoundNumber,
            winnerSide);

        ApplyRoundResultToView(
            result.Player1Card,
            result.Player2Card,
            result.Player1CardFate,
            result.Player2CardFate,
            result.WinLoss,
            result.Player1DeckCount,
            result.Player2DeckCount,
            player1HandCount,
            player2HandCount,
            _session.Player1Score,
            _session.Player2Score,
            _session.RoundNumber,
            _session.Winner);

        // Host fills its own hand straight from the session, never through a network
        // round trip to itself.
        View.MyHand = new List<CardName>(_session.HandOf(_mySide));

        // The client's hand is private — send it only to that one peer, never broadcast.
        long? clientPeerId = ClientPeerId();
        if (clientPeerId.HasValue)
        {
            Side clientSide = _sideByPeerId[clientPeerId.Value];
            RpcId(clientPeerId.Value, MethodName.PrivateHandRpc, EncodeHand(_session.HandOf(clientSide)));
        }

        EmitSignalRoundResolved();
        if (_session.Winner.HasValue)
        {
            EmitSignalMatchEnded(_session.Winner.Value == _mySide);
        }
    }

    /// <summary>Fills every View field that is public information, translated from
    /// Player1/Player2 into me/opponent via _mySide. Shared by the host's direct path and
    /// the client's RoundResolvedRpc handler so the translation logic exists exactly once.
    /// Never touches MyHand — hand contents are private and travel separately.</summary>
    private void ApplyRoundResultToView(
        CardName player1Card,
        CardName player2Card,
        CardFate player1Fate,
        CardFate player2Fate,
        WinLossResult? winLoss,
        int player1DeckCount,
        int player2DeckCount,
        int player1HandCount,
        int player2HandCount,
        int player1Score,
        int player2Score,
        int roundNumber,
        Side? winner)
    {
        if (_mySide == Side.Player1)
        {
            View.MyCard = player1Card;
            View.OpponentCard = player2Card;
            View.MyCardFate = player1Fate;
            View.OpponentCardFate = player2Fate;
            View.MyDeckCount = player1DeckCount;
            View.OpponentDeckCount = player2DeckCount;
            View.OpponentHandCount = player2HandCount;
            View.MyScore = player1Score;
            View.OpponentScore = player2Score;
        }
        else
        {
            View.MyCard = player2Card;
            View.OpponentCard = player1Card;
            View.MyCardFate = player2Fate;
            View.OpponentCardFate = player1Fate;
            View.MyDeckCount = player2DeckCount;
            View.OpponentDeckCount = player1DeckCount;
            View.OpponentHandCount = player1HandCount;
            View.MyScore = player2Score;
            View.OpponentScore = player1Score;
        }

        View.LastRoundOutcome = TranslateOutcome(winLoss, _mySide);
        View.RoundNumber = roundNumber;

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
