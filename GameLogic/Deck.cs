namespace RockAndScissPaper.GameLogic;

/// <summary>One player's supply of cards. Index 0 is the top.
///
/// It does not run out. Drawn empty, it restocks itself from the same set it was built with,
/// shuffled — so from the player's side there is no deck at all: cards simply arrive, five at a
/// time, and no round can ever be the one where they stop. DESIGN.md's 덱 고갈 loss condition is
/// gone with it.
///
/// Restocking rather than being handed one very long pre-generated list, which was the other way
/// to reach "never runs out": a list of any fixed length still has an end, so it still needs a
/// rule for reaching it, and that rule would exist purely to cover a case the design says
/// should not exist.</summary>
public sealed class Deck
{
    private readonly List<ECardName> _restock;
    private readonly List<ECardName> _cards;

    public Deck(IEnumerable<ECardName> cards)
    {
        _restock = new List<ECardName>(cards);
        if (_restock.Count == 0)
        {
            // Refused at construction rather than at the draw that finds nothing to restock
            // from, which would fail somewhere in the middle of a round with no hint of why.
            throw new ArgumentException("A deck needs at least one card to restock from.", nameof(cards));
        }

        _cards = new List<ECardName>(_restock);
    }

    /// <summary>How many are left before the next restock. Not a countdown to anything the
    /// player can lose by — see the class summary.</summary>
    public int Count => _cards.Count;

    /// <summary>The top card, restocking first if there is none. rng is taken here rather than
    /// only by Shuffle because a restock has to be shuffled to mean anything, and the caller
    /// drawing a card is not in a position to know that a restock is about to happen.</summary>
    public ECardName TakeFromTop(Random rng)
    {
        if (_cards.Count == 0)
        {
            _cards.AddRange(_restock);
            Shuffle(rng);
        }

        ECardName card = _cards[0];
        _cards.RemoveAt(0);
        return card;
    }

    public void AddToBottom(ECardName card)
    {
        _cards.Add(card);
    }

    /// <summary>Fisher-Yates: every permutation is equally likely, unlike naive per-card
    /// random-swap approaches. rng is injected rather than global so a shuffle is
    /// reproducible from its seed.</summary>
    public void Shuffle(Random rng)
    {
        // Walk backward, and for each position pick a random index from the
        // still-unshuffled range [0, i] (inclusive of i itself) to swap in.
        for (int i = _cards.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            ECardName swap = _cards[i];
            _cards[i] = _cards[j];
            _cards[j] = swap;
        }
    }
}
