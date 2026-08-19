namespace RockAndScissPaper.GameLogic;

/// <summary>드로우: draws two cards for the caster.</summary>
public sealed class DrawEffect : ICardEffect
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
        self.Draw();
        self.Draw();
    }
}
