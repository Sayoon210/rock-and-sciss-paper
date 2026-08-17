namespace RockAndScissPaper.GameLogic;

/// <summary>교체: puts any number of the caster's hand cards back into their deck,
/// shuffles, then draws that many again. Which cards go back is chosen when the card is
/// played.</summary>
public sealed class SwapEffect : ICardEffect
{
    public void Validate(CardPlay play, DeckAndHand self)
    {
        // Checked against the hand as Apply will actually see it, not as it is now:
        // RoundResolver removes the played card before any effect runs, so the Swap card
        // itself is already gone and can never be one of the cards going back. Validating
        // against the pre-removal hand let that through, and Apply then threw partway
        // through resolution — after both players' cards had left their hands but before
        // the round was recorded, wedging the match with both sides stuck as "submitted".
        var remaining = new Hand(self.Hand.Cards);
        remaining.Remove(play.Card);

        // The same card can legitimately appear twice in the list when the hand holds two
        // of it, but not three times when it holds two.
        foreach (CardName card in play.CardsToReturn)
        {
            if (!remaining.Contains(card))
            {
                throw new ArgumentException($"Hand does not hold enough copies of {card} to swap.", nameof(play));
            }

            remaining.Remove(card);
        }
    }

    public void Apply(CardPlay play, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        foreach (CardName card in play.CardsToReturn)
        {
            self.ReturnToDeckBottom(card);
        }

        self.Deck.Shuffle(rng);

        for (int i = 0; i < play.CardsToReturn.Count; i++)
        {
            self.Draw();
        }
    }
}
