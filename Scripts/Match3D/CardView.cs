using System;
using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>One card as a thing on the table: a thin slab with the art on one side and the
/// shared back image on the other. Which side the player sees is the node's rotation, not a
/// visibility flag — the same as a real card, and what BeginPoseAnimation's reveal flip turns.
///
/// Replaces the Control-based CardView now in Deprecated/. That one drew a nameplate, a type
/// border and a tooltip; none of those survive as-is in 3D (Godot has no 3D tooltip and no
/// layout containers), so they are deliberately absent here rather than reimplemented.
///
/// Pointer input lives here (Scripts/CLAUDE.md: "input handling lives in the node that owns
/// it"), in two tiers. Every card shows the hover outline. A card someone called EnableGrab
/// on (a hand card — HandView does this; the played-card slabs on the table never get it) can
/// also be grabbed: hold to pick it up, slide it up past a threshold to arm submission (the
/// outline turns the armed color), release to let the grab's owner send it. This class judges
/// only the gesture, never whether the play is legal — that stays with the host.
///
/// Not [Tool] — [Tool] usage in this scene is being kept to CharacterIdlePose only for now, to
/// narrow down a runtime freeze.</summary>
public partial class CardView : Node3D
{
    // The card's size, thickness and corner radius all live in RoundedCardMesh, which builds
    // the geometry these nodes draw. The .tscn holds only the node structure and the two
    // materials that are the same on every card.
    private const string FRONT_MESH_PATH = "Front";
    private const string BACK_MESH_PATH = "Back";
    private const string EDGE_MESH_PATH = "Rim";
    private const string HIGHLIGHT_MESH_PATH = "Highlight";
    private const string CALLOUT_PATH = "Callout";
    private const string PICK_AREA_PATH = "PickArea";
    private const string PICK_SHAPE_PATH = "PickArea/CollisionShape3D";

    /// <summary>Shown on the face of a card whose ECardName has no art yet. Six of the nine
    /// cards are in that state — see ASSETS-3D.md, where the missing art is the open blocker.
    /// Cards without art are not distinguishable from each other until it exists.</summary>
    private static readonly Color PLACEHOLDER_FACE_COLOR = new Color(0.62f, 0.60f, 0.56f);

    // Outline colors, driven from here rather than by editing the .tscn material — which is
    // duplicated per card in _Ready precisely so one card's armed outline cannot brighten every
    // other card's with it (the same shared-resource reasoning as _frontMaterial).
    //
    // The two states are told apart by BRIGHTNESS, not by hue. They used to be yellow and green,
    // which MonochromeExceptRed.gdshader flattens to the same grey — the screen keeps no hue but
    // red, so a signal carried by color is a signal the player cannot receive. Value survives
    // that pass untouched, and reads at a glance besides.
    private static readonly Color HOVER_OUTLINE_COLOR = new Color(0.45f, 0.45f, 0.45f);
    private static readonly Color SUBMIT_ARMED_OUTLINE_COLOR = new Color(1f, 1f, 1f);

    // What the callout above the card says, in the same two tiers as the outline: the card under
    // the pointer is marked, a card lifted far enough to send says so in a word.
    //
    // The arrow is a glyph rather than a MATCH_ symbol on purpose — it is the same mark in every
    // language, so strings.csv would carry one row with two identical cells. The word is a symbol
    // like any other player-facing text.
    private const string HOVER_CALLOUT_ARROW = "▼";
    private const string SUBMIT_CALLOUT_SYMBOL = "MATCH_ACTION_SUBMIT";

    // Grab feel. A held card travels straight up its own row and nowhere else: sideways drag
    // is ignored outright, and downward drag does not push it below where it started. The
    // gesture is "lift this one out", so a card that could also slide sideways only invited
    // dragging it over its neighbours.
    private const float GRAB_FOLLOW_METERS_PER_PIXEL = 0.0005f;
    private const float SNAP_BACK_SECONDS = 0.12f;

    // How far up the card has to be pulled before releasing it submits, as a fraction of
    // viewport HEIGHT rather than a pixel count. A fixed count means two different gestures on
    // two window sizes — this project runs at 1600x1000 standalone and 960x600 from the editor,
    // where the same pixel figure is a quarter of the screen in one and nearly half in the
    // other. Deliberately a long pull: submitting is the one irreversible thing in a round, and
    // a short one armed while the player was still only looking at the card.
    private const float SUBMIT_THRESHOLD_VIEWPORT_FRACTION = 0.25f;

