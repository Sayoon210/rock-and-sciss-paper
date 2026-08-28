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

    /// <summary>Shown on the face of a card whose ECardName has no art yet. Six of the nine
    /// cards are in that state — see ASSETS-3D.md, where the missing art is the open blocker.
    /// Cards without art are not distinguishable from each other until it exists.</summary>
    private static readonly Color PLACEHOLDER_FACE_COLOR = new Color(0.62f, 0.60f, 0.56f);

    private StandardMaterial3D _frontMaterial = null!;

    public override void _Ready()
    {
        // One mesh resource shared by every card — the geometry is identical, and only the
        // material differs. The back reuses the front's mesh; its node carries the 180 degree
        // turn that faces it the other way.
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).Mesh = RoundedCardMesh.FACE_MESH;
        GetNode<MeshInstance3D>(BACK_MESH_PATH).Mesh = RoundedCardMesh.FACE_MESH;
        GetNode<MeshInstance3D>(EDGE_MESH_PATH).Mesh = RoundedCardMesh.EDGE_MESH;

        // Built here rather than authored in the .tscn on purpose. A material written into the
        // scene file is one resource shared by every instance of that scene, so setting the art
        // on one card would set it on all of them. Each card needs its own. (The rim and back
        // materials are authored in the .tscn precisely because they are the same on all of them.)
        _frontMaterial = new StandardMaterial3D();
        _frontMaterial.Roughness = 0.7f;
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).MaterialOverride = _frontMaterial;

        ShowFaceDown();
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
    }

    /// <summary>Clears the face rather than hiding it. The opponent's cards are face down, and
    /// a client is never told what they are — but nothing should be sitting on the material
    /// waiting for a camera angle or a flip to expose it either.</summary>
    public void ShowFaceDown()
    {
        _frontMaterial.AlbedoTexture = null;
        _frontMaterial.AlbedoColor = PLACEHOLDER_FACE_COLOR;
    }
}
