using System;
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
/// RoundResolved arrives, and all this does is pick the animation that shows it.
///
/// Also owns the round-flow pacing: a "Round N" splash blocking entry to the hand view, cards
/// staying facedown until the flip finishes before any blow plays, and a settle beat before
/// the next round's splash. GameState's own round-open timing (SubmissionPhaseActive, the
/// submit timer) runs on its own the whole time regardless of this — the phases below are a
/// purely client-side presentation layered on top of an already-open round, the same way
/// SubmitTimeoutGaugeUI's countdown keeps ticking straight through the splash rather than
/// pausing for it.</summary>
public partial class MatchWorldView : Node3D
{
	// The clip names the .glb was exported with. 보 has no animation yet — the design calls
	// for a desk slam that has not been authored, so a 보 win currently plays nothing.
	private const string ROCK_WIN_ANIMATION = "Anim_Punch_Baked";

	private const string CARD_VIEW_SCENE_PATH = "res://Scenes/Match3D/CardView.tscn";
	private const string HEAD_CAMERA_PATH = "MySeat/Camera3D";
	private const string ROUND_INTRO_PATH = "MatchInterface/RoundIntro";
	private const string ROUND_INTRO_LABEL_PATH = "MatchInterface/RoundIntro/Label";

	/// <summary>Relative to a seat's Character node: the pose a pair of scissors is pinned to
	/// when that seat's occupant is the one who got stabbed.</summary>
	private const string STUCK_TARGET_PATH = "ScissorsStuckTarget";

	// Cards lie flat on the table, so the slab is turned face-up out of its default upright
	// pose. My card is turned to read from my side; the opponent's is turned the other way,
    // the way a card pushed across a table faces whoever pushed it.
    private static readonly Vector3 MY_CARD_ROTATION = new Vector3(-Mathf.Pi / 2f, 0f, 0f);
    private static readonly Vector3 OPPONENT_CARD_ROTATION = new Vector3(-Mathf.Pi / 2f, Mathf.Pi, 0f);

    // The same two poses turned facedown — a submitted card sits on the table back-up until
	// the reveal flips it. MY side is public because HandView's submit flight targets this
	// exact pose, so the landing hand card and the slab that replaces it line up perfectly.
	public static readonly Vector3 MY_CARD_FACEDOWN_ROTATION = new Vector3(Mathf.Pi / 2f, 0f, 0f);
	private static readonly Vector3 OPPONENT_CARD_FACEDOWN_ROTATION = new Vector3(Mathf.Pi / 2f, Mathf.Pi, 0f);

	/// <summary>How much bigger a card is once it is on the table than it was in the hand. A
	/// real-sized card (RoundedCardMesh's 6.35x8.89cm) read fine held up close in the hand view,
	/// but the played pair is looked at from the head camera across a 1.4m table, where it was
	/// too small to tell apart. Public because HandView's submit flight has to land on exactly
	/// this size — the flying card and the slab that replaces it must match.</summary>
	public const float SUBMITTED_CARD_SCALE = 2f;

	private const float REVEAL_FLIP_SECONDS = 0.35f;
	private const float REVEAL_FLIP_ARC_METERS = 0.05f;

	// Round-flow pacing. See the phase machine below for how these compose. The intro's own
	// length is GameState.ROUND_INTRO_SECONDS rather than a constant here, because the host
	// adds that same figure to its submit timer -- the splash is unplayable time, and the two
	// have to be the same number or the round clock quietly disagrees with the screen.
	private const float ROUND_INTRO_FADE_SECONDS = 0.3f;
	private const float NO_ANIMATION_RESULT_HOLD_SECONDS = 1.0f;
	private const float RESULT_SETTLE_SECONDS = 0.5f;

