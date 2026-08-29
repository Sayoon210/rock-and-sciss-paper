using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.UI;

/// <summary>Every screen-to-screen move in the game, and the one place the scene paths are
/// written down. Before this, each screen held its own copy of the paths it happened to
/// navigate to, and each one decided for itself what (if anything) to tear down on the way
/// out — which is why nothing ever called NetworkManager.Disconnect despite its own doc
/// comment naming "leaving for the title screen" as its reason to exist.
///
/// A static class rather than an Autoload: it holds no state and outlives nothing. It takes
/// the calling Node only because ChangeSceneToFile lives on SceneTree, which a plain static
/// has no way to reach on its own.
///
/// Every route releases the mouse, because MatchWorld captures it (HeadFollowCamera) and a
/// menu reached with a captured cursor cannot be clicked. MatchWorld re-captures on its own
/// way in, so doing this unconditionally costs nothing.</summary>
public static class ScreenRouter
{
    public const string TITLE_SCREEN_PATH = "res://Scenes/Screens/TitleScreen.tscn";
    public const string CONNECTION_SCREEN_PATH = "res://Scenes/Screens/ConnectionScreen.tscn";
    public const string MATCH_WORLD_PATH = "res://Scenes/Screens/MatchWorld.tscn";

    // The 교체/변화 choice phase has not been verified across two instances yet, and the
    // debug harness is still the only thing that can drive it. It stopped being the project's
    // main scene when the title screen took over, so it needs a door.
    public const string DEBUG_HARNESS_PATH = "res://Scenes/MatchDebugUI.tscn";

    /// <summary>The title screen is the one route that also ends the session's connection.
    /// Anywhere else, the connection is either not up yet or is the thing being used; here it
    /// is being abandoned, and a peer left open would still be sitting there when the player
    /// next presses 방 만들기 — NetworkManager owns the peer, so it is the one that closes it,
    /// and GameState clears the match state that peer's existence implied.</summary>
    public static void GoToTitleScreen(Node caller)
    {
        NetworkManager.Instance?.Disconnect();
        GameState.Instance?.ResetConnection();
        ChangeScene(caller, TITLE_SCREEN_PATH);
    }

    public static void GoToConnectionScreen(Node caller)
    {
        ChangeScene(caller, CONNECTION_SCREEN_PATH);
    }

    public static void GoToMatchWorld(Node caller)
    {
        ChangeScene(caller, MATCH_WORLD_PATH);
    }

    public static void GoToDebugHarness(Node caller)
    {
        ChangeScene(caller, DEBUG_HARNESS_PATH);
    }

    private static void ChangeScene(Node caller, string scenePath)
    {
        Input.MouseMode = Input.MouseModeEnum.Visible;
        caller.GetTree().ChangeSceneToFile(scenePath);
    }
}
