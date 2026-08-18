using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class MatchSessionTests
{
    /// <summary>Twelve cards, enough to mulligan six and keep playing for a while.</summary>
    private static IEnumerable<CardName> SmallDeck()
    {
        return new[]
        {
            CardName.Rock, CardName.Rock, CardName.Rock, CardName.Rock,
            CardName.Paper, CardName.Paper, CardName.Paper, CardName.Paper,
            CardName.Scissors, CardName.Scissors, CardName.Scissors, CardName.Scissors,
        };
    }

    private static MatchSession NewSession()
    {
        return new MatchSession(SmallDeck(), SmallDeck(), new Random(1));
    }

    [Fact]
    public void Constructor_deals_a_mulligan_hand_to_both_players()
    {
        MatchSession session = NewSession();

        Assert.Equal(MatchSession.MULLIGAN_HAND_SIZE, session.HandOf(Side.Player1).Count);
        Assert.Equal(MatchSession.MULLIGAN_HAND_SIZE, session.HandOf(Side.Player2).Count);
        Assert.Equal(6, session.DeckCountOf(Side.Player1));
        Assert.Equal(6, session.DeckCountOf(Side.Player2));
    }

    [Fact]
    public void A_new_match_has_no_score_and_no_winner()
    {
        MatchSession session = NewSession();

        Assert.Equal(0, session.Player1Score);
        Assert.Equal(0, session.Player2Score);
        Assert.Equal(1, session.RoundNumber);
        Assert.Null(session.Winner);
        Assert.Equal(RoundPhase.AwaitingSubmissions, session.Phase);
    }

    [Fact]
    public void SubmitCard_returns_null_until_both_sides_have_submitted()
    {
        MatchSession session = NewSession();
        CardName player1Card = session.HandOf(Side.Player1)[0];
        CardName player2Card = session.HandOf(Side.Player2)[0];

        RoundReveal? afterFirst = session.SubmitCard(Side.Player1, player1Card);
        Assert.Null(afterFirst);
        Assert.True(session.HasSubmittedCard(Side.Player1));
        Assert.False(session.HasSubmittedCard(Side.Player2));

        RoundReveal? afterSecond = session.SubmitCard(Side.Player2, player2Card);
        Assert.NotNull(afterSecond);
    }

    [Fact]
    public void A_reveal_with_no_choice_owed_carries_the_finished_round()
    {
        MatchSession session = NewSession();

        session.SubmitCard(Side.Player1, session.HandOf(Side.Player1)[0]);
        RoundReveal? reveal = session.SubmitCard(Side.Player2, session.HandOf(Side.Player2)[0]);

        Assert.False(reveal!.Player1MustChoose);
        Assert.False(reveal.Player2MustChoose);
        Assert.NotNull(reveal.Result);
        Assert.Equal(RoundPhase.AwaitingSubmissions, session.Phase);
    }

    [Fact]
    public void Resolving_a_round_clears_both_submissions_and_advances_the_round_number()
    {
        MatchSession session = NewSession();

        session.SubmitCard(Side.Player1, session.HandOf(Side.Player1)[0]);
        session.SubmitCard(Side.Player2, session.HandOf(Side.Player2)[0]);

        Assert.False(session.HasSubmittedCard(Side.Player1));
        Assert.False(session.HasSubmittedCard(Side.Player2));
        Assert.Equal(2, session.RoundNumber);
    }

    [Fact]
    public void A_win_increments_only_the_winners_score()
    {
        var session = new MatchSession(
            Repeated(CardName.Rock, 8),
            Repeated(CardName.Scissors, 8),
            new Random(1));

        session.SubmitCard(Side.Player1, CardName.Rock);
        RoundReveal? reveal = session.SubmitCard(Side.Player2, CardName.Scissors);

        Assert.Equal(WinLossResult.Player1Win, reveal!.Result!.WinLoss);
        Assert.Equal(1, session.Player1Score);
        Assert.Equal(0, session.Player2Score);
    }

    [Fact]
    public void A_round_with_no_win_loss_does_not_change_the_score()
    {
        var session = new MatchSession(
            Repeated(CardName.Dummy, 8),
            Repeated(CardName.Rock, 8),
            new Random(1));

        session.SubmitCard(Side.Player1, CardName.Dummy);
        RoundReveal? reveal = session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Null(reveal!.Result!.WinLoss);
        Assert.Equal(0, session.Player1Score);
        Assert.Equal(0, session.Player2Score);
    }

    [Fact]
    public void Winner_is_set_once_a_player_reaches_the_needed_wins()
    {
        MatchSession session = PlayRounds(MatchSession.WINS_NEEDED_FOR_MATCH);

        Assert.Equal(MatchSession.WINS_NEEDED_FOR_MATCH, session.Player1Score);
        Assert.Equal(Side.Player1, session.Winner);
    }

    [Fact]
    public void SubmitCard_throws_once_the_match_is_over()
    {
        MatchSession session = PlayRounds(MatchSession.WINS_NEEDED_FOR_MATCH);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(Side.Player1, session.HandOf(Side.Player1)[0]));
    }

    [Fact]
    public void SubmitCard_throws_when_the_same_side_submits_twice()
    {
        MatchSession session = NewSession();
        session.SubmitCard(Side.Player1, session.HandOf(Side.Player1)[0]);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(Side.Player1, session.HandOf(Side.Player1)[0]));
    }

    [Fact]
    public void SubmitCard_throws_for_a_card_the_player_does_not_hold()
    {
        MatchSession session = NewSession();

        Assert.Throws<ArgumentException>(() => session.SubmitCard(Side.Player1, CardName.Joker));
    }

    /// <summary>Ten cards, five of each. A six-card mulligan can hold at most five of one
    /// kind, so it always contains at least one of both — no seed hunting required.</summary>
    private static List<CardName> FiveEachOf(CardName first, CardName second)
    {
        var cards = new List<CardName>();
        for (int i = 0; i < 5; i++)
        {
            cards.Add(first);
            cards.Add(second);
        }

        return cards;
    }

    private static MatchSession SwapVersusPlainSession()
    {
        return new MatchSession(
            FiveEachOf(CardName.Swap, CardName.Dummy),
            Repeated(CardName.Rock, 20),
            new Random(1));
    }

    [Fact]
    public void A_choice_card_leaves_the_round_awaiting_a_choice()
    {
        MatchSession session = SwapVersusPlainSession();

        session.SubmitCard(Side.Player1, CardName.Swap);
        RoundReveal? reveal = session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.True(reveal!.Player1MustChoose);
        Assert.False(reveal.Player2MustChoose);
        Assert.Null(reveal.Result);
        Assert.Equal(RoundPhase.AwaitingChoices, session.Phase);
        Assert.True(session.IsAwaitingChoiceFrom(Side.Player1));
        Assert.Equal(CardName.Swap, session.CardAwaitingChoiceFrom(Side.Player1));
        Assert.Equal(1, session.RoundNumber);
    }

    [Fact]
    public void The_hand_offered_for_a_choice_no_longer_holds_the_played_card()
    {
        // This is the structural property that retires the old "a Swap must not return
        // itself" special case: by the time anyone chooses, the played card is gone.
        //
        // Exactly one Swap, and a deck the same size as the mulligan so the whole deck is
        // dealt — otherwise a second Swap could come up and the assertion would say
        // nothing. The round stops at the choice phase, so the empty deck is never drawn.
        var deck = new List<CardName> { CardName.Swap };
        for (int i = 0; i < MatchSession.MULLIGAN_HAND_SIZE - 1; i++)
        {
            deck.Add(CardName.Dummy);
        }

        var session = new MatchSession(deck, Repeated(CardName.Rock, 20), new Random(1));

        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Equal(RoundPhase.AwaitingChoices, session.Phase);
        Assert.DoesNotContain(CardName.Swap, session.HandOf(Side.Player1));
    }

    [Fact]
    public void Settling_the_last_owed_choice_finishes_the_round()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);

        RoundResult? result = session.SubmitChoice(
            Side.Player1, CardChoice.Swapping(new[] { CardName.Dummy }));

        Assert.NotNull(result);
        Assert.Equal(1, result!.Player1SwappedCardCount);
        Assert.Equal(RoundPhase.AwaitingSubmissions, session.Phase);
        Assert.Equal(2, session.RoundNumber);
        Assert.False(session.HasSubmittedCard(Side.Player1));
    }

    [Fact]
    public void SubmitCard_is_rejected_while_the_round_is_awaiting_choices()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(Side.Player2, CardName.Rock));
    }

    [Fact]
    public void SubmitChoice_is_rejected_from_a_side_that_was_not_asked()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitChoice(Side.Player2, CardChoice.Swapping(Array.Empty<CardName>())));
    }

    [Fact]
    public void SubmitChoice_is_rejected_when_that_side_already_chose()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);
        session.SubmitChoice(Side.Player1, CardChoice.Swapping(Array.Empty<CardName>()));

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitChoice(Side.Player1, CardChoice.Swapping(Array.Empty<CardName>())));
    }

    [Fact]
    public void An_illegal_choice_leaves_the_side_still_awaited_and_the_round_unresolved()
    {
        // The choice-phase counterpart of "a rejected card must leave the round exactly as
        // it was": a client that sends nonsense has to be able to simply try again.
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Throws<ArgumentException>(
            () => session.SubmitChoice(Side.Player1, CardChoice.Swapping(new[] { CardName.Joker })));

        Assert.True(session.IsAwaitingChoiceFrom(Side.Player1));
        Assert.Equal(RoundPhase.AwaitingChoices, session.Phase);
        Assert.Equal(1, session.RoundNumber);

        RoundResult? result = session.SubmitChoice(
            Side.Player1, CardChoice.Swapping(new[] { CardName.Dummy }));
        Assert.NotNull(result);
    }

    [Fact]
    public void DeclineChoice_finishes_the_round_without_running_the_effect()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Rock);

        RoundResult? result = session.DeclineChoice(Side.Player1);

        Assert.NotNull(result);
        Assert.Equal(0, result!.Player1SwappedCardCount);
        Assert.Equal(2, session.RoundNumber);
    }

    [Fact]
    public void DeclineChoice_is_a_no_op_for_a_side_that_owes_nothing()
    {
        // A choice timer that fires just after the round finished must not disturb the
        // next one.
        MatchSession session = SwapVersusPlainSession();

        Assert.Null(session.DeclineChoice(Side.Player1));
        Assert.Equal(1, session.RoundNumber);
        Assert.Equal(RoundPhase.AwaitingSubmissions, session.Phase);
    }

    [Theory]
    [InlineData(CardName.Swap)]
    [InlineData(CardName.Transform)]
    public void A_Joker_leaves_the_blocked_player_with_nothing_to_choose(CardName choiceCard)
    {
        // The whole point of asking after the reveal: a card the Joker destroyed never
        // prompts, instead of prompting and then throwing the answer away.
        var session = new MatchSession(
            FiveEachOf(choiceCard, CardName.Dummy),
            Repeated(CardName.Joker, 20),
            new Random(1));

        session.SubmitCard(Side.Player1, choiceCard);
        RoundReveal? reveal = session.SubmitCard(Side.Player2, CardName.Joker);

        Assert.False(reveal!.Player1MustChoose);
        Assert.NotNull(reveal.Result);
        Assert.Equal(RoundPhase.AwaitingSubmissions, session.Phase);
        Assert.Equal(0, reveal.Result!.Player1SwappedCardCount);
        Assert.False(reveal.Result.Player1TransformApplied);
    }

    [Fact]
    public void A_choice_made_against_the_offered_hand_survives_the_opponents_Reset()
    {
        // 리셋 replaces both players' hands. It runs before anyone is prompted, so the hand
        // P2 is shown here is already the post-리셋 one and a choice drawn from it always
        // applies. Choosing before the reveal — the old flow — let 리셋 invalidate a choice
        // that had already been validated, which threw partway through resolution.
        var session = new MatchSession(
            FiveEachOf(CardName.Reset, CardName.Rock),
            FiveEachOf(CardName.Transform, CardName.Dummy),
            new Random(3));

        session.SubmitCard(Side.Player1, CardName.Reset);
        RoundReveal? reveal = session.SubmitCard(Side.Player2, CardName.Transform);
        Assert.True(reveal!.Player2MustChoose);

        IReadOnlyList<CardName> offered = session.HandOf(Side.Player2);
        CardName target = offered[0];

        RoundResult? result = session.SubmitChoice(
            Side.Player2, CardChoice.Transforming(target, CardName.Paper));

        Assert.NotNull(result);
        Assert.True(result!.Player2TransformApplied);
        Assert.Contains(CardName.Paper, result.Player2Hand);
    }

    [Fact]
    public void Both_sides_choosing_resolves_identically_regardless_of_arrival_order()
    {
        // 교체 and 리셋 both shuffle from the shared rng, so applying choices as they land
        // would make the result depend on which network message arrived first. Choices are
        // recorded on arrival and applied in fixed Player 1 → Player 2 order instead.
        RoundResult player1First = ResolveBothChoices(chooseForPlayer1First: true);
        RoundResult player2First = ResolveBothChoices(chooseForPlayer1First: false);

        Assert.Equal(player1First.Player1Hand, player2First.Player1Hand);
        Assert.Equal(player1First.Player2Hand, player2First.Player2Hand);
        Assert.Equal(player1First.Player1DeckCount, player2First.Player1DeckCount);
        Assert.Equal(player1First.Player2DeckCount, player2First.Player2DeckCount);
    }

    private static RoundResult ResolveBothChoices(bool chooseForPlayer1First)
    {
        var session = new MatchSession(
            FiveEachOf(CardName.Swap, CardName.Dummy),
            FiveEachOf(CardName.Swap, CardName.Rock),
            new Random(5));

        session.SubmitCard(Side.Player1, CardName.Swap);
        session.SubmitCard(Side.Player2, CardName.Swap);

        CardChoice player1Choice = CardChoice.Swapping(new[] { CardName.Dummy });
        CardChoice player2Choice = CardChoice.Swapping(new[] { CardName.Rock });

        RoundResult? result;
        if (chooseForPlayer1First)
        {
            Assert.Null(session.SubmitChoice(Side.Player1, player1Choice));
            result = session.SubmitChoice(Side.Player2, player2Choice);
        }
        else
        {
            Assert.Null(session.SubmitChoice(Side.Player2, player2Choice));
            result = session.SubmitChoice(Side.Player1, player1Choice);
        }

        return result!;
    }

    [Fact]
    public void Running_out_of_cards_ends_the_match_even_with_a_perfect_score()
    {
        // Seven Dummy: mulligan takes six, leaving exactly one in the deck. A Dummy vanishes
        // instead of returning, so the one card left is drawn at the end of round 1 and the
        // deck is empty the moment that round is recorded — before either side has a chance
        // to also win on score.
        var session = new MatchSession(
            Repeated(CardName.Dummy, 7),
            Repeated(CardName.Rock, 20),
            new Random(1));

        session.SubmitCard(Side.Player1, CardName.Dummy);
        session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Equal(0, session.DeckCountOf(Side.Player1));
        Assert.Equal(Side.Player2, session.Winner);
    }

    [Fact]
    public void SubmitCard_throws_once_a_side_has_exhausted_its_deck()
    {
        var session = new MatchSession(
            Repeated(CardName.Dummy, 7),
            Repeated(CardName.Rock, 20),
            new Random(1));

        session.SubmitCard(Side.Player1, CardName.Dummy);
        session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(Side.Player2, session.HandOf(Side.Player2)[0]));
    }

    [Fact]
    public void Simultaneous_exhaustion_resolves_by_the_same_Player_1_first_tie_break_used_elsewhere()
    {
        // Both sides run out on the same round. DESIGN.md left this case open; the
        // implementation picks the same "Player 1 first" convention already used for
        // simultaneous Reset and same-priority submissions, rather than leaving the match
        // with no winner at all.
        var session = new MatchSession(
            Repeated(CardName.Dummy, 7),
            Repeated(CardName.Dummy, 7),
            new Random(1));

        session.SubmitCard(Side.Player1, CardName.Dummy);
        session.SubmitCard(Side.Player2, CardName.Dummy);

        Assert.Equal(0, session.DeckCountOf(Side.Player1));
        Assert.Equal(0, session.DeckCountOf(Side.Player2));
        Assert.Equal(Side.Player2, session.Winner);
    }

    [Fact]
    public void The_same_seed_produces_the_same_opening_hands()
    {
        var a = new MatchSession(SmallDeck(), SmallDeck(), new Random(99));
        var b = new MatchSession(SmallDeck(), SmallDeck(), new Random(99));

        Assert.Equal(a.HandOf(Side.Player1), b.HandOf(Side.Player1));
        Assert.Equal(a.HandOf(Side.Player2), b.HandOf(Side.Player2));
    }

    private static List<CardName> Repeated(CardName card, int count)
    {
        var cards = new List<CardName>();
        for (int i = 0; i < count; i++)
        {
            cards.Add(card);
        }

        return cards;
    }

    /// <summary>Player 1 wins every round with Rock against Scissors.</summary>
    private static MatchSession PlayRounds(int count)
    {
        var session = new MatchSession(
            Repeated(CardName.Rock, 20),
            Repeated(CardName.Scissors, 20),
            new Random(1));

        for (int round = 0; round < count; round++)
        {
            session.SubmitCard(Side.Player1, CardName.Rock);
            session.SubmitCard(Side.Player2, CardName.Scissors);
        }

        return session;
    }
}
