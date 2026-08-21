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

    /// <summary>Null when the deck is empty rather than throwing — an empty deck mid-match
    /// means this player has lost (DESIGN.md, "덱 고갈"), not that the game is broken. Callers
    /// that draw more than one card in a loop (교체/리셋/드로우) rely on this to stop taking
    /// cards instead of crashing partway through; MatchSession is what turns an empty deck
    /// into the match actually ending.</summary>
    public ECardName? Draw()
    {
        if (Deck.Count == 0)
        {
            return null;
        }

        ECardName card = Deck.TakeFromTop();
        Hand.Add(card);
        return card;
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
