using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>A row of CardViews — my own 패 face up and clickable, or the opponent's 패 as
/// that many backs. Both are the same row because the opponent's hand has to be able to
/// animate later, which a number cannot.
///
/// Driven entirely by method call from MatchScreenUI; it reads no View and subscribes to no
/// Autoload signal. A click goes straight out as an intent — GameState is the only thing
/// that knows whether this side is the host, so nothing here branches on that.</summary>
public partial class HandView : HBoxContainer
{
    private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match/CardView.tscn";

    private PackedScene _cardViewScene = null!;

    // Parallel to this node's children, in the same order.
    private readonly List<CardView> _slots = new List<CardView>();

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

    /// <summary>The click is turned into an intent and handed to GameState, which validates
    /// it on the host. Nothing here asks whether the card is playable.</summary>
    private void OnCardClicked(CardView cardView)
    {
        CardName? card = cardView.ShownCard;
        if (!card.HasValue)
        {
            return;
        }

        GameState.Instance!.RequestCardPlay(card.Value);
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

        // Removed as well as freed: QueueFree only takes effect at the end of the frame, so
        // a row rebuilt in the same frame would briefly lay out the leaving card too.
        RemoveChild(cardView);
        cardView.QueueFree();
    }
}
