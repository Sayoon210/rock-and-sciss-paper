namespace RockAndScissPaper.GameLogic;

/// <summary>Resolves one round: figures out each card's fate and any win/loss, applies
/// that to each player's DeckAndHand (vanish/return-to-bottom, then draw), and returns
/// what happened. Only handles Normal/Dummy/Joker so far — Special cards need
/// ICardEffect, which does not exist yet.</summary>
public static class RoundResolver
{
    public static RoundResult Resolve(
        CardName player1Card,
        CardName player2Card,
        DeckAndHand player1,
        DeckAndHand player2)
    {
        if (player1Card.GetCardType() == CardType.Special || player2Card.GetCardType() == CardType.Special)
        {
            throw new NotImplementedException("Special card resolution is not implemented yet.");
        }

        CardFate player1Fate;
        CardFate player2Fate;
        WinLossResult? winLoss;

        if (player1Card == CardName.Joker || player2Card == CardName.Joker)
        {
            // Joker vanishes itself and destroys whatever the other side played,
            // including another Joker. No win/loss when a Joker is involved.
            player1Fate = CardFate.Vanished;
            player2Fate = CardFate.Vanished;
            winLoss = null;
        }
        else
        {
            if (player1Card.GetCardType() == CardType.Dummy)
            {
                player1Fate = CardFate.Vanished;
            }
            else
            {
                player1Fate = CardFate.ReturnedToDeckBottom;
            }

            if (player2Card.GetCardType() == CardType.Dummy)
            {
                player2Fate = CardFate.Vanished;
            }
            else
            {
                player2Fate = CardFate.ReturnedToDeckBottom;
            }

            if (player1Card.IsNormal() && player2Card.IsNormal())
            {
                winLoss = WinLossRules.Judge(player1Card.ToNormalCard(), player2Card.ToNormalCard());
            }
            else
            {
                winLoss = null;
            }
        }

        ApplyFate(player1, player1Card, player1Fate);
        ApplyFate(player2, player2Card, player2Fate);

        CardName player1Drew = player1.Draw();
        CardName player2Drew = player2.Draw();

        return new RoundResult(
            player1Card,
            player2Card,
            player1Fate,
            player2Fate,
            winLoss,
            player1Drew,
            player2Drew);
    }

    private static void ApplyFate(DeckAndHand player, CardName card, CardFate fate)
    {
        if (fate == CardFate.Vanished)
        {
            player.Vanish(card);
        }
        else
        {
            player.ReturnToDeckBottom(card);
        }
    }
}
