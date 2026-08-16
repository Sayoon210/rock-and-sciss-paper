namespace RockAndScissPaper.GameLogic;

/// <summary>One player's deck and hand together, for the operations that cross both.
/// Vanish only touches the hand — a vanished card never returns to the deck.</summary>
public sealed class DeckAndHand
{
    public Deck Deck { get; }
    public Hand Hand { get; }

    public DeckAndHand(Deck deck, Hand hand)
    {
        Deck = deck;
        Hand = hand;
    }

    public CardName Draw()
    {
        CardName card = Deck.TakeFromTop();
        Hand.Add(card);
        return card;
    }

    public void ReturnToDeckBottom(CardName card)
    {
        Hand.Remove(card);
        Deck.AddToBottom(card);
    }

    public void Vanish(CardName card)
    {
        Hand.Remove(card);
    }
}
