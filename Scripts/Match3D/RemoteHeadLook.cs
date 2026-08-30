using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.Match3D;

/// <summary>Turns the OPPONENT's head bone on this screen, driven by GameState.OpponentLookChanged
/// rather than local mouse input — the receiving half of HeadFollowCamera's network broadcast.
/// No camera involvement at all: the opponent's seat has no Camera3D of its own on this screen,
/// so this only ever touches the head bone.
///
/// The received value is a rotation expressed relative to the SENDER's own rest bone frame, not
/// a world-space rotation — composed here onto THIS skeleton's own rest bone basis
/// (_restBoneWorldBasis * delta, right-multiply) rather than left-multiplied the way
/// HeadFollowCamera composes its own local delta. MySeat and OpponentSeat face 180 degrees
/// apart, so a world-frame delta built from the sender's own facing does not mean the same thing
/// applied to a character facing the opposite way — measured directly: a pitch that raised the
/// sender's own head-top landmark lowered this skeleton's, under a left-multiplied world delta.
/// Composing in each side's own local frame instead is what makes the result face-direction-
/// independent — see BoneLookRotator's own doc comment for the general reasoning.
///
/// Attach as a sibling of the imported Character's own nodes, under OpponentSeat/Character — the
/// same place HeadFade sits, for the same reason (a plain new child, no "editable children"
/// override needed).</summary>
public partial class RemoteHeadLook : Node3D
{
	private const string CHARACTER_PATH = "..";
	private const string ANIMATION_PLAYER_PATH = "../AnimationPlayer";

	/// <summary>How fast the shown rotation chases the last received one, as the fraction of
	/// the remaining gap closed per second — an exponential approach, so it is frame-rate
	/// independent (see the exponent in _Process) rather than a fixed step per frame.
	///
	/// Updates arrive at roughly 13-14Hz (measured; HeadFollowCamera aims for 15 and loses a
	/// little to frame quantisation), which is a visible step every ~70ms if applied raw — the
	/// head jerked between poses instead of turning. This has to converge fast enough that the
	/// head is essentially caught up before the next update lands, or the lag reads as the
	/// opponent reacting late; at 25 the gap is ~92% closed over one 70ms interval, which
	/// smooths the step without adding a perceptible delay of its own.</summary>
	private const float LOOK_SMOOTHING_PER_SECOND = 25f;

	private Skeleton3D _skeleton = null!;
	private AnimationPlayer _animationPlayer = null!;
	private int _headBoneIndex;
	private int _headBoneParentIndex;
	private Basis _restBoneWorldBasis;
	private Quaternion _targetLocalDelta = Quaternion.Identity;
	private Quaternion _shownLocalDelta = Quaternion.Identity;

	public override void _Ready()
	{
		if (MixamoRig.FindSkeleton(GetNode<Node3D>(CHARACTER_PATH)) is not Skeleton3D skeleton)
		{
			SetProcess(false);
			return;
		}

		_animationPlayer = GetNode<AnimationPlayer>(ANIMATION_PLAYER_PATH);
		_skeleton = skeleton;
		_headBoneIndex = MixamoRig.FindBone(_skeleton, MixamoRig.HEAD);
		if (_headBoneIndex < 0)
		{
			// Find has already said what is wrong. Same reasoning as HeadFollowCamera: no bone
			// to turn means nothing to do every frame.
			SetProcess(false);
			return;
		}

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

	/// <summary>Stores where the head should end up, not where it is — _Process walks the
	/// shown rotation towards this. Nothing is applied here, so an update arriving mid-turn
	/// just moves the destination rather than snapping to it.</summary>
	private void OnOpponentLookChanged(Quaternion localDeltaInBoneSpace)
	{
		_targetLocalDelta = localDeltaInBoneSpace;
	}

	public override void _Process(double delta)
	{
		// Exponential approach rather than a fixed step: 1 - e^(-rate * dt) is the fraction of
		// the remaining gap to close this frame, which lands on the same curve whatever the
		// frame rate. A plain "rate * dt" lerp would converge faster at high frame rates and
		// could overshoot past 1 on a long frame.
		float weight = 1f - Mathf.Exp(-LOOK_SMOOTHING_PER_SECOND * (float)delta);

		// Slerp, not Lerp — these are rotations, and a linear blend between quaternions does
		// not turn at a constant rate. Normalized because repeated slerping accumulates enough
		// float error to leave the quaternion slightly off unit length, which Basis then reads
		// as a scale.
		_shownLocalDelta = _shownLocalDelta.Slerp(_targetLocalDelta, weight).Normalized();

		// A running clip owns the head, exactly as it does on the local side (HeadFollowCamera) —
		// the opponent throwing a punch should be seen throwing it, not holding their head at
		// whatever their mouse last said. Outside a blow nothing else is driving this bone, since
		// HoldIdle leaves the player stopped rather than playing, so the applied pose stands and
		// the smoothing above genuinely changes it every frame between updates.
		if (_animationPlayer.IsPlaying())
		{
			return;
		}

		Basis desiredBoneWorldBasis = (_restBoneWorldBasis * new Basis(_shownLocalDelta)).Orthonormalized();
		BoneLookRotator.Apply(_skeleton, _headBoneIndex, _headBoneParentIndex, desiredBoneWorldBasis);
	}
}
