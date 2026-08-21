namespace RockAndScissPaper.GameLogic;

/// <summary>교체: puts up to MAX_SWAPPED_CARDS of the caster's hand cards back into their
/// deck, shuffles, then draws that many again. Which cards go back is chosen after both
/// players' cards are revealed.</summary>
public sealed class SwapEffect : ICardEffect
{
    /// <summary>The most cards one 교체 may put back. Public because the picker has to stop
    /// the player at the same number this rejects them at — one constant rather than a limit
    /// written down twice and free to drift apart.</summary>
    public const int MAX_SWAPPED_CARDS = 2;

    public bool RequiresChoice
    {
        get { return true; }
    }

    /// <summary>Any small enough subset of the hand is a legal answer, the empty one
    /// included, so the only hand with nothing to offer is an empty one.</summary>
    public bool HasAnyLegalChoice(DeckAndHand self)
    {
        return self.Hand.Cards.Count > 0;
    }

    public void Validate(CardChoice? choice, DeckAndHand self)
    {
        if (choice == null)
        {
            throw new ArgumentException("Swap needs a choice of which cards go back.", nameof(choice));
        }

        if (choice.CardsToReturn.Count > MAX_SWAPPED_CARDS)
        {
            throw new ArgumentException(
                $"Swap puts back at most {MAX_SWAPPED_CARDS} cards, not {choice.CardsToReturn.Count}.",
                nameof(choice));
        }

        // Checked against a copy so a rejected choice leaves the real hand untouched. The
        // hand is already exactly what Apply will see — the played Swap card is long gone,
        // and 리셋 has already run — so there is nothing to compensate for here.
        var remaining = new Hand(self.Hand.Cards);

        // The same card can legitimately appear twice in the list when the hand holds two
        // of it, but not three times when it holds two.
        foreach (ECardName card in choice.CardsToReturn)
        {
            if (!remaining.Contains(card))
            {
                throw new ArgumentException($"Hand does not hold enough copies of {card} to swap.", nameof(choice));
            }

            remaining.Remove(card);
        }
    }

    public void Apply(CardChoice? choice, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        foreach (ECardName card in choice!.CardsToReturn)
        {
            self.ReturnToDeckBottom(card);
        }

        self.Deck.Shuffle(rng);

        for (int i = 0; i < choice.CardsToReturn.Count; i++)
        {
            self.Draw();
        }
    }
}
