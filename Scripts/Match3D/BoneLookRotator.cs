using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Applies a world-space rotation delta (however it was produced — local mouse-look
/// or a network-received value) to one bone, via Skeleton3D.SetBonePoseRotation — a LOCAL
/// (parent-relative) pose setter, not the deprecated SetBoneGlobalPoseOverride. Shared by
/// HeadFollowCamera (the local player's own head, driven by mouse) and RemoteHeadLook (the
/// opponent's head on this screen, driven by what the network says their delta is), so the
/// bone-space conversion exists exactly once.</summary>
public static class BoneLookRotator
{
    /// <summary>restBoneWorldBasis: the bone's own animated rest orientation in world space,
    /// captured once before any override (Skeleton3D.GlobalTransform * GetBoneGlobalPose(bone)).
    /// deltaFromRest: how far off that rest the bone should now be turned, as a world-space
    /// rotation — self-contained, so the caller does not need to know this bone's own rig
    /// axis convention to produce it.</summary>
    public static void Apply(
        Skeleton3D skeleton, int boneIndex, int boneParentIndex, Basis restBoneWorldBasis, Basis deltaFromRest)
    {
        Basis desiredBoneWorldBasis = deltaFromRest * restBoneWorldBasis;

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
