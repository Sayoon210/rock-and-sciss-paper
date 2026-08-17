namespace RockAndScissPaper.GameLogic;

/// <summary>A card being played, together with whatever the player had to choose in order
/// to play it. Most cards need no choice; Transform and Swap do, and DESIGN.md guarantees
/// every such choice is made at submission time — no card asks the player to decide
/// something after seeing a result.</summary>
public sealed class CardPlay
{
    public CardName Card { get; }

    /// <summary>Transform only: the hand card being changed.</summary>
    public CardName? CardToTransform { get; }

    /// <summary>Transform only: what it becomes.</summary>
    public CardName? TransformInto { get; }

    /// <summary>Swap only: the hand cards going back into the deck. Empty for every other card.</summary>
    public IReadOnlyList<CardName> CardsToReturn { get; }

    private CardPlay(
        CardName card,
        CardName? cardToTransform,
        CardName? transformInto,
        IEnumerable<CardName> cardsToReturn)
    {
        Card = card;
        CardToTransform = cardToTransform;
        TransformInto = transformInto;
        CardsToReturn = new List<CardName>(cardsToReturn);
    }

    /// <summary>A card that needs no choice: every normal card, Dummy, Joker, Reset,
    /// Refill, and Draw.</summary>
    public static CardPlay WithoutChoice(CardName card)
    {
        return new CardPlay(card, null, null, Array.Empty<CardName>());
    }

    public static CardPlay Transforming(CardName cardToTransform, CardName transformInto)
    {
        return new CardPlay(CardName.Transform, cardToTransform, transformInto, Array.Empty<CardName>());
    }

    public static CardPlay Swapping(IEnumerable<CardName> cardsToReturn)
    {
        return new CardPlay(CardName.Swap, null, null, cardsToReturn);
    }
}
