using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>One pair of scissors standing in the table, and the three places it can be during a
/// 가위 win: in the table, in the winner's fist, and stuck in the loser's hand. There are two of
/// these, one per seat, because the two seats are point-symmetric — the stab's grab sweep passes
/// through world +X on my side and -X on the opponent's, so a single pair on the table could
/// only ever be reached by one of the two players.
///
/// The two moments come from elapsed TIME, not from a Call Method Track on the animation.
/// Anim_StabScissor_Baked is imported from MainCharacter.glb, and glTF carries no method tracks,
/// so a track added in the editor would be thrown away on the next re-export; a measured
/// constant here survives that and shows up in a diff. Precision is not the reason to prefer one
/// over the other — Godot fires a method track on the process tick that crosses its time, which
/// is exactly what the check in _Process does.
///
/// What DOES matter is that a trigger never decides a position. The hand is travelling at
/// 6.8 m/s through the strike, so a callback landing one 60Hz frame late is 11cm of hand travel
/// — enough to see. So the grab only switches on per-frame following (the pose comes from the
/// bone, every frame, not from wherever the hand was when the timer fired), and the strike pins
/// the scissors to the LOSER's own ScissorsStuckTarget marker — a pose that is the same whenever
/// it is read — rather than to the winner's moving hand. Late by a frame then changes when it
/// lands, never where.</summary>
public partial class ScissorsController : Node3D
{
    private const string SKELETON_PATH = "Armature/Skeleton3D";
    private const string ANIMATION_PLAYER_PATH = "AnimationPlayer";

    /// <summary>The clip these timings were measured against. Lives here rather than in
    /// MatchWorldView (which plays it) because a clip and the moments picked out of it are the
    /// same fact — re-export the clip and both move together.</summary>
    public const string STAB_ANIMATION_NAME = "Anim_StabScissor_Baked";

    /// <summary>The fist that swings. Mixamo's rig is right-handed for this clip — the left hand
    /// never leaves the table through the whole 2.5s (measured off the bone tracks).</summary>
    private const string GRIP_BONE_NAME = "mixamorig10_RightHand";

    // Both measured off Anim_StabScissor_Baked's own bone tracks by walking the right hand
    // through all 75 frames, not read off a timeline by eye:
    //
    //   0.70s (frame 21) - the hand sweeps across the table at 8.5 m/s and passes closest to
    //                      where the scissors stand.
    //   1.733s (frame 52) - the hand, having wound up overhead to y=1.45, has just finished
    //                      falling at 6.8 m/s and reached its lowest point before rebounding.
    //
    // Seconds rather than frame numbers because that is the only unit AnimationPlayer has:
    // playback is time-based and frame-rate independent, and Animation.step (1/30 here) is the
    // editor's snap grid, not a playback quantum. The clip runs 2.5s at 30fps or at 144fps.
    //
    // Both are compared against the player's OWN CurrentAnimationPosition rather than a timer
    // counted up here. A separate timer looks equivalent and is not: started alongside Play(),
    // it was measured running 0.15s ahead of the clip, because Play() does not advance the
    // animation until the next process frame and the frame that starts a round is a long one.
    // Any hitch, and Engine.TimeScale, would pull them apart the same way. Reading the player's
    // clock is what makes these two numbers mean the same thing a Call Method Track would.
    private const float GRAB_SECONDS = 0.70f;
    private const float STRIKE_SECONDS = 1.733f;

    /// <summary>Half the model's own length, measured: the mesh is 13.1cm tip to handle, centred
    /// on its origin, with the tip at local -Z and the handle at +Z.</summary>
    private const float HALF_LENGTH_METERS = 0.065677f;

    // How the scissors sit in the fist, in the hand bone's own frame. The rotation turns the
    // model's tip (local -Z) along the bone's +Y, which is the direction Mixamo bones extend —
    // so the blades point out past the fingers — and the offset slides the handle back to the
    // wrist so the rings are in the fist rather than floating past it. Eyeball these two against
    // the running game; they are the grip's whole definition.
    private static readonly Vector3 GRIP_ROTATION = new Vector3(Mathf.Pi / 2f, 0f, 0f);
    private static readonly Vector3 GRIP_OFFSET = new Vector3(0f, HALF_LENGTH_METERS, 0f);

    private enum EScissorsPlace
    {
        AtRest,
        Winding,
        InHand,
        StuckInHand,
    }

    private Transform3D _restTransform;
    private EScissorsPlace _place = EScissorsPlace.AtRest;
    private AnimationPlayer? _gripAnimationPlayer;
    private Skeleton3D? _gripSkeleton;
    private int _gripBoneIndex;
    private Node3D? _stuckTarget;

    /// <summary>Run after everything on the default priority, the AnimationPlayer driving the
    /// skeleton included. Godot walks _Process in priority order and then tree order, and these
    /// nodes hang off Table, which sits ABOVE both seats in MatchWorld — so on the default
    /// priority the grip pose would be read before the animation had written that frame's bone
    /// poses, and the scissors would trail the hand by a frame. Measured as a 4.2cm gap during
    /// the grab sweep, and the strike is faster still: 6.8 m/s is 11cm of lag at 60Hz.</summary>
    private const int AFTER_THE_ANIMATION_HAS_POSED_THE_SKELETON = 1;

    public override void _Ready()
    {
        ProcessPriority = AFTER_THE_ANIMATION_HAS_POSED_THE_SKELETON;

        // Whatever the scene author dialled in, verbatim — this is the only record of it once
        // the scissors have been picked up, and it is local so moving the table takes it along.
        _restTransform = Transform;
    }

