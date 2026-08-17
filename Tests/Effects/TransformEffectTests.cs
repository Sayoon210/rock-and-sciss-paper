using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Tests;

public class TransformEffectTests
{
    private static DeckAndHand HandOf(params CardName[] cards)
    {
        return new DeckAndHand(new Deck(new[] { CardName.Paper }), new Hand(cards));
    }

    [Fact]
    public void Apply_replaces_the_chosen_card_with_the_chosen_replacement()
    {
        DeckAndHand self = HandOf(CardName.Rock, CardName.Scissors);
        DeckAndHand opponent = HandOf(CardName.Rock);

        new TransformEffect().Apply(
            CardPlay.Transforming(CardName.Rock, CardName.Paper), self, opponent, new Random(1));

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
            CardPlay.Transforming(CardName.Rock, CardName.Dummy), self, opponent, new Random(1));

        Assert.Equal(new[] { CardName.Rock, CardName.Dummy }, self.Hand.Cards);
    }

    [Theory]
    [InlineData(CardName.Rock, CardName.Paper)]
    [InlineData(CardName.Dummy, CardName.Scissors)]
    [InlineData(CardName.Rock, CardName.Dummy)]
    public void Validate_accepts_normal_and_dummy_cards_in_either_position(CardName from, CardName into)
    {
        DeckAndHand self = HandOf(from);

        new TransformEffect().Validate(CardPlay.Transforming(from, into), self);
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
            () => new TransformEffect().Validate(CardPlay.Transforming(from, into), self));
    }

    [Fact]
    public void Validate_rejects_a_card_that_is_not_in_hand()
    {
        DeckAndHand self = HandOf(CardName.Rock);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(
                CardPlay.Transforming(CardName.Scissors, CardName.Paper), self));
    }

    [Fact]
    public void Validate_rejects_a_play_that_carries_no_choice()
    {
        DeckAndHand self = HandOf(CardName.Rock);

        Assert.Throws<ArgumentException>(
            () => new TransformEffect().Validate(CardPlay.WithoutChoice(CardName.Transform), self));
    }
}
