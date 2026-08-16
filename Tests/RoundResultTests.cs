using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class RoundResultTests
{
    [Fact]
    public void Constructor_assigns_every_property()
    {
        var result = new RoundResult(
            player1Card: CardName.Rock,
            player2Card: CardName.Scissors,
            player1CardFate: CardFate.ReturnedToDeckBottom,
            player2CardFate: CardFate.ReturnedToDeckBottom,
            winLoss: WinLossResult.Player1Win,
            player1Drew: CardName.Paper,
            player2Drew: CardName.Dummy);

        Assert.Equal(CardName.Rock, result.Player1Card);
        Assert.Equal(CardName.Scissors, result.Player2Card);
        Assert.Equal(CardFate.ReturnedToDeckBottom, result.Player1CardFate);
        Assert.Equal(CardFate.ReturnedToDeckBottom, result.Player2CardFate);
        Assert.Equal(WinLossResult.Player1Win, result.WinLoss);
        Assert.Equal(CardName.Paper, result.Player1Drew);
        Assert.Equal(CardName.Dummy, result.Player2Drew);
    }

    [Fact]
    public void WinLoss_is_null_for_a_round_with_no_win_loss()
    {
        var result = new RoundResult(
            player1Card: CardName.Joker,
            player2Card: CardName.Rock,
            player1CardFate: CardFate.Vanished,
            player2CardFate: CardFate.Vanished,
            winLoss: null,
            player1Drew: CardName.Reset,
            player2Drew: CardName.Paper);

        Assert.Null(result.WinLoss);
    }
}
