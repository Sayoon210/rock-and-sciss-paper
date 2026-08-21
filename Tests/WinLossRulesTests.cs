using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class WinLossRulesTests
{
    [Theory]
    // Player 1 wins
    [InlineData(ENormalCard.Scissors, ENormalCard.Paper, EWinLossResult.Player1Win)]
    [InlineData(ENormalCard.Rock, ENormalCard.Scissors, EWinLossResult.Player1Win)]
    [InlineData(ENormalCard.Paper, ENormalCard.Rock, EWinLossResult.Player1Win)]
    // Player 2 wins
    [InlineData(ENormalCard.Paper, ENormalCard.Scissors, EWinLossResult.Player2Win)]
    [InlineData(ENormalCard.Scissors, ENormalCard.Rock, EWinLossResult.Player2Win)]
    [InlineData(ENormalCard.Rock, ENormalCard.Paper, EWinLossResult.Player2Win)]
    // Same card draws
    [InlineData(ENormalCard.Rock, ENormalCard.Rock, EWinLossResult.Draw)]
    [InlineData(ENormalCard.Paper, ENormalCard.Paper, EWinLossResult.Draw)]
    [InlineData(ENormalCard.Scissors, ENormalCard.Scissors, EWinLossResult.Draw)]
    public void Judge_resolves_every_matchup(ENormalCard player1, ENormalCard player2, EWinLossResult expected)
    {
        Assert.Equal(expected, WinLossRules.Judge(player1, player2));
    }
}
