using System;
using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Reaches into an imported character without depending on what the exporter happened
/// to call things, so the model behind a seat can be swapped without editing code.
///
/// Two names get in the way of that, and neither is worth trusting:
///
/// The rig's bone prefix. Mixamo stamps every bone with "mixamorig" plus a number it picks per
/// download — MainCharacter came down as mixamorig10:Head and a fresh Y Bot as mixamorig:Head —
/// and Godot turns the colon into an underscore on import. A full name spelled into a const
/// belongs to one particular download rather than to the rig.
///
/// The armature node's name. Blender names it after whatever the object was called, so a file
/// that once held two armatures exports "Armature.002" and a path like "Armature/Skeleton3D"
/// stops resolving. The skeleton is found by type instead — a character has exactly one.
///
/// Misses are reported here rather than returned as a bare -1 or null and forgotten, because
/// both read as ordinary values to the caller and surface far from the model swap that actually
/// caused them.</summary>
public static class MixamoRig
{
    public const string HEAD = "Head";
    public const string RIGHT_HAND = "RightHand";

    /// <summary>The bone's index, or -1 when this rig has no such bone. Accepts both
    /// "&lt;prefix&gt;_Head" and a bare "Head", so a rig exported without a prefix works too.
    /// The underscore is part of the match on purpose: a plain EndsWith would let RIGHT_HAND
    /// pick up any bone whose name merely finishes with those letters.</summary>
    public static int FindBone(Skeleton3D skeleton, string mixamoBoneName)
    {
        for (int boneIndex = 0; boneIndex < skeleton.GetBoneCount(); boneIndex++)
        {
            string boneName = skeleton.GetBoneName(boneIndex);
            if (boneName == mixamoBoneName
                || boneName.EndsWith("_" + mixamoBoneName, StringComparison.Ordinal))
            {
                return boneIndex;
            }
        }

        GD.PushError(
            $"MixamoRig: {skeleton.GetPath()} has no '{mixamoBoneName}' bone, with or without a "
            + "mixamorig prefix. A character swapped for one on a different rig is the usual "
            + "cause.");
        return -1;
    }

    /// <summary>The character's Skeleton3D, or null when it has none. Searched by type through
    /// the whole subtree rather than by path, since how deep it sits is the exporter's business
    /// and not something a caller should have to know.</summary>
    public static Skeleton3D? FindSkeleton(Node character)
    {
        Skeleton3D? skeleton = SearchForSkeleton(character);
        if (skeleton == null)
        {
            GD.PushError(
                $"MixamoRig: {character.GetPath()} contains no Skeleton3D. A model imported "
                + "without a skin is the usual cause — glTF only makes bones out of a rig that "
                + "something is actually skinned to.");
        }

        return skeleton;
    }

    private static Skeleton3D? SearchForSkeleton(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Skeleton3D skeleton)
            {
                return skeleton;
            }

            Skeleton3D? deeper = SearchForSkeleton(child);
            if (deeper != null)
            {
                return deeper;
            }
        }

        return null;
    }
}
