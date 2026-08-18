using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>What a click on a hand card currently means. Play is the default — every card,
/// 교체/변화 included, is a single click that submits it. The other two only apply while a
/// choice is outstanding for a card this hand's owner already played.</summary>
public enum HandSelectionMode
{
    Play,
    SelectMultipleForSwap,
    SelectOneForTransform,
}

/// <summary>A row of CardViews — my own 패 face up and clickable, or the opponent's 패 as
/// that many backs. Both are the same row because the opponent's hand has to be able to
/// animate later, which a number cannot.
///
/// Driven entirely by method call from MatchScreenUI; it reads no View and subscribes to no
/// Autoload signal. A click's meaning depends on SelectionMode, set by the owner — in Play
/// mode it goes straight out as an intent via GameState; in the two selection modes it only
/// toggles a highlight and reports through SelectionChanged, and the owner decides when to
/// actually send anything.</summary>
public partial class HandView : HBoxContainer
{
    private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match/CardView.tscn";

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

    /// <summary>Show my 패, face up and clickable. A card that was already in hand keeps the
    /// node it had — rebuilding the whole row on every change throws away which node the
    /// player was looking at (Scripts/CLAUDE.md, "Card presentation").</summary>
    public void ShowFaceUpHand(IReadOnlyList<CardName> cards)
    {
        List<CardName> arrived = new List<CardName>(cards);

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
    /// this method does not decide it, only dispatches to it.</summary>
    private void OnCardClicked(CardView cardView)
    {
        switch (_selectionMode)
        {
            case HandSelectionMode.Play:
                PlayCard(cardView);
                return;

            case HandSelectionMode.SelectMultipleForSwap:
                ToggleSwapSelection(cardView);
                return;

            case HandSelectionMode.SelectOneForTransform:
                SelectTransformSource(cardView);
                return;
        }
    }

    private static void PlayCard(CardView cardView)
    {
        CardName? card = cardView.ShownCard;
        if (!card.HasValue)
        {
            return;
        }

        // No judgment here — the click is handed to GameState as an intent, and the host
        // validates it (Scripts/CLAUDE.md: "a node that receives input does not judge it").
        GameState.Instance!.RequestCardPlay(card.Value);
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

        // Removed as well as freed: QueueFree only takes effect at the end of the frame, so
        // a row rebuilt in the same frame would briefly lay out the leaving card too.
        RemoveChild(cardView);
        cardView.QueueFree();
    }
}
