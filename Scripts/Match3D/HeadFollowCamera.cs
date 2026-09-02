using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.Match3D;

/// <summary>Mouse-look for the character's own head — FPS-camera-style, clamped to a limited
/// range off the rest facing (DESIGN.md's "카메라 상호작용": ~60 degrees up/down/left/right on
/// round entry). Turns the actual head BONE, not just the camera, so the character's head
/// visibly turns with it; the camera then rigidly follows wherever that bone ends up, position
/// and rotation both. The resulting delta is also broadcast over the network (throttled), so the
/// opponent's screen shows the same head turn — see RemoteHeadLook, the receiving half.
///
/// Space blends the camera off the head and onto Field/CardRest/HandViewCamera, and back again
/// (DESIGN.md's round flow: "스페이스바를 누르면 카드 있는 쪽으로 카메라 블렌드 전환"). That
/// second camera is only ever a pose to aim at — this one stays the rendering camera the whole
/// time, since Godot has no built-in blend between two Camera3Ds and switching between them
/// would cut rather than travel. Mouse-look keeps driving the head bone throughout, so the head
/// still turns (and the opponent still sees it) while its owner is reading their own hand.
///
/// The bone is turned via BoneLookRotator (Skeleton3D.SetBonePoseRotation, a LOCAL/parent-
/// relative pose setter) — not the deprecated SetBoneGlobalPoseOverride an earlier version of
/// this used. That one produced a confirmed, reproducible bug (the character's own face
/// rendering as an enormous close-up regardless of camera distance) traced to it specifically,
/// and its exact coordinate convention could not be pinned down with confidence in the time
/// spent on it. SetBonePoseRotation is not deprecated, and is safe to call every frame here
/// because CharacterIdlePose leaves the AnimationPlayer stopped (Play+Seek+Stop, not looping) —
/// nothing else is re-driving this bone's pose each frame to fight with.
///
/// The look direction itself is computed against the CAMERA's own authored rest orientation
/// (already tuned to face the opponent correctly), not the bone's — the rig's head bone has its
/// own axis convention (verified by measurement: its local -Z is not "the direction the face
/// points" the way Camera3D's is), so copying the bone's basis straight onto the camera pointed
/// it backward. The fixed rotational offset between the camera's rest and the bone's rest is
/// captured once in _Ready() and reused every frame to carry the same world-space look rotation
/// over onto the bone, whatever its own axis convention turns out to be.
///
/// Escape releases the captured mouse — needed to reach anything else on screen (the debug
/// animation panel included) while look is active.</summary>
public partial class HeadFollowCamera : Camera3D
{
	private const string CHARACTER_PATH = "../Character";
	private const string ANIMATION_PLAYER_PATH = "../Character/AnimationPlayer";
	private const string HAND_VIEW_CAMERA_PATH = "../../Field/CardRest/HandViewCamera";
	private const string HEAD_FADE_PATH = "../Character/HeadFade";

	private const float MOUSE_SENSITIVITY_DEGREES_PER_PIXEL = 0.1f;
	private const float MAX_LOOK_DEGREES = 60f;

	// Where the head aims itself while the hand view is up: centred, and tipped down at the
	// cards. The pitch is measured rather than judged by eye -- from the camera's authored rest
	// position, the hand row's centre sits 34.5 degrees below the rest facing (which is itself
	// already 35.6 degrees down, so this is 70 degrees below horizontal in total). Yaw goes to
	// 0 rather than the 4.9 degrees that would aim exactly at the row's centre: the row is a
    // hair off-centre in X, and "centred" is what this is meant to look like.
    private const float HAND_VIEW_YAW_DEGREES = 0f;
    private const float HAND_VIEW_PITCH_DEGREES = -34.5f;

    // Tuned against HAND_VIEW_BLEND_SECONDS so the head finishes turning at about the same
    // time the camera finishes travelling -- roughly 95% of the way there over that 0.35s.
    private const float HAND_VIEW_LOOK_SETTLE_PER_SECOND = 9f;

    // How long the trip between the head and the hand takes, each way.
    private const float HAND_VIEW_BLEND_SECONDS = 0.35f;

    // Cosmetic-only network traffic (BoneLookRotator/GameState.SendMyLookDirection) — sent on
    // a timer well under the render rate rather than every _Process, since a dropped or
    // superseded update costs nothing but does not need repeating 60 times a second.
    private const double LOOK_SEND_INTERVAL_SECONDS = 1.0 / 15.0;

    // How far a shake at full strength throws the view, and how fast it dies. Eyeball both
    // against the running game; 1.6 degrees was the first guess here and read as nothing at all
	// on screen, which is most of a punch's worth of feedback thrown away for caution.
	private const float SHAKE_MAX_DEGREES = 6.5f;
	private const float SHAKE_DECAY_PER_SECOND = 11f;
	private const float SHAKE_YAW_CYCLES_PER_SECOND = 42f;
	private const float SHAKE_PITCH_CYCLES_PER_SECOND = 31f;

