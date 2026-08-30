using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Holds the character on its idle pose (CharacterAnimationController.HoldIdle) purely
/// so the editor's own 3D viewport shows the seated pose while editing a scene, instead of the
/// raw imported standing bind pose. [Tool] is what makes _Ready() run in the editor at all, not
/// only when the game is actually playing.
///
/// Attached directly to a Character instance node (a sibling of its own AnimationPlayer), not
/// to the imported MainCharacter.glb itself — the asset stays untouched, this is scene-local.
///
/// At runtime this duplicates MatchWorldView's own initial HoldIdle() call through its own
/// CharacterAnimationController — harmless, since holding the same frame twice is a no-op.
/// MatchWorldView still owns everything past that: playing a win blow, and returning to idle
/// once it finishes.</summary>
[Tool]
public partial class CharacterIdlePose : Node3D
{
	public override void _Ready()
	{
		if (GetNodeOrNull<AnimationPlayer>("AnimationPlayer") is AnimationPlayer animationPlayer)
		{
			new CharacterAnimationController(animationPlayer).HoldIdle();
		}
	}
}
