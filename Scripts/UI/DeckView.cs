using System.Collections.Generic;
using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>덱, drawn as an actual stack of backs rather than written as a number. It exists
/// so the cards have somewhere to come from and go back to: it says where the top of the stack
/// is, so a card being drawn can ease out of that point, and it takes cards back in through
/// AbsorbCard.
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
    private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match/CardView.tscn";

    /// <summary>How sharply an absorbed card converges on the stack, as a rate rather than a
    /// duration, applied as 1 - e^(-rate * delta). Deliberately the same rate HandView lays its
    /// row out at, so a card going into the deck and a card coming out of it move alike.</summary>
    private const float ABSORB_RATE = 14f;

    /// <summary>How close a card has to get before it is freed. Exact arrival never happens,
    /// since the easing is exponential, so this is a distance rather than an equality.</summary>
    private const float ABSORB_ARRIVAL_DISTANCE = 6f;

    private PackedScene _cardViewScene = null!;
    private Control _absorbLayer = null!;
    private Control _stack = null!;
    private CardView _topCard = null!;
    private Label _countLabel = null!;

    // Stand-in cards on their way into the stack. Only ever non-empty while something is
    // actually moving, which is also the only time this node processes at all.
    private readonly List<CardView> _absorbing = new List<CardView>();

    public override void _Ready()
    {
        _cardViewScene = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH);
        _absorbLayer = GetNode<Control>("AbsorbLayer");
        _stack = GetNode<Control>("Stack");
        _topCard = GetNode<CardView>("Stack/TopCard");
        _countLabel = GetNode<Label>("CountLabel");

        SetProcess(false);
    }

    /// <summary>Where a card sitting in this deck would have its top-left corner, in screen
    /// coordinates. A card being drawn starts here and an absorbed card ends here, so both
    /// travel to and from the same point the top of the stack is actually drawn at.
    ///
    /// Still meaningful while the deck reads as empty: an emptied deck is exactly where a
    /// returning card is headed, and it refills the moment that card arrives.</summary>
    public Vector2 TopCardGlobalPosition
    {
        get { return _topCard.GlobalPosition; }
    }

    /// <summary>Whether anything is still on its way in. Read by HandView, which holds newly
    /// drawn cards back until the hand being put away has finished going in — 리셋 is "the old
    /// hand goes back, a new one comes out", and the two halves overlapping reads as neither.</summary>
    public bool IsAbsorbingCards
    {
        get { return _absorbing.Count > 0; }
    }

    public void ShowCount(int count)
    {
        _countLabel.Text = $"덱 {count}";

        // An empty deck with a full stack of backs still drawn on it would be showing the
        // count and the picture as two different numbers. Made transparent rather than
        // hidden: a hidden Control stops being laid out, and TopCardGlobalPosition still has
        // to be right while the deck is empty, because that is exactly where the cards that
        // are about to refill it are heading.
        _stack.Modulate = new Color(1f, 1f, 1f, count > 0 ? 1f : 0f);
    }

    /// <summary>Draw a card into this deck: a stand-in for it appears at fromGlobalPosition and
    /// is pulled into the stack, turning face down on the way, and is freed when it arrives.
    ///
    /// Nothing fades. The card ends up as an exact copy of the back already on top of the
    /// stack, in exactly its place, so there is nothing left for a fade to hide — it simply
    /// stops being a separate card, which is what going into a deck looks like.
    ///
    /// The caller's own node is deliberately not involved. A card goes back to the deck from
    /// two completely different places — out of a 패 (HandView) and off the field
    /// (MatchScreenUI) — whose nodes are owned, laid out and freed on their own terms, and
    /// neither of them should have to keep one alive just to watch it fly. They say what card
    /// it was and where it was, and are free to dispose of their own the same instant.</summary>
    public void AbsorbCard(CardName card, Vector2 fromGlobalPosition)
    {
        CardView cardView = _cardViewScene.Instantiate<CardView>();

        // Added before it is told what to show: _Ready is what wires up its own children,
        // and AddChild is what runs it. The layer it goes into sits under the stack, so the
        // card disappears behind the deck rather than landing on top of it.
        _absorbLayer.AddChild(cardView);
        cardView.ShowFaceUp(card);

        // It is scenery, not a card anyone can do anything with: no drag, and no swallowing a
        // hover or a click meant for whatever it passes over.
        cardView.CanBeDragged = false;
        cardView.MouseFilter = MouseFilterEnum.Ignore;

        cardView.GlobalPosition = fromGlobalPosition;

        // A Control scales about its pivot, which starts at the top-left corner; without this
        // the card would flip about its own left edge and swing sideways instead of turning
        // over where it is.
        cardView.PivotOffset = cardView.Size / 2f;

        _absorbing.Add(cardView);
        SetProcess(true);
    }

    /// <summary>Only ever running while something is being absorbed; AbsorbCard switches it on
    /// and the last card to arrive switches it off again.</summary>
    public override void _Process(double delta)
    {
        float weight = 1f - Mathf.Exp(-ABSORB_RATE * (float)delta);
        Vector2 target = TopCardGlobalPosition;

        // Backwards, because an arriving card is removed from the list inside the loop.
        for (int index = _absorbing.Count - 1; index >= 0; index--)
        {
            CardView cardView = _absorbing[index];

            cardView.GlobalPosition = cardView.GlobalPosition.Lerp(target, weight);

            // The flip is the same easing at the same rate as the travel, which is what keeps
            // the two in step without either being timed: both are the same exponential, so
            // whatever fraction of the distance is left, the same fraction of the flip is left.
            // The card is edge-on exactly at the half-way point of its path and lands with the
            // turn finished, at any rate and over any distance.
            cardView.Scale = new Vector2(Mathf.Lerp(cardView.Scale.X, -1f, weight), 1f);

            // Past edge-on, so the far side is what is now facing the player. Scale.X is the
            // flip's own progress and ShownCard is whether the turn has already happened, so
            // there is no separate state saying how far through it is.
            if (cardView.Scale.X < 0f && cardView.ShownCard.HasValue)
            {
                cardView.ShowFaceDown();
            }

            if (cardView.GlobalPosition.DistanceTo(target) > ABSORB_ARRIVAL_DISTANCE)
            {
                continue;
            }

            _absorbing.RemoveAt(index);
            _absorbLayer.RemoveChild(cardView);
            cardView.QueueFree();
        }

        if (_absorbing.Count == 0)
        {
            SetProcess(false);
        }
    }
}
