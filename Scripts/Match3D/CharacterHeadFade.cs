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
/// Not [Tool] — it only takes effect once the game is actually run with the scene's own
/// Camera3D active, not while just orbiting the editor's own camera. [Tool] usage on scripts
/// in this scene is being kept to CharacterIdlePose only for now, to narrow down a runtime
/// freeze.</summary>
public partial class CharacterHeadFade : Node3D
{
    private static readonly string[] HEAD_AREA_MESH_NAMES =
    {
        "Ch28_Body",
        "Ch28_Eyelashes",
        "Ch28_Hair",
    };

    // Fully hidden inside MIN, fully visible past MAX, dithered in between. MIN and MAX must
    // stay meaningfully apart — the shader computes the dither ratio as
    // (distance - MIN) / (MAX - MIN), so a near-zero-width band divides by (near) zero and the
    // fade stops being reliable. Camera-to-head rest distance measures ~0.40m (HeadFollowCamera
    // was pulled back from an original ~0.13m, which put the camera inside the head volume for
    // most mouse-look angles) — MIN sits comfortably above that.
    private const float FADE_MIN_DISTANCE = 0.3f;
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
