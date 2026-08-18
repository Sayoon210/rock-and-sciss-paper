namespace RockAndScissPaper.GameLogic;

/// <summary>One special card's effect. opponent is unused by every effect except Reset —
/// DESIGN.md notes Reset is the only special card that affects both players.
///
/// Note what is *not* here: the card that was played. An effect is looked up by card, so
/// it already knows which one it is, and not passing it means an effect cannot ask whether
/// its own card is still in hand. It never is — RoundResolver removes both played cards
/// before any effect runs — and a previous bug came from Validate assuming otherwise.
/// Taking the card away makes that mistake unrepresentable rather than merely guarded.</summary>
public interface ICardEffect
{
    /// <summary>Whether the player must choose something before this effect can run. True
    /// for 교체 and 변화 only. RoundResolver asks the effect rather than testing card names,
    /// so a sixth special card slots in without editing the resolver.</summary>
    bool RequiresChoice { get; }

    /// <summary>Rejects a choice this effect cannot carry out, before anything is mutated.
    /// choice is null exactly when RequiresChoice is false.</summary>
    void Validate(CardChoice? choice, DeckAndHand self);

    void Apply(CardChoice? choice, DeckAndHand self, DeckAndHand opponent, Random rng);
}