	/// <summary>Where the round-flow pacing currently is. Intro and the two result phases are
	/// timed (see _phaseSecondsRemaining); Open and Reveal end on a GameState signal instead
	/// (both submitted, and the reveal flip's own callback respectively — Reveal is technically
	/// timed too, by REVEAL_FLIP_SECONDS, but is named for what the player sees during it).</summary>
	private enum ERoundPhase
	{
		Intro,
		Open,
		Reveal,
		ResultHold,
		ResultSettle,
	}

	private CharacterAnimationController _myAnimation = null!;
	private CharacterAnimationController _opponentAnimation = null!;
	private Node3D _myCharacter = null!;
	private Node3D _opponentCharacter = null!;
	private ScissorsController _myScissors = null!;
	private ScissorsController _opponentScissors = null!;
	private Node3D _myStuckTarget = null!;
	private Node3D _opponentStuckTarget = null!;
	private CardView _myPlayedCard = null!;
	private CardView _opponentPlayedCard = null!;
	private HandView _handView = null!;
	private HeadFollowCamera _headCamera = null!;
	private Control _roundIntroOverlay = null!;
	private Label _roundIntroLabel = null!;
	private Label _roundLabel = null!;
	private Label _myScoreLabel = null!;
	private Label _opponentScoreLabel = null!;

	// Starts in Open rather than Intro: nothing below ever advances this without a GameState
	// signal, and the scene runs standalone too (HandView's own fallback hand, for judging the
	// grab/submit feel with no match at all) — Open is the phase where the hand view already
	// works exactly as before this state machine existed.
	private ERoundPhase _phase = ERoundPhase.Open;
	private float _phaseSecondsRemaining;

