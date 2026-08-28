using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Tracks the head bone's world X/Z offset from wherever it sat when this node became
/// ready, 1:1 and every frame — position only, on the horizontal plane only, not the bone's
/// full transform (BoneAttachment3D would give that), which swings the camera's rotation right
/// along with big gestures like the punch/stab win blows and reads as motion sickness more than
/// embodiment. Y and rotation stay exactly as authored in the scene.</summary>
public partial class HeadFollowCamera : Camera3D
{
    private const string SKELETON_PATH = "../Character/Armature/Skeleton3D";
    private const string HEAD_BONE_NAME = "mixamorig10_Head";

    private Skeleton3D _skeleton = null!;
    private int _headBoneIndex;
    private Vector2 _restHeadWorldXZ;
    private Vector2 _baseLocalXZ;

    public override void _Ready()
    {
        _skeleton = GetNode<Skeleton3D>(SKELETON_PATH);
        _headBoneIndex = _skeleton.FindBone(HEAD_BONE_NAME);
        _restHeadWorldXZ = HeadWorldXZ();
        _baseLocalXZ = new Vector2(Position.X, Position.Z);
    }

    public override void _Process(double delta)
    {
        Vector2 offset = HeadWorldXZ() - _restHeadWorldXZ;

        Vector3 position = Position;
        position.X = _baseLocalXZ.X + offset.X;
        position.Z = _baseLocalXZ.Y + offset.Y;
        Position = position;
    }

    private Vector2 HeadWorldXZ()
    {
        Transform3D worldPose = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_headBoneIndex);
        return new Vector2(worldPose.Origin.X, worldPose.Origin.Z);
    }
}
