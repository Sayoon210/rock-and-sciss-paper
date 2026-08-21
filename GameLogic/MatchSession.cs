namespace RockAndScissPaper.GameLogic;

/// <summary>Which side of the match. Not a peer id and not a connection — the translation
/// from peer id to ESide happens in GameState, outside this project.</summary>
public enum ESide
{
    Player1,
    Player2,
}

/// <summary>Where a round currently is. A round only sits in AwaitingChoices when 교체 or
/// 변화 was played and not blocked by a Joker.</summary>
public enum ERoundPhase
{
    AwaitingSubmissions,
    AwaitingChoices,
}

/// <summary>One match, from the opening mulligan to a player reaching ten wins.
/// Instantiated on the host only, once both players are known.
///
/// Rounds are simultaneous: each side submits independently, and nothing is revealed until
/// both have. SubmitCard therefore returns null for the first submission of a round and a
/// RoundReveal for the second. If that reveal says nobody owes a choice, it also carries
/// the finished RoundResult; otherwise the round waits in AwaitingChoices for SubmitChoice
/// or DeclineChoice from every side that owes one.
///
/// Illegal submissions throw. Callers relay client requests, so GameState is expected to
/// catch and drop them rather than let a malformed or malicious request reach here twice.</summary>
public sealed class MatchSession
{
    public const int WINS_NEEDED_FOR_MATCH = 10;
    public const int MULLIGAN_HAND_SIZE = 4;

    private readonly DeckAndHand _player1;
    private readonly DeckAndHand _player2;
    private readonly Random _rng;
    private readonly Action<string>? _log;

    private ECardName? _player1SubmittedCard;
    private ECardName? _player2SubmittedCard;
    private RoundInProgress? _round;

    // Sticky once set — a side that ran out of cards has lost regardless of score, and
    // score alone can never turn back on. Checked in Winner ahead of the score-based result.
    private bool _player1Exhausted;
    private bool _player2Exhausted;

    /// <summary>Takes each player's assembled deck. Deck composition is the caller's job —
    /// CardDatabase lives on the Godot side, so this project never decides what goes in.
    ///
    /// log is a debug trace sink, null by default so tests stay quiet. It is injected
    /// rather than written straight to the console because this project can't reference
    /// GD.Print, and because a session that printed on its own would be the one piece of
    /// GameLogic with a side effect of its own.</summary>
    public MatchSession(
        IEnumerable<ECardName> player1Deck,
        IEnumerable<ECardName> player2Deck,
        Random rng,
        Action<string>? log = null)
    {
        _rng = rng;
        _log = log;
        _player1 = new DeckAndHand(new Deck(player1Deck), new Hand(Array.Empty<ECardName>()));
        _player2 = new DeckAndHand(new Deck(player2Deck), new Hand(Array.Empty<ECardName>()));

        Deal(ESide.Player1);
        Deal(ESide.Player2);
    }

    public int Player1Score { get; private set; }
    public int Player2Score { get; private set; }
    public int RoundNumber { get; private set; } = 1;

    public ERoundPhase Phase
    {
        get
        {
            if (_round == null)
            {
                return ERoundPhase.AwaitingSubmissions;
            }

            return ERoundPhase.AwaitingChoices;
        }
    }

    /// <summary>The side that won — ten wins, or the other side running out of cards
    /// (DESIGN.md, "덱 고갈"), whichever happened. Null while the match is still running.
    ///
    /// Exhaustion is checked first. If both sides exhaust in the same round, Player 1's
    /// exhaustion is the one that resolves it — the same "Player 1 first" tie-break this
    /// codebase already uses for simultaneous Reset and same-priority submissions — so
    /// Player 2 is declared the winner rather than the match hanging with no result.</summary>
    public ESide? Winner
    {
        get
        {
            if (_player1Exhausted)
            {
                return ESide.Player2;
            }

            if (_player2Exhausted)
            {
                return ESide.Player1;
            }

            if (Player1Score >= WINS_NEEDED_FOR_MATCH)
            {
                return ESide.Player1;
            }

            if (Player2Score >= WINS_NEEDED_FOR_MATCH)
            {
                return ESide.Player2;
            }

            return null;
        }
    }

    public IReadOnlyList<ECardName> HandOf(ESide side)
    {
        return DeckAndHandOf(side).Hand.Cards;
    }

    public int DeckCountOf(ESide side)
    {
        return DeckAndHandOf(side).Deck.Count;
    }

    public bool HasSubmittedCard(ESide side)
    {
        return SubmittedCardOf(side) != null;
    }

    public bool IsAwaitingChoiceFrom(ESide side)
    {
        if (_round == null)
        {
            return false;
        }

        return _round.ChoiceStatusOf(side) == EChoiceStatus.Awaited;
    }

