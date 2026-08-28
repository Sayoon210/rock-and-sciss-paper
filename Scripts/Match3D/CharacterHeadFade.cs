using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Fades a character's head-area meshes out near whichever camera is currently
/// rendering it, using StandardMaterial3D's built-in per-pixel distance fade rather than a
/// hand-written shader. Ch28_Body is one combined skin covering head, torso and hands, so a
/// whole-mesh visibility toggle (a Visibility Layer, GeometryInstance3D.visibility_range_begin)
/// would hide the hands DESIGN.md wants visible along with the head — those only judge one
/// distance for the whole mesh instance. A per-pixel fade clips only whatever is actually close
/// to the camera, so the head disappears while the hands, resting further away on the table,
/// do not.
///
/// Body is not the only mesh near the head — Ch28_Eyelashes and Ch28_Hair sit right on it too
/// (Ch28_Hoody/Pants/Sneakers do not, so they are left alone), and a fade on Body alone left
/// eyebrows floating on their own once the rest of the head disappeared.
///
/// Symmetric by construction: both seats carry this on their own child of Character, and each
/// client's own camera only ever sits close to its own seat's character (DESIGN.md's head-
/// center framing), so this only ever hides "my own" head on each screen without needing to
/// know which seat is "mine".
///
/// A sibling node of the mesh it fades, not attached to Character itself or to MainCharacter.glb
/// — MatchWorld.tscn adds one as a plain new child under each Character instance, which needs
/// no "editable children" override since it is a new node, not an edit to one already inside
/// the imported scene.
///
/// [Tool] on purpose — the fade only actually applies against whatever camera is currently
/// rendering the viewport, and without this it never applies while just orbiting the editor's
/// own camera close to the head to check it; it would only take effect once the game were
/// actually run with the scene's own Camera3D active.</summary>
[Tool]
public partial class CharacterHeadFade : Node3D
{
    private static readonly string[] HEAD_AREA_MESH_NAMES =
    {
        "Ch28_Body",
        "Ch28_Eyelashes",
        "Ch28_Hair",
    };

    // Fully hidden inside this distance, fully visible past it, dithered in between. Wider
    // than the measured 0.18m camera-to-head distance needs while framing is still being
    // tuned. Watch this once hands/cards are placed on the table — MAX this wide could start
    // fading a hand that reaches close to a head-level camera too. Tighten once the final
    // head-camera framing is locked in.
    private const float FADE_MIN_DISTANCE = 0.4f;
    private const float FADE_MAX_DISTANCE = 0.4f;

    public override void _Ready()
    {
        foreach (string meshName in HEAD_AREA_MESH_NAMES)
        {
            ApplyFade(GetNode<MeshInstance3D>($"../Armature/Skeleton3D/{meshName}"));
        }
    }

    private static void ApplyFade(MeshInstance3D meshInstance)
    {
        if (meshInstance.GetActiveMaterial(0) is not StandardMaterial3D material)
        {
            return;
        }

        // Duplicated, not mutated in place — MainCharacter.glb's material may be the same
        // shared resource both seats' characters (and CharacterAnimationPreview's own
        // instance, which does not want this fade at all) would otherwise all see change.
        StandardMaterial3D faded = (StandardMaterial3D)material.Duplicate();
        faded.DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelDither;
        faded.DistanceFadeMinDistance = FADE_MIN_DISTANCE;
        faded.DistanceFadeMaxDistance = FADE_MAX_DISTANCE;
        meshInstance.SetSurfaceOverrideMaterial(0, faded);
    }
}
