namespace RockAndScissPaper.GameLogic;

/// <summary>What happens to a played card after a round: back into the deck, or gone
/// for good. Not derivable from the card's CardType alone — a normal card that gets hit
/// by a Joker vanishes instead of its usual deck-bottom return.</summary>
public enum CardFate
{
    ReturnedToDeckBottom,
    Vanished,
}

/// <summary>What happened in one round. WinLoss is null whenever a special, dummy, or
/// Joker card was involved — those rounds have no win/loss and do not count toward the
/// 10-win score.
///
/// The hands are the full post-round contents rather than "what each player drew",
/// because Reset replaces both hands outright and Draw adds two extra cards, so a single
/// drawn card can't describe the round. GameState broadcasts the counts publicly and
/// sends each hand only to the player it belongs to.
///
/// The swapped counts and transform flags exist so the opponent's screen can animate what
/// happened. They are counts and booleans on purpose: 교체 and 변화 both leave hand size
/// unchanged, so neither is derivable from the public counts, and neither reveals which
/// cards were involved.</summary>
public sealed class RoundResult
{
    public CardName Player1Card { get; }
    public CardName Player2Card { get; }
    public CardFate Player1CardFate { get; }
    public CardFate Player2CardFate { get; }
    public WinLossResult? WinLoss { get; }
    public IReadOnlyList<CardName> Player1Hand { get; }
    public IReadOnlyList<CardName> Player2Hand { get; }
    public int Player1DeckCount { get; }
    public int Player2DeckCount { get; }

    /// <summary>How many cards each side put back into their deck with 교체. Zero when
    /// they played something else, swapped nothing, or let the choice time out.</summary>
    public int Player1SwappedCardCount { get; }
    public int Player2SwappedCardCount { get; }

    /// <summary>Whether each side's 변화 actually ran. False when they played something
    /// else, were blocked by a Joker, or let the choice time out.</summary>
    public bool Player1TransformApplied { get; }
    public bool Player2TransformApplied { get; }

    public RoundResult(
        CardName player1Card,
        CardName player2Card,
        CardFate player1CardFate,
        CardFate player2CardFate,
        WinLossResult? winLoss,
        IEnumerable<CardName> player1Hand,
        IEnumerable<CardName> player2Hand,
        int player1DeckCount,
        int player2DeckCount,
        int player1SwappedCardCount,
        int player2SwappedCardCount,
        bool player1TransformApplied,
        bool player2TransformApplied)
    {
        Player1Card = player1Card;
        Player2Card = player2Card;
        Player1CardFate = player1CardFate;
        Player2CardFate = player2CardFate;
        WinLoss = winLoss;

        // Copied, not referenced — Hand.Cards is a live view over a list that keeps
        // changing as the match goes on.
        Player1Hand = new List<CardName>(player1Hand);
        Player2Hand = new List<CardName>(player2Hand);

        Player1DeckCount = player1DeckCount;
        Player2DeckCount = player2DeckCount;
        Player1SwappedCardCount = player1SwappedCardCount;
        Player2SwappedCardCount = player2SwappedCardCount;
        Player1TransformApplied = player1TransformApplied;
        Player2TransformApplied = player2TransformApplied;
    }
}