	public override void _Ready()
	{
		// Off by default in Godot, and nothing in 3D reports a hover or a click without it —
		// this is what makes CardView's own Area3D signals fire at all. Set on the scene root
		// rather than by a card, since it is one switch for the whole viewport and no card
		// should be the one deciding it for every other card.
		GetViewport().PhysicsObjectPicking = true;

		_myCharacter = GetNode<Node3D>("MySeat/Character");
		_opponentCharacter = GetNode<Node3D>("OpponentSeat/Character");
		_myAnimation = new CharacterAnimationController(
			_myCharacter.GetNode<AnimationPlayer>("AnimationPlayer"));
		_opponentAnimation = new CharacterAnimationController(
			_opponentCharacter.GetNode<AnimationPlayer>("AnimationPlayer"));
		_myScissors = GetNode<ScissorsController>("Table/MyScissors");
		_opponentScissors = GetNode<ScissorsController>("Table/OpponentScissors");

		// Only MyScissors is placed by hand; the opponent's pair is that placement reflected
		// across the table. The reflection is read off the two seats themselves rather than
		// written down as numbers, so moving a seat carries the scissors with it. Children are
		// _Ready before their parent, so both have already banked their authored rest pose by
		// now and this replaces the one that was only ever a stand-in.
		_opponentScissors.MirrorRestFrom(
			_myScissors, _opponentCharacter.GlobalTransform * _myCharacter.GlobalTransform.Inverse());

		// Where a pair ends up planted when this seat's occupant loses. One marker per seat, but
		// only MySeat's is authored: the two characters sit in identical local frames, so the
		// same LOCAL transform is the same spot on either of them, and copying it across means
		// dragging one gizmo in the editor moves both. (The scissors above need a reflection for
		// the same job because they hang off the Table, in world terms, rather than off a seat.)
		_myStuckTarget = _myCharacter.GetNode<Node3D>(STUCK_TARGET_PATH);
		_opponentStuckTarget = _opponentCharacter.GetNode<Node3D>(STUCK_TARGET_PATH);
		_opponentStuckTarget.Transform = _myStuckTarget.Transform;
		_headCamera = GetNode<HeadFollowCamera>(HEAD_CAMERA_PATH);
		_roundIntroOverlay = GetNode<Control>(ROUND_INTRO_PATH);
		_roundIntroLabel = GetNode<Label>(ROUND_INTRO_LABEL_PATH);
		_roundLabel = GetNode<Label>("MatchInterface/Readout/RoundLabel");
		_myScoreLabel = GetNode<Label>("MatchInterface/Readout/MyScoreLabel");
		_opponentScoreLabel = GetNode<Label>("MatchInterface/Readout/OpponentScoreLabel");

		_myPlayedCard = AddCardToSlot("Table/MyCardSlot", MY_CARD_ROTATION);
		_opponentPlayedCard = AddCardToSlot("Table/OpponentCardSlot", OPPONENT_CARD_ROTATION);

		// Empty table until someone actually submits — the slabs exist from scene load but
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

		// Round 1's intro cannot come from the MatchStarted signal: ConnectionScreenUI is what
		// listens for it, and what it does with it is change the scene to this one -- so by the
		// time this node exists to subscribe, that signal has already been and gone. Round 2
		// onward come from the phase machine itself (EnterNextRoundOrIdle) and were always
		// fine, which is exactly why only the first one was missing its splash.
		//
		// SubmissionPhaseActive is the test for "a match is actually underway", the same
		// question HandView answers with View.MyHand -- it is false when this scene is run on
		// its own for visual work, which is the case that must stay in Open.
		if (GameState.Instance.View.SubmissionPhaseActive)
		{
			EnterIntroPhase();
		}
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

	/// <summary>The two slots hang off the Table rather than off the card rests they used to sit
	/// under. The rests are props with a 0.5 scale and, on the opponent's side, a 180 degree
	/// turn, so a slot parented to one needed a counter-scale and counter-rotation baked into
	/// its own transform just to come out upright and life-sized — and nudging a rest for looks
	/// silently dragged the played card with it. On the table the same two spots are plain
	/// (0, 0.025, +/-0.15): dead centre, half the table's own thickness up, which is its top
	/// face.</summary>
	private CardView AddCardToSlot(string slotPath, Vector3 rotation)
	{
		CardView card = GD.Load<PackedScene>(CARD_VIEW_SCENE_PATH).Instantiate<CardView>();
		card.Rotation = rotation;
		card.Scale = Vector3.One * SUBMITTED_CARD_SCALE;
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

		// Same reasoning for a pair left planted from the last match, except that this one has
		// to be the unconditional send-home rather than the counted one — round 1 of a rematch
		// must not inherit half of the previous match's planted round.
		_myScissors.ReturnToRest();
		_opponentScissors.ReturnToRest();

		RefreshReadout();
		EnterIntroPhase();
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
		// Only while the round is still taking cards — after the reveal a rejection can only
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
	/// a face. Also where the Reveal phase starts: the winning blow (if any) is deliberately
	/// deferred until the flip animation started here actually finishes — see the phase
	/// machine below.</summary>
	private void OnRoundRevealed()
	{
		MatchView view = GameState.Instance!.View;
		RevealPlayedCard(_myPlayedCard, view.MyCard, MY_CARD_FACEDOWN_ROTATION, MY_CARD_ROTATION);
		RevealPlayedCard(_opponentPlayedCard, view.OpponentCard, OPPONENT_CARD_FACEDOWN_ROTATION, OPPONENT_CARD_ROTATION);

		_phase = ERoundPhase.Reveal;
		_phaseSecondsRemaining = REVEAL_FLIP_SECONDS;
	}

	/// <summary>Flips one slab from facedown to face up. A slab not on the table yet (its
	/// owner never submitted in person — the submit timer filled the card in) appears facedown
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

		// SUBMITTED_CARD_SCALE has to be rebuilt into the target pose: this is a whole
		// Transform3D handed to an interpolation, not a rotation assignment, so anything left
		// out of it is animated away — a flip to a plain rotation basis would shrink the card
		// back to hand size on its way over.
		Transform3D faceUpPose = cardView.GetParent<Node3D>().GlobalTransform
			* new Transform3D(
				Basis.FromEuler(faceUpRotation).Scaled(Vector3.One * SUBMITTED_CARD_SCALE),
				Vector3.Zero);
		cardView.BeginPoseAnimation(faceUpPose, REVEAL_FLIP_SECONDS, REVEAL_FLIP_ARC_METERS, null);
	}

	/// <summary>Only the health/round readout — the winning blow itself no longer plays from
	/// here. It waits for the Reveal phase's flip to finish first (see the phase machine),
	/// which this signal on its own cannot express: RoundRevealed and RoundResolved fire on
	/// the same call today (no ability card leaves a choice pending), so playing the blow
	/// straight from here would start it while the cards are still mid-flip.</summary>
	private void OnRoundResolved()
	{
		RefreshReadout();
	}

	/// <summary>Plays the blow on the side that won, chosen by the card it won with — 바위
	/// punches, 가위 stabs. A draw plays nothing, and so does a win with a card whose
	/// animation is not authored yet. Returns whether one actually started, so the caller
	/// (EnterResultHoldPhase) knows whether to wait on onFinished or fall back to a timed
	/// hold that will never otherwise be cleared.</summary>
	private bool PlayWinningBlow(Action onFinished)
	{
		MatchView view = GameState.Instance!.View;
		if (view.LastRoundOutcome == null || view.LastRoundOutcome == ERoundOutcome.Draw)
		{
			return false;
		}

		bool didIWin = view.LastRoundOutcome == ERoundOutcome.MyWin;
		ECardName? winningCard = didIWin ? view.MyCard : view.OpponentCard;
		if (winningCard == null)
		{
			return false;
		}

		string? animationName = FindAnimationForWinningCard(winningCard.Value);
		if (animationName == null)
		{
			return false;
		}

		CharacterAnimationController winnerAnimation = didIWin ? _myAnimation : _opponentAnimation;
		winnerAnimation.PlayBlow(animationName, onFinished);

		// The prop only exists for the 가위 clip — 바위 punches bare-handed. Needs no RPC of its
		// own: both screens reach here off the same already-agreed LastRoundOutcome, and each
		// resolves "the winner" to its own side of the table, so the two runs stay in step.
		if (winningCard.Value == ECardName.Scissors)
		{
			ScissorsController winnerScissors = didIWin ? _myScissors : _opponentScissors;
			winnerScissors.PlayStabSequence(
				didIWin ? _myCharacter : _opponentCharacter,
				didIWin ? _opponentStuckTarget : _myStuckTarget);
		}

		return true;
	}

	private static string? FindAnimationForWinningCard(ECardName winningCard)
	{
		switch (winningCard)
		{
			case ECardName.Rock:
				return ROCK_WIN_ANIMATION;

			case ECardName.Scissors:
				// Named by ScissorsController, which also carries the two moments measured
				// out of this exact clip — one owner for the clip and its timings.
				return ScissorsController.STAB_ANIMATION_NAME;

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

	// ---- Round-flow phase machine ----
	//
	// Intro (2s, splash + hand-view locked) -> Open (unchanged existing play) ->
	// [RoundRevealed] -> Reveal (0.35s flip) -> ResultHold (blow, or a fixed hold if there is
	// none) -> ResultSettle (0.5s beat) -> Intro for the next round, or idle if the match just
	// ended. Only Intro/Reveal/ResultHold(no-blow)/ResultSettle carry a countdown
	// (_phaseSecondsRemaining); Open and a blow-driven ResultHold end on a callback instead.

	public override void _Process(double delta)
	{
		if (_phase == ERoundPhase.Intro)
		{
			UpdateIntroFade();
		}

		if (_phaseSecondsRemaining <= 0f)
		{
			return;
		}

		_phaseSecondsRemaining -= (float)delta;
		if (_phaseSecondsRemaining <= 0f)
		{
			AdvancePhase();
		}
	}

	private void AdvancePhase()
	{
		switch (_phase)
		{
			case ERoundPhase.Intro:
				EnterOpenPhase();
				break;

			case ERoundPhase.Reveal:
				EnterResultHoldPhase();
				break;

			case ERoundPhase.ResultHold:
				// Only reached for a round with no blow to wait on — a blow-driven hold clears
				// itself via PlayWinningBlow's onFinished callback instead of this timer.
				EnterResultSettlePhase();
				break;

			case ERoundPhase.ResultSettle:
				EnterNextRoundOrIdle();
				break;
		}
	}

	private void EnterIntroPhase()
	{
		_phase = ERoundPhase.Intro;
		_phaseSecondsRemaining = (float)GameState.ROUND_INTRO_SECONDS;
		_headCamera.SetRoundIntroLocked(true);

		// Both pairs are told an intro happened; each decides for itself whether this is the one
        // it leaves on. A planted pair sits out the whole round after the stab and goes home on
        // the intro after that, so this is deliberately NOT an unconditional send-home.
        _myScissors.OnRoundIntro();
        _opponentScissors.OnRoundIntro();

        _roundIntroLabel.Text = string.Format(Tr("MATCH_ROUND"), GameState.Instance!.View.RoundNumber);
        _roundIntroOverlay.Visible = true;
        _roundIntroOverlay.Modulate = new Color(1f, 1f, 1f, 0f);
    }

    /// <summary>Fades the whole splash (dim backdrop and text together, since both are
    /// children of the one Control this sets Modulate on) in over the first
    /// ROUND_INTRO_FADE_SECONDS, holds, then out over the last ROUND_INTRO_FADE_SECONDS.</summary>
    private void UpdateIntroFade()
    {
        float elapsed = (float)GameState.ROUND_INTRO_SECONDS - _phaseSecondsRemaining;
        float alpha;
        if (elapsed < ROUND_INTRO_FADE_SECONDS)
        {
            alpha = elapsed / ROUND_INTRO_FADE_SECONDS;
        }
        else if (_phaseSecondsRemaining < ROUND_INTRO_FADE_SECONDS)
        {
            alpha = _phaseSecondsRemaining / ROUND_INTRO_FADE_SECONDS;
        }
        else
        {
            alpha = 1f;
        }

        _roundIntroOverlay.Modulate = new Color(1f, 1f, 1f, Mathf.Clamp(alpha, 0f, 1f));
    }

    private void EnterOpenPhase()
    {
        _phase = ERoundPhase.Open;
        _phaseSecondsRemaining = 0f;
        _headCamera.SetRoundIntroLocked(false);
        _roundIntroOverlay.Visible = false;
    }

    private void EnterResultHoldPhase()
    {
        _phase = ERoundPhase.ResultHold;

        bool isBlowPlaying = PlayWinningBlow(EnterResultSettlePhase);
        // A blow already in flight clears this phase through its own onFinished callback
		// instead — see AdvancePhase's ResultHold case.
		_phaseSecondsRemaining = isBlowPlaying ? 0f : NO_ANIMATION_RESULT_HOLD_SECONDS;
	}

	private void EnterResultSettlePhase()
	{
		_phase = ERoundPhase.ResultSettle;
		_phaseSecondsRemaining = RESULT_SETTLE_SECONDS;
	}

	/// <summary>No dedicated match-end screen yet — MatchLogPanel's own "=== Match won/lost
    /// ===" line (Tab to see it) is the whole of it for now. So a finished match just settles
    /// here: camera unlocked, splash stays down, nothing left to submit.</summary>
    private void EnterNextRoundOrIdle()
    {
        if (GameState.Instance!.View.MatchResult.HasValue)
        {
            _phase = ERoundPhase.Open;
            _headCamera.SetRoundIntroLocked(false);
            return;
        }

        EnterIntroPhase();
    }
}
