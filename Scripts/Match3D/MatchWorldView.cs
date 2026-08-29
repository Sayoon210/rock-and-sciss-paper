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

    // The same two poses turned facedown - a submitted card sits on the table back-up until
    // the reveal flips it. MY side is public because HandView's submit flight targets this
    // exact pose, so the landing hand card and the slab that replaces it line up perfectly.
    public static readonly Vector3 MY_CARD_FACEDOWN_ROTATION = new Vector3(Mathf.Pi / 2f, 0f, 0f);
    private static readonly Vector3 OPPONENT_CARD_FACEDOWN_ROTATION = new Vector3(Mathf.Pi / 2f, Mathf.Pi, 0f);

    private const float REVEAL_FLIP_SECONDS = 0.35f;
    private const float REVEAL_FLIP_ARC_METERS = 0.05f;

    private CharacterAnimationController _myAnimation = null!;
    private CharacterAnimationController _opponentAnimation = null!;
    private CardView _myPlayedCard = null!;
    private CardView _opponentPlayedCard = null!;
    private HandView _handView = null!;
    private Label _roundLabel = null!;
    private Label _myScoreLabel = null!;
    private Label _opponentScoreLabel = null!;

    public override void _Ready()
    {
        // Off by default in Godot, and nothing in 3D reports a hover or a click without it —
        // this is what makes CardView's own Area3D signals fire at all. Set on the scene root
        // rather than by a card, since it is one switch for the whole viewport and no card
        // should be the one deciding it for every other card.
        GetViewport().PhysicsObjectPicking = true;

        _myAnimation = new CharacterAnimationController(
            GetNode<AnimationPlayer>("MySeat/Character/AnimationPlayer"));
        _opponentAnimation = new CharacterAnimationController(
            GetNode<AnimationPlayer>("OpponentSeat/Character/AnimationPlayer"));
        _roundLabel = GetNode<Label>("MatchInterface/Readout/RoundLabel");
        _myScoreLabel = GetNode<Label>("MatchInterface/Readout/MyScoreLabel");
        _opponentScoreLabel = GetNode<Label>("MatchInterface/Readout/OpponentScoreLabel");

        _myPlayedCard = AddCardToSlot("Field/CardRest/MyCardSlot", MY_CARD_ROTATION);
        _opponentPlayedCard = AddCardToSlot("Field/CardRest2/OpponentCardSlot", OPPONENT_CARD_ROTATION);

        // Empty table until someone actually submits - the slabs exist from scene load but
        // stay invisible, so an open round shows an empty rest rather than a blank card.
        _myPlayedCard.Visible = false;
        _opponentPlayedCard.Visible = false;

        // HandView events share this scene's lifetime (both are freed together), unlike the
        // Autoload signals below, so they need no _ExitTree unhooking.
        _handView = GetNode<HandView>("Field/CardRest/HandView");
        _handView.MyCardLanded += OnMyCardLanded;
        _handView.MySubmissionRejected += OnMySubmissionRejected;

        AnimationDebugPanel.BuildInto(
            GetNode<VBoxContainer>("DebugInterface/AnimationButtons"),
            GetNode<AnimationPlayer>("MySeat/Character/AnimationPlayer"));

        GameState.Instance!.MatchStarted += OnMatchStarted;
        GameState.Instance.RoundRevealed += OnRoundRevealed;
        GameState.Instance.RoundResolved += OnRoundResolved;
        GameState.Instance.OpponentSubmitted += OnOpponentSubmitted;

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
            GameState.Instance.OpponentSubmitted -= OnOpponentSubmitted;
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
        _myPlayedCard.Visible = false;
        _opponentPlayedCard.Visible = false;
        RefreshReadout();
    }

    private void OnMyCardLanded()
    {
        PresentFacedown(_myPlayedCard, MY_CARD_FACEDOWN_ROTATION);
    }

    private void OnOpponentSubmitted()
    {
        PresentFacedown(_opponentPlayedCard, OPPONENT_CARD_FACEDOWN_ROTATION);
    }

    private void OnMySubmissionRejected()
    {
        // Only while the round is still taking cards - after the reveal a rejection can only
        // be about a choice, which has nothing to do with the slab.
        if (GameState.Instance!.View.SubmissionPhaseActive)
        {
            _myPlayedCard.Visible = false;
        }
    }

    private static void PresentFacedown(CardView cardView, Vector3 facedownRotation)
    {
        cardView.CancelPoseAnimation();
        cardView.ShowFaceDown();
        cardView.Rotation = facedownRotation;
        cardView.Visible = true;
    }

    /// <summary>Both played cards become known at the same moment, to both sides — the reveal
    /// is what ends the round's hidden phase, so this is the first point either card may show
    /// a face.</summary>
    private void OnRoundRevealed()
    {
        MatchView view = GameState.Instance!.View;
        RevealPlayedCard(_myPlayedCard, view.MyCard, MY_CARD_FACEDOWN_ROTATION, MY_CARD_ROTATION);
        RevealPlayedCard(_opponentPlayedCard, view.OpponentCard, OPPONENT_CARD_FACEDOWN_ROTATION, OPPONENT_CARD_ROTATION);
    }

    /// <summary>Flips one slab from facedown to face up. A slab not on the table yet (its
    /// owner never submitted in person - the submit timer filled the card in) appears facedown
    /// first, so the flip always starts from the same place. Setting the face BEFORE the flip
    /// is safe: the art points at the table until the rotation carries it past edge-on.</summary>
    private static void RevealPlayedCard(
        CardView cardView, ECardName? playedCard, Vector3 facedownRotation, Vector3 faceUpRotation)
    {
        if (playedCard == null)
        {
            cardView.ShowFaceDown();
            return;
        }

        if (!cardView.Visible)
        {
            PresentFacedown(cardView, facedownRotation);
        }

        cardView.ShowFaceUp(playedCard.Value);
        Transform3D faceUpPose = cardView.GetParent<Node3D>().GlobalTransform
            * new Transform3D(Basis.FromEuler(faceUpRotation), Vector3.Zero);
        cardView.BeginPoseAnimation(faceUpPose, REVEAL_FLIP_SECONDS, REVEAL_FLIP_ARC_METERS, null);
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
