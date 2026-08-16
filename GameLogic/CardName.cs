namespace RockAndScissPaper.GameLogic;

/// <summary>Every card identity in the game. This is the only thing GameLogic needs to know
/// about a card — no display name, no art, no flavor text. See CardType below for how
/// a card is grouped for round resolution.</summary>
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

    // Special — has an ICardEffect via SpecialEffectRegistry, vanishes after use
    Reset,
    Swap,
    Transform,
    Refill,
    Foresight,
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
            case CardName.Refill:
            case CardName.Foresight:
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
