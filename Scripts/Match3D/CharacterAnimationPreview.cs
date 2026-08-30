using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>A Play + Loop row per clip the character was imported with (AnimationDebugPanel),
/// so an animation authored in Blender can be judged under the game's own renderer rather
/// than Blender's viewport. It knows nothing about a match — a real round decides which
/// animation runs from a GameState signal.</summary>
public partial class CharacterAnimationPreview : Node3D
{
	public override void _Ready()
	{
		AnimationDebugPanel.BuildInto(
			GetNode<VBoxContainer>("DebugInterface/AnimationButtons"),
			GetNode<AnimationPlayer>("Character/AnimationPlayer"));
	}
}
