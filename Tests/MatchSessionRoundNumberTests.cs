using System;
using System.Collections.Generic;
using RockAndScissPaper.GameLogic;
using Xunit;

namespace RockAndScissPaper.Tests;

/// <summary>MatchSession advances RoundNumber as the last step of recording a resolution, so
/// the number a resolved round is broadcast with already names the NEXT round. GameState's
/// match log subtracts one to label the round that actually finished, and this is what pins
/// that offset down — if the increment ever moves, the log starts labelling rounds wrongly
/// with nothing else failing.</summary>
public class MatchSessionRoundNumberTests
{
    private static MatchSession NewRockPaperScissorsSession()
    {
        // The deck DeckAssembler now builds: 3 each of Rock/Paper/Scissors, nothing else.
        List<ECardName> deck = new List<ECardName>();
        for (int i = 0; i < 3; i++)
        {
            deck.Add(ECardName.Rock);
            deck.Add(ECardName.Paper);
            deck.Add(ECardName.Scissors);
        }

        return new MatchSession(new List<ECardName>(deck), new List<ECardName>(deck), new Random(1));
    }

    [Fact]
    public void RoundNumberStartsAtOne()
    {
        Assert.Equal(1, NewRockPaperScissorsSession().RoundNumber);
    }

    [Fact]
    public void RoundNumberHasAlreadyAdvancedByTheTimeAResolvedRoundIsReadable()
    {
        MatchSession session = NewRockPaperScissorsSession();

        ECardName player1Card = session.HandOf(ESide.Player1)[0];
        ECardName player2Card = session.HandOf(ESide.Player2)[0];

        Assert.Null(session.SubmitCard(ESide.Player1, player1Card));
        RoundReveal? reveal = session.SubmitCard(ESide.Player2, player2Card);

        // Every card in this deck is normal, so nobody owes a choice and the reveal already
        // carries the finished result — the reason taking the abilities out matters here.
        Assert.NotNull(reveal);
        Assert.NotNull(reveal!.Result);

        // The round that just finished was 1; the counter now reads 2.
        Assert.Equal(2, session.RoundNumber);
    }

    /// <summary>With only Rock/Paper/Scissors in play every round produces a real verdict —
    /// which is the whole reason blank, 조커 and the ability cards left the deck.</summary>
    [Fact]
    public void EveryRoundOfARockPaperScissorsDeckProducesAVerdict()
    {
        MatchSession session = NewRockPaperScissorsSession();

        for (int round = 0; round < 5 && session.Winner == null; round++)
        {
            ECardName player1Card = session.HandOf(ESide.Player1)[0];
            ECardName player2Card = session.HandOf(ESide.Player2)[0];

            session.SubmitCard(ESide.Player1, player1Card);
            RoundReveal? reveal = session.SubmitCard(ESide.Player2, player2Card);

            Assert.NotNull(reveal);
            Assert.NotNull(reveal!.Result);
            Assert.NotNull(reveal.Result!.WinLoss);
        }
    }
}
