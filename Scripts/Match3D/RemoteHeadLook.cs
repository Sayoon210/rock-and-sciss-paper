using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.Match3D;

/// <summary>Turns the OPPONENT's head bone on this screen, driven by GameState.OpponentLookChanged
/// rather than local mouse input — the receiving half of HeadFollowCamera's network broadcast.
/// No camera involvement at all: the opponent's seat has no Camera3D of its own on this screen,
/// so this only ever touches the head bone.
///
/// Attach as a sibling of the imported Character's own nodes, under OpponentSeat/Character — the
/// same place HeadFade sits, for the same reason (a plain new child, no "editable children"
/// override needed).</summary>
public partial class RemoteHeadLook : Node3D
{
    private const string SKELETON_PATH = "../Armature/Skeleton3D";
    private const string HEAD_BONE_NAME = "mixamorig10_Head";

    private Skeleton3D _skeleton = null!;
    private int _headBoneIndex;
    private int _headBoneParentIndex;
    private Basis _restBoneWorldBasis;
    private Basis _lastReceivedDelta = Basis.Identity;

    public override void _Ready()
    {
        _skeleton = GetNode<Skeleton3D>(SKELETON_PATH);
        _headBoneIndex = _skeleton.FindBone(HEAD_BONE_NAME);
        _headBoneParentIndex = _skeleton.GetBoneParent(_headBoneIndex);

        // CharacterIdlePose (a sibling script on the parent Character node) has already applied
        // the idle pose by the time this runs — same ordering reasoning as HeadFollowCamera.
        _restBoneWorldBasis = (_skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_headBoneIndex)).Basis.Orthonormalized();

        GameState.Instance!.OpponentLookChanged += OnOpponentLookChanged;
    }

    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.OpponentLookChanged -= OnOpponentLookChanged;
        }
    }

    private void OnOpponentLookChanged(Quaternion deltaFromRest)
    {
        _lastReceivedDelta = new Basis(deltaFromRest);
    }

    public override void _Process(double delta)
    {
        // Re-applied every frame, not just on receipt — nothing else is driving this bone's
        // pose (CharacterIdlePose's AnimationPlayer is stopped), so without this the pose would
        // hold whatever it last was set to rather than the LATEST received delta specifically;
        // in practice these coincide, but re-applying costs nothing and needs no extra state.
        BoneLookRotator.Apply(_skeleton, _headBoneIndex, _headBoneParentIndex, _restBoneWorldBasis, _lastReceivedDelta);
    }
}
