using System.Collections.Generic;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Network;

/// <summary>Converts a CardChoice to and from the primitive values Godot's RPC
/// serialization understands. CardChoice carries ECardName enums and nullable CardNames, and
/// Godot's Variant marshalling has no built-in notion of either — everything here becomes
/// int, with NO_CARD standing in for "not part of this choice".</summary>
public static class CardChoiceCodec
{
    public const int NO_CARD = -1;

    public readonly struct EncodedCardChoice
    {
        public int CardToTransform { get; }
        public int TransformInto { get; }
        public int[] CardsToReturn { get; }

        public EncodedCardChoice(int cardToTransform, int transformInto, int[] cardsToReturn)
        {
            CardToTransform = cardToTransform;
            TransformInto = transformInto;
            CardsToReturn = cardsToReturn;
        }
    }

    public static EncodedCardChoice Encode(CardChoice choice)
    {
        int cardToTransform = NO_CARD;
        if (choice.CardToTransform.HasValue)
        {
            cardToTransform = (int)choice.CardToTransform.Value;
        }

        int transformInto = NO_CARD;
        if (choice.TransformInto.HasValue)
        {
            transformInto = (int)choice.TransformInto.Value;
        }

        int[] cardsToReturn = new int[choice.CardsToReturn.Count];
        for (int i = 0; i < choice.CardsToReturn.Count; i++)
        {
            cardsToReturn[i] = (int)choice.CardsToReturn[i];
        }

        return new EncodedCardChoice(cardToTransform, transformInto, cardsToReturn);
    }

    /// <summary>Reconstructs a choice for the card the host asked this player about, or
    /// null if the payload cannot form one.
    ///
    /// The card is a parameter rather than something read out of the payload on purpose:
    /// the host already knows what it prompted for, and inferring the shape from the
    /// payload would let a client pick which effect runs by choosing which fields to fill
    /// in. A client prompted for 교체 that answers with a 변화-shaped payload simply has
    /// those fields ignored.</summary>
    public static CardChoice? Decode(
        ECardName promptedCard,
        int cardToTransform,
        int transformInto,
        int[] cardsToReturn)
    {
        if (promptedCard == ECardName.Transform)
        {
            if (cardToTransform == NO_CARD || transformInto == NO_CARD)
            {
                return null;
            }

            return CardChoice.Transforming((ECardName)cardToTransform, (ECardName)transformInto);
        }

        List<ECardName> returned = new List<ECardName>();
        foreach (int value in cardsToReturn)
        {
            returned.Add((ECardName)value);
        }

        return CardChoice.Swapping(returned);
    }
}
