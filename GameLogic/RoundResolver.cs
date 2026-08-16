namespace RockAndScissPaper.GameLogic;

/// <summary>Resolves one round: figures out each card's fate and any win/loss, applies
/// that to each player's DeckAndHand (vanish/return-to-bottom — this also removes the
/// played card from the hand before any effect runs, so a card like Reset never sees
/// itself as still "in hand"), runs Special effects in priority order, then draws for
/// both sides. Transform, Foresight, and Swap have no ICardEffect yet — resolving a round
/// that actually needs to run one of them throws.</summary>
public static class RoundResolver
{
    private static readonly IReadOnlyDictionary<CardName, ICardEffect> SpecialEffects = new Dictionary<CardName, ICardEffect>
    {
        { CardName.Reset, new ResetEffect() },
        { CardName.Refill, new RefillEffect() },
        { CardName.Draw, new DrawEffect() },
    };

    public static RoundResult Resolve(
        CardName player1Card,
        CardName player2Card,
        DeckAndHand player1,
        DeckAndHand player2,
        Random rng)
    {
        CardFate player1Fate;
        CardFate player2Fate;
        WinLossResult? winLoss;
        bool runEffects;

        if (player1Card == CardName.Joker || player2Card == CardName.Joker)
        {
            // Joker vanishes itself and destroys whatever the other side played,
            // blocking its effect entirely — including another Joker's (no-op) effect.
            player1Fate = CardFate.Vanished;
            player2Fate = CardFate.Vanished;
            winLoss = null;
            runEffects = false;
        }
        else
        {
            player1Fate = DefaultFate(player1Card);
            player2Fate = DefaultFate(player2Card);

            if (player1Card.IsNormal() && player2Card.IsNormal())
            {
                winLoss = WinLossRules.Judge(player1Card.ToNormalCard(), player2Card.ToNormalCard());
            }
            else
            {
                winLoss = null;
            }

            runEffects = true;
        }

        if (runEffects)
        {
            // Check before mutating anything: a throw halfway through would leave both
            // players' DeckAndHand in a half-applied round.
            RequireEffectExists(player1Card);
            RequireEffectExists(player2Card);
        }

        // Remove each played card from its hand before any effect runs, so a special
        // card's own effect never sees itself as still part of "my hand".
        ApplyFate(player1, player1Card, player1Fate);
        ApplyFate(player2, player2Card, player2Fate);

        if (runEffects)
        {
            RunSpecialEffectsInPriorityOrder(player1Card, player1, player2Card, player2, rng);
        }

        player1.Draw();
        player2.Draw();

        return new RoundResult(
            player1Card,
            player2Card,
            player1Fate,
            player2Fate,
            winLoss,
            player1.Hand.Cards,
            player2.Hand.Cards,
            player1.Deck.Count,
            player2.Deck.Count);
    }

    private static CardFate DefaultFate(CardName card)
    {
        if (card.GetCardType() == CardType.Dummy)
        {
            return CardFate.Vanished;
        }

        if (card.GetCardType() == CardType.Special)
        {
            return CardFate.Vanished;
        }

        return CardFate.ReturnedToDeckBottom;
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

    private static void RunSpecialEffectsInPriorityOrder(
        CardName player1Card,
        DeckAndHand player1,
        CardName player2Card,
        DeckAndHand player2,
        Random rng)
    {
        // Reset outranks every other special. If both players played Reset, it runs
        // twice, Player 1's card first, per DESIGN.md.
        if (player1Card == CardName.Reset)
        {
            RunEffect(player1Card, player1, player2, rng);
        }

        if (player2Card == CardName.Reset)
        {
            RunEffect(player2Card, player2, player1, rng);
        }

        if (player1Card.GetCardType() == CardType.Special && player1Card != CardName.Reset)
        {
            RunEffect(player1Card, player1, player2, rng);
        }

        if (player2Card.GetCardType() == CardType.Special && player2Card != CardName.Reset)
        {
            RunEffect(player2Card, player2, player1, rng);
        }
    }

    private static void RequireEffectExists(CardName card)
    {
        if (card.GetCardType() != CardType.Special)
        {
            return;
        }

        if (!SpecialEffects.ContainsKey(card))
        {
            throw new NotImplementedException($"{card} has no ICardEffect implementation yet.");
        }
    }

    private static void RunEffect(CardName card, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        SpecialEffects[card].Apply(self, opponent, rng);
    }
}
