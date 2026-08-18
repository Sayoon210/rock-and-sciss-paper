using System.Collections.Generic;
using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>What a hand card is for right now. Play is the default — every card, 교체/변화
/// included, is submitted by picking it up and dragging it onto a drop zone (CardDropZone),
/// not by clicking it. The other two only apply while a choice is outstanding for a card
/// this hand's owner already played, and stay click-to-toggle.</summary>
public enum HandSelectionMode
{
    Play,
    SelectMultipleForSwap,
    SelectOneForTransform,
}

/// <summary>A row of CardViews — my own 패 face up and draggable, or the opponent's 패 as
/// that many backs. Both are the same row because the opponent's hand has to be able to
/// animate later, which a number cannot.
///
/// Lays its cards out by hand instead of being a Container. A Container places a child the
/// instant anything changes, which makes every hand change a set of jumps: picking a card up
/// snaps the rest closed, putting it back snaps them open, and a card rejoining the row is
/// teleported into its slot at the end of whatever animation brought it there. Here every card
/// instead eases toward a target position each frame, so all of those become one continuous
/// motion and a card that is picked up, moved and released never changes position discontinuously.
///
/// Driven entirely by method call from MatchScreenUI; it reads no View and subscribes to no
/// Autoload signal. What a hand card responds to depends on SelectionMode, set by the owner —
/// in Play mode a card is draggable and a click does nothing (CardView's own drag handling and
/// a CardDropZone elsewhere are what turn a drop into a request); in the two selection modes
/// dragging is off and a click toggles a highlight, reported through SelectionChanged, and
/// the owner decides when to actually send anything.</summary>
public partial class HandView : Control
{
    private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match/CardView.tscn";

    // Cards overlap by this much when the row is full enough to need it; a hand that fits
    // keeps them at this spacing rather than spreading across the whole width.
    private const float CARD_OVERLAP_STEP = 160f;

    /// <summary>How sharply a card converges on its target, as a rate rather than a duration.
    /// Applied as 1 - e^(-rate * delta) so the easing is identical at any frame rate, and so a
    /// target that moves mid-flight (the rest of the hand closing up while a card is still on
    /// its way back) is followed smoothly instead of restarting an animation.</summary>
    private const float SETTLE_RATE = 14f;

    // How far a hovered card rises out of the row. It is only a shifted target, so the same
    // easing carries the card up and back down again.
    private const float HOVER_LIFT = 32f;

    /// <summary>Fires whenever the selection changes in either selection mode, so the owner
    /// can update a count or enable a confirm button without polling every frame.</summary>
    [Signal] public delegate void SelectionChangedEventHandler();

    private PackedScene _cardViewScene = null!;

    // Parallel to this node's children, in the same order.
    private readonly List<CardView> _slots = new List<CardView>();

    private HandSelectionMode _selectionMode = HandSelectionMode.Play;
    private readonly List<CardView> _selectedForSwap = new List<CardView>();
    private CardView? _selectedForTransform;

