using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class RoundResolverTests
{
    private static DeckAndHand MakeZone(CardName handCard)
    {
        return new DeckAndHand(
            new Deck(new[] { CardName.Paper }),
            new Hand(new[] { handCard }));
    }

    [Theory]
    [InlineData(CardName.Rock, CardName.Scissors, CardFate.ReturnedToDeckBottom, CardFate.ReturnedToDeckBottom, WinLossResult.Player1Win)]
    [InlineData(CardName.Scissors, CardName.Rock, CardFate.ReturnedToDeckBottom, CardFate.ReturnedToDeckBottom, WinLossResult.Player2Win)]
    [InlineData(CardName.Rock, CardName.Rock, CardFate.ReturnedToDeckBottom, CardFate.ReturnedToDeckBottom, WinLossResult.Draw)]
    [InlineData(CardName.Rock, CardName.Dummy, CardFate.ReturnedToDeckBottom, CardFate.Vanished, null)]
    [InlineData(CardName.Dummy, CardName.Rock, CardFate.Vanished, CardFate.ReturnedToDeckBottom, null)]
    [InlineData(CardName.Dummy, CardName.Dummy, CardFate.Vanished, CardFate.Vanished, null)]
    [InlineData(CardName.Joker, CardName.Rock, CardFate.Vanished, CardFate.Vanished, null)]
    [InlineData(CardName.Rock, CardName.Joker, CardFate.Vanished, CardFate.Vanished, null)]
    [InlineData(CardName.Joker, CardName.Dummy, CardFate.Vanished, CardFate.Vanished, null)]
    [InlineData(CardName.Joker, CardName.Joker, CardFate.Vanished, CardFate.Vanished, null)]
    public void Resolve_produces_the_right_fates_and_winloss(
        CardName player1Card,
        CardName player2Card,
        CardFate expectedPlayer1Fate,
        CardFate expectedPlayer2Fate,
        WinLossResult? expectedWinLoss)
    {
        DeckAndHand player1 = MakeZone(player1Card);
        DeckAndHand player2 = MakeZone(player2Card);

        RoundResult result = RoundResolver.Resolve(player1Card, player2Card, player1, player2);

        Assert.Equal(expectedPlayer1Fate, result.Player1CardFate);
        Assert.Equal(expectedPlayer2Fate, result.Player2CardFate);
        Assert.Equal(expectedWinLoss, result.WinLoss);
    }

    [Fact]
    public void Resolve_removes_the_played_card_from_each_hand()
    {
        DeckAndHand player1 = MakeZone(CardName.Rock);
        DeckAndHand player2 = MakeZone(CardName.Scissors);

        RoundResolver.Resolve(CardName.Rock, CardName.Scissors, player1, player2);

        Assert.False(player1.Hand.Contains(CardName.Rock));
        Assert.False(player2.Hand.Contains(CardName.Scissors));
    }

    [Fact]
    public void Resolve_returns_a_normal_card_to_the_deck_bottom()
    {
        DeckAndHand player1 = MakeZone(CardName.Rock);
        DeckAndHand player2 = MakeZone(CardName.Scissors);

        RoundResolver.Resolve(CardName.Rock, CardName.Scissors, player1, player2);

        // Deck started as [Paper]. Rock is returned to the bottom -> [Paper, Rock].
        // Then Paper is drawn off the top, leaving just [Rock].
        Assert.Equal(1, player1.Deck.Count);
        Assert.Equal(CardName.Rock, player1.Deck.TakeFromTop());
    }

    [Fact]
    public void Resolve_draws_a_new_card_for_both_players_regardless_of_outcome()
    {
        DeckAndHand player1 = MakeZone(CardName.Joker);
        DeckAndHand player2 = MakeZone(CardName.Dummy);

        RoundResult result = RoundResolver.Resolve(CardName.Joker, CardName.Dummy, player1, player2);

        Assert.Equal(CardName.Paper, result.Player1Drew);
        Assert.Equal(CardName.Paper, result.Player2Drew);
        Assert.Contains(CardName.Paper, player1.Hand.Cards);
        Assert.Contains(CardName.Paper, player2.Hand.Cards);
    }

    [Theory]
    [InlineData(CardName.Reset, CardName.Rock)]
    [InlineData(CardName.Rock, CardName.Foresight)]
    public void Resolve_throws_for_a_special_card(CardName player1Card, CardName player2Card)
    {
        DeckAndHand player1 = MakeZone(player1Card);
        DeckAndHand player2 = MakeZone(player2Card);

        Assert.Throws<NotImplementedException>(
            () => RoundResolver.Resolve(player1Card, player2Card, player1, player2));
    }
}
