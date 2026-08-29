using System.Collections.Generic;
using RockAndScissPaper.GameLogic;
using Xunit;

namespace RockAndScissPaper.Tests;

/// <summary>MatchLog derives each round's damage from the health the previous round left
/// behind rather than being told it, so the arithmetic is what these cover — including the
/// cases where the caller feeds it something it should refuse to turn into healing.</summary>
public class MatchLogTests
{
    [Fact]
    public void FirstRoundDamageIsMeasuredAgainstStartingHealth()
    {
        MatchLog log = new MatchLog(10);

        log.RecordRound(1, ECardName.Rock, ECardName.Scissors, EMatchLogOutcome.MyWin, 10, 8);

        MatchLogEntry entry = Assert.Single(log.Entries);
        Assert.Equal(0, entry.MyDamageTaken);
        Assert.Equal(2, entry.OpponentDamageTaken);
        Assert.Equal(10, entry.MyHealthAfter);
        Assert.Equal(8, entry.OpponentHealthAfter);
    }

    [Fact]
    public void LaterRoundDamageIsMeasuredAgainstThePreviousRound()
    {
        MatchLog log = new MatchLog(10);

        log.RecordRound(1, ECardName.Rock, ECardName.Scissors, EMatchLogOutcome.MyWin, 10, 8);
        log.RecordRound(2, ECardName.Rock, ECardName.Paper, EMatchLogOutcome.OpponentWin, 9, 8);

        MatchLogEntry second = log.Entries[1];
        Assert.Equal(1, second.MyDamageTaken);
        Assert.Equal(0, second.OpponentDamageTaken);
    }

    [Fact]
    public void ADrawTakesNothingOffEitherSide()
    {
        MatchLog log = new MatchLog(10);

        log.RecordRound(1, ECardName.Rock, ECardName.Rock, EMatchLogOutcome.Draw, 10, 10);

        MatchLogEntry entry = Assert.Single(log.Entries);
        Assert.Equal(0, entry.MyDamageTaken);
        Assert.Equal(0, entry.OpponentDamageTaken);
        Assert.Equal(EMatchLogOutcome.Draw, entry.Outcome);
    }

    /// <summary>Health only ever falls. A reading that goes back up is a caller mistake, and
    /// logging it as negative damage would read as healing — a rule this game does not have.</summary>
    [Fact]
    public void HealthGoingUpIsRecordedAsNoDamageRatherThanHealing()
    {
        MatchLog log = new MatchLog(10);

        log.RecordRound(1, ECardName.Rock, ECardName.Scissors, EMatchLogOutcome.MyWin, 10, 8);
        log.RecordRound(2, ECardName.Rock, ECardName.Scissors, EMatchLogOutcome.MyWin, 10, 9);

        Assert.Equal(0, log.Entries[1].OpponentDamageTaken);
    }

    [Fact]
    public void MatchEndIsRecordedSeparatelyFromRounds()
    {
        MatchLog log = new MatchLog(10);
        Assert.Null(log.DidIWin);

        log.RecordRound(1, ECardName.Rock, ECardName.Scissors, EMatchLogOutcome.MyWin, 10, 8);
        Assert.Null(log.DidIWin);

        log.RecordMatchEnd(true);
        Assert.True(log.DidIWin);
    }

    /// <summary>A rematch reuses the one log GameState holds, so the reset has to put the
    /// damage baseline back too — not just empty the list.</summary>
    [Fact]
    public void ResetClearsEntriesAndTheDamageBaseline()
    {
        MatchLog log = new MatchLog(10);
        log.RecordRound(1, ECardName.Rock, ECardName.Scissors, EMatchLogOutcome.MyWin, 10, 3);
        log.RecordMatchEnd(true);

        log.Reset(10);

        Assert.Empty(log.Entries);
        Assert.Null(log.DidIWin);

        log.RecordRound(1, ECardName.Paper, ECardName.Rock, EMatchLogOutcome.MyWin, 10, 9);
        Assert.Equal(1, Assert.Single(log.Entries).OpponentDamageTaken);
    }

    /// <summary>Blank, 조커 and ability rounds produce no verdict at all. They are out of the
    /// deck now, but the rules still resolve them, so the log keeps them distinct from a draw.</summary>
    [Fact]
    public void NoContestIsDistinctFromADraw()
    {
        MatchLog log = new MatchLog(10);

        log.RecordRound(1, ECardName.Joker, ECardName.Rock, EMatchLogOutcome.NoContest, 10, 10);

        Assert.Equal(EMatchLogOutcome.NoContest, Assert.Single(log.Entries).Outcome);
    }
}
