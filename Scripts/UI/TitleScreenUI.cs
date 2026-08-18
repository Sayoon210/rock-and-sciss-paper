using Godot;

namespace RockAndScissPaper.UI;

/// <summary>The first screen. Nothing here touches match state — it only picks the next
/// scene.</summary>
public partial class TitleScreenUI : Control
{
    private const string CONNECTION_SCENE_PATH = "res://Scenes/Screens/ConnectionScreen.tscn";

    // The 교체/변화 choice phase has not been verified across two instances yet, and the
    // debug harness is still the only thing that can drive it. It stopped being the project's
    // main scene when this screen took over, so it needs a door.
    private const string DEBUG_HARNESS_SCENE_PATH = "res://Scenes/MatchDebugUI.tscn";

    public override void _Ready()
    {
        GetNode<Button>("CenterContainer/Buttons/MatchButton").Pressed += OnMatchPressed;
        GetNode<Button>("CenterContainer/Buttons/QuitButton").Pressed += OnQuitPressed;
        GetNode<Button>("CenterContainer/Buttons/DebugHarnessButton").Pressed += OnDebugHarnessPressed;
    }

    private void OnMatchPressed()
    {
        GetTree().ChangeSceneToFile(CONNECTION_SCENE_PATH);
    }

    private void OnQuitPressed()
    {
        GetTree().Quit();
    }

    private void OnDebugHarnessPressed()
    {
        GetTree().ChangeSceneToFile(DEBUG_HARNESS_SCENE_PATH);
    }
}