    /// <summary>The card this side has to choose for, or null when it owes no choice.
    /// GameState needs it to tell the player what they are choosing about.</summary>
    public ECardName? CardAwaitingChoiceFrom(ESide side)
    {
        if (!IsAwaitingChoiceFrom(side))
        {
            return null;
        }

        return _round!.CardOf(side);
    }

    /// <summary>Plays a card for every side that has not submitted yet, drawn at random from
    /// that side's own hand, and returns the reveal if that completes the round. A no-op, and
    /// null, once the round is past taking cards.
    ///
    /// Random rather than the first card in hand, because hand order carries no meaning here —
    /// nothing in the rules ever assigns a card a slot, so "the first one" would be an
    /// arbitrary artefact of insertion order. Random rather than sparing 조커/능력, because a
    /// rule that protects the cards that vanish would pay a player for not answering.</summary>
    public RoundReveal? SubmitRandomCardForIdleSides()
    {
        if (Winner != null || Phase != ERoundPhase.AwaitingSubmissions)
        {
            return null;
        }

        // Player 1 first, the same order this class settles everything else in.
        RoundReveal? reveal = SubmitRandomCardFor(ESide.Player1);
        RoundReveal? afterPlayer2 = SubmitRandomCardFor(ESide.Player2);
        if (afterPlayer2 != null)
        {
            reveal = afterPlayer2;
        }

        return reveal;
    }

    private RoundReveal? SubmitRandomCardFor(ESide side)
    {
        if (HasSubmittedCard(side))
        {
            return null;
        }

        IReadOnlyList<ECardName> hand = DeckAndHandOf(side).Hand.Cards;
        ECardName card = hand[_rng.Next(hand.Count)];
        _log?.Invoke($"[timeout] round {RoundNumber}: {side} did not submit, playing {card}");

        return SubmitCard(side, card);
    }

    /// <summary>Records one side's card. Returns null while waiting on the other side, and
    /// a RoundReveal once both are in.</summary>
    public RoundReveal? SubmitCard(ESide side, ECardName card)
    {
        if (Winner != null)
        {
            throw new InvalidOperationException("The match is already over.");
        }

        if (Phase != ERoundPhase.AwaitingSubmissions)
        {
            throw new InvalidOperationException("This round is waiting on a choice, not a card.");
        }

        if (HasSubmittedCard(side))
        {
            throw new InvalidOperationException($"{side} has already submitted this round.");
        }

        // Validated before it is recorded: a rejected card must leave the round exactly
        // as it was, or the side would be stuck unable to submit again.
        RoundResolver.ValidateSubmission(card, DeckAndHandOf(side));

        if (side == ESide.Player1)
        {
            _player1SubmittedCard = card;
        }
        else
        {
            _player2SubmittedCard = card;
        }

        _log?.Invoke($"[submit] round {RoundNumber}: {side} played {card}");

        if (_player1SubmittedCard == null || _player2SubmittedCard == null)
        {
            return null;
        }

        // Read out before anything can finish the round — FinishRoundIfSettled clears both
        // submitted cards on its way out, so these fields are gone by the time it returns.
        ECardName player1Card = _player1SubmittedCard.Value;
        ECardName player2Card = _player2SubmittedCard.Value;

        _round = RoundResolver.Reveal(player1Card, player2Card, _player1, _player2, _rng);

        bool player1MustChoose = _round.ChoiceStatusOf(ESide.Player1) == EChoiceStatus.Awaited;
        bool player2MustChoose = _round.ChoiceStatusOf(ESide.Player2) == EChoiceStatus.Awaited;

        _log?.Invoke(
            $"[reveal] round {RoundNumber}: {player1Card} vs {player2Card}"
            + $" (choices awaited: P1 {player1MustChoose}, P2 {player2MustChoose})");

        RoundResult? result = FinishRoundIfSettled();

        return new RoundReveal(
            player1Card,
            player2Card,
            player1MustChoose,
            player2MustChoose,
            result);
    }

    /// <summary>Records one side's choice. Returns null until every owed choice is settled
    /// and the round can finish.</summary>
    public RoundResult? SubmitChoice(ESide side, CardChoice choice)
    {
        if (Winner != null)
        {
            throw new InvalidOperationException("The match is already over.");
        }

        if (_round == null)
        {
            throw new InvalidOperationException("No round is waiting on a choice.");
        }

        if (_round.ChoiceStatusOf(side) != EChoiceStatus.Awaited)
        {
            throw new InvalidOperationException($"{side} was not asked to choose this round.");
        }

        // Validated before it is recorded, for the same reason a card is: a rejected
        // choice must leave the side still awaited so it can simply try again.
        RoundResolver.ValidateChoice(_round.CardOf(side), choice, DeckAndHandOf(side));
        _round.RecordChoice(side, choice);

        _log?.Invoke($"[choice] round {RoundNumber}: {side} chose for {_round.CardOf(side)}");

        return FinishRoundIfSettled();
    }

