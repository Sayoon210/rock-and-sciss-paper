using System.Collections.Generic;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Network;

/// <summary>Converts CardPlay to and from the primitive values Godot's RPC serialization
/// understands. CardPlay carries CardName enums and nullable CardNames, and Godot's Variant
/// marshalling has no built-in notion of either — everything here becomes int, with NoCard
/// as the sentinel for "this choice wasn't made."</summary>
public static class CardPlayCodec
{
    public const int NoCard = -1;

    public readonly struct EncodedCardPlay
    {
        public int Card { get; }
        public int CardToTransform { get; }
        public int TransformInto { get; }
        public int[] CardsToReturn { get; }

        public EncodedCardPlay(int card, int cardToTransform, int transformInto, int[] cardsToReturn)
        {
            Card = card;
            CardToTransform = cardToTransform;
            TransformInto = transformInto;
            CardsToReturn = cardsToReturn;
        }
    }

    public static EncodedCardPlay Encode(CardPlay play)
    {
        int cardToTransform = NoCard;
        if (play.CardToTransform.HasValue)
        {
            cardToTransform = (int)play.CardToTransform.Value;
        }

        int transformInto = NoCard;
        if (play.TransformInto.HasValue)
        {
            transformInto = (int)play.TransformInto.Value;
        }

        int[] cardsToReturn = new int[play.CardsToReturn.Count];
        for (int i = 0; i < play.CardsToReturn.Count; i++)
        {
            cardsToReturn[i] = (int)play.CardsToReturn[i];
        }

        return new EncodedCardPlay((int)play.Card, cardToTransform, transformInto, cardsToReturn);
    }

    /// <summary>Reconstructs a CardPlay from RPC primitives. Which factory method applies
    /// is inferred from which fields carry real values rather than the NoCard sentinel —
    /// Transform and Swap are the only cards with a choice attached, and their sentinels
    /// can't overlap (a Transform play never populates cardsToReturn).</summary>
    public static CardPlay Decode(int card, int cardToTransform, int transformInto, int[] cardsToReturn)
    {
        if (cardToTransform != NoCard && transformInto != NoCard)
        {
            return CardPlay.Transforming((CardName)cardToTransform, (CardName)transformInto);
        }

        if (cardsToReturn.Length > 0)
        {
            List<CardName> returned = new List<CardName>();
            foreach (int value in cardsToReturn)
            {
                returned.Add((CardName)value);
            }

            return CardPlay.Swapping(returned);
        }

        return CardPlay.WithoutChoice((CardName)card);
    }
}
