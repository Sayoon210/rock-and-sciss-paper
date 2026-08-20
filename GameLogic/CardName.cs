namespace RockAndScissPaper.GameLogic;

/// <summary>Every card identity in the game. This is the only thing GameLogic needs to know
/// about a card — no display name, no art, no flavor text. See CardType below for how
/// a card is grouped for round resolution.
///
/// The order matters outside this file: Data/Cards/*.tres each store their card as this
/// enum's integer value, so adding a name anywhere but the end — or taking one out —
/// renumbers every name after it and those files have to be renumbered to match. Nothing
/// checks that for you; a mismatch surfaces as an out-of-range card at deck assembly.</summary>
public enum CardName
{
    // Normal — compared via WinLossRules
    Rock,
    Paper,
    Scissors,

    // No effect, vanishes on play
    Dummy,

    // Destroys the opponent's card and blocks its effect, then vanishes itself
    Joker,

    // Special — has an ICardEffect, vanishes after use
    Reset,
    Swap,
    Transform,
    Draw,
}

/// <summary>How a card is handled during round resolution. RoundResolver dispatches on
/// this rather than on CardName directly, and rather than on a type hierarchy —
/// see CLAUDE.md's "Avoid: subclass trees for card variants".</summary>
public enum CardType
{
    Normal,
    Dummy,
    Joker,
    Special,
}

public static class CardNameExtensions
{
    public static CardType GetCardType(this CardName name)
    {
        switch (name)
        {
            case CardName.Rock:
            case CardName.Paper:
            case CardName.Scissors:
                return CardType.Normal;

            case CardName.Dummy:
                return CardType.Dummy;

            case CardName.Joker:
                return CardType.Joker;

            case CardName.Reset:
            case CardName.Swap:
            case CardName.Transform:
            case CardName.Draw:
                return CardType.Special;

            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unhandled CardName — every card must map to a type.");
        }
    }

    public static bool IsNormal(this CardName name)
    {
        return name.GetCardType() == CardType.Normal;
    }

    public static NormalCard ToNormalCard(this CardName name)
    {
        switch (name)
        {
            case CardName.Rock:
                return NormalCard.Rock;

            case CardName.Paper:
                return NormalCard.Paper;

            case CardName.Scissors:
                return NormalCard.Scissors;

            default:
                throw new ArgumentException($"{name} is not a normal card.", nameof(name));
        }
    }
}
