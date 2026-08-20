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
    /// Only normal cards reach here — abilities, blanks and Jokers never produce a win or loss.
    /// </summary>
    public static WinLossResult Judge(NormalCard player1, NormalCard player2)
    {
        if (player1 == player2)
        {
            return WinLossResult.Draw;
        }

        if (Beats(player1, player2))
        {
            return WinLossResult.Player1Win;
        }

        return WinLossResult.Player2Win;
    }

    private static bool Beats(NormalCard attacker, NormalCard defender)
    {
        if (attacker == NormalCard.Scissors && defender == NormalCard.Paper)
        {
            return true;
        }

        if (attacker == NormalCard.Rock && defender == NormalCard.Scissors)
        {
            return true;
        }

        if (attacker == NormalCard.Paper && defender == NormalCard.Rock)
        {
            return true;
        }

        return false;
    }
}
