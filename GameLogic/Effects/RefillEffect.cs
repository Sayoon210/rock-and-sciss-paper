namespace RockAndScissPaper.GameLogic;

/// <summary>보충: adds two Dummy cards to the caster's own deck and shuffles.</summary>
public sealed class RefillEffect : ICardEffect
{
    public bool RequiresChoice
    {
        get { return false; }
    }

    public void Validate(CardChoice? choice, DeckAndHand self)
    {
    }

    public void Apply(CardChoice? choice, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        self.Deck.AddToBottom(CardName.Dummy);
        self.Deck.AddToBottom(CardName.Dummy);
        self.Deck.Shuffle(rng);
    }
}
