using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class ResetEffectTests
{
    [Fact]
    public void Apply_redraws_the_same_hand_size_for_both_players()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors, CardName.Dummy, CardName.Joker }),
            new Hand(new[] { CardName.Reset, CardName.Refill }));
        var opponent = new DeckAndHand(
            new Deck(new[] { CardName.Rock }),
            new Hand(Array.Empty<CardName>()));

        new ResetEffect().Apply(self, opponent, new Random(1));

        Assert.Equal(2, self.Hand.Cards.Count);
        Assert.Empty(opponent.Hand.Cards);
    }

    [Fact]
    public void Apply_preserves_each_players_total_card_count()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Rock, CardName.Paper }),
            new Hand(new[] { CardName.Reset }));
        var opponent = new DeckAndHand(
            new Deck(new[] { CardName.Scissors, CardName.Dummy }),
            new Hand(new[] { CardName.Joker, CardName.Draw }));

        new ResetEffect().Apply(self, opponent, new Random(1));

        Assert.Equal(3, self.Deck.Count + self.Hand.Cards.Count);
        Assert.Equal(4, opponent.Deck.Count + opponent.Hand.Cards.Count);
    }
}
