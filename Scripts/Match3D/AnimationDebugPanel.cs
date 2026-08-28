using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Debug-only: builds a Play button + Loop toggle row per clip an AnimationPlayer
/// knows, into an existing container. Shared by CharacterAnimationPreview (the dedicated
/// animation-inspection scene) and MatchWorldView's own debug panel (the real match camera and
/// scene, for judging a clip in context — head fade, the head-follow camera, everything else —
/// rather than in isolation), so the two do not carry two copies of the same wiring.
///
/// At most one clip loops at a time: checking a different row's Loop toggle takes over from
/// whichever was looping before, without unchecking that row's box.</summary>
public static class AnimationDebugPanel
{
    public static void BuildInto(VBoxContainer container, AnimationPlayer animationPlayer)
    {
        string? loopingClip = null;

        animationPlayer.AnimationFinished += animationName =>
        {
            if (loopingClip == animationName.ToString())
            {
                animationPlayer.Play(loopingClip);
            }
        };

        foreach (string animationName in animationPlayer.GetAnimationList())
        {
            HBoxContainer row = new HBoxContainer();

            Button playButton = new Button();
            playButton.Text = animationName;
            playButton.Pressed += () =>
            {
                animationPlayer.Stop();
                animationPlayer.Play(animationName);
            };

            CheckButton loopToggle = new CheckButton();
            loopToggle.Text = "Loop";
            loopToggle.Toggled += (bool toggled) =>
            {
                if (toggled)
                {
                    loopingClip = animationName;
                    animationPlayer.Stop();
                    animationPlayer.Play(animationName);
                }
                else if (loopingClip == animationName)
                {
                    loopingClip = null;
                    animationPlayer.Stop();
                }
            };

            row.AddChild(playButton);
            row.AddChild(loopToggle);
            container.AddChild(row);
        }
    }
}
