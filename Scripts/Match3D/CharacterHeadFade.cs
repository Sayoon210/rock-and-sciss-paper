using System.Collections.Generic;
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
/// Which meshes those are is per model and so comes from the scene (HeadAreaMeshNames) rather
/// than from here. On Ch28 it took three: a fade on Body alone left the eyelashes and hair
/// floating on their own once the rest of the head had gone.
///
/// Symmetric by construction: both seats carry this on their own child of Character, and each
/// client's own camera only ever sits close to its own seat's character (DESIGN.md's head-
/// center framing), so this only ever hides "my own" head on each screen without needing to
/// know which seat is "mine".
///
/// A sibling node of the mesh it fades, not attached to Character itself or to the model's own
/// .glb — Character.tscn adds one as a plain new child, which needs no "editable children"
/// override since it is a new node, not an edit to one already inside the imported scene.
///
/// Not [Tool] — it only takes effect once the game is actually run with the scene's own
/// Camera3D active, not while just orbiting the editor's own camera. [Tool] usage on scripts
/// in this scene is being kept to CharacterIdlePose only for now, to narrow down a runtime
/// freeze.</summary>
public partial class CharacterHeadFade : Node3D
{
    /// <summary>Which of the character's meshes reach up into the head, named as they appear
    /// under the character's own Skeleton3D. Set per character scene rather than here, because the
    /// answer is a property of the model: Ch28 splits into Body/Eyelashes/Hair, and a Mixamo
    /// Y Bot is two meshes covering the whole figure. A name that is not on this model is
    /// reported and skipped, so swapping the model in fails as one readable line rather than
    /// as a crash on a missing node.</summary>
    [Export]
    public string[] HeadAreaMeshNames { get; set; } = System.Array.Empty<string>();

    // Fully hidden inside MIN, fully visible past MAX, dithered in between. MIN and MAX must
    // stay meaningfully apart — the shader computes the dither ratio as
    // (distance - MIN) / (MAX - MIN), so a near-zero-width band divides by (near) zero and the
    // fade stops being reliable. Camera-to-head rest distance measures ~0.40m (HeadFollowCamera
    // was pulled back from an original ~0.13m, which put the camera inside the head volume for
    // most mouse-look angles) — MIN sits comfortably above that.
    private const float FADE_MIN_DISTANCE = 0.3f;
    private const float FADE_MAX_DISTANCE = 0.4f;

    // Where the band slides to when the fade is fully relaxed — the hand view is meant to show
    // the character normally, since the fade only exists to keep the player's own head out of
    // their own first-person view. Not zero-width, for the divide-by-zero reason above: a 1cm
    // band is past anything a camera will ever be, so it reads as "no fade" without being one.
    private const float RELAXED_FADE_MIN_DISTANCE = 0f;
    private const float RELAXED_FADE_MAX_DISTANCE = 0.01f;

    private readonly List<StandardMaterial3D> _fadedMaterials = new List<StandardMaterial3D>();

    public override void _Ready()
    {
        if (MixamoRig.FindSkeleton(GetParent()) is not Skeleton3D skeleton)
        {
            return;
        }

        foreach (string meshName in HeadAreaMeshNames)
        {
            MeshInstance3D? meshInstance = skeleton.GetNodeOrNull<MeshInstance3D>(meshName);
            if (meshInstance == null)
            {
                GD.PushError(
                    $"CharacterHeadFade: {GetPath()} lists '{meshName}', which this character "
                    + "has no mesh for. A model swapped for one whose meshes are named "
                    + "differently is the usual cause.");
                continue;
            }

            ApplyFade(meshInstance);
        }
    }

    /// <summary>1 keeps the normal near-camera fade; 0 relaxes it away entirely. Takes a
    /// continuous value rather than a bool so a camera moving off the head can dissolve it back
    /// in over the move instead of popping it at the end — see HeadFollowCamera's hand-view
    /// blend, the only caller.</summary>
    public void SetFadeStrength(float fadeStrength)
    {
        foreach (StandardMaterial3D material in _fadedMaterials)
        {
            material.DistanceFadeMinDistance =
                Mathf.Lerp(RELAXED_FADE_MIN_DISTANCE, FADE_MIN_DISTANCE, fadeStrength);
            material.DistanceFadeMaxDistance =
                Mathf.Lerp(RELAXED_FADE_MAX_DISTANCE, FADE_MAX_DISTANCE, fadeStrength);
        }
    }

    private void ApplyFade(MeshInstance3D meshInstance)
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
        _fadedMaterials.Add(faded);
    }
}
