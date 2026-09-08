using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Snaps this character's upper body backwards, then lets it come home — what being
/// punched looks like from the other side of the table.
///
/// **Local and cosmetic, and deliberately not symmetric.** Only the character the player is
/// looking at ever gets kicked, and only when that character is the one who lost: MatchWorldView
/// arms this on the opponent when the local player wins with 바위, and on nobody otherwise. The
/// loser's own screen looks out of that character's own eyes, so there is no body there to watch
/// rock backwards — being hit is already carried on that side by the camera shake, which fires
/// on both screens for exactly that reason. Nothing about this crosses the network; both clients
/// resolve the round the same way and each decides for itself whether it has anything to draw.
///
/// 바위 only, of the three. 보 lands on the desk rather than on anyone. 가위 pins a hand to the
/// table and leaves the scissors standing in it (ScissorsController) — throwing that body
/// backwards would tear the pinned hand away from the prop it is impaled on.
///
/// The bend is applied against a pose banked when the blow lands, not composed onto whatever the
/// bone currently holds. Composing looks like the obvious answer — multiply the bend onto the
/// pose that is there and a clip underneath keeps its own motion — but nothing rewrites these
/// bones between frames while a character is idle (the loser's AnimationPlayer is stopped, and
/// CharacterIdlePose poses the skeleton once), so each frame would multiply its bend onto the
/// last frame's bend. Measured: the head went 0.48m back over six frames, kept going, and came
/// out in FRONT of the body having wrapped past a half turn. Banking the pose makes every frame
/// an absolute "this far back from where the body was", which is the thing being animated.
///
/// **The hands stay on the table.** Both arms hang off the top of the spine, so bending it alone
/// carries them along — measured, a full 22 degrees lifted each hand 9.5cm straight up off a
/// table its palm was resting on, which reads as the whole character floating rather than as a
/// waist bending. So the hands are banked in WORLD space alongside the spine and put back every
/// frame, by opening the elbow to the right span and then swinging the shoulder onto the mark
/// (SolveArmOntoBankedHand). The arms come out nearly straight at full bend, which is what an
/// arm braced against a table does — there is 1.7cm of reach to spare at 22 degrees, and about
/// 5mm at 28, so that is roughly where this stops being possible at all.</summary>
public partial class CharacterHitRecoil : Node3D
{
    /// <summary>How far the whole chain bends at full strength, and how it is shared out along
    /// it. Split across three joints rather than hinged at the waist alone — one joint doing all
    /// of it reads as a folding chair, and the same total angle spread up the spine reads as a
    /// body being moved. Lowest joint takes the most, which is what puts the break at the waist.</summary>
    private const float RECOIL_DEGREES = 22f;

    private static readonly (string BoneName, float Share)[] SPINE_CHAIN =
    {
        (MixamoRig.SPINE, 0.5f),
        (MixamoRig.SPINE_1, 0.3f),
        (MixamoRig.SPINE_2, 0.2f),
    };

    /// <summary>How long the body stays thrown back before it starts coming up, and how long it
    /// then takes to get there. Three beats, not two: the snap is instant, the hold is what makes
    /// the hit register as having landed, and the return is slow enough to read as the body
    /// recovering rather than the pose being switched off.
    ///
    /// This replaced an exponential decay, which was wrong for the shape rather than merely
    /// mistuned — an exponential moves FASTEST at the instant it starts, so the body left the
    /// bottom of the swing the same frame it arrived and no amount of slowing the rate down
    /// bought a hold. HeadFollowCamera's shake keeps its exponential because that IS the shape a
    /// knock wants: hardest on the first frame, gone almost at once.</summary>
    private const float RECOIL_HOLD_SECONDS = 0.15f;
    private const float RECOIL_RETURN_SECONDS = 1.0f;

    /// <summary>The two arms whose hands are held still while the body moves, each as the three
    /// bones between the shoulder joint and the wrist.</summary>
    private static readonly (string Arm, string ForeArm, string Hand)[] PLANTED_ARMS =
    {
        (MixamoRig.LEFT_ARM, MixamoRig.LEFT_FORE_ARM, MixamoRig.LEFT_HAND),
        (MixamoRig.RIGHT_ARM, MixamoRig.RIGHT_FORE_ARM, MixamoRig.RIGHT_HAND),
    };

    /// <summary>Below this a direction is too short to normalise into a rotation without the
    /// float noise in it becoming the answer.</summary>
    private const float NEGLIGIBLE_LENGTH_SQUARED = 1e-8f;

