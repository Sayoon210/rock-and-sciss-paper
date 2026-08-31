using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DeckAndHandTests
{
    private static readonly Random UnusedRng = new Random(0);

    [Fact]
    public void Draw_moves_the_deck_top_into_the_hand()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper }),
            new Hand(Array.Empty<ECardName>()));

        ECardName drawn = deckAndHand.Draw(UnusedRng);

        Assert.Equal(ECardName.Rock, drawn);
        Assert.Equal(new[] { ECardName.Rock }, deckAndHand.Hand.Cards);
        Assert.Equal(1, deckAndHand.Deck.Count);
    }

    /// <summary>Where an empty deck used to hand back null for MatchSession to turn into a 덱
    /// 고갈 loss. The deck restocks itself now, so there is no empty to draw from.</summary>
    [Fact]
    public void Draw_past_the_end_of_the_deck_restocks_rather_than_failing()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock }),
            new Hand(Array.Empty<ECardName>()));

        deckAndHand.Draw(UnusedRng);
        deckAndHand.Draw(new Random(1));

        Assert.Equal(new[] { ECardName.Rock, ECardName.Rock }, deckAndHand.Hand.Cards);
    }

    [Fact]
    public void RefillHandIfSpent_deals_a_whole_hand_when_the_last_card_is_gone()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors }),
            new Hand(Array.Empty<ECardName>()));

        deckAndHand.RefillHandIfSpent(new Random(3), MatchSession.HAND_SIZE);

        Assert.Equal(MatchSession.HAND_SIZE, deckAndHand.Hand.Cards.Count);
    }

    /// <summary>The rule the round is built on: five cards, one a round, and nothing arrives
    /// until all five are gone. A hand with anything left in it is not topped up.</summary>
    [Fact]
    public void RefillHandIfSpent_leaves_a_hand_that_still_has_cards_alone()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper }),
            new Hand(new[] { ECardName.Scissors }));

        deckAndHand.RefillHandIfSpent(new Random(3), MatchSession.HAND_SIZE);

        Assert.Equal(new[] { ECardName.Scissors }, deckAndHand.Hand.Cards);
    }

    [Fact]
    public void ReturnToDeckBottom_moves_a_hand_card_to_the_deck_bottom()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock }),
            new Hand(new[] { ECardName.Paper }));

        deckAndHand.ReturnToDeckBottom(ECardName.Paper);

        Assert.Empty(deckAndHand.Hand.Cards);
        Assert.Equal(2, deckAndHand.Deck.Count);
        Assert.Equal(ECardName.Rock, deckAndHand.Deck.TakeFromTop(UnusedRng));
        Assert.Equal(ECardName.Paper, deckAndHand.Deck.TakeFromTop(UnusedRng));
    }

    [Fact]
    public void Vanish_removes_a_hand_card_without_returning_it_to_the_deck()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock }),
            new Hand(new[] { ECardName.Blank }));

        deckAndHand.Vanish(ECardName.Blank);

        Assert.Empty(deckAndHand.Hand.Cards);
        Assert.Equal(1, deckAndHand.Deck.Count);
    }
}
