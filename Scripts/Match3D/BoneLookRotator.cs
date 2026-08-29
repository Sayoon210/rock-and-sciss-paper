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
    public static void Apply(Skeleton3D skeleton, int boneIndex, int boneParentIndex, Basis desiredBoneWorldBasis)
    {
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