    private Skeleton3D _skeleton = null!;
    private readonly int[] _spineBoneIndices = new int[SPINE_CHAIN.Length];
    private readonly int[] _armBoneIndices = new int[PLANTED_ARMS.Length];
    private readonly int[] _foreArmBoneIndices = new int[PLANTED_ARMS.Length];
    private readonly int[] _handBoneIndices = new int[PLANTED_ARMS.Length];

    /// <summary>Where each hand was in WORLD space when the blow landed — position and
    /// orientation both, so a palm flat on the table stays flat instead of tilting with the
    /// forearm that is being rotated under it.</summary>
    private readonly Transform3D[] _bankedHands = new Transform3D[PLANTED_ARMS.Length];

    /// <summary>The arm bones' own local poses at the same instant. The solver leaves them
    /// wherever the last frame's aiming put them, so these are what puts every bone this class
    /// touched back exactly as it found it once the recoil is over.</summary>
    private readonly Quaternion[] _bankedArmPoses = new Quaternion[PLANTED_ARMS.Length];
    private readonly Quaternion[] _bankedForeArmPoses = new Quaternion[PLANTED_ARMS.Length];
    private readonly Quaternion[] _bankedHandPoses = new Quaternion[PLANTED_ARMS.Length];

    /// <summary>Where the spine sat when the blow landed, and what the bend is measured from
    /// for as long as it lasts. Taken on the first Kick only: a second blow arriving mid-recoil
    /// must not bank an already-bent spine as the pose to come home to.</summary>
    private readonly Quaternion[] _bankedPoses = new Quaternion[SPINE_CHAIN.Length];

    /// <summary>How far back the blow threw the body, and how long ago it landed. Together they
    /// are the whole animation — the bend at any moment is read off the clock rather than
    /// carried forward from last frame, so there is nothing to drift.</summary>
    private float _peakStrength;
    private float _secondsSinceKick;

    /// <summary>Whether the body is mid-recoil. RemoteHeadLook asks, so the head goes with the
    /// body instead of staying pinned to where the mouse last pointed — that bone is held to a
    /// WORLD orientation, so without this the neck would bend out from under a head that never
    /// moved.</summary>
    public bool IsRecoiling
    {
        get { return _peakStrength > 0f; }
    }

    public override void _Ready()
    {
        Skeleton3D? skeleton = MixamoRig.FindSkeleton(GetParent());
        if (skeleton == null)
        {
            // FindSkeleton has already said what is wrong. No skeleton means nothing to bend.
            SetProcess(false);
            return;
        }

        _skeleton = skeleton;
        for (int link = 0; link < SPINE_CHAIN.Length; link++)
        {
            _spineBoneIndices[link] = MixamoRig.FindBone(_skeleton, SPINE_CHAIN[link].BoneName);
            if (_spineBoneIndices[link] < 0)
            {
                SetProcess(false);
                return;
            }
        }

        for (int side = 0; side < PLANTED_ARMS.Length; side++)
        {
            _armBoneIndices[side] = MixamoRig.FindBone(_skeleton, PLANTED_ARMS[side].Arm);
            _foreArmBoneIndices[side] = MixamoRig.FindBone(_skeleton, PLANTED_ARMS[side].ForeArm);
            _handBoneIndices[side] = MixamoRig.FindBone(_skeleton, PLANTED_ARMS[side].Hand);
            if (_armBoneIndices[side] < 0 || _foreArmBoneIndices[side] < 0 || _handBoneIndices[side] < 0)
            {
                SetProcess(false);
                return;
            }
        }

        // Nothing to do until something lands. _Process is turned back on by Kick.
        SetProcess(false);
    }

    /// <summary>Throws the body backwards, now. Called on the frame the blow actually connects,
    /// by the same code that kicks the camera — one instant, read off the clip's own clock
    /// (MatchWorldView), so the body moves when the fist arrives rather than when the animation
    /// was started.
    ///
    /// Takes the larger of the two rather than adding, the same way the camera shake does: two
    /// blows landing close together must not fold the body twice as far as either one meant to.
    /// The clock restarts either way, so the second blow gets its own hold rather than inheriting
    /// however much of the first one's was left.</summary>
    public void Kick(float strength)
    {
        if (!IsRecoiling)
        {
            for (int link = 0; link < SPINE_CHAIN.Length; link++)
            {
                _bankedPoses[link] = _skeleton.GetBonePoseRotation(_spineBoneIndices[link]);
            }

            for (int side = 0; side < PLANTED_ARMS.Length; side++)
            {
                _bankedHands[side] = BoneWorldTransform(_handBoneIndices[side]);
                _bankedArmPoses[side] = _skeleton.GetBonePoseRotation(_armBoneIndices[side]);
                _bankedForeArmPoses[side] = _skeleton.GetBonePoseRotation(_foreArmBoneIndices[side]);
                _bankedHandPoses[side] = _skeleton.GetBonePoseRotation(_handBoneIndices[side]);
            }
        }

        _peakStrength = Mathf.Max(_peakStrength, Mathf.Clamp(strength, 0f, 1f));
        _secondsSinceKick = 0f;
        SetProcess(true);
    }

