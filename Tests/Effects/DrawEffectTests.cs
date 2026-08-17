using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DrawEffectTests
{
    [Fact]
    public void Apply_draws_two_cards_into_the_hand()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors }),
            new Hand(Array.Empty<CardName>()));
        var opponent = new DeckAndHand(new Deck(Array.Empty<CardName>()), new Hand(Array.Empty<CardName>()));

        new DrawEffect().Apply(CardPlay.WithoutChoice(CardName.Rock), self, opponent, new Random(1));

        Assert.Equal(new[] { CardName.Rock, CardName.Paper }, self.Hand.Cards);
        Assert.Equal(1, self.Deck.Count);
    }

    [Fact]
    public void Apply_does_not_touch_the_opponent()
    {
        var self = new DeckAndHand(new Deck(new[] { CardName.Rock, CardName.Paper }), new Hand(Array.Empty<CardName>()));
        var opponent = new DeckAndHand(new Deck(new[] { CardName.Scissors }), new Hand(Array.Empty<CardName>()));

        new DrawEffect().Apply(CardPlay.WithoutChoice(CardName.Rock), self, opponent, new Random(1));

        Assert.Equal(1, opponent.Deck.Count);
        Assert.Empty(opponent.Hand.Cards);
    }
}
