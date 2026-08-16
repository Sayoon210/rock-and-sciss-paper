using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DeckTests
{
    [Fact]
    public void Count_reflects_the_initial_cards()
    {
        var deck = new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors });

        Assert.Equal(3, deck.Count);
    }

    [Fact]
    public void TakeFromTop_removes_and_returns_the_first_card()
    {
        var deck = new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors });

        CardName card = deck.TakeFromTop();

        Assert.Equal(CardName.Rock, card);
        Assert.Equal(2, deck.Count);
        Assert.Equal(CardName.Paper, deck.TakeFromTop());
    }

    [Fact]
    public void AddToBottom_appends_after_the_last_card()
    {
        var deck = new Deck(new[] { CardName.Rock, CardName.Paper });

        deck.AddToBottom(CardName.Scissors);
        deck.TakeFromTop();
        deck.TakeFromTop();

        Assert.Equal(CardName.Scissors, deck.TakeFromTop());
    }

    [Fact]
    public void InsertAtTop_becomes_the_next_TakeFromTop()
    {
        var deck = new Deck(new[] { CardName.Rock, CardName.Paper });

        deck.InsertAtTop(CardName.Joker);

        Assert.Equal(CardName.Joker, deck.TakeFromTop());
        Assert.Equal(CardName.Rock, deck.TakeFromTop());
    }

    [Fact]
    public void PeekTop_does_not_remove_cards()
    {
        var deck = new Deck(new[] { CardName.Rock, CardName.Paper, CardName.Scissors });

        IReadOnlyList<CardName> peeked = deck.PeekTop(2);

        Assert.Equal(new[] { CardName.Rock, CardName.Paper }, peeked);
        Assert.Equal(3, deck.Count);
    }

    [Fact]
    public void Shuffle_preserves_every_card()
    {
        var original = new[]
        {
            CardName.Rock, CardName.Rock, CardName.Rock,
            CardName.Paper, CardName.Paper, CardName.Paper,
            CardName.Scissors, CardName.Dummy, CardName.Joker,
        };
        var deck = new Deck(original);

        deck.Shuffle(new Random(1));

        var remaining = new List<CardName>();
        while (deck.Count > 0)
        {
            remaining.Add(deck.TakeFromTop());
        }

        Assert.Equal(original.OrderBy(c => c), remaining.OrderBy(c => c));
    }

    [Fact]
    public void Shuffle_with_the_same_seed_is_deterministic()
    {
        var cards = new[]
        {
            CardName.Rock, CardName.Paper, CardName.Scissors,
            CardName.Dummy, CardName.Joker, CardName.Reset,
        };
        var deckA = new Deck(cards);
        var deckB = new Deck(cards);

        deckA.Shuffle(new Random(42));
        deckB.Shuffle(new Random(42));

        Assert.Equal(deckA.PeekTop(deckA.Count), deckB.PeekTop(deckB.Count));
    }
}
