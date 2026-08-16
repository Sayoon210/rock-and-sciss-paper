using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class RefillEffectTests
{
    [Fact]
    public void Apply_adds_two_dummy_cards_to_the_casters_deck()
    {
        var self = new DeckAndHand(new Deck(new[] { CardName.Rock }), new Hand(Array.Empty<CardName>()));
        var opponent = new DeckAndHand(new Deck(Array.Empty<CardName>()), new Hand(Array.Empty<CardName>()));

        new RefillEffect().Apply(self, opponent, new Random(1));

        Assert.Equal(3, self.Deck.Count);
    }

    [Fact]
    public void Apply_does_not_touch_the_opponent()
    {
        var self = new DeckAndHand(new Deck(new[] { CardName.Rock }), new Hand(Array.Empty<CardName>()));
        var opponent = new DeckAndHand(new Deck(new[] { CardName.Paper }), new Hand(Array.Empty<CardName>()));

        new RefillEffect().Apply(self, opponent, new Random(1));

        Assert.Equal(1, opponent.Deck.Count);
        Assert.Equal(CardName.Paper, opponent.Deck.TakeFromTop());
    }
}
