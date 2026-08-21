namespace RockAndScissPaper.GameLogic;

/// <summary>The three cards that actually decide a round.</summary>
public enum ENormalCard
{
    Rock,
    Paper,
    Scissors,
}

/// <summary>Who won a round, or whether it was a draw.</summary>
public enum EWinLossResult
{
    Player1Win,
    Player2Win,
    Draw,
}

public static class WinLossRules
{
    /// <summary>
    /// Scissors beats Paper, Rock beats Scissors, Paper beats Rock. Same card draws.
    /// Only normal cards reach here — abilities, blanks and Jokers never produce a win or loss.
    /// </summary>
    public static EWinLossResult Judge(ENormalCard player1, ENormalCard player2)
    {
        if (player1 == player2)
        {
            return EWinLossResult.Draw;
        }

        if (Beats(player1, player2))
        {
            return EWinLossResult.Player1Win;
        }

        return EWinLossResult.Player2Win;
    }

    private static bool Beats(ENormalCard attacker, ENormalCard defender)
    {
        if (attacker == ENormalCard.Scissors && defender == ENormalCard.Paper)
        {
            return true;
        }

        if (attacker == ENormalCard.Rock && defender == ENormalCard.Scissors)
        {
            return true;
        }

        if (attacker == ENormalCard.Paper && defender == ENormalCard.Rock)
        {
            return true;
        }

        return false;
    }
}