	// Below this the shake is under a hundredth of a degree, which is nothing anyone sees and
	// still costs two sines and a pair of rotations every frame forever. Snapped to zero instead.
	private const float SHAKE_NEGLIGIBLE_STRENGTH = 0.005f;

	private Skeleton3D _skeleton = null!;
	private AnimationPlayer _animationPlayer = null!;
	private Camera3D _handViewCamera = null!;
	private CharacterHeadFade _headFade = null!;
	private int _headBoneIndex;
	private int _headBoneParentIndex;
	private Basis _restCameraWorldBasis;
	private Basis _restBoneWorldBasis;
	private Vector3 _restCameraWorldPosition;
	private Vector3 _restBoneWorldPosition;
	private float _restFieldOfView;
	private float _yawDegrees;
	private float _pitchDegrees;
	private double _timeUntilNextLookSend;
	private bool _isHandViewHeld;
	private float _handViewBlend;
	private float _shakeStrength;
	private float _shakeSeconds;
	private float _lookAuthority = 1f;

	/// <summary>Whether the hand view is the current destination — true from the Space press
	/// that starts the trip toward the hand until whatever starts the trip back (another Space,
	/// or a submission calling ReturnToHeadView). HandView gates card grabbing on this, which
	/// also means an in-progress grab cancels itself the moment the camera is sent home.</summary>
	public bool IsHandViewHeld
	{
		get { return _isHandViewHeld; }
	}

	// Set by MatchWorldView's round-flow state machine while the "Round N" splash is up --
	// blocks entering the hand view (Space), not mouse-look, so the player can still look
	// around during the splash and only the card-submitting part of control is held back.
	private bool _isRoundIntroLocked;

	public void SetRoundIntroLocked(bool isLocked)
	{
		_isRoundIntroLocked = isLocked;
	}

	/// <summary>Kicks the camera. Called on the frame a blow actually lands, by whoever is
	/// watching that clip's own clock (MatchWorldView) — and on BOTH players' screens, because a
	/// punch only the thrower feels reads as the victim not having been hit.
	///
	/// Takes the larger of the two rather than adding, so two impacts landing close together
	/// cannot stack into a shake well past what either one was meant to be.</summary>
	public void Shake(float strength)
	{
		_shakeStrength = Mathf.Max(_shakeStrength, strength);

		// Restarted, so every impact swings the same way from its own first frame. Left running,
		// the oscillation would be at whatever phase the last shake happened to leave it at —
		// one punch throwing the view wide and the next barely moving it, for no reason the
		// player could see.
		_shakeSeconds = 0f;
	}

	/// <summary>The camera pose with the current shake laid over it. Rotation only, never
	/// position: this camera sits at the head, and shoving it through space walks it into the
	/// character's own geometry — a rotational knock reads as being hit without ever moving the
	/// eye out of its socket.
	///
	/// Two waves whose frequencies do not divide into one another, so the pair does not
	/// repeat on a short cycle and read as a wobble. The decay is exponential rather than linear
	/// because a knock should drop hard and then trail off, and because that shape is frame-rate
	/// independent for the same reason RemoteHeadLook's smoothing is.</summary>
	private Transform3D ShakenBy(Transform3D pose, double delta)
	{
		if (_shakeStrength <= SHAKE_NEGLIGIBLE_STRENGTH)
		{
			_shakeStrength = 0f;
			return pose;
		}

		_shakeSeconds += (float)delta;
		_shakeStrength *= Mathf.Exp(-SHAKE_DECAY_PER_SECOND * (float)delta);

		// Cos, not sin, and off a clock Shake() reset to zero: sin starts a cycle at zero
		// deflection, so the hardest part of the envelope was being multiplied by nothing and the
		// view only reached its widest a quarter-cycle later, by which point the decay had already
		// eaten most of it. Cos puts the whole first swing on the frame of impact, which is where
		// a knock belongs.
		float swingDegrees = SHAKE_MAX_DEGREES * _shakeStrength;
		float yawDegrees = Mathf.Cos(_shakeSeconds * SHAKE_YAW_CYCLES_PER_SECOND) * swingDegrees;
		float pitchDegrees = Mathf.Cos(_shakeSeconds * SHAKE_PITCH_CYCLES_PER_SECOND) * swingDegrees;

		Basis shaken = pose.Basis.Rotated(pose.Basis.Y, Mathf.DegToRad(yawDegrees));
		shaken = shaken.Rotated(shaken.X, Mathf.DegToRad(pitchDegrees));
		return new Transform3D(shaken, pose.Origin);
	}

