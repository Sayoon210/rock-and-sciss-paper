namespace RockAndScissPaper.GameLogic;

/// <summary>드로우: draws two cards for the caster.</summary>
public sealed class DrawEffect : ICardEffect
{
    public void Validate(CardPlay play, DeckAndHand self)
    {
    }

    public void Apply(CardPlay play, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        self.Draw();
        self.Draw();
    }
}
