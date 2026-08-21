namespace RockAndScissPaper.GameLogic;

/// <summary>Every card identity in the game. This is the only thing GameLogic needs to know
/// about a card — no display name, no art, no flavor text. See ECardType below for how
/// a card is grouped for round resolution.
///
/// The order matters outside this file: Data/Cards/*.tres each store their card as this
/// enum's integer value, so adding a name anywhere but the end — or taking one out —
/// renumbers every name after it and those files have to be renumbered to match. Nothing
/// checks that for you; a mismatch surfaces as an out-of-range card at deck assembly.</summary>
public enum ECardName
{
    // Normal — compared via WinLossRules
    Rock,
    Paper,
    Scissors,

    // No effect, vanishes on play
    Blank,

    // Destroys the opponent's card and blocks its effect, then vanishes itself
    Joker,

    // Ability — has an ICardEffect, vanishes after use
    Reset,
    Swap,
    Transform,
    Draw,
}

/// <summary>How a card is handled during round resolution. RoundResolver dispatches on
/// this rather than on ECardName directly, and rather than on a type hierarchy —
/// see CLAUDE.md's "Avoid: subclass trees for card variants".</summary>
public enum ECardType
{
    Normal,
    Blank,
    Joker,
    Ability,
}

public static class CardNameExtensions
{
    public static ECardType GetCardType(this ECardName name)
    {
        switch (name)
        {
            case ECardName.Rock:
            case ECardName.Paper:
            case ECardName.Scissors:
                return ECardType.Normal;

            case ECardName.Blank:
                return ECardType.Blank;

            case ECardName.Joker:
                return ECardType.Joker;

            case ECardName.Reset:
            case ECardName.Swap:
            case ECardName.Transform:
            case ECardName.Draw:
                return ECardType.Ability;

            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unhandled ECardName — every card must map to a type.");
        }
    }

    public static bool IsNormal(this ECardName name)
    {
        return name.GetCardType() == ECardType.Normal;
    }

    public static ENormalCard ToNormalCard(this ECardName name)
    {
        switch (name)
        {
            case ECardName.Rock:
                return ENormalCard.Rock;

            case ECardName.Paper:
                return ENormalCard.Paper;

            case ECardName.Scissors:
                return ENormalCard.Scissors;

            default:
                throw new ArgumentException($"{name} is not a normal card.", nameof(name));
        }
    }
}
