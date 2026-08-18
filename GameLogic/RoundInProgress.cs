namespace RockAndScissPaper.GameLogic;

/// <summary>Whether a side owes a choice this round, and whether it has settled it.
/// Declined is what a timeout produces — the effect simply does not run.</summary>
public enum ChoiceStatus
{
    NotRequired,
    Awaited,
    Made,
    Declined,
}

/// <summary>A round that has been revealed but not finished: both cards are public and
/// their fates already applied, and every choiceless effect has already run, but one or
/// both sides still owe a choice.
///
/// This exists as one object rather than a handful of nullable fields on MatchSession so
/// that ending a round is a single `_round = null` instead of remembering to clear eleven
/// things — forgetting one of those is exactly the shape of the bug that wedged a match
/// before.</summary>
public sealed class RoundInProgress
{
    public CardName Player1Card { get; }
    public CardName Player2Card { get; }
    public CardFate Player1CardFate { get; }
    public CardFate Player2CardFate { get; }
    public WinLossResult? WinLoss { get; }

    private ChoiceStatus _player1ChoiceStatus;
    private ChoiceStatus _player2ChoiceStatus;
    private CardChoice? _player1Choice;
    private CardChoice? _player2Choice;

    public RoundInProgress(
        CardName player1Card,
        CardName player2Card,
        CardFate player1CardFate,
        CardFate player2CardFate,
        WinLossResult? winLoss,
        bool player1MustChoose,
        bool player2MustChoose)
    {
        Player1Card = player1Card;
        Player2Card = player2Card;
        Player1CardFate = player1CardFate;
        Player2CardFate = player2CardFate;
        WinLoss = winLoss;

        if (player1MustChoose)
        {
            _player1ChoiceStatus = ChoiceStatus.Awaited;
        }
        else
        {
            _player1ChoiceStatus = ChoiceStatus.NotRequired;
        }

        if (player2MustChoose)
        {
            _player2ChoiceStatus = ChoiceStatus.Awaited;
        }
        else
        {
            _player2ChoiceStatus = ChoiceStatus.NotRequired;
        }
    }

    public bool IsAwaitingAnyChoice
    {
        get
        {
            return _player1ChoiceStatus == ChoiceStatus.Awaited
                || _player2ChoiceStatus == ChoiceStatus.Awaited;
        }
    }

    public CardName CardOf(Side side)
    {
        if (side == Side.Player1)
        {
            return Player1Card;
        }

        return Player2Card;
    }

    public ChoiceStatus ChoiceStatusOf(Side side)
    {
        if (side == Side.Player1)
        {
            return _player1ChoiceStatus;
        }

        return _player2ChoiceStatus;
    }

    /// <summary>The choice to apply, or null when that side owes none or declined.</summary>
    public CardChoice? ChoiceOf(Side side)
    {
        if (side == Side.Player1)
        {
            return _player1Choice;
        }

        return _player2Choice;
    }

    /// <summary>Records a choice that has already been validated. Nothing is mutated here —
    /// every effect runs later, in fixed order, so the outcome cannot depend on which
    /// player's message reached the host first.</summary>
    public void RecordChoice(Side side, CardChoice choice)
    {
        if (side == Side.Player1)
        {
            _player1Choice = choice;
            _player1ChoiceStatus = ChoiceStatus.Made;
        }
        else
        {
            _player2Choice = choice;
            _player2ChoiceStatus = ChoiceStatus.Made;
        }
    }

    public void DeclineChoice(Side side)
    {
        if (side == Side.Player1)
        {
            _player1ChoiceStatus = ChoiceStatus.Declined;
        }
        else
        {
            _player2ChoiceStatus = ChoiceStatus.Declined;
        }
    }
}
