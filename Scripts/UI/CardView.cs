using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>One card on screen, face up or face down. There is exactly one of these for
/// every card in the game — 일반카드, 더미, 조커 and 특수 all render through it, because a
/// subclass per card variant is the thing root CLAUDE.md rules out.
///
/// It renders and it reports a click. It does not read GameState.View, does not subscribe to
/// any Autoload signal, and does not decide what a click on it means — the owner binds the
/// node when it connects (cardView.Clicked += () =&gt; OnCardClicked(cardView);) and decides
/// there.
///
/// No card art exists yet: every CardData.CardArt is null, so every card currently draws as
/// the placeholder fill. Four things make the eventual art a drop-in rather than a relayout —
/// the 5:7 rect is fixed and identical in both modes, the name label sits *over* the art
/// instead of beside it, the 카드 종류 border is carried by its own node instead of by the
/// fill colour, and CardArt stays nullable so a half-illustrated deck still renders.</summary>
public partial class CardView : Control
{
    /// <summary>Argument-free on purpose: what this click means is the owner's business.</summary>
    [Signal] public delegate void ClickedEventHandler();

    private ColorRect _placeholderFill = null!;
    private TextureRect _artView = null!;
    private ColorRect _faceDownBack = null!;
    private Panel _typeBorder = null!;
    private Label _nameLabel = null!;
    private Panel _selectionOverlay = null!;
    private StyleBoxFlat _borderStyle = null!;

    // Draw order among sibling cards, which overlap. Raised so a card the player is
    // interacting with is never partly hidden behind the next card along. Set as a z-index
    // rather than by reordering the nodes, because node order is what HandView lays out by.
    private const int RESTING_Z_INDEX = 0;
    private const int HOVERED_Z_INDEX = 1;
    private const int DRAGGED_Z_INDEX = 2;

    private bool _isDragging;
    private bool _isHovered;
    private Vector2 _dragGrabOffset;
    private CardDropZone? _hoveredDropZone;

    /// <summary>The card this view is currently showing face up, or null while it is face
    /// down. Read-only: it lets an owner that bound the *node* recover the card without the
    /// card ever deciding anything about itself.</summary>
    public CardName? ShownCard { get; private set; }

    /// <summary>Whether picking this card up and dragging it means anything right now. Off
    /// by default; the owner (HandView) turns it on only in Play mode — during 교체/변화
    /// selection, cards are picked by click instead, and dragging one should do nothing.</summary>
    public bool CanBeDragged { get; set; }

    /// <summary>True from the moment this card is picked up to the moment it is released.
    /// HandView reads it to leave the card's position alone — a card under the cursor is the
    /// one thing it must not try to lay out.</summary>
    public bool IsDragging
    {
        get { return _isDragging; }
    }

    /// <summary>Where this card should sit instead of in the hand, or null when it belongs in
    /// the hand like any other. Set when the card is dropped on a zone. It is a target rather
    /// than a position the card writes itself: HandView eases every card toward its target the
    /// same way, so settling onto a zone and settling back into the hand are the same motion
    /// and neither can jump.</summary>
    public Vector2? DockTarget { get; private set; }

    /// <summary>Set on a brand new card so HandView places it outright instead of easing it in
    /// from wherever an unpositioned node happens to start. Cleared the first time it is laid
    /// out.</summary>
    public bool NeedsLayoutSnap { get; set; } = true;

    /// <summary>How much longer this card should stay in the deck before it starts moving to
    /// its place in the row. Another of HandView's layout hints parked on the card, for the
    /// same reason NeedsLayoutSnap is: it is per-card, and the card is the only thing that
    /// stays put while the row around it is rebuilt.
    ///
    /// It is what makes a 드로우 read as cards coming out of the deck one after another rather
    /// than a whole hand appearing at once, and what holds a refilled hand back until the old
    /// one has finished being drawn into the deck.</summary>
    public float EntryDelaySeconds { get; set; }

    /// <summary>Whether the cursor is over this card. HandView reads it to lift the card clear
    /// of the row — the cards overlap, so a hovered one is otherwise partly buried and hard to
    /// read.</summary>
    public bool IsHovered
    {
        get { return _isHovered; }
    }

    public override void _Ready()
    {
        _placeholderFill = GetNode<ColorRect>("PlaceholderFill");
        _artView = GetNode<TextureRect>("ArtView");
        _faceDownBack = GetNode<ColorRect>("FaceDownBack");
        _typeBorder = GetNode<Panel>("TypeBorder");
        _nameLabel = GetNode<Label>("NameLabel");
        _selectionOverlay = GetNode<Panel>("SelectionOverlay");

        // The scene's border stylebox is one resource shared by every instance of the scene,
        // so tinting it in place would repaint every card on screen. Each view takes a copy.
        _borderStyle = (StyleBoxFlat)_typeBorder.GetThemeStylebox("panel").Duplicate();
        _typeBorder.AddThemeStyleboxOverride("panel", _borderStyle);

        // Both are switched on only for the duration of a drag: _Process to follow the
        // cursor, _Input to catch the release that ends it.
        SetProcess(false);
        SetProcessInput(false);

        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;

        ShowFaceDown();
    }

