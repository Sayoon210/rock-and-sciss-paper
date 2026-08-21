using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class TransformEffectTests
{
    private static DeckAndHand HandOf(params ECardName[] cards)
    {
        return new DeckAndHand(new Deck(new[] { ECardName.Paper }), new Hand(cards));
    }

    [Fact]
    public void A_hand_with_a_normal_or_blank_card_has_a_legal_choice()
    {
        Assert.True(new TransformEffect().HasAnyLegalChoice(HandOf(ECardName.Joker, ECardName.Rock)));
        Assert.True(new TransformEffect().HasAnyLegalChoice(HandOf(ECardName.Reset, ECardName.Blank)));
    }

    [Fact]
    public void A_hand_of_only_jokers_and_abilities_has_no_legal_choice()
    {
        // What 리셋 can deal to someone who has already played 변화 — the picker would
        // otherwise open with every card greyed out and no way to answer it.
        Assert.False(new TransformEffect().HasAnyLegalChoice(
            HandOf(ECardName.Joker, ECardName.Reset, ECardName.Swap)));
    }

    [Fact]
    public void An_empty_hand_has_no_legal_choice()
    {
        Assert.False(new TransformEffect().HasAnyLegalChoice(HandOf()));
    }

    [Fact]
    public void Apply_replaces_the_chosen_card_with_the_chosen_replacement()
    {
        DeckAndHand self = HandOf(ECardName.Rock, ECardName.Scissors);
        DeckAndHand opponent = HandOf(ECardName.Rock);

        new TransformEffect().Apply(
            CardChoice.Transforming(ECardName.Rock, ECardName.Paper), self, opponent, new Random(1));

        Assert.DoesNotContain(ECardName.Rock, self.Hand.Cards);
        Assert.Contains(ECardName.Paper, self.Hand.Cards);
        Assert.Equal(2, self.Hand.Cards.Count);
    }

    [Fact]
    public void Apply_leaves_a_duplicate_of_the_transformed_card_alone()
    {
        DeckAndHand self = HandOf(ECardName.Rock, ECardName.Rock);
        DeckAndHand opponent = HandOf(ECardName.Rock);

        new TransformEffect().Apply(
            CardChoice.Transforming(ECardName.Rock, ECardName.Blank), self, opponent, new Random(1));

        Assert.Equal(new[] { ECardName.Rock, ECardName.Blank }, self.Hand.Cards);
    }

    [Theory]
    [InlineData(ECardName.Rock, ECardName.Paper)]
    [InlineData(ECardName.Blank, ECardName.Scissors)]
    [InlineData(ECardName.Rock, ECardName.Blank)]
    public void Validate_accepts_normal_and_blank_cards_in_either_position(ECardName from, ECardName into)
    {
        DeckAndHand self = HandOf(from);

        new TransformEffect().Validate(CardChoice.Transforming(from, into), self);
    }

    [Theory]
    [InlineData(ECardName.Joker, ECardName.Rock)]
    [InlineData(ECardName.Reset, ECardName.Rock)]
    [InlineData(ECardName.Rock, ECardName.Joker)]
    [InlineData(ECardName.Rock, ECardName.Draw)]
    public void Validate_rejects_anything_that_is_not_a_normal_or_blank_card(ECardName from, ECardName into)
    {
        DeckAndHand self = HandOf(from);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(CardChoice.Transforming(from, into), self));
    }

    [Fact]
    public void Validate_rejects_a_card_that_is_not_in_hand()
    {
        DeckAndHand self = HandOf(ECardName.Rock);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(
                CardChoice.Transforming(ECardName.Scissors, ECardName.Paper), self));
    }

    [Fact]
    public void Validate_rejects_a_missing_choice()
    {
        DeckAndHand self = HandOf(ECardName.Rock);

        Assert.Throws<ArgumentException>(() => new TransformEffect().Validate(null, self));
    }

    [Fact]
    public void Validate_rejects_a_choice_shaped_for_a_different_card()
    {
        // A client prompted for 변화 that answers with a 교체-shaped payload gets nothing
        // read out of it, so validation must reject rather than silently transform null.
        DeckAndHand self = HandOf(ECardName.Rock);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(CardChoice.Swapping(new[] { ECardName.Rock }), self));
    }
}
