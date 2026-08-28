using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>One character's AnimationPlayer, wrapped so idle-hold and blow-then-return-to-idle
/// exist once instead of duplicated per character in MatchWorldView — there are always two of
/// these, one per seat.</summary>
public sealed class CharacterAnimationController
{
    // No idle clip has been authored on its own — every baked clip already starts from the
    // same seated resting pose (bone positions checked directly, not just eyeballed), so idle
    // is that first frame held rather than a separate animation. Any of the three would do;
    // this one is picked because it is also a win animation already in use.
    private const string IDLE_ANIMATION_SOURCE = "Anim_Punch_Baked";

    private readonly AnimationPlayer _animationPlayer;

    public CharacterAnimationController(AnimationPlayer animationPlayer)
    {
        _animationPlayer = animationPlayer;

        // A finished blow returns to idle rather than freezing on its last frame forever.
        _animationPlayer.AnimationFinished += _ => HoldIdle();

        HoldIdle();
    }

    public void PlayBlow(string clipName)
    {
        _animationPlayer.Play(clipName);
    }

    /// <summary>Applies IDLE_ANIMATION_SOURCE's first frame as a held pose rather than
    /// playing it — Play then Seek to frame 0 then Stop(keepState: true) is what leaves the
    /// pose applied without the AnimationPlayer still "running" anything.</summary>
    public void HoldIdle()
    {
        _animationPlayer.Play(IDLE_ANIMATION_SOURCE);
        _animationPlayer.Seek(0.0, true);
        _animationPlayer.Stop(true);
    }
}
