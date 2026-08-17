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
    }

    [Fact]
    public void SubmitCard_returns_null_until_both_sides_have_submitted()
    {
        MatchSession session = NewSession();
        CardName player1Card = session.HandOf(Side.Player1)[0];
        CardName player2Card = session.HandOf(Side.Player2)[0];

        RoundResult? afterFirst = session.SubmitCard(Side.Player1, player1Card);
        Assert.Null(afterFirst);
        Assert.True(session.HasSubmittedCard(Side.Player1));
        Assert.False(session.HasSubmittedCard(Side.Player2));

        RoundResult? afterSecond = session.SubmitCard(Side.Player2, player2Card);
        Assert.NotNull(afterSecond);
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
        RoundResult? result = session.SubmitCard(Side.Player2, CardName.Scissors);

        Assert.Equal(WinLossResult.Player1Win, result!.WinLoss);
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
        RoundResult? result = session.SubmitCard(Side.Player2, CardName.Rock);

        Assert.Null(result!.WinLoss);
        Assert.Equal(0, session.Player1Score);
        Assert.Equal(0, session.Player2Score);
    }

    [Fact]
    public void Winner_is_set_once_a_player_reaches_five_wins()
    {
        MatchSession session = PlayRounds(5);

        Assert.Equal(5, session.Player1Score);
        Assert.Equal(Side.Player1, session.Winner);
    }

    [Fact]
    public void SubmitCard_throws_once_the_match_is_over()
    {
        MatchSession session = PlayRounds(5);

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

    /// <summary>Every card is either Transform or Rock, so a six-card mulligan always
    /// holds at least one of each.</summary>
    private static List<CardName> TransformAndRockDeck()
    {
        var cards = new List<CardName>();
        for (int i = 0; i < 5; i++)
        {
            cards.Add(CardName.Transform);
            cards.Add(CardName.Rock);
        }

        return cards;
    }

    [Fact]
    public void SubmitCard_accepts_a_play_that_carries_a_choice()
    {
        var session = new MatchSession(TransformAndRockDeck(), Repeated(CardName.Rock, 8), new Random(1));

        session.SubmitCard(Side.Player1, CardPlay.Transforming(CardName.Rock, CardName.Paper));

        Assert.True(session.HasSubmittedCard(Side.Player1));
    }

    [Fact]
    public void SubmitCard_rejects_a_bad_choice_without_consuming_the_submission()
    {
        // A rejected play must leave the round untouched, or the side could never submit
        // again this round and the match would wedge.
        var session = new MatchSession(TransformAndRockDeck(), Repeated(CardName.Rock, 8), new Random(1));

        Assert.Throws<ArgumentException>(
            () => session.SubmitCard(Side.Player1, CardPlay.Transforming(CardName.Joker, CardName.Rock)));

        Assert.False(session.HasSubmittedCard(Side.Player1));
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
