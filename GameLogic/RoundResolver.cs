namespace RockAndScissPaper.GameLogic;

/// <summary>Resolves one round: figures out each card's fate and any win/loss, applies
/// that to each player's DeckAndHand (vanish/return-to-bottom — this also removes the
/// played card from the hand before any effect runs, so a card like Reset never sees
/// itself as still "in hand"), runs Special effects in priority order, then draws for
/// both sides.</summary>
public static class RoundResolver
{
    private static readonly IReadOnlyDictionary<CardName, ICardEffect> SpecialEffects = new Dictionary<CardName, ICardEffect>
    {
        { CardName.Reset, new ResetEffect() },
        { CardName.Swap, new SwapEffect() },
        { CardName.Transform, new TransformEffect() },
        { CardName.Refill, new RefillEffect() },
        { CardName.Draw, new DrawEffect() },
    };

    /// <summary>Rejects a play the rules can't carry out, without changing anything.
    /// MatchSession runs this before it records a submission, so a bad play from a client
    /// can't wedge a round halfway through.</summary>
    public static void ValidatePlay(CardPlay play, DeckAndHand player)
    {
        if (!player.Hand.Contains(play.Card))
        {
            throw new ArgumentException($"{play.Card} is not in hand.", nameof(play));
        }

        if (play.Card.GetCardType() != CardType.Special)
        {
            return;
        }

        SpecialEffects[play.Card].Validate(play, player);
    }

    public static RoundResult Resolve(
        CardPlay player1Play,
        CardPlay player2Play,
        DeckAndHand player1,
        DeckAndHand player2,
        Random rng)
    {
        CardName player1Card = player1Play.Card;
        CardName player2Card = player2Play.Card;

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

        // Remove each played card from its hand before any effect runs, so a special
        // card's own effect never sees itself as still part of "my hand".
        ApplyFate(player1, player1Card, player1Fate);
        ApplyFate(player2, player2Card, player2Fate);

        if (runEffects)
        {
            RunSpecialEffectsInPriorityOrder(player1Play, player1, player2Play, player2, rng);
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
        CardPlay player1Play,
        DeckAndHand player1,
        CardPlay player2Play,
        DeckAndHand player2,
        Random rng)
    {
        // Reset outranks every other special. If both players played Reset, it runs
        // twice, Player 1's card first, per DESIGN.md.
        if (player1Play.Card == CardName.Reset)
        {
            RunEffect(player1Play, player1, player2, rng);
        }

        if (player2Play.Card == CardName.Reset)
        {
            RunEffect(player2Play, player2, player1, rng);
        }

        if (player1Play.Card.GetCardType() == CardType.Special && player1Play.Card != CardName.Reset)
        {
            RunEffect(player1Play, player1, player2, rng);
        }

        if (player2Play.Card.GetCardType() == CardType.Special && player2Play.Card != CardName.Reset)
        {
            RunEffect(player2Play, player2, player1, rng);
        }
    }

    private static void RunEffect(CardPlay play, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        SpecialEffects[play.Card].Apply(play, self, opponent, rng);
    }
}
