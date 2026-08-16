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
/// 5-win score. Both players always draw exactly one card, regardless of outcome.</summary>
public sealed class RoundResult
{
    public CardName Player1Card { get; }
    public CardName Player2Card { get; }
    public CardFate Player1CardFate { get; }
    public CardFate Player2CardFate { get; }
    public WinLossResult? WinLoss { get; }
    public CardName Player1Drew { get; }
    public CardName Player2Drew { get; }

    public RoundResult(
        CardName player1Card,
        CardName player2Card,
        CardFate player1CardFate,
        CardFate player2CardFate,
        WinLossResult? winLoss,
        CardName player1Drew,
        CardName player2Drew)
    {
        Player1Card = player1Card;
        Player2Card = player2Card;
        Player1CardFate = player1CardFate;
        Player2CardFate = player2CardFate;
        WinLoss = winLoss;
        Player1Drew = player1Drew;
        Player2Drew = player2Drew;
    }
}
