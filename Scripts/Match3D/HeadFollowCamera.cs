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
    private const string SKELETON_PATH = "../Character/Armature/Skeleton3D";
    private const string HEAD_BONE_NAME = "mixamorig10_Head";

    private const float MOUSE_SENSITIVITY_DEGREES_PER_PIXEL = 0.1f;
    private const float MAX_LOOK_DEGREES = 60f;

    // Cosmetic-only network traffic (BoneLookRotator/GameState.SendMyLookDirection) — sent on
    // a timer well under the render rate rather than every _Process, since a dropped or
    // superseded update costs nothing but does not need repeating 60 times a second.
    private const double LOOK_SEND_INTERVAL_SECONDS = 1.0 / 15.0;

    private Skeleton3D _skeleton = null!;
    private int _headBoneIndex;
    private int _headBoneParentIndex;
    private Basis _restCameraWorldBasis;
    private Basis _restBoneWorldBasis;
    private Vector3 _restCameraWorldPosition;
    private Vector3 _restBoneWorldPosition;
    private float _yawDegrees;
    private float _pitchDegrees;
    private double _timeUntilNextLookSend;

    public override void _Ready()
    {
        Input.MouseMode = Input.MouseModeEnum.Captured;

        // The camera's own authored transform (Scenes/Screens/MatchWorld.tscn) — already tuned
        // to face the opponent correctly, and NOT the same spot as the head bone's own joint
        // (that sits inside the head — a camera placed exactly there ends up embedded in the
        // character's own geometry). Read before anything below touches GlobalTransform.
        _restCameraWorldBasis = GlobalTransform.Basis;
        _restCameraWorldPosition = GlobalTransform.Origin;

        _skeleton = GetNode<Skeleton3D>(SKELETON_PATH);
        _headBoneIndex = _skeleton.FindBone(HEAD_BONE_NAME);
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
        else if (@event is InputEventKey key && key.Pressed && key.Keycode == Key.Escape)
        {
            Input.MouseMode = Input.MouseModeEnum.Visible;
        }
    }

    public override void _Process(double delta)
    {
        // Yaw around world up, then pitch around the resulting (post-yaw) local right axis —
        // the standard FPS-look composition, so pitch always means "up/down relative to
        // whichever way you're currently turned" rather than a fixed world axis.
        Basis lookBasis = _restCameraWorldBasis.Rotated(Vector3.Up, Mathf.DegToRad(_yawDegrees));
        lookBasis = lookBasis.Rotated(lookBasis.X, Mathf.DegToRad(_pitchDegrees));

        // The same world-space rotation, carried from the camera's rest over onto the bone's
        // own rest — this is what sidesteps needing to know the bone's own axis convention,
        // here or on the receiving end of the network broadcast below.
        Basis deltaFromRest = lookBasis * _restCameraWorldBasis.Inverse();
        BoneLookRotator.Apply(_skeleton, _headBoneIndex, _headBoneParentIndex, _restBoneWorldBasis, deltaFromRest);

        // Camera position: the tuned rest position, shifted by however much the bone itself has
        // moved off ITS rest position (e.g. an animation clip's own head bob) — not the bone's
        // raw coordinates, which would put the camera inside the character's own head.
        Vector3 animatedBoneWorldPosition = _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(_headBoneIndex).Origin;
        Vector3 boneOffsetFromRest = animatedBoneWorldPosition - _restBoneWorldPosition;
        Vector3 cameraPosition = _restCameraWorldPosition + boneOffsetFromRest;

        GlobalTransform = new Transform3D(lookBasis, cameraPosition);

        _timeUntilNextLookSend -= delta;
        if (_timeUntilNextLookSend <= 0)
        {
            _timeUntilNextLookSend = LOOK_SEND_INTERVAL_SECONDS;
            GameState.Instance?.SendMyLookDirection(deltaFromRest.GetRotationQuaternion());
        }
    }
}