    // How much of the card's own face color is added back in regardless of scene lighting.
    // The match's lights are narrow spots over the table (MatchWorld.tscn) with a dim ambient
    // floor, tuned for the characters and picked up as an afterthought by whatever a card
    // happens to be sitting under -- a hand card a few centimetres outside a cone reads as
    // solid black. Emission set to mirror the card's own albedo (below) keeps every card
    // legible at that same brightness no matter where it sits or how the light rig is later
    // retuned, rather than this needing its own light aimed at the hand.
    //
    // Kept low on purpose: emission is flat and uniform across the whole face, so it has no
    // shading of its own -- push it too far and it drowns the actual shading the spotlight was
    // giving the art (creases, print lines, the difference between a fold and flat card stock),
    // which reads as the card going hazy/washed-out rather than legible. This value is a floor
    // for total darkness, not a general brightness boost -- it should be barely noticeable
    // wherever the card is already lit.
    private const float FACE_EMISSION_ENERGY = 0.22f;

    // A held card is pulled toward the camera along the hand's own facing. Hand cards sit
    // coplanar in a row, so a card dragged sideways lands exactly on top of its neighbour and
    // the two z-fight; lifting the held one out of the shared plane is what stops that. Local
    // to the hand node, whose own parent halves it — this is 5mm of real separation, an order
    // of magnitude past the card's own 0.5mm thickness.
    private const float GRAB_LIFT_TOWARD_CAMERA_METERS = 0.01f;

    /// <summary>Which card this is currently showing, or null while it is face down. The
    /// submit gesture has to be able to say what was grabbed, and a card that is face down is
    /// one this screen is not allowed to name.</summary>
    public ECardName? ShownCard { get; private set; }

    /// <summary>True while a BeginPoseAnimation move is still travelling.</summary>
    public bool IsPoseAnimating
    {
        get { return _poseSecondsRemaining > 0f; }
    }

    /// <summary>True while the pointer is holding this card. HandView skips a grabbed card
    /// when re-laying the row out, so a layout move never fights the pointer for the card.</summary>
    public bool IsGrabbed
    {
        get { return _isGrabbed; }
    }

    private StandardMaterial3D _frontMaterial = null!;
    private StandardMaterial3D _highlightMaterial = null!;
    private MeshInstance3D _highlight = null!;
    private Label3D _callout = null!;
    private bool _isPointerInside;

    private Func<bool>? _canGrab;
    private Action<CardView>? _onSubmitGesture;
    private bool _isGrabbed;
    private bool _isSubmitArmed;
    private Vector2 _grabStartMousePosition;
    private Transform3D _restLocalTransform;

    private float _poseSecondsRemaining;
    private float _poseSecondsTotal;
    private float _poseArcHeightMeters;
    private Transform3D _poseStartGlobalPose;
    private Transform3D _poseTargetGlobalPose;
    private Action? _onPoseAnimationFinished;

    public override void _Ready()
    {
        // One mesh resource shared by every card — the geometry is identical, and only the
        // material differs. The back reuses the front's mesh; its node carries the 180 degree
        // turn that faces it the other way.
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).Mesh = RoundedCardMesh.FACE_MESH;
        GetNode<MeshInstance3D>(BACK_MESH_PATH).Mesh = RoundedCardMesh.FACE_MESH;
        GetNode<MeshInstance3D>(EDGE_MESH_PATH).Mesh = RoundedCardMesh.EDGE_MESH;

        _callout = GetNode<Label3D>(CALLOUT_PATH);
        _highlight = GetNode<MeshInstance3D>(HIGHLIGHT_MESH_PATH);
        _highlight.Mesh = RoundedCardMesh.HIGHLIGHT_MESH;
        _highlightMaterial = (StandardMaterial3D)_highlight.MaterialOverride.Duplicate();
        _highlight.MaterialOverride = _highlightMaterial;

