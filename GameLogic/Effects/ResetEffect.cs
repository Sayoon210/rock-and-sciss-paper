namespace RockAndScissPaper.GameLogic;

/// <summary>리셋: both players put their whole hand back into their own deck, shuffle,
/// then draw back up to whatever hand size they had before. The only special card that
/// touches the opponent.</summary>
public sealed class ResetEffect : ICardEffect
{
    public bool RequiresChoice
    {
        get { return false; }
    }

    public bool HasAnyLegalChoice(DeckAndHand self)
    {
        return false;
    }

    public void Validate(CardChoice? choice, DeckAndHand self)
    {
    }

    public void Apply(CardChoice? choice, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        ResetOne(self, rng);
        ResetOne(opponent, rng);
    }

    private static void ResetOne(DeckAndHand player, Random rng)
    {
        int handSize = player.Hand.Cards.Count;
        var handCards = new List<CardName>(player.Hand.Cards);

        foreach (CardName card in handCards)
        {
            player.ReturnToDeckBottom(card);
        }

        player.Deck.Shuffle(rng);

        for (int i = 0; i < handSize; i++)
        {
            player.Draw();
        }
    }
}
