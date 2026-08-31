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

    /// <summary>Takes one card. Never fails: the deck restocks itself rather than running out,
    /// so the null this used to return for an empty deck — and the 덱 고갈 loss it stood for —
    /// no longer has a case to describe.</summary>
    public ECardName Draw(Random rng)
    {
        ECardName card = Deck.TakeFromTop(rng);
        Hand.Add(card);
        return card;
    }

    /// <summary>Deals a whole new hand, but only once the last one is spent. This is the shape
    /// of the round now: five cards, one per round, and nothing arrives until all five are
    /// gone — so both players always know how many plays the other has left, even though not
    /// which ones.
    ///
    /// Checked rather than counted up to, because a hand is only ever refilled from empty. A
    /// version that topped up to HAND_SIZE would quietly paper over an ability that took a card
    /// out mid-cycle, and hide the very thing this rule is meant to make visible.</summary>
    public void RefillHandIfSpent(Random rng, int handSize)
    {
        if (Hand.Cards.Count > 0)
        {
            return;
        }

        for (int i = 0; i < handSize; i++)
        {
            Draw(rng);
        }
    }

    public void ReturnToDeckBottom(ECardName card)
    {
        Hand.Remove(card);
        Deck.AddToBottom(card);
    }

    public void Vanish(ECardName card)
    {
        Hand.Remove(card);
    }
}
