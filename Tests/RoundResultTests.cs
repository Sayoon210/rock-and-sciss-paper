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
            player1Hand: new[] { CardName.Paper },
            player2Hand: new[] { CardName.Dummy, CardName.Joker },
            player1DeckCount: 12,
            player2DeckCount: 11,
            player1SwappedCardCount: 2,
            player2SwappedCardCount: 0,
            player1TransformApplied: false,
            player2TransformApplied: true);

        Assert.Equal(CardName.Rock, result.Player1Card);
        Assert.Equal(CardName.Scissors, result.Player2Card);
        Assert.Equal(CardFate.ReturnedToDeckBottom, result.Player1CardFate);
        Assert.Equal(CardFate.ReturnedToDeckBottom, result.Player2CardFate);
        Assert.Equal(WinLossResult.Player1Win, result.WinLoss);
        Assert.Equal(new[] { CardName.Paper }, result.Player1Hand);
        Assert.Equal(new[] { CardName.Dummy, CardName.Joker }, result.Player2Hand);
        Assert.Equal(12, result.Player1DeckCount);
        Assert.Equal(11, result.Player2DeckCount);
        Assert.Equal(2, result.Player1SwappedCardCount);
        Assert.Equal(0, result.Player2SwappedCardCount);
        Assert.False(result.Player1TransformApplied);
        Assert.True(result.Player2TransformApplied);
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
            player1Hand: Array.Empty<CardName>(),
            player2Hand: Array.Empty<CardName>(),
            player1DeckCount: 0,
            player2DeckCount: 0,
            player1SwappedCardCount: 0,
            player2SwappedCardCount: 0,
            player1TransformApplied: false,
            player2TransformApplied: false);

        Assert.Null(result.WinLoss);
    }

    [Fact]
    public void Hands_are_snapshots_that_later_hand_changes_do_not_affect()
    {
        var hand = new Hand(new[] { CardName.Rock });

        var result = new RoundResult(
            player1Card: CardName.Paper,
            player2Card: CardName.Paper,
            player1CardFate: CardFate.ReturnedToDeckBottom,
            player2CardFate: CardFate.ReturnedToDeckBottom,
            winLoss: WinLossResult.Draw,
            player1Hand: hand.Cards,
            player2Hand: Array.Empty<CardName>(),
            player1DeckCount: 1,
            player2DeckCount: 1,
            player1SwappedCardCount: 0,
            player2SwappedCardCount: 0,
            player1TransformApplied: false,
            player2TransformApplied: false);

        hand.Add(CardName.Joker);

        Assert.Equal(new[] { CardName.Rock }, result.Player1Hand);
    }
}
