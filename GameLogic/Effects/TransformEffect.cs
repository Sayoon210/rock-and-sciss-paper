namespace RockAndScissPaper.GameLogic;

/// <summary>변화: turns one normal or dummy card in the caster's hand into any other
/// normal or dummy card. Both the card being changed and what it becomes are chosen after
/// both players' cards are revealed.</summary>
public sealed class TransformEffect : ICardEffect
{
    public bool RequiresChoice
    {
        get { return true; }
    }

    public void Validate(CardChoice? choice, DeckAndHand self)
    {
        if (choice == null || choice.CardToTransform == null || choice.TransformInto == null)
        {
            throw new ArgumentException("Transform needs both a card to change and what it becomes.", nameof(choice));
        }

        CardName cardToTransform = choice.CardToTransform.Value;
        CardName transformInto = choice.TransformInto.Value;

        if (!IsTransformable(cardToTransform))
        {
            throw new ArgumentException($"{cardToTransform} is not a normal or dummy card.", nameof(choice));
        }

        if (!IsTransformable(transformInto))
        {
            throw new ArgumentException($"{transformInto} is not a normal or dummy card.", nameof(choice));
        }

        if (!self.Hand.Contains(cardToTransform))
        {
            throw new ArgumentException($"{cardToTransform} is not in hand.", nameof(choice));
        }
    }

    public void Apply(CardChoice? choice, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        self.Hand.Remove(choice!.CardToTransform!.Value);
        self.Hand.Add(choice.TransformInto!.Value);
    }

    private static bool IsTransformable(CardName card)
    {
        CardType type = card.GetCardType();
        return type == CardType.Normal || type == CardType.Dummy;
    }
}
