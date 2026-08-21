using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DeckTests
{
    [Fact]
    public void Count_reflects_the_initial_cards()
    {
        var deck = new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors });

        Assert.Equal(3, deck.Count);
    }

    [Fact]
    public void TakeFromTop_removes_and_returns_the_first_card()
    {
        var deck = new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors });

        ECardName card = deck.TakeFromTop();

        Assert.Equal(ECardName.Rock, card);
        Assert.Equal(2, deck.Count);
        Assert.Equal(ECardName.Paper, deck.TakeFromTop());
    }

    [Fact]
    public void AddToBottom_appends_after_the_last_card()
    {
        var deck = new Deck(new[] { ECardName.Rock, ECardName.Paper });

        deck.AddToBottom(ECardName.Scissors);
        deck.TakeFromTop();
        deck.TakeFromTop();

        Assert.Equal(ECardName.Scissors, deck.TakeFromTop());
    }

    [Fact]
    public void Shuffle_preserves_every_card()
    {
        var original = new[]
        {
            ECardName.Rock, ECardName.Rock, ECardName.Rock,
            ECardName.Paper, ECardName.Paper, ECardName.Paper,
            ECardName.Scissors, ECardName.Blank, ECardName.Joker,
        };
        var deck = new Deck(original);

        deck.Shuffle(new Random(1));

        var remaining = new List<ECardName>();
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
            ECardName.Rock, ECardName.Paper, ECardName.Scissors,
            ECardName.Blank, ECardName.Joker, ECardName.Reset,
        };
        var deckA = new Deck(cards);
        var deckB = new Deck(cards);

        deckA.Shuffle(new Random(42));
        deckB.Shuffle(new Random(42));

        while (deckA.Count > 0)
        {
            Assert.Equal(deckA.TakeFromTop(), deckB.TakeFromTop());
        }
    }
}
