using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>The match as a place rather than a screen: two seats facing each other across
/// a table, and the round played out on the characters sitting in them. It replaces
/// MatchScreenUI, which is in Deprecated/ — see that folder's README.
///
/// This reads GameState.View and nothing else, on the host too (Scripts/CLAUDE.md), and it
/// decides no rules: which side won and with what card is already settled by the time
/// RoundResolved arrives, and all this does is pick the animation that shows it.</summary>
public partial class MatchWorldView : Node3D
{
    // The clip names the .glb was exported with. 보 has no animation yet — the design calls
    // for a desk slam that has not been authored, so a 보 win currently plays nothing.
    private const string ROCK_WIN_ANIMATION = "Anim_Punch_Baked";
    private const string SCISSORS_WIN_ANIMATION = "Anim_StabScissor_Baked";

    private AnimationPlayer _myAnimationPlayer = null!;
    private AnimationPlayer _opponentAnimationPlayer = null!;
    private Label _roundLabel = null!;
    private Label _myScoreLabel = null!;
    private Label _opponentScoreLabel = null!;

    public override void _Ready()
    {
        _myAnimationPlayer = GetNode<AnimationPlayer>("MySeat/Character/AnimationPlayer");
        _opponentAnimationPlayer = GetNode<AnimationPlayer>("OpponentSeat/Character/AnimationPlayer");
        _roundLabel = GetNode<Label>("MatchInterface/Readout/RoundLabel");
        _myScoreLabel = GetNode<Label>("MatchInterface/Readout/MyScoreLabel");
        _opponentScoreLabel = GetNode<Label>("MatchInterface/Readout/OpponentScoreLabel");

        GameState.Instance!.MatchStarted += OnMatchStarted;
        GameState.Instance.RoundResolved += OnRoundResolved;

        // The menu music plays through the connection screen and stops here, because this
        // is the first screen that is no longer the menu. TitleScreenUI starts it.
        AudioManager.Instance!.StopMusic();

        RefreshReadout();
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance != null)
        {
            GameState.Instance.MatchStarted -= OnMatchStarted;
            GameState.Instance.RoundResolved -= OnRoundResolved;
        }
    }

    private void OnMatchStarted()
    {
        RefreshReadout();
    }

    private void OnRoundResolved()
    {
        PlayWinningBlow();
        RefreshReadout();
    }

    /// <summary>Plays the blow on the side that won, chosen by the card it won with — 바위
    /// punches, 가위 stabs. A draw plays nothing, and so does a win with a card whose
    /// animation is not authored yet.</summary>
    private void PlayWinningBlow()
    {
        MatchView view = GameState.Instance!.View;
        if (view.LastRoundOutcome == null || view.LastRoundOutcome == ERoundOutcome.Draw)
        {
            return;
        }

        bool didIWin = view.LastRoundOutcome == ERoundOutcome.MyWin;
        ECardName? winningCard = didIWin ? view.MyCard : view.OpponentCard;
        if (winningCard == null)
        {
            return;
        }

        string? animationName = FindAnimationForWinningCard(winningCard.Value);
        if (animationName == null)
        {
            return;
        }

        AnimationPlayer winnerAnimationPlayer = didIWin ? _myAnimationPlayer : _opponentAnimationPlayer;
        winnerAnimationPlayer.Play(animationName);
    }

    private static string? FindAnimationForWinningCard(ECardName winningCard)
    {
        switch (winningCard)
        {
            case ECardName.Rock:
                return ROCK_WIN_ANIMATION;

            case ECardName.Scissors:
                return SCISSORS_WIN_ANIMATION;

            default:
                return null;
        }
    }

    private void RefreshReadout()
    {
        MatchView view = GameState.Instance!.View;
        _roundLabel.Text = string.Format(Tr("MATCH_ROUND"), view.RoundNumber);
        _myScoreLabel.Text = string.Format(Tr("MATCH_MY_SCORE"), view.MyScore, MatchSession.WINS_NEEDED_FOR_MATCH);
        _opponentScoreLabel.Text = string.Format(Tr("MATCH_OPPONENT_SCORE"), view.OpponentScore, MatchSession.WINS_NEEDED_FOR_MATCH);
    }
}
