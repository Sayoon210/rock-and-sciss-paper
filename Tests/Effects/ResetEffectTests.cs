using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class ResetEffectTests
{
    [Fact]
    public void Apply_redraws_the_same_hand_size_for_both_players()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors, ECardName.Blank, ECardName.Joker }),
            new Hand(new[] { ECardName.Reset, ECardName.Draw }));
        var opponent = new DeckAndHand(
            new Deck(new[] { ECardName.Rock }),
            new Hand(Array.Empty<ECardName>()));

        new ResetEffect().Apply(null, self, opponent, new Random(1));

        Assert.Equal(2, self.Hand.Cards.Count);
        Assert.Empty(opponent.Hand.Cards);
    }

    [Fact]
    public void Apply_preserves_each_players_total_card_count()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper }),
            new Hand(new[] { ECardName.Reset }));
        var opponent = new DeckAndHand(
            new Deck(new[] { ECardName.Scissors, ECardName.Blank }),
            new Hand(new[] { ECardName.Joker, ECardName.Draw }));

        new ResetEffect().Apply(null, self, opponent, new Random(1));

        Assert.Equal(3, self.Deck.Count + self.Hand.Cards.Count);
        Assert.Equal(4, opponent.Deck.Count + opponent.Hand.Cards.Count);
    }
}
