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

    private CardName? _player1Submission;
    private CardName? _player2Submission;

    /// <summary>Takes each player's assembled deck. Deck composition is the caller's job —
    /// CardDatabase lives on the Godot side, so this project never decides what goes in.</summary>
    public MatchSession(IEnumerable<CardName> player1Deck, IEnumerable<CardName> player2Deck, Random rng)
    {
        _rng = rng;
        _player1 = new DeckAndHand(new Deck(player1Deck), new Hand(Array.Empty<CardName>()));
        _player2 = new DeckAndHand(new Deck(player2Deck), new Hand(Array.Empty<CardName>()));

        Deal(_player1);
        Deal(_player2);
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
        return ZoneOf(side).Hand.Cards;
    }

    public int DeckCountOf(Side side)
    {
        return ZoneOf(side).Deck.Count;
    }

    public bool HasSubmitted(Side side)
    {
        return SubmissionOf(side) != null;
    }

    /// <summary>Records one side's play. Returns null while waiting on the other side, and
    /// the resolved RoundResult once both have submitted.</summary>
    public RoundResult? SubmitCard(Side side, CardName card)
    {
        if (Winner != null)
        {
            throw new InvalidOperationException("The match is already over.");
        }

        if (HasSubmitted(side))
        {
            throw new InvalidOperationException($"{side} has already submitted this round.");
        }

        if (!ZoneOf(side).Hand.Contains(card))
        {
            throw new ArgumentException($"{side} does not hold {card}.", nameof(card));
        }

        if (side == Side.Player1)
        {
            _player1Submission = card;
        }
        else
        {
            _player2Submission = card;
        }

        if (_player1Submission == null || _player2Submission == null)
        {
            return null;
        }

        return ResolveRound(_player1Submission.Value, _player2Submission.Value);
    }

    private RoundResult ResolveRound(CardName player1Card, CardName player2Card)
    {
        RoundResult result = RoundResolver.Resolve(player1Card, player2Card, _player1, _player2, _rng);

        if (result.WinLoss == WinLossResult.Player1Win)
        {
            Player1Score++;
        }
        else if (result.WinLoss == WinLossResult.Player2Win)
        {
            Player2Score++;
        }

        _player1Submission = null;
        _player2Submission = null;
        RoundNumber++;

        return result;
    }

    private void Deal(DeckAndHand player)
    {
        player.Deck.Shuffle(_rng);

        for (int i = 0; i < MulliganHandSize; i++)
        {
            player.Draw();
        }
    }

    private DeckAndHand ZoneOf(Side side)
    {
        if (side == Side.Player1)
        {
            return _player1;
        }

        return _player2;
    }

    private CardName? SubmissionOf(Side side)
    {
        if (side == Side.Player1)
        {
            return _player1Submission;
        }

        return _player2Submission;
    }
}
