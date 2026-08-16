using RockAndScissPaper.Core;

namespace RockAndScissPaper.Tests;

public class WinLossRulesTests
{
    [Theory]
    // Player 1 wins
    [InlineData(NormalCard.Scissors, NormalCard.Paper, WinLossResult.Player1Win)]
    [InlineData(NormalCard.Rock, NormalCard.Scissors, WinLossResult.Player1Win)]
    [InlineData(NormalCard.Paper, NormalCard.Rock, WinLossResult.Player1Win)]
    // Player 2 wins
    [InlineData(NormalCard.Paper, NormalCard.Scissors, WinLossResult.Player2Win)]
    [InlineData(NormalCard.Scissors, NormalCard.Rock, WinLossResult.Player2Win)]
    [InlineData(NormalCard.Rock, NormalCard.Paper, WinLossResult.Player2Win)]
    // Same card draws
    [InlineData(NormalCard.Rock, NormalCard.Rock, WinLossResult.Draw)]
    [InlineData(NormalCard.Paper, NormalCard.Paper, WinLossResult.Draw)]
    [InlineData(NormalCard.Scissors, NormalCard.Scissors, WinLossResult.Draw)]
    public void Judge_resolves_every_matchup(NormalCard player1, NormalCard player2, WinLossResult expected)
    {
        Assert.Equal(expected, WinLossRules.Judge(player1, player2));
    }
}
