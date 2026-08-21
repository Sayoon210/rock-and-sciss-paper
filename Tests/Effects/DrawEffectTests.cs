using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DrawEffectTests
{
    [Fact]
    public void Apply_draws_two_cards_into_the_hand()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors }),
            new Hand(Array.Empty<ECardName>()));
        var opponent = new DeckAndHand(new Deck(Array.Empty<ECardName>()), new Hand(Array.Empty<ECardName>()));

        new DrawEffect().Apply(null, self, opponent, new Random(1));

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

    [Fact]
    public void Apply_stops_at_whatever_is_left_instead_of_throwing_when_the_deck_runs_out()
    {
        // 드로우 always tries to draw two, unconditionally — the one effect that can run
        // out of deck mid-effect (교체/리셋 only ever draw back what they just returned, so
        // they can never ask for more than the deck already holds). DeckAndHand.Draw()
        // returning null instead of throwing on an empty deck (DESIGN.md, "덱 고갈") is what
        // keeps this from crashing a round in progress.
        var self = new DeckAndHand(new Deck(new[] { ECardName.Rock }), new Hand(Array.Empty<ECardName>()));
        var opponent = new DeckAndHand(new Deck(Array.Empty<ECardName>()), new Hand(Array.Empty<ECardName>()));

        new DrawEffect().Apply(null, self, opponent, new Random(1));

        Assert.Equal(new[] { ECardName.Rock }, self.Hand.Cards);
        Assert.Equal(0, self.Deck.Count);
    }
}
