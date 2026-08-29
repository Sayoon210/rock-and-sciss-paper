using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>Both sides' health in the top-right corner, as bars rather than the numbers the
/// existing top-left readout already prints. A bar is what makes a hit legible at a glance —
/// DESIGN.md's damage table gives 바위 twice the bite of 가위/보, and that difference is the
/// thing a number two pixels tall does not communicate.
///
/// Reads GameState.View and nothing else (Scripts/CLAUDE.md), on the host too, and refreshes
/// on the same signals MatchWorldView does. It shows health; it never works out what health
/// should be.</summary>
public partial class HealthBarsUI : Control
{
    private const string MY_BAR_PATH = "MyHealth/Bar";
    private const string MY_LABEL_PATH = "MyHealth/Label";
    private const string OPPONENT_BAR_PATH = "OpponentHealth/Bar";
    private const string OPPONENT_LABEL_PATH = "OpponentHealth/Label";

    private ProgressBar _myBar = null!;
    private Label _myLabel = null!;
    private ProgressBar _opponentBar = null!;
    private Label _opponentLabel = null!;

    public override void _Ready()
    {
        _myBar = GetNode<ProgressBar>(MY_BAR_PATH);
        _myLabel = GetNode<Label>(MY_LABEL_PATH);
        _opponentBar = GetNode<ProgressBar>(OPPONENT_BAR_PATH);
        _opponentLabel = GetNode<Label>(OPPONENT_LABEL_PATH);

        _myBar.MaxValue = MatchSession.STARTING_HEALTH;
        _opponentBar.MaxValue = MatchSession.STARTING_HEALTH;

        GameState.Instance!.MatchStarted += Refresh;
        GameState.Instance.RoundResolved += Refresh;

        Refresh();
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.MatchStarted -= Refresh;
            GameState.Instance.RoundResolved -= Refresh;
        }
    }

    private void Refresh()
    {
        MatchView view = GameState.Instance!.View;

        _myBar.Value = view.MyHealth;
        _opponentBar.Value = view.OpponentHealth;

        // string.Format around a Tr'd template, not a bare assignment: the composed string is
        // not itself a key, so auto-translation would never look it up (Scripts/CLAUDE.md).
        _myLabel.Text = string.Format(Tr("MATCH_MY_HEALTH"), view.MyHealth, MatchSession.STARTING_HEALTH);
        _opponentLabel.Text = string.Format(
            Tr("MATCH_OPPONENT_HEALTH"), view.OpponentHealth, MatchSession.STARTING_HEALTH);
    }
}
