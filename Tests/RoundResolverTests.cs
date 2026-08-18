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
    public void A_revealed_and_finished_round_produces_the_right_fates_and_winloss(
        CardName player1Card,
        CardName player2Card,
        CardFate expectedPlayer1Fate,
        CardFate expectedPlayer2Fate,
        WinLossResult? expectedWinLoss)
    {
        DeckAndHand player1 = MakeZone(player1Card);
        DeckAndHand player2 = MakeZone(player2Card);

        RoundResult result = ResolveWithoutChoices(player1Card, player2Card, player1, player2);

        Assert.Equal(expectedPlayer1Fate, result.Player1CardFate);
        Assert.Equal(expectedPlayer2Fate, result.Player2CardFate);
        Assert.Equal(expectedWinLoss, result.WinLoss);
    }

    [Fact]
    public void Resolving_removes_the_played_card_from_each_hand()
    {
        DeckAndHand player1 = MakeZone(CardName.Rock);
        DeckAndHand player2 = MakeZone(CardName.Scissors);

        ResolveWithoutChoices(CardName.Rock, CardName.Scissors, player1, player2);

        Assert.False(player1.Hand.Contains(CardName.Rock));
        Assert.False(player2.Hand.Contains(CardName.Scissors));
    }

    [Fact]
    public void Resolving_returns_a_normal_card_to_the_deck_bottom()
    {
        DeckAndHand player1 = MakeZone(CardName.Rock);
        DeckAndHand player2 = MakeZone(CardName.Scissors);

        ResolveWithoutChoices(CardName.Rock, CardName.Scissors, player1, player2);

        // Deck started as [Paper]. Rock is returned to the bottom -> [Paper, Rock].
        // Then Paper is drawn off the top, leaving just [Rock].
        Assert.Equal(1, player1.Deck.Count);
        Assert.Equal(CardName.Rock, player1.Deck.TakeFromTop());
    }

    [Fact]
    public void Resolving_draws_a_new_card_for_both_players_regardless_of_outcome()
    {
        DeckAndHand player1 = MakeZone(CardName.Joker);
        DeckAndHand player2 = MakeZone(CardName.Dummy);

        RoundResult result = ResolveWithoutChoices(CardName.Joker, CardName.Dummy, player1, player2);

        Assert.Equal(new[] { CardName.Paper }, result.Player1Hand);
        Assert.Equal(new[] { CardName.Paper }, result.Player2Hand);
        Assert.Contains(CardName.Paper, player1.Hand.Cards);
        Assert.Contains(CardName.Paper, player2.Hand.Cards);
    }

    [Fact]
    public void Resolving_reports_the_full_post_round_hand_after_a_Draw_card()
    {
        // Draw's own card vanishes, its effect draws 2, then the round draw adds 1 more —
        // three new cards in hand, which a single "what you drew" field could not describe.
        DeckAndHand player1 = new DeckAndHand(
            new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors }),
            new Hand(new[] { CardName.Draw }));
        DeckAndHand player2 = MakeZone(CardName.Dummy);

        RoundResult result = ResolveWithoutChoices(CardName.Draw, CardName.Dummy, player1, player2);

        Assert.Equal(new[] { CardName.Rock, CardName.Paper, CardName.Scissors }, result.Player1Hand);
        Assert.Equal(0, result.Player1DeckCount);
    }

    [Fact]
    public void Resolving_runs_the_played_specials_effect()
    {
        DeckAndHand player1 = MakeZone(CardName.Refill);
        DeckAndHand player2 = MakeZone(CardName.Rock);

        ResolveWithoutChoices(CardName.Refill, CardName.Rock, player1, player2);

        // Refill's own card vanishes (deck(1)+hand(1)=2 -> 1), then Refill's effect
        // adds two Dummy cards to the deck (1 -> 3). Total ends at 3.
        Assert.Equal(3, player1.Deck.Count + player1.Hand.Cards.Count);

        var remaining = new List<CardName>(player1.Hand.Cards);
        remaining.AddRange(DeckContents(player1));
        Assert.Equal(2, remaining.FindAll(card => card == CardName.Dummy).Count);
    }

    [Fact]
    public void Resolving_does_not_include_the_played_special_card_in_its_own_effect()
    {
        // Reset's effect reads "my current hand" — the just-played Reset card must
        // already be gone from the hand by the time that happens, or it would
        // incorrectly get shuffled back into the deck instead of vanishing.
        DeckAndHand player1 = new DeckAndHand(
            new Deck(new[] { CardName.Paper }),
            new Hand(new[] { CardName.Reset, CardName.Rock, CardName.Scissors }));
        DeckAndHand player2 = MakeZone(CardName.Dummy);

        ResolveWithoutChoices(CardName.Reset, CardName.Dummy, player1, player2);

        // Reset's own card never reappears: only Rock and Scissors (plus one post-round
        // draw) can possibly end up in player1's hand.
        Assert.DoesNotContain(CardName.Reset, player1.Hand.Cards);
        Assert.DoesNotContain(CardName.Reset, DeckContents(player1));
    }

    /// <summary>Runs a round with no choice in it through both phases. Every card here
    /// either needs no choice or is blocked by a Joker, so Reveal always leaves the round
    /// ready to finish immediately.</summary>
    private static RoundResult ResolveWithoutChoices(
        CardName player1Card,
        CardName player2Card,
        DeckAndHand player1,
        DeckAndHand player2)
    {
        var rng = new Random(1);
        RoundInProgress round = RoundResolver.Reveal(player1Card, player2Card, player1, player2, rng);
        Assert.False(round.IsAwaitingAnyChoice);
        return RoundResolver.Finish(round, player1, player2, rng);
    }


    [Theory]
    [InlineData(CardName.Swap, true)]
    [InlineData(CardName.Transform, true)]
    [InlineData(CardName.Reset, false)]
    [InlineData(CardName.Refill, false)]
    [InlineData(CardName.Draw, false)]
    [InlineData(CardName.Rock, false)]
    [InlineData(CardName.Dummy, false)]
    [InlineData(CardName.Joker, false)]
    public void Every_card_declares_whether_playing_it_needs_a_choice(CardName card, bool expected)
    {
        // The phase machine asks this rather than testing card names, so a sixth special
        // card only has to answer it — this is the test that catches one that forgets.
        Assert.Equal(expected, RoundResolver.RequiresChoice(card));
    }

    [Fact]
    public void Reveal_runs_the_opponents_Reset_before_anyone_is_asked_to_choose()
    {
        // Reset replaces both hands. Running it during Reveal is what makes the hand a
        // player is offered the same hand their choice will be applied to.
        var player1 = new DeckAndHand(new Deck(RepeatedCards(CardName.Rock, 20)), new Hand(new[] { CardName.Reset }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(CardName.Paper, 20)), new Hand(new[] { CardName.Transform, CardName.Dummy }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(CardName.Reset, CardName.Transform, player1, player2, rng);

        Assert.True(round.IsAwaitingAnyChoice);
        Assert.Equal(ChoiceStatus.Awaited, round.ChoiceStatusOf(Side.Player2));
        Assert.Equal(ChoiceStatus.NotRequired, round.ChoiceStatusOf(Side.Player1));

        // Whatever Reset left in hand can be transformed without throwing — which is the
        // property the old submit-time choice could not offer.
        CardName offered = player2.Hand.Cards[0];
        round.RecordChoice(Side.Player2, CardChoice.Transforming(offered, CardName.Scissors));
        RoundResult result = RoundResolver.Finish(round, player1, player2, rng);

        Assert.True(result.Player2TransformApplied);
        Assert.Contains(CardName.Scissors, result.Player2Hand);
    }

    [Fact]
    public void A_declined_choice_finishes_the_round_with_the_effect_unrun()
    {
        var player1 = new DeckAndHand(new Deck(RepeatedCards(CardName.Rock, 20)), new Hand(new[] { CardName.Swap, CardName.Dummy }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(CardName.Rock, 20)), new Hand(new[] { CardName.Rock }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(CardName.Swap, CardName.Rock, player1, player2, rng);
        round.DeclineChoice(Side.Player1);
        RoundResult result = RoundResolver.Finish(round, player1, player2, rng);

        Assert.Equal(0, result.Player1SwappedCardCount);
        Assert.Contains(CardName.Dummy, result.Player1Hand);
    }

    private static List<CardName> RepeatedCards(CardName card, int count)
    {
        var cards = new List<CardName>();
        for (int i = 0; i < count; i++)
        {
            cards.Add(card);
        }

        return cards;
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
