namespace RockAndScissPaper.GameLogic;

/// <summary>변화: turns one normal or dummy card in the caster's hand into any other
/// normal or dummy card. Both the card being changed and what it becomes are chosen when
/// the card is played.</summary>
public sealed class TransformEffect : ICardEffect
{
    public void Validate(CardPlay play, DeckAndHand self)
    {
        if (play.CardToTransform == null || play.TransformInto == null)
        {
            throw new ArgumentException("Transform needs both a card to change and what it becomes.", nameof(play));
        }

        CardName cardToTransform = play.CardToTransform.Value;
        CardName transformInto = play.TransformInto.Value;

        if (!IsTransformable(cardToTransform))
        {
            throw new ArgumentException($"{cardToTransform} is not a normal or dummy card.", nameof(play));
        }

        if (!IsTransformable(transformInto))
        {
            throw new ArgumentException($"{transformInto} is not a normal or dummy card.", nameof(play));
        }

        if (!self.Hand.Contains(cardToTransform))
        {
            throw new ArgumentException($"{cardToTransform} is not in hand.", nameof(play));
        }
    }

    public void Apply(CardPlay play, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        self.Hand.Remove(play.CardToTransform!.Value);
        self.Hand.Add(play.TransformInto!.Value);
    }

    private static bool IsTransformable(CardName card)
    {
        CardType type = card.GetCardType();
        return type == CardType.Normal || type == CardType.Dummy;
    }
}
