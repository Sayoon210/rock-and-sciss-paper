namespace RockAndScissPaper.GameLogic;

/// <summary>One player's deck. Index 0 is the deck top.</summary>
public sealed class Deck
{
    private readonly List<ECardName> _cards;

    public Deck(IEnumerable<ECardName> cards)
    {
        _cards = new List<ECardName>(cards);
    }

    public int Count => _cards.Count;

    public ECardName TakeFromTop()
    {
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
