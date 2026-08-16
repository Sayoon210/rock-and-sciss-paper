using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class CardTypeTests
{
    [Theory]
    [InlineData(CardName.Rock, CardType.Normal)]
    [InlineData(CardName.Paper, CardType.Normal)]
    [InlineData(CardName.Scissors, CardType.Normal)]
    [InlineData(CardName.Dummy, CardType.Dummy)]
    [InlineData(CardName.Joker, CardType.Joker)]
    [InlineData(CardName.Reset, CardType.Special)]
    [InlineData(CardName.Swap, CardType.Special)]
    [InlineData(CardName.Transform, CardType.Special)]
    [InlineData(CardName.Refill, CardType.Special)]
    [InlineData(CardName.Foresight, CardType.Special)]
    [InlineData(CardName.Draw, CardType.Special)]
    public void Every_card_maps_to_the_right_type(CardName name, CardType expected)
    {
        Assert.Equal(expected, name.GetCardType());
    }

    [Fact]
    public void Every_CardName_value_has_a_type()
    {
        // Guards against a new CardName being added without updating GetCardType's switch —
        // that throws ArgumentOutOfRangeException instead of silently miscategorizing.
        foreach (CardName name in Enum.GetValues<CardName>())
        {
            var type = name.GetCardType();
            Assert.True(Enum.IsDefined(type));
        }
    }

    [Theory]
    [InlineData(CardName.Rock, NormalCard.Rock)]
    [InlineData(CardName.Paper, NormalCard.Paper)]
    [InlineData(CardName.Scissors, NormalCard.Scissors)]
    public void ToNormalCard_converts_normal_cards(CardName name, NormalCard expected)
    {
        Assert.Equal(expected, name.ToNormalCard());
    }

    [Fact]
    public void ToNormalCard_throws_for_non_normal_cards()
    {
        Assert.Throws<ArgumentException>(() => CardName.Joker.ToNormalCard());
    }
}
