namespace RockAndScissPaper.GameLogic;

/// <summary>One special card's effect. opponent is unused by every effect except Reset —
/// DESIGN.md notes Reset is the only special card that affects both players.</summary>
public interface ICardEffect
{
    /// <summary>Rejects a play this effect cannot carry out, before anything is mutated.
    /// Callers run this on every play up front, so a bad choice from a client can never
    /// leave a round half-applied.</summary>
    void Validate(CardPlay play, DeckAndHand self);

    void Apply(CardPlay play, DeckAndHand self, DeckAndHand opponent, Random rng);
}