    /// <summary>Gives up on a side's choice — what a choice timeout calls. The effect
    /// simply does not run, which for 교체 is indistinguishable from swapping nothing.
    /// A no-op for a side that owes nothing, so a timer that fires late is harmless.</summary>
    public RoundResult? DeclineChoice(ESide side)
    {
        if (!IsAwaitingChoiceFrom(side))
        {
            return null;
        }

        _round!.DeclineChoice(side);
        _log?.Invoke($"[choice] round {RoundNumber}: {side} declined for {_round.CardOf(side)}");

        return FinishRoundIfSettled();
    }

    /// <summary>Finishes the round once nobody is still awaited, or returns null.
    ///
    /// The finally is the wedge guard: if anything at all throws while the round is being
    /// finished, the round is still torn down, so both sides can submit again next call.
    /// An exception here previously left both sides marked as submitted forever, which
    /// ended the match with no way to tell why.</summary>
    private RoundResult? FinishRoundIfSettled()
    {
        if (_round == null || _round.IsAwaitingAnyChoice)
        {
            return null;
        }

        try
        {
            RoundResult result = RoundResolver.Finish(_round, _player1, _player2, _rng);
            RecordResolvedRound(result);
            return result;
        }
        catch (Exception exception)
        {
            _log?.Invoke($"[error]  round {RoundNumber}: resolution failed — {exception.Message}");
            throw;
        }
        finally
        {
            _round = null;
            _player1SubmittedCard = null;
            _player2SubmittedCard = null;
        }
    }

    /// <summary>Closes out a resolved round: records its score, notes whether either deck
    /// just ran dry, and moves the round counter on. The rules of the round itself belong to
    /// RoundResolver; this only writes the outcome into the match.</summary>
    private void RecordResolvedRound(RoundResult result)
    {
        if (result.WinLoss == EWinLossResult.Player1Win)
        {
            Player1Score++;
        }
        else if (result.WinLoss == EWinLossResult.Player2Win)
        {
            Player2Score++;
        }

        // Checked off the result's counts, not by re-reading the live decks — same reason
        // RecordResolvedRound already reads everything else off `result` rather than `_player1`
        // /`_player2` directly. A deck emptied by any draw this round (the guaranteed
        // per-round draw, or a 교체/리셋/드로우 that ran out partway through) is caught here,
        // in one place, regardless of which draw was the one that hit zero.
        if (result.Player1DeckCount == 0)
        {
            _player1Exhausted = true;
        }

        if (result.Player2DeckCount == 0)
        {
            _player2Exhausted = true;
        }

        if (_log != null)
        {
            string verdict;
            if (result.WinLoss == null)
            {
                verdict = "no win/loss";
            }
            else
            {
                verdict = result.WinLoss.ToString()!;
            }

            _log($"[resolve] round {RoundNumber}: {result.Player1Card} ({result.Player1CardFate}) vs {result.Player2Card} ({result.Player2CardFate}) -> {verdict}, score {Player1Score}-{Player2Score}");
            _log($"[hands]   P1 {Describe(result.Player1Hand)} (deck {result.Player1DeckCount}) | P2 {Describe(result.Player2Hand)} (deck {result.Player2DeckCount})");
        }

        RoundNumber++;

        if (Winner != null)
        {
            _log?.Invoke($"[match]   {Winner} wins {Player1Score}-{Player2Score}");
        }
    }

    private void Deal(ESide side)
    {
        DeckAndHand player = DeckAndHandOf(side);
        player.Deck.Shuffle(_rng);

        for (int i = 0; i < MULLIGAN_HAND_SIZE; i++)
        {
            player.Draw();
        }

        _log?.Invoke($"[deal] {side} mulligan: {Describe(player.Hand.Cards)} (deck {player.Deck.Count})");
    }

    private static string Describe(IReadOnlyList<ECardName> cards)
    {
        return string.Join(", ", cards);
    }

    private DeckAndHand DeckAndHandOf(ESide side)
    {
        if (side == ESide.Player1)
        {
            return _player1;
        }

        return _player2;
    }

    private ECardName? SubmittedCardOf(ESide side)
    {
        if (side == ESide.Player1)
        {
            return _player1SubmittedCard;
        }

        return _player2SubmittedCard;
    }
}
