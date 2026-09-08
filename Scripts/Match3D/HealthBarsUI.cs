using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>Both sides' health, pushed into the two strips that draw it: the player's own in
/// the bottom-right corner, the opponent's floating over their head (OverheadHealthBarUI). One
/// script owns both because they are one fact read twice, and two subscriptions to the same
/// signal is how the screen ends up showing two different numbers.
///
/// Reads GameState.View and nothing else (Scripts/CLAUDE.md), on the host too. It shows health;
/// it never works out what health should be.
///
/// It does not decide when to show it either. MatchStarted resets it, but a ROUND's health is
/// committed by MatchWorldView calling ShowCurrentHealth on the frame the winning blow actually
/// lands. Subscribing to RoundResolved instead — which is what this did — drained the bar about
/// 1.2 seconds early: that signal fires before the two cards have even flipped face up, so the
/// health readout announced the result while the round was still being presented as unknown,
/// and the cells burst with nobody on screen doing anything.</summary>
public partial class HealthBarsUI : Control
{
    private const string MY_SEGMENTS_PATH = "MyHealth/Segments";
    private const string MY_LABEL_PATH = "MyHealth/Label";
    private const string OPPONENT_SEGMENTS_PATH = "OpponentHealth/Segments";

    private SegmentedHealthBarUI _mySegments = null!;
    private Label _myLabel = null!;
    private SegmentedHealthBarUI _opponentSegments = null!;

    public override void _Ready()
    {
        _mySegments = GetNode<SegmentedHealthBarUI>(MY_SEGMENTS_PATH);
        _myLabel = GetNode<Label>(MY_LABEL_PATH);
        _opponentSegments = GetNode<SegmentedHealthBarUI>(OPPONENT_SEGMENTS_PATH);

        GameState.Instance!.MatchStarted += OnMatchStarted;

        ShowCurrentHealth();
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.MatchStarted -= OnMatchStarted;
        }
    }

    /// <summary>Draws the health the view is currently holding, and returns how many points the
    /// PLAYER lost getting there — which is what the screen-edge blood is scaled by, and is
    /// zero on a round the player won or drew.
    ///
    /// Called by MatchWorldView at the moment of impact rather than off a signal; see the class
    /// comment for why.</summary>
    public int ShowCurrentHealth()
    {
        MatchView view = GameState.Instance!.View;

        int myHealthLost = _mySegments.ShowHealth(view.MyHealth);
        _opponentSegments.ShowHealth(view.OpponentHealth);

        // string.Format around a Tr'd template, not a bare assignment: the composed string is
        // not itself a key, so auto-translation would never look it up (Scripts/CLAUDE.md).
        _myLabel.Text = string.Format(Tr("MATCH_MY_HEALTH"), view.MyHealth, MatchSession.STARTING_HEALTH);

        return myHealthLost;
    }

    private void OnMatchStarted()
    {
        ShowCurrentHealth();
    }
}
