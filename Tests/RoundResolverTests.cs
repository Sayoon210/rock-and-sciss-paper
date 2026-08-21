using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class RoundResolverTests
{
    private static DeckAndHand MakeZone(ECardName handCard)
    {
        return new DeckAndHand(
            new Deck(new[] { ECardName.Paper }),
            new Hand(new[] { handCard }));
    }

    [Theory]
    [InlineData(ECardName.Rock, ECardName.Scissors, ECardFate.ReturnedToDeckBottom, ECardFate.ReturnedToDeckBottom, EWinLossResult.Player1Win)]
    [InlineData(ECardName.Scissors, ECardName.Rock, ECardFate.ReturnedToDeckBottom, ECardFate.ReturnedToDeckBottom, EWinLossResult.Player2Win)]
    [InlineData(ECardName.Rock, ECardName.Rock, ECardFate.ReturnedToDeckBottom, ECardFate.ReturnedToDeckBottom, EWinLossResult.Draw)]
    [InlineData(ECardName.Rock, ECardName.Blank, ECardFate.ReturnedToDeckBottom, ECardFate.Vanished, null)]
    [InlineData(ECardName.Blank, ECardName.Rock, ECardFate.Vanished, ECardFate.ReturnedToDeckBottom, null)]
    [InlineData(ECardName.Blank, ECardName.Blank, ECardFate.Vanished, ECardFate.Vanished, null)]
    [InlineData(ECardName.Joker, ECardName.Rock, ECardFate.Vanished, ECardFate.Vanished, null)]
    [InlineData(ECardName.Rock, ECardName.Joker, ECardFate.Vanished, ECardFate.Vanished, null)]
    [InlineData(ECardName.Joker, ECardName.Blank, ECardFate.Vanished, ECardFate.Vanished, null)]
    [InlineData(ECardName.Joker, ECardName.Joker, ECardFate.Vanished, ECardFate.Vanished, null)]
    [InlineData(ECardName.Rock, ECardName.Draw, ECardFate.ReturnedToDeckBottom, ECardFate.Vanished, null)]
    public void A_revealed_and_finished_round_produces_the_right_fates_and_winloss(
        ECardName player1Card,
        ECardName player2Card,
        ECardFate expectedPlayer1Fate,
        ECardFate expectedPlayer2Fate,
        EWinLossResult? expectedWinLoss)
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
        DeckAndHand player1 = MakeZone(ECardName.Rock);
        DeckAndHand player2 = MakeZone(ECardName.Scissors);

        ResolveWithoutChoices(ECardName.Rock, ECardName.Scissors, player1, player2);

        Assert.False(player1.Hand.Contains(ECardName.Rock));
        Assert.False(player2.Hand.Contains(ECardName.Scissors));
    }

    [Fact]
    public void Resolving_returns_a_normal_card_to_the_deck_bottom()
    {
        DeckAndHand player1 = MakeZone(ECardName.Rock);
        DeckAndHand player2 = MakeZone(ECardName.Scissors);

        ResolveWithoutChoices(ECardName.Rock, ECardName.Scissors, player1, player2);

        // Deck started as [Paper]. Rock is returned to the bottom -> [Paper, Rock].
        // Then Paper is drawn off the top, leaving just [Rock].
        Assert.Equal(1, player1.Deck.Count);
        Assert.Equal(ECardName.Rock, player1.Deck.TakeFromTop());
    }

    [Fact]
    public void Resolving_draws_a_new_card_for_both_players_regardless_of_outcome()
    {
        DeckAndHand player1 = MakeZone(ECardName.Joker);
        DeckAndHand player2 = MakeZone(ECardName.Blank);

        RoundResult result = ResolveWithoutChoices(ECardName.Joker, ECardName.Blank, player1, player2);

        Assert.Equal(new[] { ECardName.Paper }, result.Player1Hand);
        Assert.Equal(new[] { ECardName.Paper }, result.Player2Hand);
        Assert.Contains(ECardName.Paper, player1.Hand.Cards);
        Assert.Contains(ECardName.Paper, player2.Hand.Cards);
    }

    [Fact]
    public void Resolving_reports_the_full_post_round_hand_after_a_Draw_card()
    {
        // Draw's own card vanishes, its effect draws 2, then the round draw adds 1 more —
        // three new cards in hand, which a single "what you drew" field could not describe.
        DeckAndHand player1 = new DeckAndHand(
            new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors }),
            new Hand(new[] { ECardName.Draw }));
        DeckAndHand player2 = MakeZone(ECardName.Blank);

        RoundResult result = ResolveWithoutChoices(ECardName.Draw, ECardName.Blank, player1, player2);

        Assert.Equal(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors }, result.Player1Hand);
        Assert.Equal(0, result.Player1DeckCount);
    }

    [Fact]
    public void Resolving_runs_the_played_abilities_effect()
    {
        // A deck of its own rather than MakeZone's single card, because 드로우 is the effect
        // being watched for and one card cannot show two draws happening.
        var player1 = new DeckAndHand(
            new Deck(new[] { ECardName.Paper, ECardName.Paper, ECardName.Paper, ECardName.Paper }),
            new Hand(new[] { ECardName.Draw }));
        DeckAndHand player2 = MakeZone(ECardName.Rock);

        ResolveWithoutChoices(ECardName.Draw, ECardName.Rock, player1, player2);

        // 드로우's own card vanishes, leaving an empty hand, then its effect draws two and the
        // round's own draw adds a third. Without the effect the hand would hold one card.
        Assert.Equal(3, player1.Hand.Cards.Count);
        Assert.Equal(1, player1.Deck.Count);
    }

    [Fact]
    public void Resolving_does_not_include_the_played_ability_card_in_its_own_effect()
    {
        // Reset's effect reads "my current hand" — the just-played Reset card must
        // already be gone from the hand by the time that happens, or it would
        // incorrectly get shuffled back into the deck instead of vanishing.
        DeckAndHand player1 = new DeckAndHand(
            new Deck(new[] { ECardName.Paper }),
            new Hand(new[] { ECardName.Reset, ECardName.Rock, ECardName.Scissors }));
        DeckAndHand player2 = MakeZone(ECardName.Blank);

        ResolveWithoutChoices(ECardName.Reset, ECardName.Blank, player1, player2);

        // Reset's own card never reappears: only Rock and Scissors (plus one post-round
        // draw) can possibly end up in player1's hand.
        Assert.DoesNotContain(ECardName.Reset, player1.Hand.Cards);
        Assert.DoesNotContain(ECardName.Reset, DeckContents(player1));
    }

    /// <summary>Runs a round with no choice in it through both phases. Every card here
    /// either needs no choice or is blocked by a Joker, so Reveal always leaves the round
    /// ready to finish immediately.</summary>
    private static RoundResult ResolveWithoutChoices(
        ECardName player1Card,
        ECardName player2Card,
        DeckAndHand player1,
        DeckAndHand player2)
    {
        var rng = new Random(1);
        RoundInProgress round = RoundResolver.Reveal(player1Card, player2Card, player1, player2, rng);
        Assert.False(round.IsAwaitingAnyChoice);
        return RoundResolver.Finish(round, player1, player2, rng);
    }


    [Theory]
    [InlineData(ECardName.Swap, true)]
    [InlineData(ECardName.Transform, true)]
    [InlineData(ECardName.Reset, false)]
    [InlineData(ECardName.Draw, false)]
    [InlineData(ECardName.Rock, false)]
    [InlineData(ECardName.Blank, false)]
    [InlineData(ECardName.Joker, false)]
    public void Every_card_declares_whether_playing_it_needs_a_choice(ECardName card, bool expected)
    {
        // The phase machine asks this rather than testing card names, so a sixth ability
        // card only has to answer it — this is the test that catches one that forgets.
        Assert.Equal(expected, RoundResolver.RequiresChoice(card));
    }

    [Fact]
    public void Reveal_runs_the_opponents_Reset_before_anyone_is_asked_to_choose()
    {
        // Reset replaces both hands. Running it during Reveal is what makes the hand a
        // player is offered the same hand their choice will be applied to.
        var player1 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Rock, 20)), new Hand(new[] { ECardName.Reset }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Paper, 20)), new Hand(new[] { ECardName.Transform, ECardName.Blank }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(ECardName.Reset, ECardName.Transform, player1, player2, rng);

        Assert.True(round.IsAwaitingAnyChoice);
        Assert.Equal(EChoiceStatus.Awaited, round.ChoiceStatusOf(ESide.Player2));
        Assert.Equal(EChoiceStatus.NotRequired, round.ChoiceStatusOf(ESide.Player1));

        // Whatever Reset left in hand can be transformed without throwing — which is the
        // property the old submit-time choice could not offer.
        ECardName offered = player2.Hand.Cards[0];
        round.RecordChoice(ESide.Player2, CardChoice.Transforming(offered, ECardName.Scissors));
        RoundResult result = RoundResolver.Finish(round, player1, player2, rng);

        Assert.True(result.Player2TransformApplied);
        Assert.Contains(ECardName.Scissors, result.Player2Hand);
    }

    [Fact]
    public void A_Reset_that_leaves_nothing_transformable_asks_for_no_choice_at_all()
    {
        // The reachable version of "asked for a choice that cannot be made": 변화 is played,
        // the opponent's 리셋 runs first and replaces the hand, and what comes back holds no
        // 일반카드/공백카드. Before this, the player was asked anyway and could only wait out
        // the timeout with every card in the picker greyed out.
        var player1 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Rock, 20)), new Hand(new[] { ECardName.Reset }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Joker, 20)), new Hand(new[] { ECardName.Transform, ECardName.Joker }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(ECardName.Reset, ECardName.Transform, player1, player2, rng);

        Assert.False(round.IsAwaitingAnyChoice);
        Assert.Equal(EChoiceStatus.NotRequired, round.ChoiceStatusOf(ESide.Player2));

        // Finishes on its own, with 변화 simply unrun — the same ending a declined choice has.
        RoundResult result = RoundResolver.Finish(round, player1, player2, rng);
        Assert.False(result.Player2TransformApplied);
    }

    [Fact]
    public void A_Joker_blocks_a_Reset_so_neither_hand_is_replaced()
    {
        // 조커 outranks 리셋 (DESIGN.md): it destroys whatever the other side played and
        // blocks its effect outright, so nothing about either 패 changes.
        var player1 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Rock, 20)), new Hand(new[] { ECardName.Joker, ECardName.Blank }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Paper, 20)), new Hand(new[] { ECardName.Reset, ECardName.Scissors }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(ECardName.Joker, ECardName.Reset, player1, player2, rng);

        Assert.Equal(new[] { ECardName.Blank }, player1.Hand.Cards);
        Assert.Equal(new[] { ECardName.Scissors }, player2.Hand.Cards);
        Assert.False(round.ResetApplied);
    }

    [Fact]
    public void A_Reset_with_no_Joker_in_the_round_is_recorded_as_applied()
    {
        var player1 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Rock, 20)), new Hand(new[] { ECardName.Reset, ECardName.Blank }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Paper, 20)), new Hand(new[] { ECardName.Scissors, ECardName.Scissors }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(ECardName.Reset, ECardName.Scissors, player1, player2, rng);

        Assert.True(round.ResetApplied);
    }

    [Fact]
    public void A_declined_choice_finishes_the_round_with_the_effect_unrun()
    {
        var player1 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Rock, 20)), new Hand(new[] { ECardName.Swap, ECardName.Blank }));
        var player2 = new DeckAndHand(new Deck(RepeatedCards(ECardName.Rock, 20)), new Hand(new[] { ECardName.Rock }));
        var rng = new Random(1);

        RoundInProgress round = RoundResolver.Reveal(ECardName.Swap, ECardName.Rock, player1, player2, rng);
        round.DeclineChoice(ESide.Player1);
        RoundResult result = RoundResolver.Finish(round, player1, player2, rng);

        Assert.Equal(0, result.Player1SwappedCardCount);
        Assert.Contains(ECardName.Blank, result.Player1Hand);
    }

    private static List<ECardName> RepeatedCards(ECardName card, int count)
    {
        var cards = new List<ECardName>();
        for (int i = 0; i < count; i++)
        {
            cards.Add(card);
        }

        return cards;
    }

    private static List<ECardName> DeckContents(DeckAndHand zone)
    {
        var contents = new List<ECardName>();
        while (zone.Deck.Count > 0)
        {
            contents.Add(zone.Deck.TakeFromTop());
        }

        return contents;
    }
}
