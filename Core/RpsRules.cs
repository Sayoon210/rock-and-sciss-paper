namespace RockAndScissPaper.Core;

/// <summary>The three cards that actually decide a round.</summary>
public enum NormalCard
{
    Rock,
    Paper,
    Scissors,
}

public enum RoundOutcome
{
    Player1Win,
    Player2Win,
    Draw,
}

public static class RpsRules
{
    /// <summary>
    /// Scissors beats Paper, Rock beats Scissors, Paper beats Rock. Same card draws.
    /// Only normal cards reach here — specials and Jokers never produce an outcome.
    /// </summary>
    public static RoundOutcome Compare(NormalCard player1, NormalCard player2)
    {
        if (player1 == player2)
        {
            return RoundOutcome.Draw;
        }

        return Beats(player1, player2) ? RoundOutcome.Player1Win : RoundOutcome.Player2Win;
    }

    private static bool Beats(NormalCard attacker, NormalCard defender) =>
        (attacker, defender) switch
        {
            (NormalCard.Scissors, NormalCard.Paper) => true,
            (NormalCard.Rock, NormalCard.Scissors) => true,
            (NormalCard.Paper, NormalCard.Rock) => true,
            _ => false,
        };
}