    public override void _Ready()
    {
        _cardViewScene = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH);
    }

    /// <summary>Eases every card toward where it currently belongs. Run every frame rather
    /// than only when something changes, because the targets themselves move: a card taken out
    /// of the hand shifts the rest, and a card on its way back is chasing slots that are
    /// sliding as it travels.</summary>
    public override void _Process(double delta)
    {
        float weight = 1f - Mathf.Exp(-SETTLE_RATE * (float)delta);

        int restingIndex = 0;
        int restingCount = CountRestingCards();

        foreach (CardView cardView in _slots)
        {
            // A card under the cursor positions itself; anything this method did to it would
            // fight the mouse.
            if (cardView.IsDragging)
            {
                continue;
            }

            Vector2 target;
            if (cardView.DockTarget.HasValue)
            {
                target = cardView.DockTarget.Value - GlobalPosition;
            }
            else
            {
                target = RestingPositionOf(restingIndex, restingCount);
                restingIndex++;

                // Only a card showing its face lifts: the opponent's row is all backs, and
                // there is nothing there to read more closely.
                if (cardView.IsHovered && cardView.ShownCard.HasValue)
                {
                    target.Y -= HOVER_LIFT;
                }
            }

            if (cardView.NeedsLayoutSnap)
            {
                cardView.NeedsLayoutSnap = false;
                cardView.Position = target;
                continue;
            }

            cardView.Position = cardView.Position.Lerp(target, weight);
        }
    }

    /// <summary>Where the nth card of a row of `count` sits. Centred, and overlapping by a
    /// fixed step rather than spreading to fill — a two-card hand should not have its cards at
    /// opposite ends of the screen.</summary>
    private Vector2 RestingPositionOf(int index, int count)
    {
        if (count <= 0)
        {
            return Vector2.Zero;
        }

        float cardWidth = 0f;
        float cardHeight = 0f;
        if (_slots.Count > 0)
        {
            cardWidth = _slots[0].Size.X;
            cardHeight = _slots[0].Size.Y;
        }

        // Tighten the step further if even the overlapped row would not fit.
        float step = CARD_OVERLAP_STEP;
        float widthNeeded = cardWidth + (step * (count - 1));
        if (widthNeeded > Size.X && count > 1)
        {
            step = (Size.X - cardWidth) / (count - 1);
            widthNeeded = Size.X;
        }

        float startX = (Size.X - widthNeeded) / 2f;
        float y = (Size.Y - cardHeight) / 2f;

        return new Vector2(startX + (step * index), y);
    }

    private int CountRestingCards()
    {
        int resting = 0;
        foreach (CardView cardView in _slots)
        {
            if (!cardView.IsDragging && !cardView.DockTarget.HasValue)
            {
                resting++;
            }
        }

        return resting;
    }

    /// <summary>Show my 패, face up and clickable. A card that was already in hand keeps the
    /// node it had — rebuilding the whole row on every change throws away which node the
    /// player was looking at (Scripts/CLAUDE.md, "Card presentation").</summary>
    public void ShowFaceUpHand(IReadOnlyList<CardName> cards)
    {
        List<CardName> arrived = new List<CardName>(cards);

        // A docked card has already been handed to the host, and the hand arriving here is the
        // authoritative one that answers what became of it. It is dropped before the matching
        // below rather than left to take part in it: matching is by card name, so a docked
        // card could otherwise claim the slot of an identical card still in hand — stranding
        // itself on the zone forever and deleting a real hand card in its place. If the card
        // does turn out to still be in hand, the pass below simply gives it a fresh slot.
        for (int slot = _slots.Count - 1; slot >= 0; slot--)
        {
            if (_slots[slot].DockTarget.HasValue)
            {
                RemoveSlot(slot);
            }
        }

        // Anything still in hand claims its existing slot and drops out of `arrived`;
        // whatever is left over at the end is genuinely new and needs a slot.
        for (int slot = _slots.Count - 1; slot >= 0; slot--)
        {
            CardName? shown = _slots[slot].ShownCard;
            if (shown.HasValue && arrived.Remove(shown.Value))
            {
                continue;
            }

            RemoveSlot(slot);
        }

        foreach (CardName card in arrived)
        {
            CardView cardView = AddSlot();
            cardView.ShowFaceUp(card);
            cardView.Clicked += () => { OnCardClicked(cardView); };
        }

        ApplyDragEligibility();
    }

    /// <summary>Show the opponent's 패 as backs. A count is all this side is ever told, and
    /// all it is ever allowed to know.</summary>
    public void ShowFaceDownCards(int count)
    {
        while (_slots.Count > count)
        {
            RemoveSlot(_slots.Count - 1);
        }

        while (_slots.Count < count)
        {
            CardView cardView = AddSlot();
            cardView.ShowFaceDown();
        }
    }

    /// <summary>Switch back to plain play mode. A no-op if already there, so calling it on
    /// every refresh that isn't a choice prompt doesn't repaint anything.</summary>
    public void SetSelectionModeNone()
    {
        if (_selectionMode == HandSelectionMode.Play)
        {
            return;
        }

        _selectionMode = HandSelectionMode.Play;
        ClearSelection();
        ResetCardOpacity();
        ApplyDragEligibility();
    }

    /// <summary>교체: any number of hand cards may be toggled. A no-op if already in this
    /// mode, so the owner can call it on every refresh without losing the player's picks
    /// mid-selection.</summary>
    public void SetSelectionModeForSwap()
    {
        if (_selectionMode == HandSelectionMode.SelectMultipleForSwap)
        {
            return;
        }

        _selectionMode = HandSelectionMode.SelectMultipleForSwap;
        ClearSelection();
        ResetCardOpacity();
        ApplyDragEligibility();
    }

    /// <summary>변화: exactly one hand card may be picked as the card to change. Same
    /// no-op-if-unchanged rule as SetSelectionModeForSwap.
    ///
    /// Only a 일반카드/더미카드 may ever be a legal source (DESIGN.md, TransformEffect.Validate)
    /// — anything else in hand is dimmed and does not respond to a click. This is the
    /// affordance Scripts/CLAUDE.md allows ("greying out is fine, but it is not validation");
    /// the host still re-checks the choice regardless.</summary>
    public void SetSelectionModeForTransformSource()
    {
        if (_selectionMode == HandSelectionMode.SelectOneForTransform)
        {
            return;
        }

        _selectionMode = HandSelectionMode.SelectOneForTransform;
        ClearSelection();
        ApplyTransformEligibilityDimming();
        ApplyDragEligibility();
    }

    /// <summary>The cards currently toggled on in SelectMultipleForSwap mode. Tracked by
    /// node rather than by CardName so two copies of the same card in hand can be told
    /// apart — selecting one Rock must not silently also count a second one.</summary>
    public IReadOnlyList<CardName> SwapSelection
    {
        get
        {
            var cards = new List<CardName>();
            foreach (CardView cardView in _selectedForSwap)
            {
                if (cardView.ShownCard.HasValue)
                {
                    cards.Add(cardView.ShownCard.Value);
                }
            }

            return cards;
        }
    }

    /// <summary>The card picked in SelectOneForTransform mode, or null until one is.</summary>
    public CardName? TransformSourceSelection
    {
        get
        {
            return _selectedForTransform?.ShownCard;
        }
    }

    /// <summary>The click's meaning depends entirely on SelectionMode, which the owner set —
    /// this method does not decide it, only dispatches to it. Play mode has nothing for a
    /// plain click to do: submitting a card is CardView's drag gesture landing on a
    /// CardDropZone, not a click.</summary>
    private void OnCardClicked(CardView cardView)
    {
        switch (_selectionMode)
        {
            case HandSelectionMode.SelectMultipleForSwap:
                ToggleSwapSelection(cardView);
                return;

            case HandSelectionMode.SelectOneForTransform:
                SelectTransformSource(cardView);
                return;
        }
    }

    /// <summary>Clicking a selected card again deselects it — the whole selection is toggled
    /// per card, with no separate "clear" control needed.</summary>
    private void ToggleSwapSelection(CardView cardView)
    {
        if (_selectedForSwap.Remove(cardView))
        {
            cardView.SetSelected(false);
        }
        else
        {
            _selectedForSwap.Add(cardView);
            cardView.SetSelected(true);
        }

        EmitSignalSelectionChanged();
    }

    /// <summary>Clicking a different card moves the pick; clicking the current pick again
    /// clears it. Only one card can be "the one to change" at a time. A card ApplyTransform-
    /// EligibilityDimming left dimmed does not respond — dimming and click-eligibility are
    /// kept as one rule so they can never disagree about which cards are pickable.</summary>
    private void SelectTransformSource(CardView cardView)
    {
        if (!cardView.ShownCard.HasValue || !IsTransformable(cardView.ShownCard.Value))
        {
            return;
        }

        if (_selectedForTransform == cardView)
        {
            cardView.SetSelected(false);
            _selectedForTransform = null;
        }
        else
        {
            _selectedForTransform?.SetSelected(false);
            _selectedForTransform = cardView;
            cardView.SetSelected(true);
        }

        EmitSignalSelectionChanged();
    }

    private static bool IsTransformable(CardName card)
    {
        CardType type = card.GetCardType();
        return type == CardType.Normal || type == CardType.Dummy;
    }

    /// <summary>변화's eligible cards depend on what is actually in hand, unlike the "into"
    /// palette (MatchScreenUI), which is the same fixed set regardless of hand contents.</summary>
    private void ApplyTransformEligibilityDimming()
    {
        foreach (CardView cardView in _slots)
        {
            bool eligible = cardView.ShownCard.HasValue && IsTransformable(cardView.ShownCard.Value);
            cardView.Modulate = eligible ? Colors.White : new Color(1f, 1f, 1f, 0.35f);
        }
    }

    private void ResetCardOpacity()
    {
        foreach (CardView cardView in _slots)
        {
            cardView.Modulate = Colors.White;
        }
    }

    /// <summary>Only Play mode drags to submit; the two selection modes pick by click
    /// instead, so a card must not also start a drag while one of them is active.</summary>
    private void ApplyDragEligibility()
    {
        bool canDrag = _selectionMode == HandSelectionMode.Play;
        foreach (CardView cardView in _slots)
        {
            cardView.CanBeDragged = canDrag;
        }
    }

    private void ClearSelection()
    {
        foreach (CardView cardView in _selectedForSwap)
        {
            cardView.SetSelected(false);
        }

        _selectedForSwap.Clear();

        _selectedForTransform?.SetSelected(false);
        _selectedForTransform = null;
    }

    private CardView AddSlot()
    {
        CardView cardView = _cardViewScene.Instantiate<CardView>();

        // Added before it is told what to show: _Ready is what wires up its own children,
        // and AddChild is what runs it.
        AddChild(cardView);
        _slots.Add(cardView);
        return cardView;
    }

    private void RemoveSlot(int slot)
    {
        CardView cardView = _slots[slot];
        _slots.RemoveAt(slot);

        // A card leaving the row (played, swapped away, or simply not this round's hand any
        // more) must not linger in either selection set — nothing else will remove it once
        // the node itself is gone.
        _selectedForSwap.Remove(cardView);
        if (_selectedForTransform == cardView)
        {
            _selectedForTransform = null;
        }

        // Removed as well as freed: QueueFree only takes effect at the end of the frame, so a
        // row rebuilt in the same frame would briefly lay out the leaving card too.
        RemoveChild(cardView);
        cardView.QueueFree();
    }
}
