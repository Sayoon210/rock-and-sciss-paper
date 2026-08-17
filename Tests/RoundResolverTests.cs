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
    [InlineData(CardName.Rock, CardName.Refill, CardFate.ReturnedToDeckBottom, CardFate.Vanished, null)]
    public void Resolve_produces_the_right_fates_and_winloss(
        CardName player1Card,
        CardName player2Card,
        CardFate expectedPlayer1Fate,
        CardFate expectedPlayer2Fate,
        WinLossResult? expectedWinLoss)
    {
        DeckAndHand player1 = MakeZone(player1Card);
        DeckAndHand player2 = MakeZone(player2Card);

        RoundResult result = RoundResolver.Resolve(CardPlay.WithoutChoice(player1Card), CardPlay.WithoutChoice(player2Card), player1, player2, new Random(1));

        Assert.Equal(expectedPlayer1Fate, result.Player1CardFate);
        Assert.Equal(expectedPlayer2Fate, result.Player2CardFate);
        Assert.Equal(expectedWinLoss, result.WinLoss);
    }

    [Fact]
    public void Resolve_removes_the_played_card_from_each_hand()
    {
        DeckAndHand player1 = MakeZone(CardName.Rock);
        DeckAndHand player2 = MakeZone(CardName.Scissors);

        RoundResolver.Resolve(CardPlay.WithoutChoice(CardName.Rock), CardPlay.WithoutChoice(CardName.Scissors), player1, player2, new Random(1));

        Assert.False(player1.Hand.Contains(CardName.Rock));
        Assert.False(player2.Hand.Contains(CardName.Scissors));
    }

    [Fact]
    public void Resolve_returns_a_normal_card_to_the_deck_bottom()
    {
        DeckAndHand player1 = MakeZone(CardName.Rock);
        DeckAndHand player2 = MakeZone(CardName.Scissors);

        RoundResolver.Resolve(CardPlay.WithoutChoice(CardName.Rock), CardPlay.WithoutChoice(CardName.Scissors), player1, player2, new Random(1));

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

        RoundResult result = RoundResolver.Resolve(CardPlay.WithoutChoice(CardName.Joker), CardPlay.WithoutChoice(CardName.Dummy), player1, player2, new Random(1));

        Assert.Equal(new[] { CardName.Paper }, result.Player1Hand);
        Assert.Equal(new[] { CardName.Paper }, result.Player2Hand);
        Assert.Contains(CardName.Paper, player1.Hand.Cards);
        Assert.Contains(CardName.Paper, player2.Hand.Cards);
    }

    [Fact]
    public void Resolve_reports_the_full_post_round_hand_after_a_Draw_card()
    {
        // Draw's own card vanishes, its effect draws 2, then the round draw adds 1 more —
        // three new cards in hand, which a single "what you drew" field could not describe.
        DeckAndHand player1 = new DeckAndHand(
            new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors }),
            new Hand(new[] { CardName.Draw }));
        DeckAndHand player2 = MakeZone(CardName.Dummy);

        RoundResult result = RoundResolver.Resolve(CardPlay.WithoutChoice(CardName.Draw), CardPlay.WithoutChoice(CardName.Dummy), player1, player2, new Random(1));

        Assert.Equal(new[] { CardName.Rock, CardName.Paper, CardName.Scissors }, result.Player1Hand);
        Assert.Equal(0, result.Player1DeckCount);
    }

    [Fact]
    public void Resolve_runs_the_played_specials_effect()
    {
        DeckAndHand player1 = MakeZone(CardName.Refill);
        DeckAndHand player2 = MakeZone(CardName.Rock);

        RoundResolver.Resolve(CardPlay.WithoutChoice(CardName.Refill), CardPlay.WithoutChoice(CardName.Rock), player1, player2, new Random(1));

        // Refill's own card vanishes (deck(1)+hand(1)=2 -> 1), then Refill's effect
        // adds two Dummy cards to the deck (1 -> 3). Total ends at 3.
        Assert.Equal(3, player1.Deck.Count + player1.Hand.Cards.Count);

        var remaining = new List<CardName>(player1.Hand.Cards);
        remaining.AddRange(DeckContents(player1));
        Assert.Equal(2, remaining.FindAll(card => card == CardName.Dummy).Count);
    }

    [Fact]
    public void Resolve_does_not_include_the_played_special_card_in_its_own_effect()
    {
        // Reset's effect reads "my current hand" — the just-played Reset card must
        // already be gone from the hand by the time that happens, or it would
        // incorrectly get shuffled back into the deck instead of vanishing.
        DeckAndHand player1 = new DeckAndHand(
            new Deck(new[] { CardName.Paper }),
            new Hand(new[] { CardName.Reset, CardName.Rock, CardName.Scissors }));
        DeckAndHand player2 = MakeZone(CardName.Dummy);

        RoundResolver.Resolve(CardPlay.WithoutChoice(CardName.Reset), CardPlay.WithoutChoice(CardName.Dummy), player1, player2, new Random(1));

        // Reset's own card never reappears: only Rock and Scissors (plus one post-round
        // draw) can possibly end up in player1's hand.
        Assert.DoesNotContain(CardName.Reset, player1.Hand.Cards);
        Assert.DoesNotContain(CardName.Reset, DeckContents(player1));
    }

    private static List<CardName> DeckContents(DeckAndHand zone)
    {
        var contents = new List<CardName>();
        while (zone.Deck.Count > 0)
        {
            contents.Add(zone.Deck.TakeFromTop());
        }

        return contents;
    }
}
