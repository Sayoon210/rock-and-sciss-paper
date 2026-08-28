using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Plays one clip of the character over and over so an animation authored in
/// Blender can be judged under the game's own renderer rather than Blender's viewport.
/// It knows nothing about a match — a real round decides which animation runs from a
/// GameState signal, and this scene exists only so the model can be looked at.</summary>
public partial class CharacterAnimationPreview : Node3D
{
    /// <summary>Which clip to watch. These names come from the NLA tracks the .glb was
    /// exported with, so they change whenever Blender re-exports under new track names.</summary>
    [Export] public string AnimationName { get; set; } = "Anim_Punch_Baked";

    private AnimationPlayer _animationPlayer = null!;

    public override void _Ready()
    {
        _animationPlayer = GetNode<AnimationPlayer>("MainCharacter/AnimationPlayer");

        // Every imported clip arrives one-shot because glTF carries no loop flag, and
        // that is the right default for the real scene — a punch should not repeat.
        // Restarting on the signal loops it here without editing the imported animation,
        // which would change how it behaves everywhere it is used.
        _animationPlayer.AnimationFinished += OnAnimationFinished;
        _animationPlayer.Play(AnimationName);
    }

    private void OnAnimationFinished(StringName finishedAnimationName)
    {
        _animationPlayer.Play(AnimationName);
    }
}
