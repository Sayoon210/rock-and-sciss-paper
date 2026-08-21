using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class RoundResultTests
{
    [Fact]
    public void Constructor_assigns_every_property()
    {
        var result = new RoundResult(
            player1Card: ECardName.Rock,
            player2Card: ECardName.Scissors,
            player1CardFate: ECardFate.ReturnedToDeckBottom,
            player2CardFate: ECardFate.ReturnedToDeckBottom,
            winLoss: EWinLossResult.Player1Win,
            player1Hand: new[] { ECardName.Paper },
            player2Hand: new[] { ECardName.Blank, ECardName.Joker },
            player1DeckCount: 12,
            player2DeckCount: 11,
            player1SwappedCardCount: 2,
            player2SwappedCardCount: 0,
            player1TransformApplied: false,
            player2TransformApplied: true,
            resetApplied: true);

        Assert.Equal(ECardName.Rock, result.Player1Card);
        Assert.Equal(ECardName.Scissors, result.Player2Card);
        Assert.Equal(ECardFate.ReturnedToDeckBottom, result.Player1CardFate);
        Assert.Equal(ECardFate.ReturnedToDeckBottom, result.Player2CardFate);
        Assert.Equal(EWinLossResult.Player1Win, result.WinLoss);
        Assert.Equal(new[] { ECardName.Paper }, result.Player1Hand);
        Assert.Equal(new[] { ECardName.Blank, ECardName.Joker }, result.Player2Hand);
        Assert.Equal(12, result.Player1DeckCount);
        Assert.Equal(11, result.Player2DeckCount);
        Assert.Equal(2, result.Player1SwappedCardCount);
        Assert.Equal(0, result.Player2SwappedCardCount);
        Assert.False(result.Player1TransformApplied);
        Assert.True(result.Player2TransformApplied);
        Assert.True(result.ResetApplied);
    }

    [Fact]
    public void WinLoss_is_null_for_a_round_with_no_win_loss()
    {
        var result = new RoundResult(
            player1Card: ECardName.Joker,
            player2Card: ECardName.Rock,
            player1CardFate: ECardFate.Vanished,
            player2CardFate: ECardFate.Vanished,
            winLoss: null,
            player1Hand: Array.Empty<ECardName>(),
            player2Hand: Array.Empty<ECardName>(),
            player1DeckCount: 0,
            player2DeckCount: 0,
            player1SwappedCardCount: 0,
            player2SwappedCardCount: 0,
            player1TransformApplied: false,
            player2TransformApplied: false,
            resetApplied: false);

        Assert.Null(result.WinLoss);
    }

    [Fact]
    public void Hands_are_snapshots_that_later_hand_changes_do_not_affect()
    {
        var hand = new Hand(new[] { ECardName.Rock });

        var result = new RoundResult(
            player1Card: ECardName.Paper,
            player2Card: ECardName.Paper,
            player1CardFate: ECardFate.ReturnedToDeckBottom,
            player2CardFate: ECardFate.ReturnedToDeckBottom,
            winLoss: EWinLossResult.Draw,
            player1Hand: hand.Cards,
            player2Hand: Array.Empty<ECardName>(),
            player1DeckCount: 1,
            player2DeckCount: 1,
            player1SwappedCardCount: 0,
            player2SwappedCardCount: 0,
            player1TransformApplied: false,
            player2TransformApplied: false,
            resetApplied: false);

        hand.Add(ECardName.Joker);

        Assert.Equal(new[] { ECardName.Rock }, result.Player1Hand);
    }
}
