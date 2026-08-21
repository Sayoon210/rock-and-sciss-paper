using System;
using System.Collections.Generic;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Cards;

/// <summary>Builds one player's deck per DESIGN.md's fixed composition: 3 each of
/// Rock/Paper/Scissors, 2 Blank, 2 Joker, and every currently-loaded ability card. The
/// ability-card portion reads CardDatabase.LoadedCardNames rather than listing CardNames
/// itself — DESIGN.md's "확장성" section calls out a future ability-card pool expansion, and
/// listing names here would need editing every time that pool grows (see root CLAUDE.md).</summary>
public static class DeckAssembler
{
    private const int NORMAL_CARD_COPIES = 3;
    private const int DUMMY_CARD_COPIES = 2;
    private const int JOKER_CARD_COPIES = 2;

    public static List<ECardName> BuildDeck()
    {
        CardDatabase? cardDatabase = CardDatabase.Instance;
        if (cardDatabase == null)
        {
            throw new InvalidOperationException("DeckAssembler: CardDatabase autoload is not available yet.");
        }

        List<ECardName> deck = new List<ECardName>();

        AddCopies(deck, ECardName.Rock, NORMAL_CARD_COPIES);
        AddCopies(deck, ECardName.Paper, NORMAL_CARD_COPIES);
        AddCopies(deck, ECardName.Scissors, NORMAL_CARD_COPIES);
        AddCopies(deck, ECardName.Blank, DUMMY_CARD_COPIES);
        AddCopies(deck, ECardName.Joker, JOKER_CARD_COPIES);

        foreach (ECardName cardName in cardDatabase.LoadedCardNames)
        {
            if (cardName.GetCardType() == ECardType.Ability)
            {
                deck.Add(cardName);
            }
        }

        return deck;
    }

    private static void AddCopies(List<ECardName> deck, ECardName card, int count)
    {
        for (int i = 0; i < count; i++)
        {
            deck.Add(card);
        }
    }
}
