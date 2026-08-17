using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class MatchSessionLogTests
{
    [Fact]
    public void A_session_with_no_log_stays_silent()
    {
        // The default null log is what every other test relies on to keep output clean.
        var session = new MatchSession(
            Repeated(CardName.Rock, 8),
            Repeated(CardName.Scissors, 8),
            new Random(1));

        session.SubmitCard(Side.Player1, CardName.Rock);
        session.SubmitCard(Side.Player2, CardName.Scissors);

        Assert.Equal(1, session.Player1Score);
    }

    [Fact]
    public void The_log_traces_the_mulligan_every_submission_and_the_resolution()
    {
        var lines = new List<string>();
        var session = new MatchSession(
            Repeated(CardName.Rock, 8),
            Repeated(CardName.Scissors, 8),
            new Random(1),
            lines.Add);

        session.SubmitCard(Side.Player1, CardName.Rock);
        session.SubmitCard(Side.Player2, CardName.Scissors);

        Assert.Equal(2, lines.FindAll(line => line.StartsWith("[deal]")).Count);
        Assert.Equal(2, lines.FindAll(line => line.StartsWith("[submit]")).Count);
        Assert.Single(lines.FindAll(line => line.StartsWith("[resolve]")));
        Assert.Single(lines.FindAll(line => line.StartsWith("[hands]")));
        Assert.Contains(lines, line => line.Contains("Player1Win") && line.Contains("score 1-0"));
    }

    [Fact]
    public void The_log_announces_the_match_winner()
    {
        var lines = new List<string>();
        var session = new MatchSession(
            Repeated(CardName.Rock, 20),
            Repeated(CardName.Scissors, 20),
            new Random(1),
            lines.Add);

        for (int round = 0; round < MatchSession.WINS_NEEDED_FOR_MATCH; round++)
        {
            session.SubmitCard(Side.Player1, CardName.Rock);
            session.SubmitCard(Side.Player2, CardName.Scissors);
        }

        Assert.Contains(lines, line => line.StartsWith("[match]") && line.Contains($"Player1 wins {MatchSession.WINS_NEEDED_FOR_MATCH}-0"));
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
}
