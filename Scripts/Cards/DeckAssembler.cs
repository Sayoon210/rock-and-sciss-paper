using System.Collections.Generic;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Cards;

/// <summary>Builds one player's deck: 3 each of Rock/Paper/Scissors and nothing else.
///
/// 공백, 조커 and the four ability cards were all taken out of the deck. None of them are
/// deleted — ECardName still names them, RoundResolver still resolves them, and their tests
/// still pass — so putting any of them back is an AddCopies line here rather than a
/// re-implementation. What they had in common is that a round containing one has no win and
/// no loss (RoundResult.WinLoss is null for blank, Joker and ability rounds alike), which
/// made a plain rock-paper-scissors outcome the exception rather than the rule. DESIGN.md
/// already had the ability cards leaving for the item system; blank and 조커 followed so that
/// every round now produces a real verdict, damage, and the animation that shows it.
///
/// That also means this no longer reads CardDatabase.LoadedCardNames. The "don't hardcode the
/// ability roster" rule in the root CLAUDE.md exists so a growing ability pool needs no edit
/// here — with no ability cards in the deck at all, there is no roster left to read, and the
/// lookup would just be a Godot dependency this no longer has any use for.</summary>
public static class DeckAssembler
{
    private const int NORMAL_CARD_COPIES = 3;

    public static List<ECardName> BuildDeck()
    {
        List<ECardName> deck = new List<ECardName>();

        AddCopies(deck, ECardName.Rock, NORMAL_CARD_COPIES);
        AddCopies(deck, ECardName.Paper, NORMAL_CARD_COPIES);
        AddCopies(deck, ECardName.Scissors, NORMAL_CARD_COPIES);

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
