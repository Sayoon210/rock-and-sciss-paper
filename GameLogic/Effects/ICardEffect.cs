namespace RockAndScissPaper.GameLogic;

/// <summary>One special card's effect. opponent is unused by every effect except Reset —
/// DESIGN.md notes Reset is the only special card that affects both players.</summary>
public interface ICardEffect
{
    void Apply(DeckAndHand self, DeckAndHand opponent, Random rng);
}
