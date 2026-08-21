namespace RockAndScissPaper.GameLogic;

/// <summary>One player's hand.</summary>
public sealed class Hand
{
    private readonly List<ECardName> _cards;

    public Hand(IEnumerable<ECardName> cards)
    {
        _cards = new List<ECardName>(cards);
    }

    public IReadOnlyList<ECardName> Cards => _cards;

    public void Add(ECardName card)
    {
        _cards.Add(card);
    }

    public void Remove(ECardName card)
    {
        if (!_cards.Remove(card))
        {
            throw new ArgumentException($"Hand does not contain {card}.", nameof(card));
        }
    }

    public bool Contains(ECardName card)
    {
        return _cards.Contains(card);
    }
}