	public override void _Ready()
	{
		Input.MouseMode = Input.MouseModeEnum.Captured;

		// The camera's own authored transform (Scenes/Screens/MatchWorld.tscn) — already tuned
		// to face the opponent correctly, and NOT the same spot as the head bone's own joint
		// (that sits inside the head — a camera placed exactly there ends up embedded in the
		// character's own geometry). Read before anything below touches GlobalTransform.
		_restCameraWorldBasis = GlobalTransform.Basis;
		_restCameraWorldPosition = GlobalTransform.Origin;
		_restFieldOfView = Fov;

		// A pose marker, not a second renderer. Godot has no built-in Camera3D blend, so the
		// hand view is reached by moving THIS camera onto that one's transform rather than by
		// switching between the two — which is also what makes a partial blend a thing at all.
		// It stays a Camera3D because that is what lets the pose be framed through the editor's
		// own preview; ticking that preview writes current = true into the scene, hence the
		// explicit correction here.
		_handViewCamera = GetNode<Camera3D>(HAND_VIEW_CAMERA_PATH);
		_handViewCamera.Current = false;
		MakeCurrent();

		_headFade = GetNode<CharacterHeadFade>(HEAD_FADE_PATH);

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
			// Find has already said what is wrong. With no head bone there is nothing to
			// follow, so the camera holds the pose the scene gave it instead of driving itself
			// off a bad index every frame.
			SetProcess(false);
			return;
		}

		_headBoneParentIndex = _skeleton.GetBoneParent(_headBoneIndex);

