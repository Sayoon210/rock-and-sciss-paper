using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class SwapEffectTests
{
    private static DeckAndHand HandOf(params ECardName[] cards)
    {
        return new DeckAndHand(new Deck(new[] { ECardName.Blank, ECardName.Blank, ECardName.Blank }), new Hand(cards));
    }

    [Fact]
    public void Validate_rejects_a_choice_that_puts_back_more_cards_than_the_limit()
    {
        DeckAndHand self = HandOf(ECardName.Rock, ECardName.Rock, ECardName.Rock, ECardName.Blank);

        var tooMany = new List<ECardName>();
        for (int card = 0; card <= SwapEffect.MAX_SWAPPED_CARDS; card++)
        {
            tooMany.Add(ECardName.Rock);
        }

        Assert.Throws<ArgumentException>(
            () => new SwapEffect().Validate(CardChoice.Swapping(tooMany), self));
    }

    [Fact]
    public void Validate_accepts_a_choice_right_at_the_limit()
    {
        // Counted off MAX_SWAPPED_CARDS rather than written as a literal 2, so these two tests
        // keep testing the limit itself if the number ever changes.
        DeckAndHand self = HandOf(ECardName.Rock, ECardName.Rock, ECardName.Rock, ECardName.Blank);

        var atLimit = new List<ECardName>();
        for (int card = 0; card < SwapEffect.MAX_SWAPPED_CARDS; card++)
        {
            atLimit.Add(ECardName.Rock);
        }

        new SwapEffect().Validate(CardChoice.Swapping(atLimit), self);
    }

    [Fact]
    public void Apply_returns_the_chosen_cards_and_draws_the_same_number_back()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank, ECardName.Blank, ECardName.Blank }),
            new Hand(new[] { ECardName.Rock, ECardName.Paper, ECardName.Scissors }));
        var opponent = new DeckAndHand(new Deck(new[] { ECardName.Scissors }), new Hand(Array.Empty<ECardName>()));

        new SwapEffect().Apply(
            CardChoice.Swapping(new[] { ECardName.Rock, ECardName.Paper }), self, opponent, new Random(1));

        Assert.Equal(3, self.Hand.Cards.Count);
        Assert.Equal(3, self.Deck.Count);
        Assert.Contains(ECardName.Scissors, self.Hand.Cards);
    }

    [Fact]
    public void Apply_swapping_nothing_leaves_the_hand_as_it_was()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock }));
        var opponent = new DeckAndHand(new Deck(new[] { ECardName.Scissors }), new Hand(Array.Empty<ECardName>()));

        new SwapEffect().Apply(CardChoice.Swapping(Array.Empty<ECardName>()), self, opponent, new Random(1));

        Assert.Equal(new[] { ECardName.Rock }, self.Hand.Cards);
        Assert.Equal(1, self.Deck.Count);
    }

    [Fact]
    public void Apply_does_not_touch_the_opponent()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock }));
        var opponent = new DeckAndHand(
            new Deck(new[] { ECardName.Scissors }),
            new Hand(new[] { ECardName.Paper }));

        new SwapEffect().Apply(
            CardChoice.Swapping(new[] { ECardName.Rock }), self, opponent, new Random(1));

        Assert.Equal(new[] { ECardName.Paper }, opponent.Hand.Cards);
        Assert.Equal(1, opponent.Deck.Count);
    }

    // No Validate fixture holds a Swap card, and that is the point. The choice is made
    // after the reveal, by which time the played Swap has already left the hand, so the
    // hand Validate sees is exactly the hand Apply will mutate. An earlier version of this
    // file asserted the opposite — that the Swap card is always still in hand — and the
    // effect compensated for it, which is what let a player return the Swap card itself
    // and wedge the match. If a fixture here ever grows a Swap card again, that is the bug
    // coming back.
    [Fact]
    public void Validate_accepts_a_duplicate_the_hand_actually_holds_twice()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock, ECardName.Rock }));

        new SwapEffect().Validate(CardChoice.Swapping(new[] { ECardName.Rock, ECardName.Rock }), self);
    }

    [Fact]
    public void Validate_rejects_more_copies_than_the_hand_holds()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock, ECardName.Rock }));

        Assert.Throws<ArgumentException>(
            () => new SwapEffect().Validate(
                CardChoice.Swapping(new[] { ECardName.Rock, ECardName.Rock, ECardName.Rock }), self));
    }

    [Fact]
    public void Validate_rejects_a_card_that_is_not_in_hand()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock }));

        Assert.Throws<ArgumentException>(
            () => new SwapEffect().Validate(CardChoice.Swapping(new[] { ECardName.Joker }), self));
    }

    [Fact]
    public void Validate_rejects_returning_a_swap_card_the_hand_no_longer_holds()
    {
        // The played Swap is gone by the time its owner chooses, so naming it is refused
        // by the ordinary "not in hand" rule — no special case needed any more.
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock }));

        Assert.Throws<ArgumentException>(
            () => new SwapEffect().Validate(CardChoice.Swapping(new[] { ECardName.Swap }), self));
    }

    [Fact]
    public void Validate_rejects_a_missing_choice()
    {
        var self = new DeckAndHand(
            new Deck(new[] { ECardName.Blank }),
            new Hand(new[] { ECardName.Rock }));

        Assert.Throws<ArgumentException>(() => new SwapEffect().Validate(null, self));
    }
}
