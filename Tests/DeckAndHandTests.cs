using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DeckAndHandTests
{
    [Fact]
    public void Draw_moves_the_deck_top_into_the_hand()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { CardName.Rock, CardName.Paper }),
            new Hand(Array.Empty<CardName>()));

        CardName drawn = deckAndHand.Draw();

        Assert.Equal(CardName.Rock, drawn);
        Assert.Equal(new[] { CardName.Rock }, deckAndHand.Hand.Cards);
        Assert.Equal(1, deckAndHand.Deck.Count);
    }

    [Fact]
    public void ReturnToDeckBottom_moves_a_hand_card_to_the_deck_bottom()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { CardName.Rock }),
            new Hand(new[] { CardName.Paper }));

        deckAndHand.ReturnToDeckBottom(CardName.Paper);

        Assert.Empty(deckAndHand.Hand.Cards);
        Assert.Equal(2, deckAndHand.Deck.Count);
        Assert.Equal(CardName.Rock, deckAndHand.Deck.TakeFromTop());
        Assert.Equal(CardName.Paper, deckAndHand.Deck.TakeFromTop());
    }

    [Fact]
    public void Vanish_removes_a_hand_card_without_returning_it_to_the_deck()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(Array.Empty<CardName>()),
            new Hand(new[] { CardName.Dummy }));

        deckAndHand.Vanish(CardName.Dummy);

        Assert.Empty(deckAndHand.Hand.Cards);
        Assert.Equal(0, deckAndHand.Deck.Count);
    }
}