    /// <summary>Show this card's face. Resolves the CardName through CardDatabase and falls
    /// back to the enum name when no .tres was loaded for it, so a missing resource shows a
    /// readable card instead of a blank one.</summary>
    public void ShowFaceUp(CardName card)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(card);

        string displayName;
        string description;
        Texture2D? art;
        if (cardData == null)
        {
            displayName = card.ToString();
            description = string.Empty;
            art = null;
        }
        else
        {
            displayName = cardData.DisplayName;
            description = cardData.Description;
            art = cardData.CardArt;
        }

        Color typeColor = TypeColorOf(card.GetCardType());

        _placeholderFill.Color = typeColor.Darkened(0.6f);

        // Art occupies the same rect as the placeholder and simply covers it once a .tres
        // carries one. Nothing else about the card moves when that happens.
        _artView.Texture = art;
        _artView.Visible = art != null;

        _faceDownBack.Visible = false;

        _borderStyle.BorderColor = typeColor;

        _nameLabel.Text = displayName;
        _nameLabel.Visible = true;

        // A reused node (Scripts/CLAUDE.md's slot-stability rule) might still be showing a
        // selection highlight from a choice made before this round's hand arrived; the owner
        // clears selection state explicitly on a mode change, but this is a second guard for
        // a node being handed a card for the first time.
        _selectionOverlay.Visible = false;

