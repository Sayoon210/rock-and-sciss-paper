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
    private const string CARD_VANISH_EFFECT_SCENE_PATH = "res://Scenes/Match/CardVanishEffect.tscn";

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

    /// <summary>How long each successive card drawn in the same refresh waits before it leaves
    /// the deck. A whole 멀리건 arriving at once is one blob of six cards; a stagger this small
    /// is the difference between that and cards being dealt.</summary>
    private const float ENTRY_STAGGER_SECONDS = 0.08f;

    /// <summary>Extra wait given to every card entering while cards are still on their way back
    /// into the deck. 리셋 and 교체 are "the old hand goes in, a new one comes out" — without
    /// this the two halves overlap and read as one shuffle at the deck instead of a sequence.</summary>
    private const float ENTRY_DELAY_AFTER_RETURN_SECONDS = 0.35f;

    /// <summary>Fires whenever the selection changes in either selection mode, so the owner
    /// can update a count or enable a confirm button without polling every frame.</summary>
    [Signal] public delegate void SelectionChangedEventHandler();

    private PackedScene _cardViewScene = null!;
    private PackedScene _cardVanishEffectScene = null!;

    // The row, in order. Not every child is in here — a card being taken apart by 변화 stays a
    // child so it can still be drawn, but is out of this list so nothing lays it out.
    private readonly List<CardView> _slots = new List<CardView>();

    private HandSelectionMode _selectionMode = HandSelectionMode.Play;
    private readonly List<CardView> _selectedForSwap = new List<CardView>();
    private CardView? _selectedForTransform;

    // The exact card 변화 was asked about, kept from the moment the choice is sent until the
    // new 패 comes back. A node and not a CardName: the deck holds three of each 일반카드 and
    // four 더미, so a 패 nearly always has duplicates, and looking the card up by name again
    // would find whichever copy sits first in the row instead of the one that was clicked.
    private CardView? _cardBeingTransformed;

    // Where this row's cards come from and go back to. Null until the owner supplies one, in
    // which case cards appear in and vanish from the row exactly as they did before there was
    // a deck on screen at all.
    private DeckView? _deckView;

    public override void _Ready()
    {
        _cardViewScene = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH);
        _cardVanishEffectScene = GD.Load<PackedScene>(CARD_VANISH_EFFECT_SCENE_PATH);
    }

    /// <summary>Take note of which card 변화 is being asked about, while the selection that
    /// names it still exists. The answer only comes back from the host after the selection has
    /// been cleared, so without this there is nothing left pointing at the card the player
    /// actually clicked — only its name, which several cards in the row may share.</summary>
    public void RememberTransformSource()
    {
        _cardBeingTransformed = _selectedForTransform;
    }

    /// <summary>변화: show the remembered card becoming another one, in place. The card that
    /// arrives is put down first, in the slot the old one was holding, and the old one is left
    /// lying on top of it to come apart — so what the player sees is the old card crumbling off
    /// the new one, rather than a card leaving and an unrelated one turning up.
    ///
    /// Call it before handing the new 패 to ShowFaceUpHand. By then the old card is out of the
    /// row and the new one already holds the slot, so the name matching there keeps it — which
    /// is also what stops the old card being flown to the 덱 and the new one dealt out of it.
    /// 변화 never touches the deck, so that is the wrong picture as well as the wrong feel.</summary>
    public void TransformRememberedCardInto(CardName target)
    {
        CardView? remembered = _cardBeingTransformed;
        _cardBeingTransformed = null;

        // Nothing remembered, or the row was rebuilt from under it before the answer arrived.
        if (remembered == null || !IsInstanceValid(remembered))
        {
            return;
        }

        int slot = _slots.IndexOf(remembered);
        if (slot < 0)
        {
            return;
        }

        CardView oldCard = _slots[slot];
        Vector2 slotPosition = oldCard.Position;
        DetachSlot(slot);

        // Into the same place in the row, not onto the end of it: the transformed card has not
        // moved, and appending would send it sliding across to the far side.
        CardView newCard = AddSlotAt(slot);
        newCard.ShowFaceUp(target);
        newCard.Clicked += () => { OnCardClicked(newCard); };
        newCard.NeedsLayoutSnap = false;
        newCard.Position = slotPosition;

        // Hung on the new card rather than left lying beside it. The row re-centres itself the
        // moment the round's draw lands — every card slides across — and a card that has been
        // taken out of the layout would stay behind while the card replacing it walked off,
        // which is exactly what it looked like. As a child it is carried along for free, and a
        // node is drawn after its parent, so it also covers the card it is coming off.
        RemoveChild(oldCard);
        newCard.AddChild(oldCard);
        oldCard.Position = Vector2.Zero;

        // The new card takes the child position its slot has, because the row's cards overlap
        // and it is child order that decides which of two neighbours is on top. Appended, the
        // transformed card would be drawn over both and appear to jump forward in the fan.
        MoveChild(newCard, slot);

        CardVanishEffect effect = _cardVanishEffectScene.Instantiate<CardVanishEffect>();
        AddChild(effect);
        effect.PlayAndFreeAfterwards(oldCard);
    }

    /// <summary>Point this row at the 덱 its cards belong to. Supplied by the owner rather than
    /// looked up here, like everything else about this row — MatchScreenUI is the one script in
    /// the scene that knows how the screen is put together.</summary>
    public void SetDeckSource(DeckView deckView)
    {
        _deckView = deckView;
    }

    /// <summary>Eases every card toward where it currently belongs. Run every frame rather
    /// than only when something changes, because the targets themselves move: a card taken out
    /// of the hand shifts the rest, and a card on its way back is chasing slots that are
    /// sliding as it travels.</summary>
    public override void _Process(double delta)
    {
        // Nothing sensible to lay out against until the containing row has given this node a
        // width. Skipping is what keeps a card's one-time snap from landing on a position
        // computed from a zero-width row and then having to slide in from it.
        if (Size.X <= 0f)
        {
            return;
        }

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

            // A new card starts on the deck and eases out of it, which is the whole of the
            // 드로우 animation — the easing every other card is already under does the moving.
            // Done here rather than when the card was created because this method is the thing
            // that refuses to run until the row has a real width, so reaching it is also what
            // guarantees the deck has been laid out and has a real position to start from.
            if (cardView.NeedsLayoutSnap)
            {
                cardView.NeedsLayoutSnap = false;
                cardView.Position = DeckLocalPosition() ?? target;
            }

            // Still in the deck. Its place in the row is already being held open above, so the
            // rest of the hand spreads first and the card then slides into the gap.
            if (cardView.EntryDelaySeconds > 0f)
            {
                cardView.EntryDelaySeconds -= (float)delta;
                continue;
            }

            cardView.Position = cardView.Position.Lerp(target, weight);
        }
    }

    /// <summary>Where the top of the deck is in this row's own coordinates, or null when no
    /// deck was supplied. The deck is a sibling of this row rather than a child, so the
    /// conversion goes through screen coordinates; it is read every frame rather than cached
    /// because a window resize moves both of them.</summary>
    private Vector2? DeckLocalPosition()
    {
        if (_deckView == null)
        {
            return null;
        }

        return _deckView.TopCardGlobalPosition - GlobalPosition;
    }

    /// <summary>When the nth card drawn in this refresh should leave the deck. Zero when there
    /// is no deck to leave from — holding the card back would then just be a pause with nothing
    /// happening in it.</summary>
    private float EntryDelayFor(int indexAmongDrawnCards)
    {
        if (_deckView == null)
        {
            return 0f;
        }

        float delay = ENTRY_STAGGER_SECONDS * indexAmongDrawnCards;
        if (_deckView.IsAbsorbingCards)
        {
            delay += ENTRY_DELAY_AFTER_RETURN_SECONDS;
        }

        return delay;
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

        int drawnIndex = 0;
        foreach (CardName card in arrived)
        {
            CardView cardView = AddSlot();
            cardView.ShowFaceUp(card);
            cardView.EntryDelaySeconds = EntryDelayFor(drawnIndex);
            drawnIndex++;
            cardView.Clicked += () => { OnCardClicked(cardView); };
        }

        ApplyDragEligibility();
    }

    /// <summary>Put this whole row back into the deck, whichever way up its cards are.
    /// 리셋 replaces both 패 outright (DESIGN.md), and that is a change neither row can show
    /// from the hand that arrives afterwards: the opponent's row is only ever a count, so a
    /// redraw of the same size leaves it identical, and even my own row would sit still if the
    /// new hand happened to contain the same cards, since a slot whose card is still there is
    /// deliberately kept.
    ///
    /// Cards the player has hold of, or has already played, are left alone — neither is on its
    /// way to the deck.</summary>
    public void ReturnWholeHandToDeck()
    {
        if (_deckView == null)
        {
            return;
        }

        for (int slot = _slots.Count - 1; slot >= 0; slot--)
        {
            CardView cardView = _slots[slot];
            if (cardView.IsDragging || cardView.DockTarget.HasValue)
            {
                continue;
            }

            _deckView.AbsorbCard(cardView.ShownCard, cardView.GlobalPosition);
            DiscardSlot(slot);
        }
    }

    /// <summary>Show the opponent's 패 as backs. A count is all this side is ever told, and
    /// all it is ever allowed to know.</summary>
    public void ShowFaceDownCards(int count)
    {
        while (_slots.Count > count)
        {
            RemoveSlot(_slots.Count - 1);
        }

        int drawnIndex = 0;
        while (_slots.Count < count)
        {
            CardView cardView = AddSlot();
            cardView.ShowFaceDown();
            cardView.EntryDelaySeconds = EntryDelayFor(drawnIndex);
            drawnIndex++;
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
    /// instead, so a card must not also start a drag while one of them is active.
    ///
    /// A card parked on a drop zone is left alone: it has already been handed to the host, and
    /// switching its dragging back on here would let a submission be picked up again and
    /// played twice.</summary>
    private void ApplyDragEligibility()
    {
        bool canDrag = _selectionMode == HandSelectionMode.Play;
        foreach (CardView cardView in _slots)
        {
            if (cardView.DockTarget.HasValue)
            {
                continue;
            }

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
        return AddSlotAt(_slots.Count);
    }

    private CardView AddSlotAt(int slot)
    {
        CardView cardView = _cardViewScene.Instantiate<CardView>();

        // Added before it is told what to show: _Ready is what wires up its own children,
        // and AddChild is what runs it.
        AddChild(cardView);
        _slots.Insert(slot, cardView);
        return cardView;
    }

    private void RemoveSlot(int slot)
    {
        CardView cardView = _slots[slot];

        // A face-up card leaving my hand went back to the deck: 교체 and 리셋 are exactly that,
        // and 변화 swapping one card for another reads the same way. The three cases that are
        // not are excluded here — a face-down card is the opponent's, and their hand shrinking
        // usually means they *played* something rather than returned it; a card parked on a
        // drop zone was likewise played, and the field's own card view shows it from here on;
        // and a card still under the cursor is not going anywhere the player did not put it.
        //
        // The deck flies a stand-in of its own from where this card is right now, so this node
        // is still disposed of below exactly as it always was — a card in flight is never a
        // hand card that merely stopped being laid out.
        if (_deckView != null
            && cardView.ShownCard.HasValue
            && !cardView.DockTarget.HasValue
            && !cardView.IsDragging)
        {
            _deckView.AbsorbCard(cardView.ShownCard.Value, cardView.GlobalPosition);
        }

        DiscardSlot(slot);
    }

    /// <summary>Take a card out of the row without disposing of it, for a caller that has its
    /// own plans for the node. It stops being laid out, stops answering the mouse, and is out
    /// of both selection sets — everything DiscardSlot does except the disposal.</summary>
    private CardView DetachSlot(int slot)
    {
        CardView cardView = _slots[slot];
        _slots.RemoveAt(slot);

        // A card leaving the row (played, swapped away, transformed, or simply not this
        // round's hand any more) must not linger in either selection set — nothing else will
        // remove it once the node is out of the row.
        _selectedForSwap.Remove(cardView);
        if (_selectedForTransform == cardView)
        {
            _selectedForTransform = null;
        }

        cardView.CanBeDragged = false;
        cardView.MouseFilter = MouseFilterEnum.Ignore;

        return cardView;
    }

    /// <summary>Drop a slot and free the node in it, with no animation of any kind — the
    /// disposal half of RemoveSlot, shared with ReturnWholeHandToDeck, which decides for
    /// itself where the card is going first.</summary>
    private void DiscardSlot(int slot)
    {
        CardView cardView = DetachSlot(slot);

        // Removed as well as freed: QueueFree only takes effect at the end of the frame, so a
        // row rebuilt in the same frame would briefly lay out the leaving card too.
        RemoveChild(cardView);
        cardView.QueueFree();
    }
}
