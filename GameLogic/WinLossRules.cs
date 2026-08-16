namespace RockAndScissPaper.GameLogic;

/// <summary>The three cards that actually decide a round.</summary>
public enum NormalCard
{
    Rock,
    Paper,
    Scissors,
}

/// <summary>Who won a round, or whether it was a draw.</summary>
public enum WinLossResult
{
    Player1Win,
    Player2Win,
    Draw,
}

public static class WinLossRules
{
    /// <summary>
    /// Scissors beats Paper, Rock beats Scissors, Paper beats Rock. Same card draws.
    /// Only normal cards reach here — specials, dummies and Jokers never produce a win or loss.
    /// </summary>
    public static WinLossResult Judge(NormalCard player1, NormalCard player2)
    {
        if (player1 == player2)
        {
            return WinLossResult.Draw;
        }

        return Beats(player1, player2) ? WinLossResult.Player1Win : WinLossResult.Player2Win;
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
