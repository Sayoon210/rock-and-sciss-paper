using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class DeckTests
{
    private static readonly Random UnusedRng = new Random(0);

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

        ECardName card = deck.TakeFromTop(UnusedRng);

        Assert.Equal(ECardName.Rock, card);
        Assert.Equal(2, deck.Count);
        Assert.Equal(ECardName.Paper, deck.TakeFromTop(UnusedRng));
    }

    [Fact]
    public void AddToBottom_appends_after_the_last_card()
    {
        var deck = new Deck(new[] { ECardName.Rock, ECardName.Paper });

        deck.AddToBottom(ECardName.Scissors);
        deck.TakeFromTop(UnusedRng);
        deck.TakeFromTop(UnusedRng);

        Assert.Equal(ECardName.Scissors, deck.TakeFromTop(UnusedRng));
    }

    /// <summary>The deck does not run out — drawn empty it restocks from the set it was built
    /// with, which is what removes 덱 고갈 as a thing that can happen mid-match.</summary>
    [Fact]
    public void TakeFromTop_restocks_instead_of_running_out()
    {
        var deck = new Deck(new[] { ECardName.Rock, ECardName.Paper });

        deck.TakeFromTop(UnusedRng);
        deck.TakeFromTop(UnusedRng);
        Assert.Equal(0, deck.Count);

        ECardName afterRestock = deck.TakeFromTop(new Random(1));

        Assert.Contains(afterRestock, new[] { ECardName.Rock, ECardName.Paper });
        Assert.Equal(1, deck.Count);
    }

    [Fact]
    public void TakeFromTop_keeps_restocking_for_as_long_as_it_is_asked_to()
    {
        var deck = new Deck(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors });
        var rng = new Random(7);

        // Well past the three it was built from: any exhaustion would surface here.
        for (int i = 0; i < 60; i++)
        {
            deck.TakeFromTop(rng);
        }
    }

    [Fact]
    public void A_deck_with_nothing_to_restock_from_is_refused()
    {
        Assert.Throws<ArgumentException>(() => new Deck(Array.Empty<ECardName>()));
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
            remaining.Add(deck.TakeFromTop(UnusedRng));
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
            Assert.Equal(deckA.TakeFromTop(UnusedRng), deckB.TakeFromTop(UnusedRng));
        }
    }
}