        TooltipText = description;
        ShownCard = card;
    }

    /// <summary>Show the back. Used for the opponent's 패, where this side is only ever told
    /// a count.</summary>
    public void ShowFaceDown()
    {
        _faceDownBack.Visible = true;
        _artView.Visible = false;
        _nameLabel.Visible = false;

        // The border node stays visible, but neutral. Tinting it by 카드 종류 here would
        // publish exactly the information the back exists to hide.
        _borderStyle.BorderColor = new Color(0.45f, 0.47f, 0.55f);
        _selectionOverlay.Visible = false;

        TooltipText = string.Empty;
        ShownCard = null;
    }

    /// <summary>Whether this card is highlighted as picked for a 교체/변화 choice. Purely
    /// visual — the node still does not know or care what "selected" is being used for.</summary>
    public void SetSelected(bool selected)
    {
        _selectionOverlay.Visible = selected;
    }

    /// <summary>Reports the click and judges nothing — whether this card may be played is
    /// the host's answer, not this node's (Scripts/CLAUDE.md). A press also picks the card up
    /// when CanBeDragged is on; Godot's own Control drag-and-drop API was tried first and
    /// dropped, because it cannot preserve the exact point a card was grabbed at (the preview
    /// always follows at a fixed offset from the cursor) and gives no hook for the card to
    /// settle rather than teleport — both were explicitly wanted, so this drags the real node
    /// by hand instead of going through that API.</summary>
    public override void _GuiInput(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (!mouseButton.Pressed)
        {
            // Never arrives while dragging — see _Input, which is what actually ends a drag.
            return;
        }

        EmitSignalClicked();

        if (CanBeDragged)
        {
            BeginDrag(mouseButton.Position);
        }

        AcceptEvent();
    }

    /// <summary>Ends a drag on mouse release. This has to be _Input rather than _GuiInput:
    /// a card is dragged out from under the cursor's own control, and Godot only delivers a
    /// release to whichever Control it decides still holds mouse focus — which a card moving
    /// itself every frame cannot be relied on to be. _Input bypasses GUI focus routing
    /// entirely, and is only enabled while a drag is actually in progress.</summary>
    public override void _Input(InputEvent inputEvent)
    {
        if (!_isDragging)
        {
            return;
        }

        if (inputEvent is InputEventMouseButton mouseButton
            && mouseButton.ButtonIndex == MouseButton.Left
            && !mouseButton.Pressed)
        {
            // Marked handled before the drag is ended, not after: on the host, releasing the
            // card that completes a round resolves it inside EndDrag, which refreshes the hand
            // and takes this very node out of the tree. GetViewport() returns null for a node
            // that is no longer in the tree, so doing this afterwards is a null dereference on
            // the most ordinary play there is.
            GetViewport().SetInputAsHandled();
            EndDrag();
        }
    }

    /// <summary>Follows the mouse every frame while a drag is in progress, keeping the same
    /// point under the cursor that was grabbed at the start rather than snapping to a corner.
    /// This is the one case where a card writes its own position — everywhere else HandView
    /// eases it toward a target — because a dragged card should track the cursor exactly, with
    /// no lag of its own.
    ///
    /// Also tracks which CardDropZone is currently under the card, so that zone can highlight
    /// itself; a zone has no other way to know a drag is happening.</summary>
    public override void _Process(double delta)
    {
        if (!_isDragging)
        {
            return;
        }

        GlobalPosition = GetGlobalMousePosition() - _dragGrabOffset;

        CardDropZone? zoneUnderMouse = CardDropZone.FindZoneContaining(GetGlobalMousePosition());
        if (zoneUnderMouse != _hoveredDropZone)
        {
            _hoveredDropZone?.SetHighlighted(false);
            _hoveredDropZone = zoneUnderMouse;
            _hoveredDropZone?.SetHighlighted(true);
        }
    }

    /// <summary>Picks the card up. Nothing is reparented and nothing is torn out of a layout:
    /// the card stays exactly where it lives in the tree and simply stops being laid out,
    /// because HandView skips a card that is being dragged. grabPositionLocal is where inside
    /// the card it was clicked, in the card's own coordinates — _Process holds that same point
    /// under the cursor for the rest of the drag.</summary>
    private void BeginDrag(Vector2 grabPositionLocal)
    {
        _dragGrabOffset = grabPositionLocal;
        _isDragging = true;

        // The hand draws after everything above it, so a card leaving the hand row already
        // renders over the field without needing to move in the tree. Lifting it clear of its
        // own siblings is all that is left.
        RefreshDrawOrder();

        SetProcess(true);
        SetProcessInput(true);
    }

    /// <summary>Released: the card is either parked on the zone it was dropped on or handed
    /// back to the hand's layout. Either way it only gains or loses a target — HandView does
    /// the actual moving, with the same easing in both cases, so there is no seam between
    /// "returning" and "resting" and no moment where the card is teleported into place.</summary>
    private void EndDrag()
    {
        _isDragging = false;
        SetProcess(false);
        SetProcessInput(false);
        RefreshDrawOrder();

        _hoveredDropZone?.SetHighlighted(false);
        CardDropZone? droppedOn = _hoveredDropZone;
        _hoveredDropZone = null;

        if (droppedOn != null)
        {
            DockInto(droppedOn);
            droppedOn.NotifyDropped(this);
            return;
        }

        ReturnToHand();
    }

    /// <summary>Parks the card on a zone. Dragging is switched off on the way in: a docked
    /// card has already been offered to the host, so picking it up again would be taking back
    /// a submission that may already have been accepted.</summary>
    private void DockInto(CardDropZone zone)
    {
        CanBeDragged = false;

        // Centred rather than corner-aligned, so a zone that isn't exactly card-sized still
        // gets the card placed sensibly — the zone is meant to work at any size or position.
        Rect2 zoneRect = zone.GetGlobalRect();
        DockTarget = zoneRect.Position + ((zoneRect.Size - Size) / 2f);
    }

    /// <summary>Gives the card back to the hand's layout. Also the way out of a dock the host
    /// turned down — MatchScreenUI calls this when a submission is rejected, since nothing
    /// else would take the card back off the zone.</summary>
    public void ReturnToHand()
    {
        DockTarget = null;
        CanBeDragged = true;
    }

    private void OnMouseEntered()
    {
        _isHovered = true;
        RefreshDrawOrder();
    }

    private void OnMouseExited()
    {
        _isHovered = false;
        RefreshDrawOrder();
    }

    private void RefreshDrawOrder()
    {
        if (_isDragging)
        {
            ZIndex = DRAGGED_Z_INDEX;
            return;
        }

        if (_isHovered)
        {
            ZIndex = HOVERED_Z_INDEX;
            return;
        }

        ZIndex = RESTING_Z_INDEX;
    }

    /// <summary>The one colour a 카드 종류 gets. The placeholder fill is a darkened version
    /// of it rather than a second colour, so the fill and the border can never disagree.</summary>
    private static Color TypeColorOf(CardType cardType)
    {
        switch (cardType)
        {
            case CardType.Normal:
                return new Color(0.36f, 0.62f, 0.92f);

            case CardType.Dummy:
                return new Color(0.58f, 0.60f, 0.64f);

            case CardType.Joker:
                return new Color(0.85f, 0.32f, 0.34f);

            case CardType.Special:
                return new Color(0.92f, 0.74f, 0.30f);

            default:
                return new Color(0.50f, 0.50f, 0.50f);
        }
    }
}