    public override void _Process(double delta)
    {
        _secondsSinceKick += (float)delta;

        float bentFraction = BentFractionNow();
        if (bentFraction <= 0f)
        {
            // Put the spine back exactly where it was rather than a fraction of a degree out, so
            // nothing accumulates across a match's worth of punches.
            _peakStrength = 0f;
            RestoreBankedPoses();
            SetProcess(false);
            return;
        }

        // NEGATIVE about the bone's local X is backwards, measured against this rig rather than
        // assumed: rotating the Spine -25 degrees moves the head 0.20m along the character's own
        // -Z (behind it), and +25 moves it the same distance in front. Local X is also the only
        // one of the three axes that bends the body at all — Y twists it and Z tips it sideways.
        float bendDegrees = RECOIL_DEGREES * bentFraction;
        for (int link = 0; link < SPINE_CHAIN.Length; link++)
        {
            Quaternion bend = new Quaternion(
                Vector3.Right, Mathf.DegToRad(-bendDegrees * SPINE_CHAIN[link].Share));
            _skeleton.SetBonePoseRotation(_spineBoneIndices[link], _bankedPoses[link] * bend);
        }

        // The spine has moved, so every shoulder above it has moved with it — and the arm solving
        // below reads those world positions back on this same frame. Nothing rebuilds them
        // explicitly: GetBoneGlobalPose brings the chain up to date itself, which is why
        // ForceUpdateAllBoneTransforms is deprecated in 4.7 as internal-only. Measured against
        // this rig — a pose written on the Spine and a LeftHand global pose read straight after
        // it, with no forced update, gave a position identical to the forced one to six decimal
        // places.
        for (int side = 0; side < PLANTED_ARMS.Length; side++)
        {
            SolveArmOntoBankedHand(side);
        }
    }

    /// <summary>Bends one arm so its wrist ends up back where the blow found it. Two steps, in
    /// this order and only once each:
    ///
    /// 1. Open or close the elbow until the shoulder-to-wrist DISTANCE matches the distance to
    ///    the mark. That angle is the law of cosines on the two arm segments, so it is arrived at
    ///    rather than approached.
    /// 2. Swing the whole arm at the shoulder so that distance points at the mark.
    ///
    /// Splitting it that way is what makes one pass exact. Repeatedly aiming each joint at the
    /// mark instead — ordinary cyclic descent, which is what this did first — only ever corrects
    /// DIRECTION, and the error left after bending the spine is mostly radial: the wrist ends up
    /// on the right ray at the wrong distance, and each further pass shaves very little off.
    /// Measured on this rig, three passes still left the wrist 11mm out on the frame of impact.
    ///
    /// Nothing stops the elbow bending the wrong way, and nothing needs to: the bend plane is
    /// taken from where the arm already is, so the elbow opens and closes in the plane the pose
    /// was authored in. A mark further away than the arm is long is not a special case either —
    /// the distance is clamped to what the arm can span, the elbow straightens, and the hand
    /// stops short, which is what an arm does.</summary>
    private void SolveArmOntoBankedHand(int side)
    {
        int handIndex = _handBoneIndices[side];
        Vector3 target = _bankedHands[side].Origin;

        OpenElbowToSpan(side, target);
        AimBoneAtTarget(_armBoneIndices[side], handIndex, target);

        // The wrist is back in place but has been carried around by two rotations on its way
        // there, so a palm laid flat on the table would have tilted with them. Its banked WORLD
        // orientation is written straight back on, which the rest of the arm has no say in.
        BoneLookRotator.Apply(
            _skeleton,
            handIndex,
            _skeleton.GetBoneParent(handIndex),
            _bankedHands[side].Basis.Orthonormalized(),
            1f);
    }

