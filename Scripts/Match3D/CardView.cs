using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>One card as a thing on the table: a thin slab with the art on one side and the
/// shared back image on the other. Which side the player sees is the node's rotation, not a
/// visibility flag — the same as a real card, and what a flip animation will turn later.
///
/// Replaces the Control-based CardView now in Deprecated/. That one drew a nameplate, a type
/// border and a tooltip; none of those survive as-is in 3D (Godot has no 3D tooltip and no
/// layout containers), so they are deliberately absent here rather than reimplemented.
///
/// A card handles its own pointer input rather than a picker elsewhere reaching in and doing it
/// for every card (Scripts/CLAUDE.md: "input handling lives in the node that owns it"). It only
/// judges what was pointed at and clicked, never whether the card is playable — that stays with
/// the host, and OnClicked is where GameState.RequestCardPlay will be called from once the real
/// hand exists. For now it only prints, which is what makes the seam visible.
///
/// Not [Tool] — [Tool] usage in this scene is being kept to CharacterIdlePose only for now, to
/// narrow down a runtime freeze. DebugHandPreview (the only thing that needed CardView.tscn to
/// resolve to an actual CardView in the editor) lost [Tool] for the same reason, so that need
/// is gone too.</summary>
public partial class CardView : Node3D
{
    // The card's size, thickness and corner radius all live in RoundedCardMesh, which builds
    // the geometry these three nodes draw. The .tscn holds only the node structure and the two
    // materials that are the same on every card.
    private const string FRONT_MESH_PATH = "Front";
    private const string BACK_MESH_PATH = "Back";
    private const string EDGE_MESH_PATH = "Rim";
    private const string HIGHLIGHT_MESH_PATH = "Highlight";
    private const string PICK_AREA_PATH = "PickArea";
    private const string PICK_SHAPE_PATH = "PickArea/CollisionShape3D";

    /// <summary>Shown on the face of a card whose ECardName has no art yet. Six of the nine
    /// cards are in that state — see ASSETS-3D.md, where the missing art is the open blocker.
    /// Cards without art are not distinguishable from each other until it exists.</summary>
    private static readonly Color PLACEHOLDER_FACE_COLOR = new Color(0.62f, 0.60f, 0.56f);

    /// <summary>Which card this is currently showing, or null while it is face down. The click
    /// handler has to be able to say what was clicked, and a card that is face down is one this
    /// screen is not allowed to name.</summary>
    public ECardName? ShownCard { get; private set; }

    private StandardMaterial3D _frontMaterial = null!;
    private MeshInstance3D _highlight = null!;
    private bool _isPointerInside;

    public override void _Ready()
    {
        // One mesh resource shared by every card — the geometry is identical, and only the
        // material differs. The back reuses the front's mesh; its node carries the 180 degree
        // turn that faces it the other way.
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).Mesh = RoundedCardMesh.FACE_MESH;
        GetNode<MeshInstance3D>(BACK_MESH_PATH).Mesh = RoundedCardMesh.FACE_MESH;
        GetNode<MeshInstance3D>(EDGE_MESH_PATH).Mesh = RoundedCardMesh.EDGE_MESH;

        _highlight = GetNode<MeshInstance3D>(HIGHLIGHT_MESH_PATH);
        _highlight.Mesh = RoundedCardMesh.HIGHLIGHT_MESH;

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
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).MaterialOverride = _frontMaterial;

        ShowFaceDown();
    }

    /// <summary>The outline follows the pointer, but only while the pointer IS a pointer. Read
    /// every frame rather than only when the pointer crosses the card's edge, because leaving
    /// the hand view recaptures the mouse without the pointer ever crossing anything — the card
    /// would otherwise keep an outline it can no longer be asked to drop.</summary>
    public override void _Process(double delta)
    {
        _highlight.Visible = _isPointerInside && Input.MouseMode == Input.MouseModeEnum.Visible;
    }

    private void OnPointerEntered()
    {
        _isPointerInside = true;
    }

    private void OnPointerExited()
    {
        _isPointerInside = false;
    }

    private void OnPickAreaInput(Node camera, InputEvent @event, Vector3 eventPosition, Vector3 normal, long shapeIndex)
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

        // The seam GameState.RequestCardPlay will be called from. It prints instead of playing
        // because the hand these cards stand in for is still DebugHandPreview's fixed mockup —
        // there is no hand for a play to be legal against yet.
        GD.Print($"CardView clicked: {ShownCard?.ToString() ?? "face down"}");
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
        ShownCard = cardName;
    }

    /// <summary>Clears the face rather than hiding it. The opponent's cards are face down, and
    /// a client is never told what they are — but nothing should be sitting on the material
    /// waiting for a camera angle or a flip to expose it either.</summary>
    public void ShowFaceDown()
    {
        _frontMaterial.AlbedoTexture = null;
        _frontMaterial.AlbedoColor = PLACEHOLDER_FACE_COLOR;
        ShownCard = null;
    }
}
