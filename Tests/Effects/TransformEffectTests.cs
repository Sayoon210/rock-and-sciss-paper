using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class TransformEffectTests
{
    private static DeckAndHand HandOf(params CardName[] cards)
    {
        return new DeckAndHand(new Deck(new[] { CardName.Paper }), new Hand(cards));
    }

    [Fact]
    public void A_hand_with_a_normal_or_dummy_card_has_a_legal_choice()
    {
        Assert.True(new TransformEffect().HasAnyLegalChoice(HandOf(CardName.Joker, CardName.Rock)));
        Assert.True(new TransformEffect().HasAnyLegalChoice(HandOf(CardName.Reset, CardName.Dummy)));
    }

    [Fact]
    public void A_hand_of_only_jokers_and_specials_has_no_legal_choice()
    {
        // What 리셋 can deal to someone who has already played 변화 — the picker would
        // otherwise open with every card greyed out and no way to answer it.
        Assert.False(new TransformEffect().HasAnyLegalChoice(
            HandOf(CardName.Joker, CardName.Reset, CardName.Swap)));
    }

    [Fact]
    public void An_empty_hand_has_no_legal_choice()
    {
        Assert.False(new TransformEffect().HasAnyLegalChoice(HandOf()));
    }

    [Fact]
    public void Apply_replaces_the_chosen_card_with_the_chosen_replacement()
    {
        DeckAndHand self = HandOf(CardName.Rock, CardName.Scissors);
        DeckAndHand opponent = HandOf(CardName.Rock);

        new TransformEffect().Apply(
            CardChoice.Transforming(CardName.Rock, CardName.Paper), self, opponent, new Random(1));

        Assert.DoesNotContain(CardName.Rock, self.Hand.Cards);
        Assert.Contains(CardName.Paper, self.Hand.Cards);
        Assert.Equal(2, self.Hand.Cards.Count);
    }

    [Fact]
    public void Apply_leaves_a_duplicate_of_the_transformed_card_alone()
    {
        DeckAndHand self = HandOf(CardName.Rock, CardName.Rock);
        DeckAndHand opponent = HandOf(CardName.Rock);

        new TransformEffect().Apply(
            CardChoice.Transforming(CardName.Rock, CardName.Dummy), self, opponent, new Random(1));

        Assert.Equal(new[] { CardName.Rock, CardName.Dummy }, self.Hand.Cards);
    }

    [Theory]
    [InlineData(CardName.Rock, CardName.Paper)]
    [InlineData(CardName.Dummy, CardName.Scissors)]
    [InlineData(CardName.Rock, CardName.Dummy)]
    public void Validate_accepts_normal_and_dummy_cards_in_either_position(CardName from, CardName into)
    {
        DeckAndHand self = HandOf(from);

        new TransformEffect().Validate(CardChoice.Transforming(from, into), self);
    }

    [Theory]
    [InlineData(CardName.Joker, CardName.Rock)]
    [InlineData(CardName.Reset, CardName.Rock)]
    [InlineData(CardName.Rock, CardName.Joker)]
    [InlineData(CardName.Rock, CardName.Draw)]
    public void Validate_rejects_anything_that_is_not_a_normal_or_dummy_card(CardName from, CardName into)
    {
        DeckAndHand self = HandOf(from);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(CardChoice.Transforming(from, into), self));
    }

    [Fact]
    public void Validate_rejects_a_card_that_is_not_in_hand()
    {
        DeckAndHand self = HandOf(CardName.Rock);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(
                CardChoice.Transforming(CardName.Scissors, CardName.Paper), self));
    }

    [Fact]
    public void Validate_rejects_a_missing_choice()
    {
        DeckAndHand self = HandOf(CardName.Rock);

        Assert.Throws<ArgumentException>(() => new TransformEffect().Validate(null, self));
    }

    [Fact]
    public void Validate_rejects_a_choice_shaped_for_a_different_card()
    {
        // A client prompted for 변화 that answers with a 교체-shaped payload gets nothing
        // read out of it, so validation must reject rather than silently transform null.
        DeckAndHand self = HandOf(CardName.Rock);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(CardChoice.Swapping(new[] { CardName.Rock }), self));
    }
}
