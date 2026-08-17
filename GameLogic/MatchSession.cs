namespace RockAndScissPaper.GameLogic;

/// <summary>Which side of the match. Not a peer id and not a connection — the translation
/// from peer id to Side happens in GameState, outside this project.</summary>
public enum Side
{
    Player1,
    Player2,
}

/// <summary>One match, from the opening mulligan to a player reaching five wins.
/// Instantiated on the host only, once both players are known.
///
/// Rounds are simultaneous: each side submits independently, and nothing resolves until
/// both have. SubmitCard therefore returns null for the first submission of a round and
/// the RoundResult for the second.
///
/// Illegal submissions throw. Callers relay client requests, so GameState is expected to
/// catch and drop them rather than let a malformed or malicious request reach here twice.</summary>
public sealed class MatchSession
{
    public const int WinsNeededForMatch = 5;
    public const int MulliganHandSize = 6;

    private readonly DeckAndHand _player1;
    private readonly DeckAndHand _player2;
    private readonly Random _rng;
    private readonly Action<string>? _log;

    private CardPlay? _player1SubmittedCard;
    private CardPlay? _player2SubmittedCard;

    /// <summary>Takes each player's assembled deck. Deck composition is the caller's job —
    /// CardDatabase lives on the Godot side, so this project never decides what goes in.
    ///
    /// log is a debug trace sink, null by default so tests stay quiet. It is injected
    /// rather than written straight to the console because this project can't reference
    /// GD.Print, and because a session that printed on its own would be the one piece of
    /// GameLogic with a side effect of its own.</summary>
    public MatchSession(
        IEnumerable<CardName> player1Deck,
        IEnumerable<CardName> player2Deck,
        Random rng,
        Action<string>? log = null)
    {
        _rng = rng;
        _log = log;
        _player1 = new DeckAndHand(new Deck(player1Deck), new Hand(Array.Empty<CardName>()));
        _player2 = new DeckAndHand(new Deck(player2Deck), new Hand(Array.Empty<CardName>()));

        Deal(Side.Player1);
        Deal(Side.Player2);
    }

    public int Player1Score { get; private set; }
    public int Player2Score { get; private set; }
    public int RoundNumber { get; private set; } = 1;

    /// <summary>The side that reached five wins, or null while the match is still running.</summary>
    public Side? Winner
    {
        get
        {
            if (Player1Score >= WinsNeededForMatch)
            {
                return Side.Player1;
            }

            if (Player2Score >= WinsNeededForMatch)
            {
                return Side.Player2;
            }

            return null;
        }
    }

    public IReadOnlyList<CardName> HandOf(Side side)
    {
        return DeckAndHandOf(side).Hand.Cards;
    }

    public int DeckCountOf(Side side)
    {
        return DeckAndHandOf(side).Deck.Count;
    }

    public bool HasSubmittedCard(Side side)
    {
        return SubmittedCardOf(side) != null;
    }

    /// <summary>Records one side's play. Returns null while waiting on the other side, and
    /// the resolved RoundResult once both have submitted.</summary>
    public RoundResult? SubmitCard(Side side, CardPlay play)
    {
        if (Winner != null)
        {
            throw new InvalidOperationException("The match is already over.");
        }

        if (HasSubmittedCard(side))
        {
            throw new InvalidOperationException($"{side} has already submitted this round.");
        }

        // Validated before it is recorded: a rejected play must leave the round exactly
        // as it was, or the side would be stuck unable to submit again.
        RoundResolver.ValidatePlay(play, DeckAndHandOf(side));

        if (side == Side.Player1)
        {
            _player1SubmittedCard = play;
        }
        else
        {
            _player2SubmittedCard = play;
        }

        _log?.Invoke($"[submit] round {RoundNumber}: {side} played {play.Card}");

        if (_player1SubmittedCard == null || _player2SubmittedCard == null)
        {
            return null;
        }

        RoundResult result = RoundResolver.Resolve(
            _player1SubmittedCard,
            _player2SubmittedCard,
            _player1,
            _player2,
            _rng);

        AdvanceToNextRound(result);

        return result;
    }

    /// <summary>Convenience for the cards that need no choice.</summary>
    public RoundResult? SubmitCard(Side side, CardName card)
    {
        return SubmitCard(side, CardPlay.WithoutChoice(card));
    }

    /// <summary>Closes out a resolved round: records its score, clears both submitted
    /// cards, and moves the round counter on. The rules of the round itself belong to
    /// RoundResolver; this only writes the outcome into the match.</summary>
    private void AdvanceToNextRound(RoundResult result)
    {
        if (result.WinLoss == WinLossResult.Player1Win)
        {
            Player1Score++;
        }
        else if (result.WinLoss == WinLossResult.Player2Win)
        {
            Player2Score++;
        }

        if (_log != null)
        {
            string verdict;
            if (result.WinLoss == null)
            {
                verdict = "no win/loss";
            }
            else
            {
                verdict = result.WinLoss.ToString()!;
            }

            _log($"[resolve] round {RoundNumber}: {result.Player1Card} ({result.Player1CardFate}) vs {result.Player2Card} ({result.Player2CardFate}) -> {verdict}, score {Player1Score}-{Player2Score}");
            _log($"[hands]   P1 {Describe(result.Player1Hand)} (deck {result.Player1DeckCount}) | P2 {Describe(result.Player2Hand)} (deck {result.Player2DeckCount})");
        }

        _player1SubmittedCard = null;
        _player2SubmittedCard = null;
        RoundNumber++;

        if (Winner != null)
        {
            _log?.Invoke($"[match]   {Winner} wins {Player1Score}-{Player2Score}");
        }
    }

    private void Deal(Side side)
    {
        DeckAndHand player = DeckAndHandOf(side);
        player.Deck.Shuffle(_rng);

        for (int i = 0; i < MulliganHandSize; i++)
        {
            player.Draw();
        }

        _log?.Invoke($"[deal] {side} mulligan: {Describe(player.Hand.Cards)} (deck {player.Deck.Count})");
    }

    private static string Describe(IReadOnlyList<CardName> cards)
    {
        return string.Join(", ", cards);
    }

    private DeckAndHand DeckAndHandOf(Side side)
    {
        if (side == Side.Player1)
        {
            return _player1;
        }

        return _player2;
    }

    private CardPlay? SubmittedCardOf(Side side)
    {
        if (side == Side.Player1)
        {
            return _player1SubmittedCard;
        }

        return _player2SubmittedCard;
    }
}
