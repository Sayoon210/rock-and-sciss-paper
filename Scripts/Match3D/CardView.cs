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
/// layout containers), so they are deliberately absent here rather than reimplemented.</summary>
public partial class CardView : Node3D
{
    // Real card stock is 0.3mm and reads as flat paper at this scale. ASSETS-3D.md calls for
    // exaggerating it; the mesh in the .tscn uses 4mm, about 13x life.
    private const string FRONT_MESH_PATH = "Front";

    /// <summary>Shown on the face of a card whose ECardName has no art yet. Six of the nine
    /// cards are in that state — see ASSETS-3D.md, where the missing art is the open blocker.
    /// Cards without art are not distinguishable from each other until it exists.</summary>
    private static readonly Color PLACEHOLDER_FACE_COLOR = new Color(0.62f, 0.60f, 0.56f);

    private StandardMaterial3D _frontMaterial = null!;

    public override void _Ready()
    {
        // Built here rather than authored in the .tscn on purpose. A material written into the
        // scene file is one resource shared by every instance of that scene, so setting the art
        // on one card would set it on all of them. Each card needs its own.
        _frontMaterial = new StandardMaterial3D();
        _frontMaterial.Roughness = 0.7f;
        GetNode<MeshInstance3D>(FRONT_MESH_PATH).SetSurfaceOverrideMaterial(0, _frontMaterial);

        ShowFaceDown();
    }

    /// <summary>Puts this card's own art on the face. Falls back to a flat placeholder when
    /// the ECardName has no CardData or its CardData carries no art, so a missing resource
    /// shows a blank card rather than an untextured white one.</summary>
    public void ShowFaceUp(ECardName cardName)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(cardName);
        Texture2D? art = cardData?.CardArt;

        _frontMaterial.AlbedoTexture = art;
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
