using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class HandTests
{
    [Fact]
    public void Cards_reflects_the_initial_hand()
    {
        var hand = new Hand(new[] { CardName.Rock, CardName.Dummy });

        Assert.Equal(new[] { CardName.Rock, CardName.Dummy }, hand.Cards);
    }

    [Fact]
    public void Add_appends_a_card()
    {
        var hand = new Hand(new[] { CardName.Rock });

        hand.Add(CardName.Joker);

        Assert.Equal(new[] { CardName.Rock, CardName.Joker }, hand.Cards);
    }

    [Fact]
    public void Remove_takes_out_an_existing_card()
    {
        var hand = new Hand(new[] { CardName.Rock, CardName.Paper });

        hand.Remove(CardName.Rock);

        Assert.Equal(new[] { CardName.Paper }, hand.Cards);
    }

    [Fact]
    public void Remove_throws_when_the_card_is_not_in_hand()
    {
        var hand = new Hand(new[] { CardName.Rock });

        Assert.Throws<ArgumentException>(() => hand.Remove(CardName.Joker));
    }

    [Theory]
    [InlineData(CardName.Rock, true)]
    [InlineData(CardName.Joker, false)]
    public void Contains_reports_whether_the_card_is_in_hand(CardName card, bool expected)
    {
        var hand = new Hand(new[] { CardName.Rock });

        Assert.Equal(expected, hand.Contains(card));
    }
}
