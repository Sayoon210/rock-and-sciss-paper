using RockAndScissPaper.Core;

namespace RockAndScissPaper.Tests;

public class RpsRulesTests
{
    [Theory]
    // Player 1 wins
    [InlineData(NormalCard.Scissors, NormalCard.Paper, RoundOutcome.Player1Win)]
    [InlineData(NormalCard.Rock, NormalCard.Scissors, RoundOutcome.Player1Win)]
    [InlineData(NormalCard.Paper, NormalCard.Rock, RoundOutcome.Player1Win)]
    // Player 2 wins
    [InlineData(NormalCard.Paper, NormalCard.Scissors, RoundOutcome.Player2Win)]
    [InlineData(NormalCard.Scissors, NormalCard.Rock, RoundOutcome.Player2Win)]
    [InlineData(NormalCard.Rock, NormalCard.Paper, RoundOutcome.Player2Win)]
    // Same card draws
    [InlineData(NormalCard.Rock, NormalCard.Rock, RoundOutcome.Draw)]
    [InlineData(NormalCard.Paper, NormalCard.Paper, RoundOutcome.Draw)]
    [InlineData(NormalCard.Scissors, NormalCard.Scissors, RoundOutcome.Draw)]
    public void Compare_resolves_every_matchup(NormalCard p1, NormalCard p2, RoundOutcome expected)
    {
        Assert.Equal(expected, RpsRules.Compare(p1, p2));
    }
}
