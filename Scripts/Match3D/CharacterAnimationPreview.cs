using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>One button per clip the character was imported with, each playing that clip once,
/// so an animation authored in Blender can be judged under the game's own renderer rather
/// than Blender's viewport. It knows nothing about a match — a real round decides which
/// animation runs from a GameState signal.
///
/// The buttons are built from AnimationPlayer.GetAnimationList rather than written out, so a
/// re-export from Blender that adds or renames a clip needs no edit here — which matters
/// while the clip list is still changing every time the .glb is rebuilt.
///
/// Button text is plain English on purpose. This is a debug harness like MatchDebugUI, which
/// does the same; no player ever reads these, so they are not translation symbols.</summary>
public partial class CharacterAnimationPreview : Node3D
{
    private AnimationPlayer _animationPlayer = null!;
    private VBoxContainer _animationButtons = null!;

    public override void _Ready()
    {
        _animationPlayer = GetNode<AnimationPlayer>("MainCharacter/AnimationPlayer");
        _animationButtons = GetNode<VBoxContainer>("DebugInterface/AnimationButtons");

        BuildAnimationButtons();
    }

    private void BuildAnimationButtons()
    {
        foreach (string animationName in _animationPlayer.GetAnimationList())
        {
            Button button = new Button();
            button.Text = animationName;

            // Plays from the start every press, so pressing the same button twice replays it
            // instead of doing nothing once the clip has run out.
            button.Pressed += () => PlayFromStart(animationName);

            _animationButtons.AddChild(button);
        }
    }

    private void PlayFromStart(string animationName)
    {
        _animationPlayer.Stop();
        _animationPlayer.Play(animationName);
    }
}
