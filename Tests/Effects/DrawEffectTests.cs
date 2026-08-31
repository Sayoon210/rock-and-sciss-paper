using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DrawEffectTests
{
    private static DeckAndHand Untouched()
    {
        return new DeckAndHand(new Deck(new[] { ECardName.Scissors }), new Hand(Array.Empty<ECardName>()));
    }

    [Fact]
    public void Apply_draws_two_cards_into_the_hand()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors }),
            new Hand(Array.Empty<ECardName>()));

        new DrawEffect().Apply(null, self, Untouched(), new Random(1));

        Assert.Equal(new[] { ECardName.Rock, ECardName.Paper }, self.Hand.Cards);
        Assert.Equal(1, self.Deck.Count);
    }

    [Fact]
    public void Apply_does_not_touch_the_opponent()
    {
        var self = new DeckAndHand(new Deck(new[] { ECardName.Rock, ECardName.Paper }), new Hand(Array.Empty<ECardName>()));
        var opponent = new DeckAndHand(new Deck(new[] { ECardName.Scissors }), new Hand(Array.Empty<ECardName>()));

        new DrawEffect().Apply(null, self, opponent, new Random(1));

        Assert.Equal(1, opponent.Deck.Count);
        Assert.Empty(opponent.Hand.Cards);
    }

    /// <summary>드로우 always takes two, unconditionally — the one effect that can ask for more
    /// than the deck currently holds (교체/리셋 only draw back what they just returned). It used
    /// to stop short at whatever was left, because an empty deck was a real state that ended the
    /// match. The deck restocks itself now, so two means two.</summary>
    [Fact]
    public void Apply_draws_both_cards_even_when_that_means_restocking()
    {
        var self = new DeckAndHand(new Deck(new[] { ECardName.Rock }), new Hand(Array.Empty<ECardName>()));

        new DrawEffect().Apply(null, self, Untouched(), new Random(1));

        Assert.Equal(new[] { ECardName.Rock, ECardName.Rock }, self.Hand.Cards);
    }
}
