namespace RockAndScissPaper.GameLogic;

/// <summary>What a player chose so a special card's effect can be carried out. Only 교체
/// and 변화 need one, and per DESIGN.md the choice is made after both cards are revealed —
/// so a choice is a separate message from the card it belongs to, not part of it.
///
/// It deliberately does not name its own card. The host already knows which card it
/// prompted for; letting the choice carry a card would let a client decide which effect
/// runs by shaping its payload.</summary>
public sealed class CardChoice
{
    /// <summary>변화 only: the hand card being changed.</summary>
    public CardName? CardToTransform { get; }

    /// <summary>변화 only: what it becomes.</summary>
    public CardName? TransformInto { get; }

    /// <summary>교체 only: the hand cards going back into the deck. Empty otherwise.</summary>
    public IReadOnlyList<CardName> CardsToReturn { get; }

    private CardChoice(
        CardName? cardToTransform,
        CardName? transformInto,
        IEnumerable<CardName> cardsToReturn)
    {
        CardToTransform = cardToTransform;
        TransformInto = transformInto;
        CardsToReturn = new List<CardName>(cardsToReturn);
    }

    public static CardChoice Transforming(CardName cardToTransform, CardName transformInto)
    {
        return new CardChoice(cardToTransform, transformInto, Array.Empty<CardName>());
    }

    public static CardChoice Swapping(IEnumerable<CardName> cardsToReturn)
    {
        return new CardChoice(null, null, cardsToReturn);
    }
}