    /// <summary>Sets the elbow angle so the wrist sits exactly as far from the shoulder as the
    /// mark is, leaving the direction alone for the shoulder to fix.</summary>
    private void OpenElbowToSpan(int side, Vector3 target)
    {
        Vector3 shoulder = BoneWorldTransform(_armBoneIndices[side]).Origin;
        Vector3 elbow = BoneWorldTransform(_foreArmBoneIndices[side]).Origin;
        Vector3 wrist = BoneWorldTransform(_handBoneIndices[side]).Origin;

        float upperArm = (elbow - shoulder).Length();
        float foreArm = (wrist - elbow).Length();
        if (upperArm <= 0f || foreArm <= 0f)
        {
            return;
        }

        // What the arm can actually span: never longer than both segments end to end, and never
        // shorter than the difference between them (the elbow folded right back on itself).
        float span = Mathf.Clamp(
            (target - shoulder).Length(), Mathf.Abs(upperArm - foreArm), upperArm + foreArm);

        float cosineOfElbow = (upperArm * upperArm + foreArm * foreArm - span * span)
            / (2f * upperArm * foreArm);
        float desiredElbowAngle = Mathf.Acos(Mathf.Clamp(cosineOfElbow, -1f, 1f));

        Vector3 towardShoulder = shoulder - elbow;
        Vector3 towardWrist = wrist - elbow;
        Vector3 bendAxis = towardShoulder.Cross(towardWrist);
        if (bendAxis.LengthSquared() < NEGLIGIBLE_LENGTH_SQUARED)
        {
            // A perfectly straight arm has no plane to bend in, so there is no axis to turn
            // about. Left alone: the shoulder swing that follows still points it at the mark.
            return;
        }

        // Turning the forearm about shoulder-cross-wrist by a positive angle opens the elbow, so
        // the correction is simply the difference between the angle wanted and the one there.
        float currentElbowAngle = towardShoulder.AngleTo(towardWrist);
        Quaternion openBy = new Quaternion(
            bendAxis.Normalized(), desiredElbowAngle - currentElbowAngle);

        int foreArmIndex = _foreArmBoneIndices[side];
        BoneLookRotator.Apply(
            _skeleton,
            foreArmIndex,
            _skeleton.GetBoneParent(foreArmIndex),
            new Basis(openBy) * BoneWorldTransform(foreArmIndex).Basis.Orthonormalized(),
            1f);
    }

    /// <summary>Turns one joint so that a bone further down the same arm swings onto a world
    /// point. The shortest arc between where that bone is and where it should be, laid onto the
    /// joint's current world orientation — shortest specifically because it adds no twist about
    /// the joint's own axis, which is the part of an arm's pose an animator meant.</summary>
    private void AimBoneAtTarget(int jointIndex, int reachingBoneIndex, Vector3 target)
    {
        Vector3 joint = BoneWorldTransform(jointIndex).Origin;
        Vector3 toReachingBone = BoneWorldTransform(reachingBoneIndex).Origin - joint;
        Vector3 toTarget = target - joint;
        if (toReachingBone.LengthSquared() < NEGLIGIBLE_LENGTH_SQUARED
            || toTarget.LengthSquared() < NEGLIGIBLE_LENGTH_SQUARED)
        {
            return;
        }

        Quaternion swing = new Quaternion(toReachingBone.Normalized(), toTarget.Normalized());
        Basis jointWorldBasis = BoneWorldTransform(jointIndex).Basis.Orthonormalized();

        BoneLookRotator.Apply(
            _skeleton,
            jointIndex,
            _skeleton.GetBoneParent(jointIndex),
            new Basis(swing) * jointWorldBasis,
            1f);
    }

    private Transform3D BoneWorldTransform(int boneIndex)
    {
        return _skeleton.GlobalTransform * _skeleton.GetBoneGlobalPose(boneIndex);
    }

    /// <summary>How far into the bend the body is right now, 1 at full and 0 once it is home.
    /// Held flat through the hold, then eased off over the return — smoothstep rather than a
    /// straight line so it leaves the bottom of the swing gently (which is what extends the hold
    /// into something that reads as weight) and arrives at rest without stopping dead.</summary>
    private float BentFractionNow()
    {
        if (_secondsSinceKick < RECOIL_HOLD_SECONDS)
        {
            return _peakStrength;
        }

        float returned = (_secondsSinceKick - RECOIL_HOLD_SECONDS) / RECOIL_RETURN_SECONDS;
        if (returned >= 1f)
        {
            return 0f;
        }

        return _peakStrength * (1f - Mathf.SmoothStep(0f, 1f, returned));
    }

    private void RestoreBankedPoses()
    {
        for (int link = 0; link < SPINE_CHAIN.Length; link++)
        {
            _skeleton.SetBonePoseRotation(_spineBoneIndices[link], _bankedPoses[link]);
        }

        for (int side = 0; side < PLANTED_ARMS.Length; side++)
        {
            _skeleton.SetBonePoseRotation(_armBoneIndices[side], _bankedArmPoses[side]);
            _skeleton.SetBonePoseRotation(_foreArmBoneIndices[side], _bankedForeArmPoses[side]);
            _skeleton.SetBonePoseRotation(_handBoneIndices[side], _bankedHandPoses[side]);
        }
    }
}
