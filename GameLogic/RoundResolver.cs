namespace RockAndScissPaper.GameLogic;

/// <summary>Resolves a round in two phases, because 교체 and 변화 are chosen after both
/// cards are revealed.
///
/// Reveal: work out each card's fate and any win/loss, remove both played cards from their
/// hands, then run every effect that needs no choice — 리셋 included. Finish: apply the
/// choices that came back, then draw for both sides.
///
/// The split point is deliberate. 리셋 is the only effect that touches the opponent, and it
/// needs no choice, so running all choiceless effects before anyone is prompted means
/// nothing can change a player's hand between the moment they are shown it and the moment
/// their choice is applied. The hand offered for a choice is the hand the effect mutates —
/// there is no window in which a validated choice can go stale.</summary>
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

    /// <summary>Rejects a card the player does not hold, without changing anything.
    /// MatchSession runs this before it records a submission.</summary>
    public static void ValidateSubmission(CardName card, DeckAndHand player)
    {
        if (!player.Hand.Contains(card))
        {
            throw new ArgumentException($"{card} is not in hand.", nameof(card));
        }
    }

    /// <summary>Rejects a choice the effect cannot carry out, without changing anything.
    /// Runs against the live hand, which by this point is exactly what Apply will see.</summary>
    public static void ValidateChoice(CardName card, CardChoice choice, DeckAndHand player)
    {
        SpecialEffects[card].Validate(choice, player);
    }

    /// <summary>Whether playing this card obliges the player to choose something. Asked of
    /// the effect rather than tested against a list of card names, so a new special card
    /// does not need this file edited.</summary>
    public static bool RequiresChoice(CardName card)
    {
        if (card.GetCardType() != CardType.Special)
        {
            return false;
        }

        return SpecialEffects[card].RequiresChoice;
    }

    /// <summary>Phase one: make both cards public, apply their fates, and run everything
    /// that needs no choice. The returned round records who still owes one.</summary>
    public static RoundInProgress Reveal(
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

        // Remove each played card from its hand before any effect runs, so a special
        // card's own effect never sees itself as still part of "my hand".
        ApplyFate(player1, player1Card, player1Fate);
        ApplyFate(player2, player2Card, player2Fate);

        bool player1MustChoose = false;
        bool player2MustChoose = false;

        if (runEffects)
        {
            RunChoicelessEffectsInPriorityOrder(player1Card, player1, player2Card, player2, rng);

            // A card blocked by a Joker never reaches here, so its player is never asked
            // to make a choice that would only be thrown away.
            player1MustChoose = RequiresChoice(player1Card);
            player2MustChoose = RequiresChoice(player2Card);
        }

        return new RoundInProgress(
            player1Card,
            player2Card,
            player1Fate,
            player2Fate,
            winLoss,
            player1MustChoose,
            player2MustChoose);
    }

    /// <summary>Phase two: apply whatever choices came back, then draw for both sides.
    /// Only called once no side is still awaited.</summary>
    public static RoundResult Finish(
        RoundInProgress round,
        DeckAndHand player1,
        DeckAndHand player2,
        Random rng)
    {
        // Player 1 first, always — never the order the choices arrived in. 교체 and 리셋
        // both draw from the shared rng, so applying on arrival would make the resolved
        // hands depend on which network message landed first.
        ApplyChoiceIfMade(round, Side.Player1, player1, player2, rng);
        ApplyChoiceIfMade(round, Side.Player2, player2, player1, rng);

        player1.Draw();
        player2.Draw();

        return new RoundResult(
            round.Player1Card,
            round.Player2Card,
            round.Player1CardFate,
            round.Player2CardFate,
            round.WinLoss,
            player1.Hand.Cards,
            player2.Hand.Cards,
            player1.Deck.Count,
            player2.Deck.Count,
            SwappedCardCount(round, Side.Player1),
            SwappedCardCount(round, Side.Player2),
            TransformApplied(round, Side.Player1),
            TransformApplied(round, Side.Player2));
    }

    private static void ApplyChoiceIfMade(
        RoundInProgress round,
        Side side,
        DeckAndHand self,
        DeckAndHand opponent,
        Random rng)
    {
        CardChoice? choice = round.ChoiceOf(side);
        if (choice == null)
        {
            return;
        }

        SpecialEffects[round.CardOf(side)].Apply(choice, self, opponent, rng);
    }

    // Both animation facts are read off the recorded choice, which was validated against
    // the real hand — never off anything a client asserted. A declined or blocked choice
    // is null here, so it reports as "nothing happened".
    private static int SwappedCardCount(RoundInProgress round, Side side)
    {
        CardChoice? choice = round.ChoiceOf(side);
        if (choice == null)
        {
            return 0;
        }

        return choice.CardsToReturn.Count;
    }

    private static bool TransformApplied(RoundInProgress round, Side side)
    {
        CardChoice? choice = round.ChoiceOf(side);
        if (choice == null)
        {
            return false;
        }

        return choice.CardToTransform != null;
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

    private static void RunChoicelessEffectsInPriorityOrder(
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

        if (IsChoicelessSpecial(player1Card) && player1Card != CardName.Reset)
        {
            RunEffect(player1Card, player1, player2, rng);
        }

        if (IsChoicelessSpecial(player2Card) && player2Card != CardName.Reset)
        {
            RunEffect(player2Card, player2, player1, rng);
        }
    }

    private static bool IsChoicelessSpecial(CardName card)
    {
        if (card.GetCardType() != CardType.Special)
        {
            return false;
        }

        return !SpecialEffects[card].RequiresChoice;
    }

    private static void RunEffect(CardName card, DeckAndHand self, DeckAndHand opponent, Random rng)
    {
        SpecialEffects[card].Apply(null, self, opponent, rng);
    }
}
