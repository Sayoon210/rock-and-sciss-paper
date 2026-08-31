using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class MatchSessionTests
{
    private const int SMALL_DECK_SIZE = 12;

    /// <summary>Twelve cards — comfortably more than a mulligan, with enough left over to
    /// keep playing for a while.</summary>
    private static IEnumerable<ECardName> SmallDeck()
    {
        return new[]
        {
            ECardName.Rock, ECardName.Rock, ECardName.Rock, ECardName.Rock,
            ECardName.Paper, ECardName.Paper, ECardName.Paper, ECardName.Paper,
            ECardName.Scissors, ECardName.Scissors, ECardName.Scissors, ECardName.Scissors,
        };
    }

    private static MatchSession NewSession()
    {
        return new MatchSession(SmallDeck(), SmallDeck(), new Random(1));
    }

    [Fact]
    public void A_timed_out_round_plays_a_card_from_each_hand_that_is_still_empty_handed()
    {
        MatchSession session = NewSession();

        RoundReveal? reveal = session.SubmitRandomCardForIdleSides();

        // Neither side acted, so both were played for and the round revealed in one go. One
        // card lighter each: a hand is only refilled once it is empty, not once a round.
        Assert.NotNull(reveal);
        Assert.Equal(MatchSession.HAND_SIZE - 1, session.HandOf(ESide.Player1).Count);
        Assert.Equal(MatchSession.HAND_SIZE - 1, session.HandOf(ESide.Player2).Count);
    }

    [Fact]
    public void A_timed_out_round_leaves_a_card_that_was_already_played_alone()
    {
        MatchSession session = NewSession();
        ECardName played = session.HandOf(ESide.Player1)[0];
        Assert.Null(session.SubmitCard(ESide.Player1, played));

        // Only Player 2 is filled in; Player 1's own card stands, and the round completes.
        RoundReveal? reveal = session.SubmitRandomCardForIdleSides();

        Assert.NotNull(reveal);
        Assert.Equal(played, reveal!.Player1Card);
    }

    [Fact]
    public void A_round_waiting_on_a_choice_is_left_untouched_by_a_submission_timeout()
    {
        // Both cards are in and the round has moved on to asking for a 교체 choice. The
        // submission clock belongs to the phase before this one, so a late firing of it must
        // not play a card into a round that is past taking them.
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.Null(session.SubmitRandomCardForIdleSides());
        Assert.True(session.IsAwaitingChoiceFrom(ESide.Player1));
    }

    [Fact]
    public void Constructor_deals_a_mulligan_hand_to_both_players()
    {
        MatchSession session = NewSession();

        Assert.Equal(MatchSession.HAND_SIZE, session.HandOf(ESide.Player1).Count);
        Assert.Equal(MatchSession.HAND_SIZE, session.HandOf(ESide.Player2).Count);
        Assert.Equal(SMALL_DECK_SIZE - MatchSession.HAND_SIZE, session.DeckCountOf(ESide.Player1));
        Assert.Equal(SMALL_DECK_SIZE - MatchSession.HAND_SIZE, session.DeckCountOf(ESide.Player2));
    }

    [Fact]
    public void A_new_match_starts_at_full_health_with_no_winner()
    {
        MatchSession session = NewSession();

        Assert.Equal(MatchSession.STARTING_HEALTH, session.Player1Health);
        Assert.Equal(MatchSession.STARTING_HEALTH, session.Player2Health);
        Assert.Equal(1, session.RoundNumber);
        Assert.Null(session.Winner);
        Assert.Equal(ERoundPhase.AwaitingSubmissions, session.Phase);
    }

    [Fact]
    public void SubmitCard_returns_null_until_both_sides_have_submitted()
    {
        MatchSession session = NewSession();
        ECardName player1Card = session.HandOf(ESide.Player1)[0];
        ECardName player2Card = session.HandOf(ESide.Player2)[0];

        RoundReveal? afterFirst = session.SubmitCard(ESide.Player1, player1Card);
        Assert.Null(afterFirst);
        Assert.True(session.HasSubmittedCard(ESide.Player1));
        Assert.False(session.HasSubmittedCard(ESide.Player2));

        RoundReveal? afterSecond = session.SubmitCard(ESide.Player2, player2Card);
        Assert.NotNull(afterSecond);
    }

    [Fact]
    public void A_reveal_with_no_choice_owed_carries_the_finished_round()
    {
        MatchSession session = NewSession();

        session.SubmitCard(ESide.Player1, session.HandOf(ESide.Player1)[0]);
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, session.HandOf(ESide.Player2)[0]);

        Assert.False(reveal!.Player1MustChoose);
        Assert.False(reveal.Player2MustChoose);
        Assert.NotNull(reveal.Result);
        Assert.Equal(ERoundPhase.AwaitingSubmissions, session.Phase);
    }

    [Fact]
    public void Resolving_a_round_clears_both_submissions_and_advances_the_round_number()
    {
        MatchSession session = NewSession();

        session.SubmitCard(ESide.Player1, session.HandOf(ESide.Player1)[0]);
        session.SubmitCard(ESide.Player2, session.HandOf(ESide.Player2)[0]);

        Assert.False(session.HasSubmittedCard(ESide.Player1));
        Assert.False(session.HasSubmittedCard(ESide.Player2));
        Assert.Equal(2, session.RoundNumber);
    }

    [Fact]
    public void A_win_damages_only_the_losers_health()
    {
        var session = new MatchSession(
            Repeated(ECardName.Rock, 8),
            Repeated(ECardName.Scissors, 8),
            new Random(1));

        session.SubmitCard(ESide.Player1, ECardName.Rock);
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, ECardName.Scissors);

        Assert.Equal(EWinLossResult.Player1Win, reveal!.Result!.WinLoss);
        Assert.Equal(WinLossRules.ROCK_WIN_DAMAGE, reveal.Result.DamageDealt);
        Assert.Equal(MatchSession.STARTING_HEALTH, session.Player1Health);
        Assert.Equal(MatchSession.STARTING_HEALTH - WinLossRules.ROCK_WIN_DAMAGE, session.Player2Health);
    }

    [Fact]
    public void A_round_with_no_win_loss_does_not_change_health()
    {
        var session = new MatchSession(
            Repeated(ECardName.Blank, 8),
            Repeated(ECardName.Rock, 8),
            new Random(1));

        session.SubmitCard(ESide.Player1, ECardName.Blank);
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.Null(reveal!.Result!.WinLoss);
        Assert.Equal(MatchSession.STARTING_HEALTH, session.Player1Health);
        Assert.Equal(MatchSession.STARTING_HEALTH, session.Player2Health);
    }

    [Fact]
    public void Winner_is_set_once_a_players_health_reaches_zero()
    {
        int roundsToDefeat = MatchSession.STARTING_HEALTH / WinLossRules.ROCK_WIN_DAMAGE;
        MatchSession session = PlayRounds(roundsToDefeat);

        Assert.Equal(0, session.Player2Health);
        Assert.Equal(ESide.Player1, session.Winner);
    }

    [Fact]
    public void SubmitCard_throws_once_the_match_is_over()
    {
        int roundsToDefeat = MatchSession.STARTING_HEALTH / WinLossRules.ROCK_WIN_DAMAGE;
        MatchSession session = PlayRounds(roundsToDefeat);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(ESide.Player1, session.HandOf(ESide.Player1)[0]));
    }

    [Fact]
    public void SubmitCard_throws_when_the_same_side_submits_twice()
    {
        MatchSession session = NewSession();
        session.SubmitCard(ESide.Player1, session.HandOf(ESide.Player1)[0]);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(ESide.Player1, session.HandOf(ESide.Player1)[0]));
    }

    [Fact]
    public void SubmitCard_throws_for_a_card_the_player_does_not_hold()
    {
        MatchSession session = NewSession();

        Assert.Throws<ArgumentException>(() => session.SubmitCard(ESide.Player1, ECardName.Joker));
    }

    /// <summary>A deck whose mulligan always holds at least one of each card, whatever the
    /// shuffle does — no seed hunting.
    ///
    /// One short of the mulligan for each kind is the largest deck that can promise that: with
    /// only HAND_SIZE - 1 of a kind, a hand of HAND_SIZE cannot be made of
    /// that kind alone. Counted off the constant rather than written out, because the promise
    /// is arithmetic about the hand size and quietly stops holding when it changes — which is
    /// what a fixed "five of each" did when the mulligan went from six cards to three.
    ///
    /// The deck this leaves behind is short by design. These sessions exist to exercise the
    /// choice phase, not to play out many rounds.</summary>
    private static List<ECardName> MulliganHoldsBoth(ECardName first, ECardName second)
    {
        var cards = new List<ECardName>();
        for (int i = 0; i < MatchSession.HAND_SIZE - 1; i++)
        {
            cards.Add(first);
            cards.Add(second);
        }

        return cards;
    }

    private static MatchSession SwapVersusPlainSession()
    {
        return new MatchSession(
            MulliganHoldsBoth(ECardName.Swap, ECardName.Blank),
            Repeated(ECardName.Rock, 20),
            new Random(1));
    }

    [Fact]
    public void A_choice_card_leaves_the_round_awaiting_a_choice()
    {
        MatchSession session = SwapVersusPlainSession();

        session.SubmitCard(ESide.Player1, ECardName.Swap);
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.True(reveal!.Player1MustChoose);
        Assert.False(reveal.Player2MustChoose);
        Assert.Null(reveal.Result);
        Assert.Equal(ERoundPhase.AwaitingChoices, session.Phase);
        Assert.True(session.IsAwaitingChoiceFrom(ESide.Player1));
        Assert.Equal(ECardName.Swap, session.CardAwaitingChoiceFrom(ESide.Player1));
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
        var deck = new List<ECardName> { ECardName.Swap };
        for (int i = 0; i < MatchSession.HAND_SIZE - 1; i++)
        {
            deck.Add(ECardName.Blank);
        }

        var session = new MatchSession(deck, Repeated(ECardName.Rock, 20), new Random(1));

        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.Equal(ERoundPhase.AwaitingChoices, session.Phase);
        Assert.DoesNotContain(ECardName.Swap, session.HandOf(ESide.Player1));
    }

    [Fact]
    public void Settling_the_last_owed_choice_finishes_the_round()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        RoundResult? result = session.SubmitChoice(
            ESide.Player1, CardChoice.Swapping(new[] { ECardName.Blank }));

        Assert.NotNull(result);
        Assert.Equal(1, result!.Player1SwappedCardCount);
        Assert.Equal(ERoundPhase.AwaitingSubmissions, session.Phase);
        Assert.Equal(2, session.RoundNumber);
        Assert.False(session.HasSubmittedCard(ESide.Player1));
    }

    [Fact]
    public void SubmitCard_is_rejected_while_the_round_is_awaiting_choices()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitCard(ESide.Player2, ECardName.Rock));
    }

    [Fact]
    public void SubmitChoice_is_rejected_from_a_side_that_was_not_asked()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitChoice(ESide.Player2, CardChoice.Swapping(Array.Empty<ECardName>())));
    }

    [Fact]
    public void SubmitChoice_is_rejected_when_that_side_already_chose()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);
        session.SubmitChoice(ESide.Player1, CardChoice.Swapping(Array.Empty<ECardName>()));

        Assert.Throws<InvalidOperationException>(
            () => session.SubmitChoice(ESide.Player1, CardChoice.Swapping(Array.Empty<ECardName>())));
    }

    [Fact]
    public void An_illegal_choice_leaves_the_side_still_awaited_and_the_round_unresolved()
    {
        // The choice-phase counterpart of "a rejected card must leave the round exactly as
        // it was": a client that sends nonsense has to be able to simply try again.
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        Assert.Throws<ArgumentException>(
            () => session.SubmitChoice(ESide.Player1, CardChoice.Swapping(new[] { ECardName.Joker })));

        Assert.True(session.IsAwaitingChoiceFrom(ESide.Player1));
        Assert.Equal(ERoundPhase.AwaitingChoices, session.Phase);
        Assert.Equal(1, session.RoundNumber);

        RoundResult? result = session.SubmitChoice(
            ESide.Player1, CardChoice.Swapping(new[] { ECardName.Blank }));
        Assert.NotNull(result);
    }

    [Fact]
    public void DeclineChoice_finishes_the_round_without_running_the_effect()
    {
        MatchSession session = SwapVersusPlainSession();
        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Rock);

        RoundResult? result = session.DeclineChoice(ESide.Player1);

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

        Assert.Null(session.DeclineChoice(ESide.Player1));
        Assert.Equal(1, session.RoundNumber);
        Assert.Equal(ERoundPhase.AwaitingSubmissions, session.Phase);
    }

    [Theory]
    [InlineData(ECardName.Swap)]
    [InlineData(ECardName.Transform)]
    public void A_Joker_leaves_the_blocked_player_with_nothing_to_choose(ECardName choiceCard)
    {
        // The whole point of asking after the reveal: a card the Joker destroyed never
        // prompts, instead of prompting and then throwing the answer away.
        var session = new MatchSession(
            MulliganHoldsBoth(choiceCard, ECardName.Blank),
            Repeated(ECardName.Joker, 20),
            new Random(1));

        session.SubmitCard(ESide.Player1, choiceCard);
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, ECardName.Joker);

        Assert.False(reveal!.Player1MustChoose);
        Assert.NotNull(reveal.Result);
        Assert.Equal(ERoundPhase.AwaitingSubmissions, session.Phase);
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
            MulliganHoldsBoth(ECardName.Reset, ECardName.Rock),
            MulliganHoldsBoth(ECardName.Transform, ECardName.Blank),
            new Random(3));

        session.SubmitCard(ESide.Player1, ECardName.Reset);
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, ECardName.Transform);
        Assert.True(reveal!.Player2MustChoose);

        // The first card 변화 can actually work on, not simply the first card: 변화 only takes
        // a 일반카드 or 공백카드, and which of those the post-리셋 hand starts with is up to the
        // shuffle. Reaching for offered[0] happened to work at a six-card hand and stopped
        // working at four — the test never meant to care which card it got.
        IReadOnlyList<ECardName> offered = session.HandOf(ESide.Player2);
        ECardName target = offered.First(
            card => card.GetCardType() == ECardType.Normal || card.GetCardType() == ECardType.Blank);

        RoundResult? result = session.SubmitChoice(
            ESide.Player2, CardChoice.Transforming(target, ECardName.Paper));

        Assert.NotNull(result);
        Assert.True(result!.Player2TransformApplied);
        Assert.Contains(ECardName.Paper, result.Player2Hand);
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
            MulliganHoldsBoth(ECardName.Swap, ECardName.Blank),
            MulliganHoldsBoth(ECardName.Swap, ECardName.Rock),
            new Random(5));

        session.SubmitCard(ESide.Player1, ECardName.Swap);
        session.SubmitCard(ESide.Player2, ECardName.Swap);

        CardChoice player1Choice = CardChoice.Swapping(new[] { ECardName.Blank });
        CardChoice player2Choice = CardChoice.Swapping(new[] { ECardName.Rock });

        RoundResult? result;
        if (chooseForPlayer1First)
        {
            Assert.Null(session.SubmitChoice(ESide.Player1, player1Choice));
            result = session.SubmitChoice(ESide.Player2, player2Choice);
        }
        else
        {
            Assert.Null(session.SubmitChoice(ESide.Player2, player2Choice));
            result = session.SubmitChoice(ESide.Player1, player1Choice);
        }

        return result!;
    }

    [Fact]
    public void The_same_seed_produces_the_same_opening_hands()
    {
        var a = new MatchSession(SmallDeck(), SmallDeck(), new Random(99));
        var b = new MatchSession(SmallDeck(), SmallDeck(), new Random(99));

        Assert.Equal(a.HandOf(ESide.Player1), b.HandOf(ESide.Player1));
        Assert.Equal(a.HandOf(ESide.Player2), b.HandOf(ESide.Player2));
    }

    private static List<ECardName> Repeated(ECardName card, int count)
    {
        var cards = new List<ECardName>();
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
            Repeated(ECardName.Rock, 20),
            Repeated(ECardName.Scissors, 20),
            new Random(1));

        for (int round = 0; round < count; round++)
        {
            session.SubmitCard(ESide.Player1, ECardName.Rock);
            session.SubmitCard(ESide.Player2, ECardName.Scissors);
        }

        return session;
    }
}
