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
    // Health lost by the losing side when this card wins a round. DESIGN.md, "체력과 승리
    // 효과" — 가위 and 보 also carry a status effect (item lock, hand reveal) that this
    // number does not carry; that part is not implemented yet.
    public const int ROCK_WIN_DAMAGE = 2;
    public const int PAPER_WIN_DAMAGE = 1;
    public const int SCISSORS_WIN_DAMAGE = 1;

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

    /// <summary>How much health the losing side loses when winningCard is what won.</summary>
    public static int DamageOf(ENormalCard winningCard)
    {
        switch (winningCard)
        {
            case ENormalCard.Rock:
                return ROCK_WIN_DAMAGE;

            case ENormalCard.Paper:
                return PAPER_WIN_DAMAGE;

            case ENormalCard.Scissors:
                return SCISSORS_WIN_DAMAGE;

            default:
                throw new ArgumentOutOfRangeException(nameof(winningCard), winningCard, null);
        }
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