        // Sized from the same constants the meshes are built from, rather than authored in the
        // .tscn, so the card cannot end up with a hitbox that has drifted from its own edges.
        // A box, not the rounded silhouette — the corners it over-covers are 2mm of empty
        // space that no neighbouring card reaches into.
        BoxShape3D pickShape = new BoxShape3D();
        pickShape.Size = new Vector3(
            RoundedCardMesh.CARD_SIZE.X, RoundedCardMesh.CARD_SIZE.Y, RoundedCardMesh.CARD_THICKNESS);
        GetNode<CollisionShape3D>(PICK_SHAPE_PATH).Shape = pickShape;

        Area3D pickArea = GetNode<Area3D>(PICK_AREA_PATH);
        pickArea.MouseEntered += OnPointerEntered;
        pickArea.MouseExited += OnPointerExited;
        pickArea.InputEvent += OnPickAreaInput;

        // Built here rather than authored in the .tscn on purpose. A material written into the
        // scene file is one resource shared by every instance of that scene, so setting the art
        // on one card would set it on all of them. Each card needs its own. (The rim and back
        // materials are authored in the .tscn precisely because they are the same on all of them.)
        _frontMaterial = new StandardMaterial3D();
        _frontMaterial.Roughness = 0.7f;
        _frontMaterial.EmissionEnabled = true;
        _frontMaterial.EmissionEnergyMultiplier = FACE_EMISSION_ENERGY;
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).MaterialOverride = _frontMaterial;

        // Only a grabbed card listens to global input — see _Input.
        SetProcessInput(false);

        ShowFaceDown();
    }

    /// <summary>Makes this card grabbable. canGrab is consulted at grab start and every frame
    /// while held — turning false mid-grab (the camera going home, the phase closing) cancels
    /// the grab and snaps the card back. onSubmitGesture fires exactly once per armed release,
    /// with the card left wherever the drag put it — the caller owns what happens next.</summary>
    public void EnableGrab(Func<bool> canGrab, Action<CardView> onSubmitGesture)
    {
        _canGrab = canGrab;
        _onSubmitGesture = onSubmitGesture;
    }

    /// <summary>Moves this card's GLOBAL pose to the target over the duration, eased, arcing
    /// upward (world up) on the way when arcHeightMeters is above zero — a flat interpolation
    /// swings a table-lying card's halves through the table mid-flip, so the reveal flip and
    /// the hand-to-table flight both pass above the surface instead. onFinished fires once,
    /// on arrival.</summary>
    public void BeginPoseAnimation(
        Transform3D targetGlobalPose, float durationSeconds, float arcHeightMeters, Action? onFinished)
    {
        _poseStartGlobalPose = GlobalTransform;
        _poseTargetGlobalPose = targetGlobalPose;
        _poseSecondsTotal = durationSeconds;
        _poseSecondsRemaining = durationSeconds;
        _poseArcHeightMeters = arcHeightMeters;
        _onPoseAnimationFinished = onFinished;
    }

    /// <summary>Drops a running pose animation where it is, without firing its onFinished —
    /// for a caller about to place this card somewhere directly.</summary>
    public void CancelPoseAnimation()
    {
        _poseSecondsRemaining = 0f;
        _onPoseAnimationFinished = null;
    }

    public override void _Process(double delta)
    {
        StepPoseAnimation((float)delta);

        // A grab is only valid while its gate still holds — Space mid-grab sends the camera
        // home and recaptures the mouse, and the card must not stay stuck to a pointer that
        // no longer exists on screen.
        if (_isGrabbed
            && (_canGrab == null || !_canGrab() || Input.MouseMode != Input.MouseModeEnum.Visible))
        {
            CancelGrab();
        }

        // The outline follows the pointer, but only while the pointer IS a pointer. Read every
        // frame rather than only when the pointer crosses the card's edge, because leaving the
        // hand view recaptures the mouse without the pointer ever crossing anything — the card
        // would otherwise keep an outline it can no longer be asked to drop.
        _highlight.Visible =
            (_isPointerInside || _isGrabbed) && Input.MouseMode == Input.MouseModeEnum.Visible;
        if (_isSubmitArmed)
        {
            _highlightMaterial.AlbedoColor = SUBMIT_ARMED_OUTLINE_COLOR;
            SetCalloutText(Tr(SUBMIT_CALLOUT_SYMBOL));
        }
        else
        {
            _highlightMaterial.AlbedoColor = HOVER_OUTLINE_COLOR;
            SetCalloutText(HOVER_CALLOUT_ARROW);
        }

        // Shown exactly when the outline is, and saying the same thing in a second channel: the
        // arrow marks which card the pointer has, the word replaces it once letting go would
        // send that card. A child of the card rather than a Control on the interface layer, so it
        // rides the card's own lift instead of needing a world position projected onto the screen
        // every frame.
        _callout.Visible = _highlight.Visible;
    }

    /// <summary>Assigned through here rather than straight onto the node, because _Process runs
    /// this every frame and Label3D rebuilds its glyph mesh whenever its text is set — a rebuild
    /// per frame for a string that changes at most twice per grab.
    ///
    /// Tr() rather than a bare assignment, which is what a Control would take: this project's
    /// auto-translation rule (Scripts/CLAUDE.md) is written for Control, and whether Label3D
    /// honours AutoTranslateMode the same way is not something this codebase has established
    /// anywhere else. Translating here works either way — the result is no longer a key, so a
    /// second pass over it would find nothing to change.</summary>
    private void SetCalloutText(string text)
    {
        if (_callout.Text != text)
        {
            _callout.Text = text;
        }
    }

    /// <summary>Only enabled while grabbed — the drag and its release can land anywhere on
    /// screen, not just over this card's own Area3D, so they have to be read globally rather
    /// than through the pick area.</summary>
    public override void _Input(InputEvent @event)
    {
        if (!_isGrabbed)
        {
            return;
        }

        if (@event is InputEventMouseMotion)
        {
            // Upward screen travel only. Horizontal is dropped, and downward clamps to zero so
            // the card sits at its slot rather than being pushed down through the row.
            float pixelsPulledUp = Mathf.Max(
                0f, _grabStartMousePosition.Y - GetViewport().GetMousePosition().Y);

            // Screen up is local +Y and local +Z is the card's own facing, so the lift toward
            // the camera stays straight at it whatever angle the hand is tilted to.
            Position = _restLocalTransform.Origin + new Vector3(
                0f,
                pixelsPulledUp * GRAB_FOLLOW_METERS_PER_PIXEL,
                GRAB_LIFT_TOWARD_CAMERA_METERS);

            float thresholdPixels =
                GetViewport().GetVisibleRect().Size.Y * SUBMIT_THRESHOLD_VIEWPORT_FRACTION;
            _isSubmitArmed = pixelsPulledUp >= thresholdPixels;
        }
        else if (@event is InputEventMouseButton button
            && !button.Pressed && button.ButtonIndex == MouseButton.Left)
        {
            EndGrab();
        }
    }

    private void OnPointerEntered()
    {
        _isPointerInside = true;
    }

    private void OnPointerExited()
    {
        _isPointerInside = false;
    }

    private void OnPickAreaInput(
        Node camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, long shapeIndex)
    {
        if (@event is not InputEventMouseButton button || !button.Pressed
            || button.ButtonIndex != MouseButton.Left)
        {
            return;
        }

        if (Input.MouseMode != Input.MouseModeEnum.Visible)
        {
            return;
        }

        if (_canGrab != null)
        {
            if (_canGrab() && !_isGrabbed && !IsPoseAnimating)
            {
                BeginGrab();
            }
            return;
        }

        // Not grabbable (a played-card slab on the table): the debug print stays as the
        // visible seam for whatever a table card click comes to mean later.
        GD.Print($"CardView clicked: {ShownCard?.ToString() ?? "face down"}");
    }

    private void BeginGrab()
    {
        _isGrabbed = true;
        _isSubmitArmed = false;
        _grabStartMousePosition = GetViewport().GetMousePosition();
        _restLocalTransform = Transform;

        // Applied at the press rather than waiting for the first drag, so a card picked up
        // and held still is already clear of its neighbours' plane.
        Position = _restLocalTransform.Origin + Vector3.Back * GRAB_LIFT_TOWARD_CAMERA_METERS;
        SetProcessInput(true);
    }

    private void EndGrab()
    {
        _isGrabbed = false;
        SetProcessInput(false);

        bool submit = _isSubmitArmed && _canGrab != null && _canGrab();
        _isSubmitArmed = false;

        if (submit)
        {
            _onSubmitGesture?.Invoke(this);
            return;
        }

        SnapBack();
    }

    private void CancelGrab()
    {
        _isGrabbed = false;
        _isSubmitArmed = false;
        SetProcessInput(false);
        SnapBack();
    }

    private void SnapBack()
    {
        Transform3D restGlobalPose = GetParent<Node3D>().GlobalTransform * _restLocalTransform;
        BeginPoseAnimation(restGlobalPose, SNAP_BACK_SECONDS, 0f, null);
    }

    private void StepPoseAnimation(float delta)
    {
        if (_poseSecondsRemaining <= 0f)
        {
            return;
        }

        _poseSecondsRemaining -= delta;
        float progress = Mathf.Clamp(1f - _poseSecondsRemaining / _poseSecondsTotal, 0f, 1f);
        float eased = Mathf.SmoothStep(0f, 1f, progress);

        Transform3D pose = _poseStartGlobalPose.InterpolateWith(_poseTargetGlobalPose, eased);
        pose.Origin += Vector3.Up * (_poseArcHeightMeters * Mathf.Sin(Mathf.Pi * eased));
        GlobalTransform = pose;

        if (_poseSecondsRemaining <= 0f)
        {
            GlobalTransform = _poseTargetGlobalPose;
            Action? finished = _onPoseAnimationFinished;
            _onPoseAnimationFinished = null;
            finished?.Invoke();
        }
    }

    /// <summary>Puts this card's own art on the face. Falls back to a flat placeholder when
    /// the ECardName has no CardData or its CardData carries no art, so a missing resource
    /// shows a blank card rather than an untextured white one.</summary>
    public void ShowFaceUp(ECardName cardName)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(cardName);
        Texture2D? art = cardData?.CardArt;

        // Rock/Paper/Scissors' art (Assets/Cards/*Art.tres) is an AtlasTexture cropping one
        // card out of the shared CardSprite.png sheet. That cropping is a 2D-only draw-time
        // behavior (Sprite2D/TextureRect) — a 3D material's AlbedoTexture just samples the
        // atlas's underlying image directly, ignoring Region entirely, which is why all three
        // cards' art showed at once on a single card's face. Sampling the real image ourselves
        // and cropping via UV1 scale/offset reproduces the same crop in 3D.
        if (art is AtlasTexture atlas && atlas.Atlas != null)
        {
            Vector2 atlasSize = atlas.Atlas.GetSize();
            _frontMaterial.AlbedoTexture = atlas.Atlas;
            _frontMaterial.Uv1Scale = new Vector3(
                atlas.Region.Size.X / atlasSize.X, atlas.Region.Size.Y / atlasSize.Y, 1f);
            _frontMaterial.Uv1Offset = new Vector3(
                atlas.Region.Position.X / atlasSize.X, atlas.Region.Position.Y / atlasSize.Y, 0f);
        }
        else
        {
            _frontMaterial.AlbedoTexture = art;
            _frontMaterial.Uv1Scale = Vector3.One;
            _frontMaterial.Uv1Offset = Vector3.Zero;
        }

        _frontMaterial.AlbedoColor = art == null ? PLACEHOLDER_FACE_COLOR : Colors.White;

        // Mirrored off the albedo values just set, not computed separately — the two channels
        // agreeing is the whole point (see FACE_EMISSION_ENERGY), so there is one place that
        // decides what the face looks like and emission just repeats it.
        _frontMaterial.EmissionTexture = _frontMaterial.AlbedoTexture;
        _frontMaterial.Emission = _frontMaterial.AlbedoColor;

        ShownCard = cardName;
    }

    /// <summary>Clears the face rather than hiding it. The opponent's cards are face down, and
    /// a client is never told what they are — but nothing should be sitting on the material
    /// waiting for a camera angle or a flip to expose it either.</summary>
    public void ShowFaceDown()
    {
        _frontMaterial.AlbedoTexture = null;
        _frontMaterial.AlbedoColor = PLACEHOLDER_FACE_COLOR;
        _frontMaterial.EmissionTexture = null;
        _frontMaterial.Emission = PLACEHOLDER_FACE_COLOR;
        ShownCard = null;
    }
}
