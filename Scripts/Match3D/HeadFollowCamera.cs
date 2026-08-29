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
    private const string SKELETON_PATH = "../Character/Armature/Skeleton3D";
    private const string HEAD_BONE_NAME = "mixamorig10_Head";
    private const string HAND_VIEW_CAMERA_PATH = "../../Field/CardRest/HandViewCamera";
    private const string HEAD_FADE_PATH = "../Character/HeadFade";

    private const float MOUSE_SENSITIVITY_DEGREES_PER_PIXEL = 0.1f;
    private const float MAX_LOOK_DEGREES = 60f;

    // How long the trip between the head and the hand takes, each way.
    private const float HAND_VIEW_BLEND_SECONDS = 0.35f;

    // Cosmetic-only network traffic (BoneLookRotator/GameState.SendMyLookDirection) — sent on
    // a timer well under the render rate rather than every _Process, since a dropped or
    // superseded update costs nothing but does not need repeating 60 times a second.
    private const double LOOK_SEND_INTERVAL_SECONDS = 1.0 / 15.0;

    private Skeleton3D _skeleton = null!;
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

    /// <summary>Whether the hand view is the current destination — true from the Space press
    /// that starts the trip toward the hand until whatever starts the trip back (another Space,
    /// or a submission calling ReturnToHeadView). HandView gates card grabbing on this, which
    /// also means an in-progress grab cancels itself the moment the camera is sent home.</summary>
    public bool IsHandViewHeld
    {
        get { return _isHandViewHeld; }
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
        else if (@event is InputEventKey key && key.Pressed && !key.Echo)
        {
            if (key.Keycode == Key.Escape)
            {
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
            else if (key.Keycode == Key.Space)
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
        BoneLookRotator.Apply(_skeleton, _headBoneIndex, _headBoneParentIndex, desiredBoneWorldBasis);

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

        GlobalTransform = headPose.InterpolateWith(handViewPose, easedBlend);
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
