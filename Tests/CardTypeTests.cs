using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class CardTypeTests
{
    [Theory]
    [InlineData(ECardName.Rock, ECardType.Normal)]
    [InlineData(ECardName.Paper, ECardType.Normal)]
    [InlineData(ECardName.Scissors, ECardType.Normal)]
    [InlineData(ECardName.Blank, ECardType.Blank)]
    [InlineData(ECardName.Joker, ECardType.Joker)]
    [InlineData(ECardName.Reset, ECardType.Ability)]
    [InlineData(ECardName.Swap, ECardType.Ability)]
    [InlineData(ECardName.Transform, ECardType.Ability)]
    [InlineData(ECardName.Draw, ECardType.Ability)]
    public void Every_card_maps_to_the_right_type(ECardName name, ECardType expected)
    {
        Assert.Equal(expected, name.GetCardType());
    }

    [Fact]
    public void Every_CardName_value_has_a_type()
    {
        // Guards against a new ECardName being added without updating GetCardType's switch —
        // that throws ArgumentOutOfRangeException instead of silently miscategorizing.
        foreach (ECardName name in Enum.GetValues<ECardName>())
        {
            var type = name.GetCardType();
            Assert.True(Enum.IsDefined(type));
        }
    }

    [Theory]
    [InlineData(ECardName.Rock, ENormalCard.Rock)]
    [InlineData(ECardName.Paper, ENormalCard.Paper)]
    [InlineData(ECardName.Scissors, ENormalCard.Scissors)]
    public void ToNormalCard_converts_normal_cards(ECardName name, ENormalCard expected)
    {
        Assert.Equal(expected, name.ToNormalCard());
    }

    [Fact]
    public void ToNormalCard_throws_for_non_normal_cards()
    {
        Assert.Throws<ArgumentException>(() => ECardName.Joker.ToNormalCard());
    }
}
