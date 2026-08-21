using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DeckAndHandTests
{
    [Fact]
    public void Draw_moves_the_deck_top_into_the_hand()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper }),
            new Hand(Array.Empty<ECardName>()));

        ECardName? drawn = deckAndHand.Draw();

        Assert.Equal(ECardName.Rock, drawn);
        Assert.Equal(new[] { ECardName.Rock }, deckAndHand.Hand.Cards);
        Assert.Equal(1, deckAndHand.Deck.Count);
    }

    [Fact]
    public void Draw_from_an_empty_deck_returns_null_instead_of_throwing()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(Array.Empty<ECardName>()),
            new Hand(Array.Empty<ECardName>()));

        ECardName? drawn = deckAndHand.Draw();

        Assert.Null(drawn);
        Assert.Empty(deckAndHand.Hand.Cards);
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
        Assert.Equal(ECardName.Rock, deckAndHand.Deck.TakeFromTop());
        Assert.Equal(ECardName.Paper, deckAndHand.Deck.TakeFromTop());
    }

    [Fact]
    public void Vanish_removes_a_hand_card_without_returning_it_to_the_deck()
    {
        var deckAndHand = new DeckAndHand(
            new Deck(Array.Empty<ECardName>()),
            new Hand(new[] { ECardName.Blank }));

        deckAndHand.Vanish(ECardName.Blank);

        Assert.Empty(deckAndHand.Hand.Cards);
        Assert.Equal(0, deckAndHand.Deck.Count);
    }
}
