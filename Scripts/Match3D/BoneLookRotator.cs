using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Turns one bone to a given WORLD orientation, via Skeleton3D.SetBonePoseRotation — a
/// LOCAL (parent-relative) pose setter, not the deprecated SetBoneGlobalPoseOverride. Shared by
/// HeadFollowCamera (the local player's own head) and RemoteHeadLook (the opponent's head on
/// this screen), so the parent-relative conversion exists exactly once.
///
/// Deliberately takes the finished desiredBoneWorldBasis rather than a "delta plus a rest to
/// left-multiply it onto" — composing the delta onto a rest orientation is the caller's job, and
/// the two callers do it differently. HeadFollowCamera composes onto its OWN rest, which is fine
/// (a character only ever turns its own head relative to its own facing). Composing a WORLD
/// delta straight onto a DIFFERENT character's rest is not: MySeat and OpponentSeat face 180
/// degrees apart, so "my own right" is world +X but "the opponent's own right" is world -X, and
/// a world-frame delta built from one does not mean the same thing applied to the other — a
/// pitch that raises my own head-top landmark was measured lowering the opponent's. See
/// RemoteHeadLook for the fix (a delta expressed in the sender's own rest-relative frame).</summary>
public static class BoneLookRotator
{
    // How long the head takes to change hands between a running clip and look, each way.
    // Taking it is quick — a blow should throw the head at once, and the clip's own motion
    // covers the move. Handing it back is slower, because that direction has nothing of its
    // own going on to hide inside.
    private const float CLIP_TAKEOVER_SECONDS = 0.12f;
    private const float LOOK_RECOVERY_SECONDS = 0.35f;

    /// <summary>Advances how much of the bone the look direction currently owns — 0 while a
    /// clip has it, 1 once it is fully handed back. Callers keep the running value and pass it
    /// straight to Apply.
    ///
    /// Ramped rather than switched: handing the bone back on the single frame a clip ended
    /// snapped the head from wherever the blow had left it to wherever the look direction was
    /// pointing, with nothing in between.</summary>
    public static float RampedAuthority(float lookAuthority, bool clipOwnsTheBone, double delta)
    {
        float target;
        float seconds;
        if (clipOwnsTheBone)
        {
            target = 0f;
            seconds = CLIP_TAKEOVER_SECONDS;
        }
        else
        {
            target = 1f;
            seconds = LOOK_RECOVERY_SECONDS;
        }

        return Mathf.MoveToward(lookAuthority, target, (float)delta / seconds);
    }

    public static void Apply(
        Skeleton3D skeleton,
        int boneIndex,
        int boneParentIndex,
        Basis desiredBoneWorldBasis,
        float lookAuthority)
    {
        // Blended from wherever the bone actually IS — the clip's pose mid-blow, this rotator's
        // own output from last frame otherwise — so neither side has to know what the other was
        // doing, and a clip that ends anywhere at all is departed from smoothly. At authority 1
        // the slerp lands exactly on desiredBoneWorldBasis, so the steady state outside a blow
        // is unchanged; at 0 it writes back the pose already there.
        Basis posedBoneWorldBasis =
            (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(boneIndex)).Basis.Orthonormalized();
        desiredBoneWorldBasis = posedBoneWorldBasis.Slerp(desiredBoneWorldBasis, lookAuthority);

        // SetBonePoseRotation is LOCAL — relative to the bone's own PARENT, not the skeleton
        // root — so the parent's current world basis is what desiredBoneWorldBasis needs
        // dividing out by, not the skeleton's own GlobalTransform directly.
        Basis parentWorldBasis = boneParentIndex >= 0
            ? (skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(boneParentIndex)).Basis
            : skeleton.GlobalTransform.Basis;
        Basis boneLocalBasis = (parentWorldBasis.Inverse() * desiredBoneWorldBasis).Orthonormalized();
        skeleton.SetBonePoseRotation(boneIndex, boneLocalBasis.GetRotationQuaternion());
    }
}
