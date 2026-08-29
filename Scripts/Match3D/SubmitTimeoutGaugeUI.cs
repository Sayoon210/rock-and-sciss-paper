using Godot;
using RockAndScissPaper.Autoload;

namespace RockAndScissPaper.Match3D;

/// <summary>A countdown of GameState.SUBMIT_TIMEOUT_SECONDS below the health bars — how long
/// until the host plays for whoever has not submitted yet. Ticks on this screen's own clock
/// rather than being sent updates: SUBMIT_TIMEOUT_SECONDS is public specifically so both sides
/// can count the same number down independently and stay close enough (see its own doc
/// comment on GameState).
///
/// Restarts on the same two events that (re)start the host's real timer — MatchStarted, and a
/// RoundResolved that leaves View.SubmissionPhaseActive true — and stops on RoundRevealed,
/// since nothing is left to submit once the round has one.
///
/// Holds at full through MatchWorldView's own "Round N" splash rather than ticking during it,
/// which is honest rather than cosmetic: the host arms its submit timer for
/// ROUND_INTRO_SECONDS + SUBMIT_TIMEOUT_SECONDS precisely so the splash is not counted against
/// anyone. One countdown runs across both stretches and the displayed value is clamped to the
/// play portion, so the gauge cannot drift from the clock it is standing in for.</summary>
public partial class SubmitTimeoutGaugeUI : VBoxContainer
{
    private const string LABEL_PATH = "Label";
    private const string BAR_PATH = "Bar";

    private Label _label = null!;
    private ProgressBar _bar = null!;
    private float _secondsRemaining;
    private bool _isRunning;

    public override void _Ready()
    {
        _label = GetNode<Label>(LABEL_PATH);
        _bar = GetNode<ProgressBar>(BAR_PATH);
        _bar.MaxValue = GameState.SUBMIT_TIMEOUT_SECONDS;

        GameState.Instance!.MatchStarted += Restart;
        GameState.Instance.RoundResolved += OnRoundResolved;
        GameState.Instance.RoundRevealed += Stop;

        // Round 1's MatchStarted has already fired by the time this scene loads — it is what
        // ConnectionScreenUI acts on to bring this scene up in the first place — so the
        // starting state is read from the view rather than waited for, the same way
        // MatchWorldView starts its own round 1 splash.
        if (GameState.Instance.View.SubmissionPhaseActive)
        {
            Restart();
        }
        else
        {
            Stop();
        }
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.MatchStarted -= Restart;
            GameState.Instance.RoundResolved -= OnRoundResolved;
            GameState.Instance.RoundRevealed -= Stop;
        }
    }

    public override void _Process(double delta)
    {
        if (!_isRunning)
        {
            return;
        }

        _secondsRemaining = Mathf.Max(0f, _secondsRemaining - (float)delta);

        // Clamped to the play portion: while the intro is still burning down the first
        // ROUND_INTRO_SECONDS of it, this sits at full instead of counting.
        float playSecondsLeft = Mathf.Min(_secondsRemaining, (float)GameState.SUBMIT_TIMEOUT_SECONDS);
        _bar.Value = playSecondsLeft;
        _label.Text = string.Format(Tr("MATCH_SUBMIT_TIMEOUT"), Mathf.CeilToInt(playSecondsLeft));
    }

    /// <summary>A round just resolved — restart for the round it just opened, unless that
    /// resolution ended the match (View.SubmissionPhaseActive already tells us which).</summary>
    private void OnRoundResolved()
    {
        if (GameState.Instance!.View.SubmissionPhaseActive)
        {
            Restart();
        }
        else
        {
            Stop();
        }
    }

    private void Restart()
    {
        _isRunning = true;
        _secondsRemaining = (float)(GameState.ROUND_INTRO_SECONDS + GameState.SUBMIT_TIMEOUT_SECONDS);
        Visible = true;
    }

    private void Stop()
    {
        _isRunning = false;
        Visible = false;
    }
}
