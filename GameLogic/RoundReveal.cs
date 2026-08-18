namespace RockAndScissPaper.GameLogic;

/// <summary>Both played cards, made public, plus who still owes a choice before the round
/// can finish. Returned by MatchSession.SubmitCard once both sides have submitted.
///
/// Invariant: Result is non-null exactly when neither side must choose — that is, the
/// round was able to finish in the same call and there is no choice phase at all.</summary>
public sealed class RoundReveal
{
    public CardName Player1Card { get; }
    public CardName Player2Card { get; }
    public bool Player1MustChoose { get; }
    public bool Player2MustChoose { get; }

    /// <summary>The finished round, or null while waiting on a choice.</summary>
    public RoundResult? Result { get; }

    public RoundReveal(
        CardName player1Card,
        CardName player2Card,
        bool player1MustChoose,
        bool player2MustChoose,
        RoundResult? result)
    {
        Player1Card = player1Card;
        Player2Card = player2Card;
        Player1MustChoose = player1MustChoose;
        Player2MustChoose = player2MustChoose;
        Result = result;
    }
}
