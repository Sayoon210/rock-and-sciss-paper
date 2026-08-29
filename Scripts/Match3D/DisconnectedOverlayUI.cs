using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.UI;

namespace RockAndScissPaper.Match3D;

/// <summary>Covers the match when the connection ends, and offers the only way out of it.
/// Until this existed, a drop mid-match left the player sitting in a dead 3D scene with a
/// captured mouse and no route anywhere — GameState reset itself and emitted OpponentLeft,
/// and nothing in MatchWorld was listening.
///
/// One handler covers both sides of a drop: GameState.HandleDisconnect runs for a peer
/// disconnecting (what the host sees) and for the server going away (what the client sees),
/// and emits OpponentLeft either way, so this does not need to know which it is — or which
/// side it is on.
///
/// Releases the mouse on appearing. MatchWorld captures it for head-look (HeadFollowCamera),
/// and a button under a captured cursor cannot be clicked, so this would otherwise be a dead
/// end with a button drawn on it.</summary>
public partial class DisconnectedOverlayUI : Control
{
    private const string TITLE_BUTTON_PATH = "Panel/Layout/TitleScreenButton";

    public override void _Ready()
    {
        GetNode<Button>(TITLE_BUTTON_PATH).Pressed += OnTitleScreenPressed;

        GameState.Instance!.OpponentLeft += OnOpponentLeft;

        Visible = false;
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.OpponentLeft -= OnOpponentLeft;
        }
    }

    private void OnOpponentLeft()
    {
        Visible = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    private void OnTitleScreenPressed()
    {
        ScreenRouter.GoToTitleScreen(this);
    }
}
