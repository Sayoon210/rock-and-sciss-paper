using Godot;

namespace RockAndScissPaper.UI;

/// <summary>덱, drawn as an actual stack of backs rather than written as a number. It exists
/// so the cards have somewhere to come from: it says where the top of the stack is, so a card
/// being drawn can ease out of that point instead of appearing in the 패 already there.
///
/// The stack is built out of CardView instances rather than plain rectangles, so the back of a
/// card in the deck is the same back a card in hand shows — when the deck actually gets its own
/// art, both pick it up at once.
///
/// It shows a count and a stack, and nothing else: deck *order* is hidden information
/// (Scripts/CLAUDE.md), so there is deliberately nothing here that could ever reveal it, not
/// even to the player who owns the deck.</summary>
public partial class DeckView : Control
{
    private Control _stack = null!;
    private CardView _topCard = null!;
    private Label _countLabel = null!;

    public override void _Ready()
    {
        _stack = GetNode<Control>("Stack");
        _topCard = GetNode<CardView>("Stack/TopCard");
        _countLabel = GetNode<Label>("CountLabel");
    }

    /// <summary>Where a card sitting in this deck would have its top-left corner, in screen
    /// coordinates. A card being drawn starts here, so it travels from the same point the top
    /// of the stack is actually drawn at.
    ///
    /// Still meaningful while the deck reads as empty: an emptied deck is exactly where the
    /// cards that refill it will be coming from.</summary>
    public Vector2 TopCardGlobalPosition
    {
        get { return _topCard.GlobalPosition; }
    }

    public void ShowCount(int count)
    {
        _countLabel.Text = $"덱 {count}";

        // An empty deck with a full stack of backs still drawn on it would be showing the
        // count and the picture as two different numbers. Made transparent rather than
        // hidden: a hidden Control stops being laid out, and TopCardGlobalPosition still has
        // to be right while the deck is empty, because that is exactly where the cards that
        // are about to refill it are coming from.
        _stack.Modulate = new Color(1f, 1f, 1f, count > 0 ? 1f : 0f);
    }
}
