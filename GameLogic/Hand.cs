namespace RockAndScissPaper.GameLogic;

/// <summary>One player's hand.</summary>
public sealed class Hand
{
    private readonly List<CardName> _cards;

    public Hand(IEnumerable<CardName> cards)
    {
        _cards = new List<CardName>(cards);
    }

    public IReadOnlyList<CardName> Cards => _cards;

    public void Add(CardName card)
    {
        _cards.Add(card);
    }

    public void Remove(CardName card)
    {
        if (!_cards.Remove(card))
        {
            throw new ArgumentException($"Hand does not contain {card}.", nameof(card));
        }
    }

    public bool Contains(CardName card)
    {
        return _cards.Contains(card);
    }
}
