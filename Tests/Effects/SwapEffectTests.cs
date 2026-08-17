using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class SwapEffectTests
{
    [Fact]
    public void Apply_returns_the_chosen_cards_and_draws_the_same_number_back()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Dummy, CardName.Dummy, CardName.Dummy }),
            new Hand(new[] { CardName.Rock, CardName.Paper, CardName.Scissors }));
        var opponent = new DeckAndHand(new Deck(Array.Empty<CardName>()), new Hand(Array.Empty<CardName>()));

        new SwapEffect().Apply(
            CardPlay.Swapping(new[] { CardName.Rock, CardName.Paper }), self, opponent, new Random(1));

        Assert.Equal(3, self.Hand.Cards.Count);
        Assert.Equal(3, self.Deck.Count);
        Assert.Contains(CardName.Scissors, self.Hand.Cards);
    }

    [Fact]
    public void Apply_swapping_nothing_leaves_the_hand_as_it_was()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Dummy }),
            new Hand(new[] { CardName.Rock }));
        var opponent = new DeckAndHand(new Deck(Array.Empty<CardName>()), new Hand(Array.Empty<CardName>()));

        new SwapEffect().Apply(CardPlay.Swapping(Array.Empty<CardName>()), self, opponent, new Random(1));

        Assert.Equal(new[] { CardName.Rock }, self.Hand.Cards);
        Assert.Equal(1, self.Deck.Count);
    }

    [Fact]
    public void Apply_does_not_touch_the_opponent()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Dummy }),
            new Hand(new[] { CardName.Rock }));
        var opponent = new DeckAndHand(
            new Deck(new[] { CardName.Scissors }),
            new Hand(new[] { CardName.Paper }));

        new SwapEffect().Apply(
            CardPlay.Swapping(new[] { CardName.Rock }), self, opponent, new Random(1));

        Assert.Equal(new[] { CardName.Paper }, opponent.Hand.Cards);
        Assert.Equal(1, opponent.Deck.Count);
    }

    [Fact]
    public void Validate_accepts_a_duplicate_the_hand_actually_holds_twice()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Dummy }),
            new Hand(new[] { CardName.Rock, CardName.Rock }));

        new SwapEffect().Validate(CardPlay.Swapping(new[] { CardName.Rock, CardName.Rock }), self);
    }

    [Fact]
    public void Validate_rejects_more_copies_than_the_hand_holds()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Dummy }),
            new Hand(new[] { CardName.Rock, CardName.Rock }));

        Assert.Throws<ArgumentException>(
            () => new SwapEffect().Validate(
                CardPlay.Swapping(new[] { CardName.Rock, CardName.Rock, CardName.Rock }), self));
    }

    [Fact]
    public void Validate_rejects_a_card_that_is_not_in_hand()
    {
        var self = new DeckAndHand(
            new Deck(new[] { CardName.Dummy }),
            new Hand(new[] { CardName.Rock }));

        Assert.Throws<ArgumentException>(
            () => new SwapEffect().Validate(CardPlay.Swapping(new[] { CardName.Joker }), self));
    }
}
