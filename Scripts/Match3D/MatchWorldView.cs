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

    private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match3D/CardView.tscn";

    // Cards lie flat on the table, so the slab is turned face-up out of its default upright
    // pose. My card is turned to read from my side; the opponent's is turned the other way,
    // the way a card pushed across a table faces whoever pushed it.
    private static readonly Vector3 MY_CARD_ROTATION = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
    private static readonly Vector3 OPPONENT_CARD_ROTATION = new Vector3(-Mathf.Pi / 2f, Mathf.Pi, 0f);

    private CharacterAnimationController _myAnimation = null!;
    private CharacterAnimationController _opponentAnimation = null!;
    private CardView _myPlayedCard = null!;
    private CardView _opponentPlayedCard = null!;
    private Label _roundLabel = null!;
    private Label _myScoreLabel = null!;
    private Label _opponentScoreLabel = null!;

    public override void _Ready()
    {
        _myAnimation = new CharacterAnimationController(
            GetNode<AnimationPlayer>("MySeat/Character/AnimationPlayer"));
        _opponentAnimation = new CharacterAnimationController(
            GetNode<AnimationPlayer>("OpponentSeat/Character/AnimationPlayer"));
        _roundLabel = GetNode<Label>("MatchInterface/Readout/RoundLabel");
        _myScoreLabel = GetNode<Label>("MatchInterface/Readout/MyScoreLabel");
        _opponentScoreLabel = GetNode<Label>("MatchInterface/Readout/OpponentScoreLabel");

        _myPlayedCard = AddCardToSlot("Field/CardRest/MyCardSlot", MY_CARD_ROTATION);
        _opponentPlayedCard = AddCardToSlot("Field/CardRest2/OpponentCardSlot", OPPONENT_CARD_ROTATION);

        AnimationDebugPanel.BuildInto(
            GetNode<VBoxContainer>("DebugInterface/AnimationButtons"),
            GetNode<AnimationPlayer>("MySeat/Character/AnimationPlayer"));

        GameState.Instance!.MatchStarted += OnMatchStarted;
        GameState.Instance.RoundRevealed += OnRoundRevealed;
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
            GameState.Instance.RoundRevealed -= OnRoundRevealed;
            GameState.Instance.RoundResolved -= OnRoundResolved;
        }
    }

    private CardView AddCardToSlot(string slotPath, Vector3 rotation)
    {
        CardView card = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH).Instantiate<CardView>();
        card.Rotation = rotation;
        GetNode<Node3D>(slotPath).AddChild(card);
        return card;
    }

    private void OnMatchStarted()
    {
        // A card left face up from the previous match would be showing something that is no
        // longer in play, and a rematch reuses this scene.
        _myPlayedCard.ShowFaceDown();
        _opponentPlayedCard.ShowFaceDown();
        RefreshReadout();
    }

    /// <summary>Both played cards become known at the same moment, to both sides — the reveal
    /// is what ends the round's hidden phase, so this is the first point either card may show
    /// a face.</summary>
    private void OnRoundRevealed()
    {
        MatchView view = GameState.Instance!.View;
        ShowPlayedCard(_myPlayedCard, view.MyCard);
        ShowPlayedCard(_opponentPlayedCard, view.OpponentCard);
    }

    private static void ShowPlayedCard(CardView cardView, ECardName? playedCard)
    {
        if (playedCard == null)
        {
            cardView.ShowFaceDown();
            return;
        }

        cardView.ShowFaceUp(playedCard.Value);
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

        CharacterAnimationController winnerAnimation = didIWin ? _myAnimation : _opponentAnimation;
        winnerAnimation.PlayBlow(animationName);
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
        _myScoreLabel.Text = string.Format(Tr("MATCH_MY_HEALTH"), view.MyHealth, MatchSession.STARTING_HEALTH);
        _opponentScoreLabel.Text = string.Format(Tr("MATCH_OPPONENT_HEALTH"), view.OpponentHealth, MatchSession.STARTING_HEALTH);
    }
}
