using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class HandTests
{
    [Fact]
    public void Cards_reflects_the_initial_hand()
    {
        var hand = new Hand(new[] { ECardName.Rock, ECardName.Blank });

        Assert.Equal(new[] { ECardName.Rock, ECardName.Blank }, hand.Cards);
    }

    [Fact]
    public void Add_appends_a_card()
    {
        var hand = new Hand(new[] { ECardName.Rock });

        hand.Add(ECardName.Joker);

        Assert.Equal(new[] { ECardName.Rock, ECardName.Joker }, hand.Cards);
    }

    [Fact]
    public void Remove_takes_out_an_existing_card()
    {
        var hand = new Hand(new[] { ECardName.Rock, ECardName.Paper });

        hand.Remove(ECardName.Rock);

        Assert.Equal(new[] { ECardName.Paper }, hand.Cards);
    }

    [Fact]
    public void Remove_throws_when_the_card_is_not_in_hand()
    {
        var hand = new Hand(new[] { ECardName.Rock });

        Assert.Throws<ArgumentException>(() => hand.Remove(ECardName.Joker));
    }

    [Theory]
    [InlineData(ECardName.Rock, true)]
    [InlineData(ECardName.Joker, false)]
    public void Contains_reports_whether_the_card_is_in_hand(ECardName card, bool expected)
    {
        var hand = new Hand(new[] { ECardName.Rock });

        Assert.Equal(expected, hand.Contains(card));
    }
}
