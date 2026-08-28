using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>TEMP DEBUG — a static mockup of a hand fanned out on CardRest, one of each card
/// type, to judge card-versus-rest scale/spacing before the real hand-in-3D system exists.
/// Remove this node outright once real hand cards are placed here instead (checklist:
/// "손패를 3D 씬에 배치").
///
/// Not [Tool] — it only spawns once the game is actually run, not while just editing
/// MatchWorld.tscn in the Godot editor. [Tool] usage in this scene is being kept to
/// CharacterIdlePose only for now, to narrow down a runtime freeze. A plain new child of
/// CardRest, not CardRest itself, so this needs no "editable children" override on the
/// imported CardRest instance.
///
/// Position constants are in CardRest's own local space — this node is meant to be a direct
/// child of CardRest, so a later edit to CardRest's own transform carries these cards with it.</summary>
public partial class DebugHandPreview : Node3D
{
    private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match3D/CardView.tscn";

    private static readonly ECardName[] HAND_CARDS =
    {
        ECardName.Rock, ECardName.Paper, ECardName.Scissors, ECardName.Blank, ECardName.Joker,
    };

    // Unlike the played card in the middle of the table (MatchWorldView.MY_CARD_ROTATION, laid
    // flat), a hand card on the rest stands in its unrotated pose — CardView's Front mesh faces
    // +Z by default, and with CardRest/Field/DebugHandPreview all carrying no rotation of their
    // own, that +Z points straight back at the player's own seat, i.e. straight at the camera.
    private static readonly Vector3 CARD_ROTATION = Vector3.Zero;

    private static readonly Vector3 HAND_CENTER = new Vector3(0.0008f, 0.156f, 0.362658692f);
    private const float CARD_SPACING = 0.14f;
    private const float CARD_SCALE = 2f;

    public override void _Ready()
    {
        // Guards against the editor re-running _Ready on an already-populated node (a scene
        // reload, a recompile) piling up a second set of cards instead of replacing the first.
        foreach (Node existingCard in GetChildren())
        {
            existingCard.QueueFree();
        }

        float startX = HAND_CENTER.X - (HAND_CARDS.Length - 1) * CARD_SPACING / 2f;

        for (int i = 0; i < HAND_CARDS.Length; i++)
        {
            CardView card = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH).Instantiate<CardView>();
            card.Rotation = CARD_ROTATION;
            card.Position = new Vector3(startX + i * CARD_SPACING, HAND_CENTER.Y, HAND_CENTER.Z);
            card.Scale = Vector3.One * CARD_SCALE;
            AddChild(card);
            card.ShowFaceUp(HAND_CARDS[i]);
        }
    }
}
