namespace RockAndScissPaper.GameLogic;

/// <summary>Whether a side owes a choice this round, and whether it has settled it.
/// Declined is what a timeout produces — the effect simply does not run.</summary>
public enum EChoiceStatus
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
    public ECardName Player1Card { get; }
    public ECardName Player2Card { get; }
    public ECardFate Player1CardFate { get; }
    public ECardFate Player2CardFate { get; }
    public EWinLossResult? WinLoss { get; }

    /// <summary>Health lost by whichever side WinLoss says lost. Zero whenever WinLoss is
    /// null — a draw, or a round with no normal-card matchup at all.</summary>
    public int DamageDealt { get; }

    private EChoiceStatus _player1ChoiceStatus;
    private EChoiceStatus _player2ChoiceStatus;
    private CardChoice? _player1Choice;
    private CardChoice? _player2Choice;

    /// <summary>Whether 리셋 actually ran this round. Not the same as one having been played:
    /// a 조커 in the round blocks it outright (DESIGN.md), and both hands then stay exactly as
    /// they were. Recorded rather than left to be worked out from the cards, because "a 리셋
    /// was played" and "the hands were replaced" are different facts and only this side of the
    /// code knows which happened.</summary>
    public bool ResetApplied { get; }

    public RoundInProgress(
        ECardName player1Card,
        ECardName player2Card,
        ECardFate player1CardFate,
        ECardFate player2CardFate,
        EWinLossResult? winLoss,
        int damageDealt,
        bool player1MustChoose,
        bool player2MustChoose,
        bool resetApplied)
    {
        Player1Card = player1Card;
        Player2Card = player2Card;
        Player1CardFate = player1CardFate;
        Player2CardFate = player2CardFate;
        WinLoss = winLoss;
        DamageDealt = damageDealt;
        ResetApplied = resetApplied;

        if (player1MustChoose)
        {
            _player1ChoiceStatus = EChoiceStatus.Awaited;
        }
        else
        {
            _player1ChoiceStatus = EChoiceStatus.NotRequired;
        }

        if (player2MustChoose)
        {
            _player2ChoiceStatus = EChoiceStatus.Awaited;
        }
        else
        {
            _player2ChoiceStatus = EChoiceStatus.NotRequired;
        }
    }

    public bool IsAwaitingAnyChoice
    {
        get
        {
            return _player1ChoiceStatus == EChoiceStatus.Awaited
                || _player2ChoiceStatus == EChoiceStatus.Awaited;
        }
    }

    public ECardName CardOf(ESide side)
    {
        if (side == ESide.Player1)
        {
            return Player1Card;
        }

        return Player2Card;
    }

    public EChoiceStatus ChoiceStatusOf(ESide side)
    {
        if (side == ESide.Player1)
        {
            return _player1ChoiceStatus;
        }

        return _player2ChoiceStatus;
    }

    /// <summary>The choice to apply, or null when that side owes none or declined.</summary>
    public CardChoice? ChoiceOf(ESide side)
    {
        if (side == ESide.Player1)
        {
            return _player1Choice;
        }

        return _player2Choice;
    }

    /// <summary>Records a choice that has already been validated. Nothing is mutated here —
    /// every effect runs later, in fixed order, so the outcome cannot depend on which
    /// player's message reached the host first.</summary>
    public void RecordChoice(ESide side, CardChoice choice)
    {
        if (side == ESide.Player1)
        {
            _player1Choice = choice;
            _player1ChoiceStatus = EChoiceStatus.Made;
        }
        else
        {
            _player2Choice = choice;
            _player2ChoiceStatus = EChoiceStatus.Made;
        }
    }

    public void DeclineChoice(ESide side)
    {
        if (side == ESide.Player1)
        {
            _player1ChoiceStatus = EChoiceStatus.Declined;
        }
        else
        {
            _player2ChoiceStatus = EChoiceStatus.Declined;
        }
    }
}