		// The head's animated rest pose (CharacterIdlePose holds this by the time this runs —
		// Character is earlier in MySeat's child order than this Camera3D, so its _Ready() has
		// already applied the idle pose). Skeleton3D's own "global" pose is global among its
		// bones, not the scene world — multiplying by the skeleton's own GlobalTransform is
		// what makes it a world transform.
		Transform3D restBoneWorld = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_headBoneIndex);
		_restBoneWorldBasis = restBoneWorld.Basis.Orthonormalized();
		_restBoneWorldPosition = restBoneWorld.Origin;
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseMotion mouseMotion && Input.MouseMode == Input.MouseModeEnum.Captured)
		{
			_yawDegrees = Mathf.Clamp(
				_yawDegrees - mouseMotion.Relative.X * MOUSE_SENSITIVITY_DEGREES_PER_PIXEL,
				-MAX_LOOK_DEGREES, MAX_LOOK_DEGREES);
			_pitchDegrees = Mathf.Clamp(
				_pitchDegrees - mouseMotion.Relative.Y * MOUSE_SENSITIVITY_DEGREES_PER_PIXEL,
				-MAX_LOOK_DEGREES, MAX_LOOK_DEGREES);
		}
		else if (@event is InputEventKey key && key.Pressed && !key.Echo)
		{
			if (key.Keycode == Key.Escape)
			{
				Input.MouseMode = Input.MouseModeEnum.Visible;
			}
			else if (key.Keycode == Key.Space && !_isRoundIntroLocked)
			{
				SetHandViewHeld(!_isHandViewHeld);
			}
		}
	}

	/// <summary>Sends the camera home to the head immediately — what a successful card
	/// submission calls (HandView), so the trip back starts the moment the card is let go
	/// rather than waiting for another Space press.</summary>
	public void ReturnToHeadView()
	{
		SetHandViewHeld(false);
	}

	// The two views want the mouse for different things and cannot share it: the head view
	// spends it on look (captured, no cursor), the hand view on pointing at cards (free
	// cursor). Switched at the moment the destination changes rather than at the end of the
	// blend so the cursor is there for the whole trip, and so the head stops turning the
	// instant the player stops steering with it.
	private void SetHandViewHeld(bool isHeld)
	{
		_isHandViewHeld = isHeld;
		if (_isHandViewHeld)
		{
			Input.MouseMode = Input.MouseModeEnum.Visible;
		}
		else
		{
			Input.MouseMode = Input.MouseModeEnum.Captured;
		}
	}

	public override void _Process(double delta)
	{
		// Entering the hand view aims the head at the cards on its own, rather than leaving it
		// pointed wherever the mouse last left it. Only a drive TOWARDS the pose, never a
		// restore away from it: leaving the hand view simply stops this running, so mouse-look
		// picks up from the head-down pose the player is already looking out of, which is what
		// makes the return feel continuous rather than like a snap back to centre.
		//
		// No conflict with mouse input -- that branch of _UnhandledInput only reads motion
		// while the mouse is Captured, and the hand view frees it.
		if (_isHandViewHeld)
		{
			float settleWeight = 1f - Mathf.Exp(-HAND_VIEW_LOOK_SETTLE_PER_SECOND * (float)delta);
			_yawDegrees = Mathf.Lerp(_yawDegrees, HAND_VIEW_YAW_DEGREES, settleWeight);
			_pitchDegrees = Mathf.Lerp(_pitchDegrees, HAND_VIEW_PITCH_DEGREES, settleWeight);
		}

		// Yaw around world up, then pitch around the resulting (post-yaw) local right axis —
		// the standard FPS-look composition, so pitch always means "up/down relative to
		// whichever way you're currently turned" rather than a fixed world axis.
		Basis lookBasis = _restCameraWorldBasis.Rotated(Vector3.Up, Mathf.DegToRad(_yawDegrees));
		lookBasis = lookBasis.Rotated(lookBasis.X, Mathf.DegToRad(_pitchDegrees));

		// The same world-space rotation, carried from the camera's rest over onto the bone's
		// own rest — this is what sidesteps needing to know the bone's own axis convention.
        // Valid here specifically because both the camera and the bone belong to the SAME
        // character (a character only ever turns its own head relative to its own facing) — see
		// BoneLookRotator's own doc comment for why the network broadcast below cannot reuse
		// this same world delta as-is on the opponent's differently-facing character.
        Basis deltaFromRest = lookBasis * _restCameraWorldBasis.Inverse();
        Basis desiredBoneWorldBasis = deltaFromRest * _restBoneWorldBasis;

        // A clip that is actually running owns the head, and mouse look stands down for its
        // duration — but over a beat rather than on one frame, or the head jumps from where the
        // blow left it back to where the mouse is pointing. Only the BONE stands down, though:
        // lookBasis still aims the camera below, so the player keeps looking wherever they
		// please through a punch instead of having the view yanked round with the character's
		// head.
		//
		// IsPlaying is the whole test because HoldIdle deliberately leaves the player stopped
		// (Play, Seek, Stop(keepState)) — an idle character is not "playing" anything, so this is
		// false at every moment except an actual blow.
		_lookAuthority = BoneLookRotator.RampedAuthority(_lookAuthority, _animationPlayer.IsPlaying(), delta);
		BoneLookRotator.Apply(
			_skeleton, _headBoneIndex, _headBoneParentIndex, desiredBoneWorldBasis, _lookAuthority);

		// Camera position: the tuned rest position, shifted by however much the bone itself has
		// moved off ITS rest position (e.g. an animation clip's own head bob) — not the bone's
		// raw coordinates, which would put the camera inside the character's own head.
        Vector3 animatedBoneWorldPosition = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_headBoneIndex).Origin;
        Vector3 boneOffsetFromRest = animatedBoneWorldPosition - _restBoneWorldPosition;
        Vector3 cameraPosition = _restCameraWorldPosition + boneOffsetFromRest;
        Transform3D headPose = new Transform3D(lookBasis, cameraPosition);

        // Smoothstepped so the trip eases out of one pose and into the other rather than
		// starting and stopping at full speed — this is the "왔다갔다" of it.
        _handViewBlend = Mathf.MoveToward(
            _handViewBlend, _isHandViewHeld ? 1f : 0f, (float)delta / HAND_VIEW_BLEND_SECONDS);
        float easedBlend = Mathf.SmoothStep(0f, 1f, _handViewBlend);

        // Orthonormalized because the hand view camera sits under CardRest, whose 0.5 scale it
        // cancels with a 2.0 of its own. That cancellation is easy to break from the editor, and
        // a camera whose basis carries scale renders a distorted projection rather than failing.
        Transform3D handViewPose = _handViewCamera.GlobalTransform.Orthonormalized();

        GlobalTransform = ShakenBy(headPose.InterpolateWith(handViewPose, easedBlend), delta);
        Fov = Mathf.Lerp(_restFieldOfView, _handViewCamera.Fov, easedBlend);

		// The head fade exists to keep the player's own head out of their own first-person
		// view; the hand view is not that, so the fade relaxes as the camera leaves the head.
		_headFade.SetFadeStrength(1f - easedBlend);

		_timeUntilNextLookSend -= delta;
		if (_timeUntilNextLookSend <= 0)
		{
			_timeUntilNextLookSend = LOOK_SEND_INTERVAL_SECONDS;

			// Sent relative to MY OWN rest bone frame, not as the raw world delta above — see
			// BoneLookRotator's doc comment. Measured: applying the raw world delta to the
			// opponent's own rest bone (who faces 180 degrees opposite across the table) turned
			// a pitch that raised my own head-top landmark into one that lowered theirs. This
			// local-frame version does not carry that assumption, so RemoteHeadLook can compose
			// it onto ITS OWN rest instead.
			Basis localDeltaInBoneSpace = (_restBoneWorldBasis.Inverse() * desiredBoneWorldBasis).Orthonormalized();
			GameState.Instance?.SendMyLookDirection(localDeltaInBoneSpace.GetRotationQuaternion());
		}
	}
}