    /// <summary>Takes this pair's resting pose from the OTHER seat's pair, reflected by
    /// seatMirror. The two seats are point-symmetric, so the spot the grab sweep passes through
    /// is mirrored too — which means the two pairs are not independently placeable, they are one
    /// placement seen twice. Hand-maintaining both copies would work exactly until the first
    /// nudge of one of them, so only MyScissors is authored in the scene and the other is
    /// derived here. The .tscn still carries a transform for the opponent's pair, purely so the
    /// editor viewport shows it somewhere sensible; this overwrites it at load.</summary>
    public void MirrorRestFrom(ScissorsController authored, Transform3D seatMirror)
    {
        GlobalTransform = seatMirror * authored.GlobalTransform;
        _restTransform = Transform;
    }

    /// <summary>Starts the grab-lift-stab sequence. gripCharacter is the winner's Character node —
    /// the imported .glb root, with the skeleton and the player found from there, so callers need
    /// not know the rig's layout — and stuckTarget is the marker the scissors are pinned to on the
    /// strike, which is the LOSER's own ScissorsStuckTarget.
    ///
    /// A pair already planted in someone's hand refuses: it is not in the table to be picked up,
    /// which is the whole point of it staying there for a round. The winner still swings — the
    /// blow is the animation's business, not this node's — they simply swing empty-handed.</summary>
    public void PlayStabSequence(Node3D gripCharacter, Node3D stuckTarget)
    {
        if (_place == EScissorsPlace.StuckInHand)
        {
            return;
        }

        _gripAnimationPlayer = gripCharacter.GetNode<AnimationPlayer>(ANIMATION_PLAYER_PATH);
        _gripSkeleton = gripCharacter.GetNode<Skeleton3D>(SKELETON_PATH);
        _gripBoneIndex = _gripSkeleton.FindBone(GRIP_BONE_NAME);
        _stuckTarget = stuckTarget;

        _place = EScissorsPlace.Winding;
    }

    /// <summary>Every round's intro, on both pairs. A planted pair is deliberately left alone
    /// here — it has to still be in the loser's hand as the next round opens, which is the whole
    /// point of it.
    ///
    /// Anything not planted and not at rest is a sequence that got cut off mid-swing — the clip
    /// was interrupted, say — and would otherwise hang in the air, so it just goes home.</summary>
    public void OnRoundIntro()
    {
        if (_place != EScissorsPlace.StuckInHand && _place != EScissorsPlace.AtRest)
        {
            ReturnToRest();
        }
    }

    /// <summary>The reveal that closes a round's submission, on both pairs. A pair planted last
    /// round comes out exactly here and not earlier: the hand it pins is the one that reaches for
    /// items, items may be used right up until that player submits, so the pin is still doing its
    /// job for the whole submission window. Once submission is closed there is nothing left for
    /// it to block, and holding it any longer would only be decoration.
    ///
    /// Nothing has to count rounds to keep this from firing in the round the stab happened in.
    /// The stab runs out of PlayWinningBlow, which is downstream of that round's own reveal, so
    /// the next reveal to arrive here is always the following round's.</summary>
    public void OnRoundRevealed()
    {
        if (_place == EScissorsPlace.StuckInHand)
        {
            ReturnToRest();
        }
    }

    /// <summary>Straight back into the table, whatever it was doing. For a hard reset — a rematch
    /// reuses this scene, and a pair left planted from the last match is not part of the new
    /// one.</summary>
    public void ReturnToRest()
    {
        _place = EScissorsPlace.AtRest;
        Transform = _restTransform;
    }

    public override void _Process(double delta)
    {
        if (_place == EScissorsPlace.AtRest || _place == EScissorsPlace.StuckInHand)
        {
            return;
        }

        // Something else took the player over — the clip ended and CharacterAnimationController
        // put the idle pose back, or a new blow started. There is no meaningful position to read
        // off another clip, so this just stops moving; whatever pose the scissors are in is left
        // alone until the next round's intro sends them home.
        if (_gripAnimationPlayer!.CurrentAnimation != STAB_ANIMATION_NAME)
        {
            return;
        }

        float clipSeconds = (float)_gripAnimationPlayer.CurrentAnimationPosition;

        if (_place == EScissorsPlace.Winding)
        {
            if (clipSeconds < GRAB_SECONDS)
            {
                return;
            }

            _place = EScissorsPlace.InHand;
        }

        if (clipSeconds >= STRIKE_SECONDS)
        {
            // The marker's pose taken whole — position AND orientation — not aimed at and not
            // interpolated towards. Nothing reaches for it: one frame the scissors are in the
            // fist, the next they are planted. That is the point of a marker over the loser's
            // hand bone, which gave a position and left the angle to be guessed at in code.
            GlobalTransform = _stuckTarget!.GlobalTransform;
            _place = EScissorsPlace.StuckInHand;
            return;
        }

        GlobalTransform = ReadBoneWorldPose(_gripSkeleton!, _gripBoneIndex)
            * new Transform3D(Basis.FromEuler(GRIP_ROTATION), GRIP_OFFSET);
    }

    /// <summary>A bone's pose in WORLD space, with the rig's own scale divided back out.
    /// GetBoneGlobalPose is global among bones, not in the scene, so it needs the skeleton's
    /// GlobalTransform in front of it — and that carries the 0.01 scale the imported Armature
    /// node sits at (Mixamo bones are in centimetres). Left in, that scale would come straight
    /// through to whatever is parented to the result and render it a hundredth of its size, so
    /// only the rotation and the position survive here.</summary>
    private static Transform3D ReadBoneWorldPose(Skeleton3D skeleton, int boneIndex)
    {
        Transform3D bonePose = skeleton.GlobalTransform * skeleton.GetBoneGlobalPose(boneIndex);
        return new Transform3D(bonePose.Basis.Orthonormalized(), bonePose.Origin);
    }
}
