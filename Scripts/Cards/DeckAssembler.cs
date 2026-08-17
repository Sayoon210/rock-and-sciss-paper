using System;
using System.Collections.Generic;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Cards;

/// <summary>Builds one player's deck per DESIGN.md's fixed composition: 3 each of
/// Rock/Paper/Scissors, 4 Dummy, 2 Joker, and every currently-loaded special card. The
/// special-card portion reads CardDatabase.LoadedCardNames rather than listing CardNames
/// itself — DESIGN.md's "확장성" section calls out a future special-card pool expansion, and
/// listing names here would need editing every time that pool grows (see root CLAUDE.md).</summary>
public static class DeckAssembler
{
    private const int NORMAL_CARD_COPIES = 3;
    private const int DUMMY_CARD_COPIES = 4;
    private const int JOKER_CARD_COPIES = 2;

    public static List<CardName> BuildDeck()
    {
        CardDatabase? cardDatabase = CardDatabase.Instance;
        if (cardDatabase == null)
        {
            throw new InvalidOperationException("DeckAssembler: CardDatabase autoload is not available yet.");
        }

        List<CardName> deck = new List<CardName>();

        AddCopies(deck, CardName.Rock, NORMAL_CARD_COPIES);
        AddCopies(deck, CardName.Paper, NORMAL_CARD_COPIES);
        AddCopies(deck, CardName.Scissors, NORMAL_CARD_COPIES);
        AddCopies(deck, CardName.Dummy, DUMMY_CARD_COPIES);
        AddCopies(deck, CardName.Joker, JOKER_CARD_COPIES);

        foreach (CardName cardName in cardDatabase.LoadedCardNames)
        {
            if (cardName.GetCardType() == CardType.Special)
            {
                deck.Add(cardName);
            }
        }

        return deck;
    }

    private static void AddCopies(List<CardName> deck, CardName card, int count)
    {
        for (int i = 0; i < count; i++)
        {
            deck.Add(card);
        }
    }
}
