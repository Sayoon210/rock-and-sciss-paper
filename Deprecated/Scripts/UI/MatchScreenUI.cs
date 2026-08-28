using System.Collections.Generic;
using Godot;
using RockAndScissPaper.Autoload;
using RockAndScissPaper.Cards;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>The match screen. The only script in this scene that subscribes to Autoload
/// signals — HandView and CardView are driven by method call from here, so there is exactly
/// one place to look when the screen and the match disagree.
///
/// Every refresh method reads GameState.Instance.View at the top rather than holding it in a
/// field: ResetMatch() replaces the MatchView object outright, so a cached reference would go
/// stale the first time a second match started and quietly render the old one forever.</summary>
public partial class MatchScreenUI : Control
{
    /// <summary>What a rejected request says on screen. RequestRejected carries the host's
    /// own exception text, which names internal rules and English card identifiers — useful
    /// in the log, not on a player's screen.</summary>
    private const string REJECTION_MESSAGE = "MATCH_PROMPT_REJECTED";

    private const string TITLE_SCENE_PATH = "res://Scenes/Screens/TitleScreen.tscn";

    private const string JOKER_DEVOUR_EFFECT_SCENE_PATH = "res://Scenes/Match/JokerDevourEffect.tscn";

    private const int SCORE_PIP_SIZE = 18;

    /// <summary>How long the revealed cards stay on the field after the round's outcome
    /// effects begin (the win/loss states, or nothing on a 무승부) before the field is
    /// emptied for the next round.
    ///
    /// This is the single longest wait in a round and the one that decides how brisk a match
    /// feels — everything a card does at the end of a round (going back to the 덱, 소멸ing)
    /// waits on it, so a generous value here reads as the game pausing rather than as time to
    /// take the result in.</summary>
    private const double FIELD_CLEAR_DELAY_SECONDS = 0.7;

    /// <summary>How long the opponent's card sits face-down before it turns over. Short
    /// enough to still feel like part of the same beat as the reveal, not a separate wait.</summary>
    private const double PRE_FLIP_PAUSE_SECONDS = 0.15;

    /// <summary>How long the field holds still, opponent's card now face-up, before the two
    /// cards take on their win/loss state. Long enough to read the opponent's card as its own
    /// beat before the result lands on top of it, and no longer.</summary>
    private const double POST_FLIP_PAUSE_SECONDS = 0.2;

    /// <summary>How much of a timed phase the gauge spends reddening. It stays calm for the
    /// rest: a bar that is always partly red says nothing, and a colour that only means
    /// something once it starts moving is the whole point of a warning.</summary>
    private const double PHASE_TIMER_URGENT_FRACTION = 0.4;

    private DeckView _opponentDeckView = null!;
    private HandView _opponentHandView = null!;
    private Label _scoreHeadingLabel = null!;
    private Label _myScoreLabel = null!;
    private Label _opponentScoreLabel = null!;
    private HBoxContainer _myScorePipRow = null!;
    private HBoxContainer _opponentScorePipRow = null!;
    private Label _roundLabel = null!;
    private CardView _myPlayedCardView = null!;
    private CardDropZone _mySubmitDropZone = null!;
    private CardVanishEffect _myVanishEffect = null!;
    private CardOutcomeEffect _myOutcomeEffect = null!;
    private Label _myActionLabel = null!;
    private CardView _opponentPlayedCardView = null!;
    private CardVanishEffect _opponentVanishEffect = null!;
    private CardFlipEffect _opponentCardFlip = null!;
    private CardSlideInEffect _opponentCardSlideIn = null!;
    private CardOutcomeEffect _opponentOutcomeEffect = null!;

    // One node for both cards, unlike the per-side vanish effects: a 조커 round is the one
    // case where the two played cards are part of a single effect, one being pulled into
    // the other.
    private JokerDevourEffect _jokerDevourEffect = null!;
    private Label _opponentActionLabel = null!;
    private Label _outcomeLabel = null!;
    private Label _promptLabel = null!;
    private Button _confirmButton = null!;
    private ProgressBar _phaseTimerBar = null!;
    private StyleBoxFlat _phaseTimerFillStyle = null!;
    private HBoxContainer _targetPaletteRow = null!;
    private DeckView _myDeckView = null!;
    private HandView _myHandView = null!;
    private PanelContainer _matchEndOverlay = null!;
    private Label _matchEndResultLabel = null!;
    private Button _rematchButton = null!;
    private Label _rematchStatusLabel = null!;
    private Button _returnToTitleButton = null!;
    private Timer _fieldClearTimer = null!;
    private Timer _preFlipPauseTimer = null!;
    private Timer _postFlipPauseTimer = null!;

    // True while the field is deliberately empty, between one round's cards being cleared and
    // the next round's reveal. RefreshField honours it, so an unrelated refresh (a hand
    // change, a score update) cannot put the old round's cards back on screen.
    private bool _fieldCleared = true;

    // True from the moment a round is revealed until the opponent's card has finished
    // turning face-up. RefreshField honours it the same way it honours _fieldCleared — an
    // unrelated refresh firing mid-turn must not stomp the card back to a flat
    // ShowFaceUp/ShowFaceDown call, since CardFlipEffect owns that card's face and Scale
    // until it finishes.
    private bool _opponentCardFlipPending;

    // Whether OnRoundResolved has already run for the round currently on the field. Set
    // false at each reveal, true the moment resolution actually happens -- kept as its own
    // flag rather than inferred from View.LastRoundOutcome being non-null, since a round can
    // resolve with no winner at all (Joker, or any round without two normal cards to judge).
    private bool _roundAlreadyResolved;

    // How much of the running phase is left, and how long it was to begin with — zero when
    // nothing is being timed. The total is kept rather than recomputed because it is also what
    // identifies the phase: the two have different limits, so a change in this number is
    // exactly a change of phase.
    private double _phaseSecondsRemaining;
    private double _phaseTotalSeconds;

    // What the 변화 this side has sent turns its card into, or null when none is outstanding.
    // Held because the hand that comes back says only what is in it, not that one card became
    // another — and those two look identical as lists of card names. Which card is changing is
    // HandView's to remember, since only it can tell two copies of the same card apart.
    // Cleared either by the hand arriving or by a rejection.
    private ECardName? _pendingTransformTarget;

    // The card currently sitting on the submit zone, while the host has not answered yet.
    // Kept only so a rejected submission can be sent back to the hand — nothing else moves
    // it off the zone.
    private CardView? _dockedCardView;

    private readonly List<ColorRect> _myScorePips = new List<ColorRect>();
    private readonly List<ColorRect> _opponentScorePips = new List<ColorRect>();

    // 변화's "into" palette. Built once — the loaded card roster does not change mid-match —
    // and shown or hidden rather than rebuilt on every prompt.
    private readonly List<Button> _targetPaletteButtons = new List<Button>();

    public override void _Ready()
    {
        // The title track is menu music and carries through the connection screen, but a match
        // has its own sounds to be heard over — and no music of its own yet.
        AudioManager.Instance!.StopMusic();

        _opponentDeckView = GetNode<DeckView>("Rows/OpponentArea/OpponentDeckView");
        _opponentHandView = GetNode<HandView>("Rows/OpponentArea/OpponentHandView");
        _scoreHeadingLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/ScoreHeadingLabel");
        _myScoreLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/MyScoreLabel");
        _myScorePipRow = GetNode<HBoxContainer>("Rows/MiddleRow/ScoreBoard/MyScorePipRow");
        _opponentScoreLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/OpponentScoreLabel");
        _opponentScorePipRow = GetNode<HBoxContainer>("Rows/MiddleRow/ScoreBoard/OpponentScorePipRow");
        _roundLabel = GetNode<Label>("Rows/MiddleRow/Field/RoundLabel");
        _myPlayedCardView = GetNode<CardView>("Rows/MiddleRow/Field/MyPlayedArea/MySubmitSlot/MyPlayedCardView");
        _mySubmitDropZone = GetNode<CardDropZone>("Rows/MiddleRow/Field/MyPlayedArea/MySubmitSlot/MySubmitDropZone");
        _myVanishEffect = GetNode<CardVanishEffect>("Rows/MiddleRow/Field/MyPlayedArea/MySubmitSlot/MyVanishEffect");
        _myOutcomeEffect = GetNode<CardOutcomeEffect>("Rows/MiddleRow/Field/MyPlayedArea/MySubmitSlot/MyOutcomeEffect");
        _myActionLabel = GetNode<Label>("Rows/MiddleRow/Field/MyPlayedArea/MyActionLabel");
        _opponentPlayedCardView = GetNode<CardView>("Rows/MiddleRow/Field/OpponentPlayedArea/OpponentSlot/OpponentPlayedCardView");
        _opponentVanishEffect = GetNode<CardVanishEffect>("Rows/MiddleRow/Field/OpponentPlayedArea/OpponentSlot/OpponentVanishEffect");
        _opponentCardFlip = new CardFlipEffect();
        _opponentCardFlip.Finished += OnOpponentCardFlipFinished;
        AddChild(_opponentCardFlip);
        _opponentCardSlideIn = new CardSlideInEffect();
        AddChild(_opponentCardSlideIn);
        _opponentOutcomeEffect = GetNode<CardOutcomeEffect>("Rows/MiddleRow/Field/OpponentPlayedArea/OpponentSlot/OpponentOutcomeEffect");
        _jokerDevourEffect = GD.Load<PackedScene>(JOKER_DEVOUR_EFFECT_SCENE_PATH).Instantiate<JokerDevourEffect>();
        _jokerDevourEffect.Finished += OnJokerDevourFinished;
        AddChild(_jokerDevourEffect);
        _opponentActionLabel = GetNode<Label>("Rows/MiddleRow/Field/OpponentPlayedArea/OpponentActionLabel");
        _outcomeLabel = GetNode<Label>("Rows/MiddleRow/Field/OutcomeLabel");
        _promptLabel = GetNode<Label>("Rows/PromptStrip/PromptRow/PromptLabel");
        _confirmButton = GetNode<Button>("Rows/PromptStrip/PromptRow/ConfirmButton");
        _phaseTimerBar = GetNode<ProgressBar>("Rows/PromptStrip/PromptRow/PhaseTimerBar");

        // Mutated in place rather than duplicated first: this stylebox belongs to the one
        // PhaseTimerBar in the one MatchScreen, unlike CardView's border, which has to take a
        // copy because its scene is instanced once per card on screen.
        _phaseTimerFillStyle = (StyleBoxFlat)_phaseTimerBar.GetThemeStylebox("fill");
        _targetPaletteRow = GetNode<HBoxContainer>("Rows/PromptStrip/PromptRow/TargetPaletteRow");
        _myDeckView = GetNode<DeckView>("Rows/MyArea/MyDeckView");
        _myHandView = GetNode<HandView>("Rows/MyArea/MyHandView");
        _matchEndOverlay = GetNode<PanelContainer>("MatchEndOverlay");
        _matchEndResultLabel = GetNode<Label>("MatchEndOverlay/Center/Box/ResultLabel");
        _rematchButton = GetNode<Button>("MatchEndOverlay/Center/Box/RematchButton");
        _rematchStatusLabel = GetNode<Label>("MatchEndOverlay/Center/Box/RematchStatusLabel");
        _returnToTitleButton = GetNode<Button>("MatchEndOverlay/Center/Box/ReturnToTitleButton");
        _fieldClearTimer = new Timer();
        _fieldClearTimer.OneShot = true;
        _fieldClearTimer.WaitTime = FIELD_CLEAR_DELAY_SECONDS;
        _fieldClearTimer.Timeout += OnFieldClearTimeout;
        AddChild(_fieldClearTimer);

        _preFlipPauseTimer = new Timer();
        _preFlipPauseTimer.OneShot = true;
        _preFlipPauseTimer.WaitTime = PRE_FLIP_PAUSE_SECONDS;
        _preFlipPauseTimer.Timeout += OnPreFlipPauseTimeout;
        AddChild(_preFlipPauseTimer);

        _postFlipPauseTimer = new Timer();
        _postFlipPauseTimer.OneShot = true;
        _postFlipPauseTimer.WaitTime = POST_FLIP_PAUSE_SECONDS;
        _postFlipPauseTimer.Timeout += OnPostFlipPauseTimeout;
        AddChild(_postFlipPauseTimer);

        // Each row is told which 덱 its cards belong to, and animates 드로우 out of it
        // and 교체/리셋 back into it from there. Wired here rather than found by either
        // node, because how this screen is laid out is this script's business alone.
        _myHandView.SetDeckSource(_myDeckView);
        _opponentHandView.SetDeckSource(_opponentDeckView);

        BuildScorePips(_myScorePipRow, _myScorePips);
        BuildScorePips(_opponentScorePipRow, _opponentScorePips);
        BuildTransformTargetPalette();

        _confirmButton.Pressed += OnConfirmSwapPressed;
        _myHandView.SelectionChanged += OnHandSelectionChanged;
        _mySubmitDropZone.CardDropped += OnCardDroppedForSubmission;
        _rematchButton.Pressed += OnRematchPressed;
        _returnToTitleButton.Pressed += OnReturnToTitlePressed;

        // Only runs while a phase is being timed; RefreshPhaseTimer switches it on and off.
        SetProcess(false);

        ConnectToAutoloadSignals();
        RefreshEverything();
    }

    /// <summary>Drains the phase gauge. This is a copy of the host's countdown rather than
    /// the countdown itself — the host owns both real Timers and is the only thing that can
    /// end a phase (GameState.OnChoiceTimedOut, OnSubmitTimedOut). Both sides simply count the
    /// same constants down locally, which is why a few frames of drift between the bar
    /// emptying and the round moving on costs nothing: nothing is decided by this number.</summary>
    public override void _Process(double delta)
    {
        _phaseSecondsRemaining -= delta;
        if (_phaseSecondsRemaining < 0.0)
        {
            _phaseSecondsRemaining = 0.0;
        }

        _phaseTimerBar.Value = _phaseSecondsRemaining;
        _phaseTimerFillStyle.BgColor = PhaseTimerColorAt(_phaseSecondsRemaining, _phaseTotalSeconds);
    }

    /// <summary>The gauge's colour with this much of a phase of this length left: unchanged
    /// while there is still time, then running to red over the last stretch of it. Taken as a
    /// fraction of the phase rather than as a fixed number of seconds, so the warning arrives
    /// at the same point of a 20-second submission and a 15-second choice.</summary>
    private static Color PhaseTimerColorAt(double secondsRemaining, double phaseSeconds)
    {
        double reddeningSeconds = phaseSeconds * PHASE_TIMER_URGENT_FRACTION;
        float urgency = 1f - Mathf.Clamp((float)(secondsRemaining / reddeningSeconds), 0f, 1f);

        // By way of amber rather than in one step: green and red are far enough apart in RGB
        // that the midpoint of a direct blend is a desaturated olive, which reads as a
        // rendering fault rather than as a warning. Every point of this ramp is a colour
        // somebody could have picked on purpose.
        Color calm = new Color(0.36f, 0.72f, 0.46f);
        Color warning = new Color(0.95f, 0.76f, 0.28f);
        Color urgent = new Color(0.90f, 0.26f, 0.26f);

        if (urgency < 0.5f)
        {
            return calm.Lerp(warning, urgency * 2f);
        }

        return warning.Lerp(urgent, (urgency - 0.5f) * 2f);
    }

    /// <summary>A freed node still connected to a session-lifetime Autoload signal is a
    /// crash waiting for the next emit (Scripts/Autoload/CLAUDE.md).</summary>
    public override void _ExitTree()
    {
        if (GameState.Instance == null)
        {
            return;
        }

        GameState.Instance.MatchStarted -= OnMatchStarted;
        GameState.Instance.RoundRevealed -= OnRoundRevealed;
        GameState.Instance.OpponentSubmitted -= OnOpponentSubmitted;
        GameState.Instance.ChoiceRequired -= OnChoiceRequired;
        GameState.Instance.RoundResolved -= OnRoundResolved;
        GameState.Instance.MyHandChanged -= OnMyHandChanged;
        GameState.Instance.RequestRejected -= OnRequestRejected;
        GameState.Instance.MatchEnded -= OnMatchEnded;
        GameState.Instance.OpponentLeft -= OnOpponentLeft;
    }

    private void ConnectToAutoloadSignals()
    {
        GameState.Instance!.MatchStarted += OnMatchStarted;
        GameState.Instance.RoundRevealed += OnRoundRevealed;
        GameState.Instance.OpponentSubmitted += OnOpponentSubmitted;
        GameState.Instance.ChoiceRequired += OnChoiceRequired;
        GameState.Instance.RoundResolved += OnRoundResolved;
        GameState.Instance.MyHandChanged += OnMyHandChanged;
        GameState.Instance.RequestRejected += OnRequestRejected;
        GameState.Instance.MatchEnded += OnMatchEnded;
        GameState.Instance.OpponentLeft += OnOpponentLeft;
    }

    /// <summary>The 10선승 track, one pip per win, built from the rule constant rather than
    /// placed by hand — the moment WINS_NEEDED_FOR_MATCH changes, so does this.</summary>
    private static void BuildScorePips(HBoxContainer row, List<ColorRect> pips)
    {
        for (int win = 0; win < MatchSession.WINS_NEEDED_FOR_MATCH; win++)
        {
            ColorRect pip = new ColorRect();
            pip.CustomMinimumSize = new Vector2(SCORE_PIP_SIZE, SCORE_PIP_SIZE);
            row.AddChild(pip);
            pips.Add(pip);
        }
    }

    private void OnMatchStarted()
    {
        // Fires on a rematch too (HostStartsMatch -> MatchStartedRpc on the client), so the
        // overlay from the previous match's end has to be explicitly closed here.
        _matchEndOverlay.Visible = false;

        _fieldClearTimer.Stop();
        _preFlipPauseTimer.Stop();
        _postFlipPauseTimer.Stop();
        _opponentCardFlip.Stop();
        _opponentCardSlideIn.Stop();
        _jokerDevourEffect.Stop();
        _myOutcomeEffect.Stop();
        _opponentOutcomeEffect.Stop();
        _opponentCardFlipPending = false;
        _fieldCleared = true;
        _dockedCardView = null;
        _pendingTransformTarget = null;
        _myHandView.ForgetRememberedChoiceCards();

        RefreshEverything();
    }

    /// <summary>The opponent's card is in, but mine is not yet — nothing is revealed by this,
    /// only that they are no longer still deciding. Shown face-down in their field slot, and
    /// eased in from above so it reads as having just left their 패 rather than appearing out
    /// of nowhere.</summary>
    private void OnOpponentSubmitted()
    {
        _opponentPlayedCardView.Visible = true;
        _opponentPlayedCardView.ShowFaceDown();
        _opponentCardSlideIn.Play(_opponentPlayedCardView);
    }

    /// <summary>Both cards are public, but the opponent's is shown face-down for one more
    /// beat before CardFlipEffect turns it over — see OnPreFlipPauseTimeout. On the host the
    /// choice prompt is set immediately after this, so the prompt strip is refreshed again
    /// by ChoiceRequired.</summary>
    private void OnRoundRevealed()
    {
        // The card that was sitting on the submit zone is about to be taken out of the hand
        // by the refresh that follows this, which frees the node — so let go of it here
        // rather than keeping a reference that is about to dangle.
        _dockedCardView = null;

        // The field's card views are reused for this round. A dissolve, or a win/loss state
        // still held from the last one, would open the round mid-effect — so every one of
        // them is given back whole first.
        _myVanishEffect.Stop();
        _opponentVanishEffect.Stop();
        _myOutcomeEffect.Stop();
        _opponentOutcomeEffect.Stop();
        _preFlipPauseTimer.Stop();
        _postFlipPauseTimer.Stop();
        _opponentCardFlip.Stop();
        _opponentCardSlideIn.Stop();
        _jokerDevourEffect.Stop();

        _fieldClearTimer.Stop();
        _fieldCleared = false;

        // Reset to a known face-down state regardless of whether OpponentSubmitted already
        // got here first this round -- the pause-then-turn choreography below needs a fixed
        // starting point, not "whatever it happened to already look like."
        _opponentCardFlipPending = true;
        _roundAlreadyResolved = false;
        _opponentPlayedCardView.Visible = true;
        _opponentPlayedCardView.ShowFaceDown();

        RefreshField();
        RefreshPromptStrip();

        _preFlipPauseTimer.Start();
    }

    /// <summary>The pause before the opponent's card turns over.</summary>
    private void OnPreFlipPauseTimeout()
    {
        ECardName? opponentCard = GameState.Instance!.View.OpponentCard;
        if (!opponentCard.HasValue)
        {
            // Defensive only -- a reveal always sets both cards, so this timer should never
            // fire without one. Leaves the flag clear rather than the field stuck mid-turn.
            _opponentCardFlipPending = false;
            return;
        }

        AudioManager.Instance!.Play(ESoundName.CardFlip);
        _opponentCardFlip.Play(_opponentPlayedCardView, opponentCard.Value);
    }

    /// <summary>The turn finished; RefreshField is free to touch this card again. If the
    /// round has already resolved (no choice was owed), the win/loss pause starts right
    /// here -- OnRoundResolved already ran in the same frame as the reveal and found this
    /// flag still set, so it left starting that pause to this instead.
    ///
    /// Reads _roundAlreadyResolved rather than LastRoundOutcome != null: a Joker (or any
    /// round without two normal cards to compare) resolves with no winner at all, which is a
    /// null outcome that still needs the field cleared and the vanish effects played --
    /// treating null as "not resolved yet" left those rounds' field-clear timer never
    /// starting, so a Joker's vanish animation silently never played.</summary>
    private void OnOpponentCardFlipFinished()
    {
        _opponentCardFlipPending = false;

        if (_roundAlreadyResolved)
        {
            _postFlipPauseTimer.Start();
        }
    }

    /// <summary>The pause after the reveal (the opponent's turn included) before the two
    /// cards take on their win/loss state.</summary>
    private void OnPostFlipPauseTimeout()
    {
        PlayRoundOutcomeEffects();

        // A 조커 round runs its own choreography instead, and it is longer than the pause
        // below — the field has to stay up until the 조커 has finished bursting, so the clear
        // is started from that effect's Finished rather than here. Nothing is lost by
        // skipping the call above for those rounds: a 조커 round has no winner to mark.
        if (PlayJokerEffectsAndReportIfDevouring())
        {
            return;
        }

        // Started here rather than the moment the round resolves: the field needs to stay up
        // through the reveal's own pauses and turn, not just for FIELD_CLEAR_DELAY_SECONDS
        // after an outcome nobody has seen yet.
        _fieldClearTimer.Start();
    }

    /// <summary>Plays whatever this round owes a 조커 — the cue for any 조커 at all, and the
    /// devouring choreography when there is a card to devour — and says whether that
    /// choreography started, since the field must then wait for it rather than clear on a
    /// timer.
    ///
    /// Called only once both cards are face-up. The effect is one card being pulled into the
    /// other, which reads as nothing at all while either is still face-down, and the cue is
    /// deliberately tied to the same beat so a 조커 sounds the same whoever played it.</summary>
    private bool PlayJokerEffectsAndReportIfDevouring()
    {
        MatchView view = GameState.Instance!.View;

        if (!view.MyCard.HasValue || !view.OpponentCard.HasValue)
        {
            return false;
        }

        bool mineIsJoker = view.MyCard.Value == ECardName.Joker;
        bool opponentIsJoker = view.OpponentCard.Value == ECardName.Joker;

        if (!mineIsJoker && !opponentIsJoker)
        {
            return false;
        }

        // One cue for the whole round, wherever the 조커 came from. It used to be played from
        // two places -- my own at the moment I dropped it, the opponent's as it turned over --
        // which made the same card sound like two different events depending on who played it.
        // Here it lands with the devouring, which is the beat the effect below is built around.
        AudioManager.Instance!.Play(ESoundName.Joker);

        // Two 조커 destroy each other without either devouring anything (DESIGN.md), so the
        // cue above still plays but there is no card to pull in -- the ordinary 소멸 handles
        // both of them.
        if (mineIsJoker && opponentIsJoker)
        {
            return false;
        }

        CardView jokerCardView = mineIsJoker ? _myPlayedCardView : _opponentPlayedCardView;
        CardView victimCardView = mineIsJoker ? _opponentPlayedCardView : _myPlayedCardView;

        _jokerDevourEffect.Play(jokerCardView, victimCardView);
        return true;
    }

    /// <summary>The 조커 has finished bursting and both cards are off the field.
    ///
    /// Cleared at once rather than after FIELD_CLEAR_DELAY_SECONDS. That delay exists to hold
    /// the two cards on the field while their win/loss states are read, and then they leave;
    /// here they have already left, so waiting would just be empty slots for that long. The
    /// shards keep flying either way — they belong to the effect node, not to the cards.
    ///
    /// SendPlayedCardsWhereTheyWent, inside this, finds both cards already hidden and leaves
    /// them alone, which is what keeps the devoured card from also being dissolved.</summary>
    private void OnJokerDevourFinished()
    {
        OnFieldClearTimeout();
    }

    private void OnChoiceRequired()
    {
        RefreshPromptStrip();
    }

    /// <summary>For a round with no choice in it, this fires in the same frame as
    /// RoundRevealed — the opponent's card is still sitting face-down, waiting on
    /// _preFlipPauseTimer, and _opponentCardFlipPending is still true. Rather than race that
    /// choreography, this leaves the win/loss pause to OnOpponentCardFlipFinished,
    /// which checks _roundAlreadyResolved once the turn is done and starts it there instead.
    /// For a round that needed a choice, the turn finished long ago and the flag is already
    /// clear, so the pause starts here right away.</summary>
    private void OnRoundResolved()
    {
        ReturnBothHandsToDecksIfReset();

        RefreshEverything();

        _roundAlreadyResolved = true;

        if (!_opponentCardFlipPending)
        {
            _postFlipPauseTimer.Start();
        }
    }

    /// <summary>Marks the round's winner and loser on the cards RefreshEverything already put
    /// on the field — the winner brightens green and grows, the loser darkens and shrinks. A
    /// 무승부 gets neither: no card won, so nothing should look like it did.
    ///
    /// Reads LastRoundOutcome rather than comparing the two cards itself: which side won is a
    /// GameLogic decision (Joker/Reset/ability precedence included) already resolved into the
    /// View, not something this screen should be re-deriving from card identities.</summary>
    private void PlayRoundOutcomeEffects()
    {
        ERoundOutcome? outcome = GameState.Instance!.View.LastRoundOutcome;

        if (outcome == ERoundOutcome.MyWin)
        {
            _myOutcomeEffect.PlayWin(_myPlayedCardView);
            _opponentOutcomeEffect.PlayLoss(_opponentPlayedCardView);
            AudioManager.Instance!.Play(ESoundName.RoundWon);
        }
        else if (outcome == ERoundOutcome.OpponentWin)
        {
            _opponentOutcomeEffect.PlayWin(_opponentPlayedCardView);
            _myOutcomeEffect.PlayLoss(_myPlayedCardView);
            AudioManager.Instance!.Play(ESoundName.RoundLost);
        }
    }

    /// <summary>리셋 puts both whole 패 back into their own decks, shuffles, and deals the
    /// same number of cards again (DESIGN.md). The refresh that follows cannot show any of
    /// that on its own — a redraw of the same size leaves the opponent's row, which is only a
    /// count, looking untouched — so both rows are emptied into their decks first and the
    /// refresh deals them out again, which is what actually happened.
    ///
    /// It reads ResetApplied rather than looking for a 리셋 among the played cards: a 조커 in
    /// the round blocks 리셋 outright (DESIGN.md), and nothing about either 패 changes. Asking
    /// the cards would replay the whole animation over hands that were never touched.</summary>
    private void ReturnBothHandsToDecksIfReset()
    {
        MatchView view = GameState.Instance!.View;

        if (!view.ResetApplied)
        {
            return;
        }

        _myHandView.ReturnWholeHandToDeck();
        _opponentHandView.ReturnWholeHandToDeck();
    }

    private void OnFieldClearTimeout()
    {
        // The round's cards are leaving, and their win/loss state goes with them — 진 카드가
        // 어두운 것은 덱에 들어가기 전까지다. Released before the cards are sent home, both
        // so a 소멸 plays on a card at its own size and brightness, and so the winner's glow
        // is never left hanging over a slot the card has already left.
        _myOutcomeEffect.Stop();
        _opponentOutcomeEffect.Stop();

        SendPlayedCardsWhereTheyWent();

        _fieldCleared = true;
        RefreshField();
    }

    /// <summary>Sends the round's cards where they actually went, just as the field is
    /// emptied. A 일반카드 goes to the bottom of its owner's 덱 and a 능력/조커 소멸s
    /// (DESIGN.md) — two different endings, so they are shown as two different things rather
    /// than both being switched off.
    ///
    /// It reads MyCardFate/OpponentCardFate rather than deciding anything: which cards came
    /// back is the host's answer, already in the View by the time the field clears.</summary>
    private void SendPlayedCardsWhereTheyWent()
    {
        MatchView view = GameState.Instance!.View;

        SendPlayedCardHome(view.MyCard, view.MyCardFate, _myPlayedCardView, _myDeckView, _myVanishEffect);
        SendPlayedCardHome(view.OpponentCard, view.OpponentCardFate, _opponentPlayedCardView, _opponentDeckView, _opponentVanishEffect);
    }

    private static void SendPlayedCardHome(
        ECardName? card,
        ECardFate? fate,
        CardView playedCardView,
        DeckView deckView,
        CardVanishEffect vanishEffect)
    {
        // Nothing to send anywhere from a field that is not currently showing this card — a
        // round that ended without one being revealed, or a clear that has already happened.
        if (!card.HasValue || !playedCardView.Visible)
        {
            return;
        }

        if (fate == ECardFate.ReturnedToDeckBottom)
        {
            deckView.AbsorbCard(card.Value, playedCardView.GlobalPosition);
            return;
        }

        if (fate == ECardFate.Vanished)
        {
            // The card stays on screen from here and takes itself off when it has finished
            // coming apart — see RefreshField, which leaves it alone while that runs.
            vanishEffect.Play(playedCardView);
        }
    }

    /// <summary>On a client this is what actually shows the new 패 — the public round
    /// broadcast carries no hand contents at all.</summary>
    private void OnMyHandChanged()
    {
        RefreshMyArea();
    }

    private void OnRequestRejected(string reason)
    {
        GD.Print($"[MatchScreenUI] request rejected: {reason}");

        // A rejected choice leaves the side still owed one, and the offered hand has not
        // changed — so the picker is simply shown again rather than left dead. A rejected
        // plain card submission has no picker to restore, so it falls through to the plain
        // message below.
        MatchView view = GameState.Instance!.View;
        if (view.CardIMustChooseFor.HasValue)
        {
            RefreshPromptStrip();
            return;
        }

        // A rejected card is still sitting on the submit zone; nothing else would take it
        // off. IsInstanceValid because the node is HandView's to free, and a refresh between
        // the drop and this rejection may already have done so.
        if (_dockedCardView != null && IsInstanceValid(_dockedCardView))
        {
            _dockedCardView.ReturnToHand();
        }

        _dockedCardView = null;
        _pendingTransformTarget = null;
        _myHandView.ForgetRememberedChoiceCards();
        _promptLabel.Text = REJECTION_MESSAGE;
    }

    private void OnHandSelectionChanged()
    {
        RefreshPromptStrip();
    }

    /// <summary>A hand card was dropped on the submit zone. Same request HandView's old
    /// click-to-play used to send directly — no judgment here either, the host still decides
    /// whether this card may actually be played (Scripts/CLAUDE.md).</summary>
    private void OnCardDroppedForSubmission(CardView cardView)
    {
        if (!cardView.ShownCard.HasValue)
        {
            return;
        }

        // The card has already parked itself on the zone by this point. Remembered so a
        // rejection can send it back; an accepted one is let go of at the reveal instead.
        _dockedCardView = cardView;

        GameState.Instance!.RequestCardPlay(cardView.ShownCard.Value);
    }

    private void OnConfirmSwapPressed()
    {
        IReadOnlyList<ECardName> chosen = _myHandView.SwapSelection;

        // Noted before the request goes out, and before the selection is cleared below: on the
        // host RequestChoice resolves the whole round in-process, so the new 패 arrives inside
        // this call. Same ordering rule as OnConfirmTransformTarget.
        _myHandView.RememberSwapSelection();

        GameState.Instance!.RequestChoice(CardChoice.Swapping(chosen));

        // Optimistic: assume the host accepts it. If it rejects instead, OnRequestRejected
        // rebuilds this same picker from View.CardIMustChooseFor, which a rejection does not
        // clear.
        _myHandView.SetSelectionModeNone();
        _confirmButton.Visible = false;
        _promptLabel.Text = "MATCH_PROMPT_CHOICE_SENT";
    }

    private void OnConfirmTransformTarget(ECardName target)
    {
        ECardName? source = _myHandView.TransformSourceSelection;
        if (!source.HasValue)
        {
            // The palette is disabled until a source is picked, so this should not fire —
            // guarded rather than trusted, per the project's own rule about client input.
            return;
        }

        // Both noted before the request goes out, and in this order: on the host, RequestChoice
        // resolves the whole round in-process and the answer comes back inside the call below,
        // so anything set afterwards would be set too late. Optimistic in the same way
        // OnConfirmSwapPressed is — a rejection clears it again.
        _pendingTransformTarget = target;
        _myHandView.RememberTransformSource();

        GameState.Instance!.RequestChoice(CardChoice.Transforming(source.Value, target));

        _myHandView.SetSelectionModeNone();
        SetTargetPaletteVisible(false);
        _promptLabel.Text = "MATCH_PROMPT_CHOICE_SENT";
    }

    private void OnMatchEnded(bool didIWin)
    {
        RefreshScoreBoard();

        _matchEndResultLabel.Text = didIWin ? "MATCH_END_WIN" : "MATCH_END_LOSS";

        // Rematch is host-initiated only, the same asymmetry ConnectionScreenUI already uses
        // for 매치 시작 — a client has nothing to press here but a status line.
        bool isHost = Multiplayer.IsServer();
        _rematchButton.Visible = isHost;
        _rematchButton.Disabled = false;
        _rematchStatusLabel.Visible = !isHost;
        _rematchStatusLabel.Text = "MATCH_END_WAITING_REMATCH";

        _matchEndOverlay.Visible = true;
    }

    private void OnOpponentLeft()
    {
        // Reconnection is out of scope (Scripts/Autoload/GameState.cs), so a rematch here has
        // nothing to reconnect to — only the door back to the title screen makes sense.
        _matchEndResultLabel.Text = "MATCH_END_OPPONENT_LEFT";
        _rematchButton.Visible = false;
        _rematchStatusLabel.Visible = false;
        _matchEndOverlay.Visible = true;
    }

    private void OnRematchPressed()
    {
        // Disabled immediately so a second click before OnMatchStarted hides the overlay
        // can't fire a second HostStartsMatch() into an already-resetting match. Re-enabled
        // in OnMatchEnded, the only other place this button becomes visible again.
        _rematchButton.Disabled = true;

        // Same call ConnectionScreenUI's host-only 매치 시작 button makes; it already resets
        // the match (GameState.ResetMatch) before rebuilding it, so no separate reset here.
        GameState.Instance!.HostStartsMatch();
    }

    private void OnReturnToTitlePressed()
    {
        NetworkManager.Instance!.Disconnect();
        GameState.Instance!.ResetConnection();
        GetTree().ChangeSceneToFile(TITLE_SCENE_PATH);
    }

    private void RefreshEverything()
    {
        RefreshOpponentArea();
        RefreshScoreBoard();
        RefreshField();
        RefreshPromptStrip();
        RefreshMyArea();
    }

    private void RefreshOpponentArea()
    {
        MatchView view = GameState.Instance!.View;

        _opponentDeckView.ShowCount(view.OpponentDeckCount);
        _opponentHandView.ShowFaceDownCards(view.OpponentHandCount);
    }

    private void RefreshScoreBoard()
    {
        MatchView view = GameState.Instance!.View;

        // The win target used to be written into the heading's words, where changing
        // WINS_NEEDED_FOR_MATCH would leave the screen saying something untrue.
        _scoreHeadingLabel.Text = string.Format(Tr("MATCH_WINS_NEEDED"), MatchSession.WINS_NEEDED_FOR_MATCH);

        _myScoreLabel.Text = string.Format(Tr("MATCH_MY_SCORE"), view.MyScore, MatchSession.WINS_NEEDED_FOR_MATCH);
        _opponentScoreLabel.Text = string.Format(Tr("MATCH_OPPONENT_SCORE"), view.OpponentScore, MatchSession.WINS_NEEDED_FOR_MATCH);

        PaintScorePips(_myScorePips, view.MyScore, new Color(0.36f, 0.72f, 0.46f));
        PaintScorePips(_opponentScorePips, view.OpponentScore, new Color(0.85f, 0.42f, 0.40f));
    }

    private void RefreshField()
    {
        MatchView view = GameState.Instance!.View;

        _roundLabel.Text = string.Format(Tr("MATCH_ROUND"), view.RoundNumber);

        // Between rounds the field is empty and stays empty: View still holds the last round's
        // cards (nothing clears them there), so this is what keeps an unrelated refresh from
        // putting them back on screen. The slots the cards live in are fixed-size, so hiding
        // them leaves the layout exactly where it was.
        if (_fieldCleared)
        {
            // A card being taken apart is still on its way out and hides itself when it gets
            // there; switching it off here would be cutting the effect off at frame one.
            _myPlayedCardView.Visible = _myVanishEffect.IsPlaying;
            _opponentPlayedCardView.Visible = _opponentVanishEffect.IsPlaying;
            _myActionLabel.Text = string.Empty;
            _opponentActionLabel.Text = string.Empty;
            _outcomeLabel.Text = string.Empty;
            return;
        }

        // The 조커 연출 owns both played cards while it runs — one is being wound into the
        // other and that one is coming apart — so an unrelated refresh must not put them back
        // at full size in their own slots. Same rule as _opponentCardFlipPending below, for
        // both cards at once rather than one. The labels underneath are still refreshed,
        // since nothing about the effect touches them.
        if (!_jokerDevourEffect.IsPlaying)
        {
            _myPlayedCardView.Visible = true;
            _opponentPlayedCardView.Visible = true;

            // Null before both sides have submitted: nothing is revealed yet, so the field
            // shows backs rather than an empty gap that would shift the row when a card lands.
            if (view.MyCard.HasValue)
            {
                _myPlayedCardView.ShowFaceUp(view.MyCard.Value);
            }
            else
            {
                _myPlayedCardView.ShowFaceDown();
            }

            // While the flip choreography is running, it owns this card's face and Scale --
            // OnRoundRevealed already set it face-down as that choreography's starting point,
            // and an unrelated refresh landing mid-turn (a hand change, a score update) must
            // not stomp it back to a flat ShowFaceUp/ShowFaceDown call.
            if (!_opponentCardFlipPending)
            {
                if (view.OpponentCard.HasValue)
                {
                    _opponentPlayedCardView.ShowFaceUp(view.OpponentCard.Value);
                }
                else
                {
                    _opponentPlayedCardView.ShowFaceDown();
                }
            }
        }

        // Counts and flags only — never which cards were involved (Scripts/CLAUDE.md's
        // hidden-information rule). Both reset to 0/false on reveal, so this reads blank
        // until the round this same refresh belongs to has actually resolved.
        _myActionLabel.Text = DescribeAction(view.MySwappedCardCount, view.MyTransformApplied);
        _opponentActionLabel.Text = DescribeAction(view.OpponentSwappedCardCount, view.OpponentTransformApplied);

        _outcomeLabel.Text = DescribeOutcome(view);
    }

    private static string DescribeAction(int swappedCardCount, bool transformApplied)
    {
        if (transformApplied)
        {
            return "MATCH_ACTION_TRANSFORMED";
        }

        if (swappedCardCount > 0)
        {
            return string.Format(TranslationServer.Translate("MATCH_ACTION_SWAPPED"), swappedCardCount);
        }

        return string.Empty;
    }

    /// <summary>The prompt strip is a permanent region that is empty most of the time. If it
    /// only existed during the choice phase the 패 below it would jump up and down mid-round.
    ///
    /// 교체 and 변화 are picked by clicking hand cards directly, not through a separate
    /// dialog — HandView switches into a selection mode and this strip becomes the confirm
    /// bar for it. This method is re-entered on every selection change (via
    /// SelectionChanged), so each branch sets the hand's mode idempotently rather than
    /// assuming it starts from Play.</summary>
    private void RefreshPromptStrip()
    {
        MatchView view = GameState.Instance!.View;

        RefreshPhaseTimer(view);

        if (view.CardIMustChooseFor == ECardName.Swap)
        {
            ShowSwapPrompt();
            return;
        }

        if (view.CardIMustChooseFor == ECardName.Transform)
        {
            ShowTransformPrompt();
            return;
        }

        HidePrompt();

        if (view.OpponentIsChoosing)
        {
            _promptLabel.Text = "MATCH_PROMPT_OPPONENT_CHOOSING";
            return;
        }

        _promptLabel.Text = string.Empty;
    }

    /// <summary>Starts, holds or clears the gauge. One bar serves both timed phases, because
    /// a round takes cards until it reveals and only asks for choices afterwards — they never
    /// run at once, and a player watching one clock is never also on another.
    ///
    /// It shows through the whole of each phase, including while it is the opponent who is
    /// still deciding: the host arms one timeout covering the phase rather than one per
    /// player, so a bar that appeared only on my own turn to act would be drawing a clock that
    /// is not the one running.
    ///
    /// The phase is identified by its own limit, so switching from one to the other restarts
    /// the gauge simply by being a different number. Restarting on that change rather than on
    /// every call is what matters here: this method is re-entered on every card the player
    /// clicks while selecting, and resetting there would hand out more time per click.</summary>
    private void RefreshPhaseTimer(MatchView view)
    {
        double phaseSeconds = PhaseTimeoutFor(view);
        if (phaseSeconds == _phaseTotalSeconds)
        {
            return;
        }

        _phaseTotalSeconds = phaseSeconds;
        _phaseTimerBar.Visible = phaseSeconds > 0.0;
        SetProcess(phaseSeconds > 0.0);

        if (phaseSeconds <= 0.0)
        {
            return;
        }

        _phaseSecondsRemaining = phaseSeconds;
        _phaseTimerBar.MaxValue = phaseSeconds;
        _phaseTimerBar.Value = phaseSeconds;
        _phaseTimerFillStyle.BgColor = PhaseTimerColorAt(phaseSeconds, phaseSeconds);
    }

    /// <summary>How long the phase currently running lasts, or zero when none is. Choice is
    /// tested first because it begins in the same frame the submission phase ends, and this
    /// order keeps the gauge off a phase that is already over.</summary>
    private static double PhaseTimeoutFor(MatchView view)
    {
        if (view.CardIMustChooseFor.HasValue || view.OpponentIsChoosing)
        {
            return GameState.CHOICE_TIMEOUT_SECONDS;
        }

        if (view.SubmissionPhaseActive)
        {
            return GameState.SUBMIT_TIMEOUT_SECONDS;
        }

        return 0.0;
    }

    private void ShowSwapPrompt()
    {
        _myHandView.SetSelectionModeForSwap();
        _confirmButton.Visible = true;
        SetTargetPaletteVisible(false);

        int selectedCount = _myHandView.SwapSelection.Count;
        _promptLabel.Text = string.Format(
            Tr("MATCH_PROMPT_SWAP"),
            selectedCount,
            SwapEffect.MAX_SWAPPED_CARDS);
    }

    private void ShowTransformPrompt()
    {
        _myHandView.SetSelectionModeForTransformSource();
        _confirmButton.Visible = false;

        ECardName? source = _myHandView.TransformSourceSelection;
        SetTargetPaletteVisible(true);
        SetTargetPaletteEnabled(source.HasValue);

        if (!source.HasValue)
        {
            _promptLabel.Text = "MATCH_PROMPT_TRANSFORM_SOURCE";
        }
        else
        {
            _promptLabel.Text = string.Format(
                Tr("MATCH_PROMPT_TRANSFORM_TARGET"),
                Tr(DisplayNameOf(source.Value)));
        }
    }

    private void HidePrompt()
    {
        _myHandView.SetSelectionModeNone();
        _confirmButton.Visible = false;
        SetTargetPaletteVisible(false);
    }

    /// <summary>Built once from CardDatabase rather than a hardcoded roster — the same
    /// reasoning DeckAssembler follows for the ability-card roster applies here.
    ///
    /// Only 일반카드/공백카드 are ever a legal 변화 result (DESIGN.md, TransformEffect.Validate),
    /// and that never depends on hand contents, unlike the source side — so the palette simply
    /// excludes everything else rather than building and disabling it. MatchDebugUI's own
    /// picker deliberately stays unfiltered; this one does not need to double as that
    /// exercise of the host's rejection path.</summary>
    private void BuildTransformTargetPalette()
    {
        if (CardDatabase.Instance == null)
        {
            return;
        }

        foreach (ECardName card in CardDatabase.Instance.LoadedCardNames)
        {
            ECardType cardType = card.GetCardType();
            if (cardType != ECardType.Normal && cardType != ECardType.Blank)
            {
                continue;
            }

            ECardName target = card;

            Button button = new Button();
            button.Text = DisplayNameOf(card);
            button.Visible = false;
            button.Pressed += () => { OnConfirmTransformTarget(target); };

            _targetPaletteRow.AddChild(button);
            _targetPaletteButtons.Add(button);
        }
    }

    private void SetTargetPaletteVisible(bool visible)
    {
        foreach (Button button in _targetPaletteButtons)
        {
            button.Visible = visible;
        }
    }

    /// <summary>Greyed out until a source card is picked. This is a UI affordance, not
    /// validation — the host still re-checks every choice regardless (Scripts/CLAUDE.md).</summary>
    private void SetTargetPaletteEnabled(bool enabled)
    {
        foreach (Button button in _targetPaletteButtons)
        {
            button.Disabled = !enabled;
        }
    }

    private void RefreshMyArea()
    {
        MatchView view = GameState.Instance!.View;

        _myDeckView.ShowCount(view.MyDeckCount);

        // Both of these run before the new 패 reaches the row, so the row is already settled
        // when the name matching in ShowFaceUpHand runs. Each is a no-op unless that kind of
        // choice is outstanding.
        _myHandView.ReturnRememberedSwapCardsToDeck();

        // Played before the new 패 reaches the row, so the row is already holding the
        // transformed card when the name matching in ShowFaceUpHand runs — otherwise 변화
        // looks like 교체: the old card flying off to the 덱 and the new one dealt out of it,
        // neither of which happened.
        if (_pendingTransformTarget.HasValue)
        {
            _myHandView.TransformRememberedCardInto(_pendingTransformTarget.Value);
            _pendingTransformTarget = null;
        }

        _myHandView.ShowFaceUpHand(view.MyHand);
    }

    private static void PaintScorePips(List<ColorRect> pips, int score, Color wonColor)
    {
        for (int index = 0; index < pips.Count; index++)
        {
            if (index < score)
            {
                pips[index].Color = wonColor;
            }
            else
            {
                pips[index].Color = new Color(0.22f, 0.22f, 0.26f);
            }
        }
    }

    private static string DescribeOutcome(MatchView view)
    {
        if (view.MyCardFate == null)
        {
            // Revealed but not resolved — a 교체/변화 choice can still be outstanding.
            return string.Empty;
        }

        if (view.LastRoundOutcome == null)
        {
            // 능력/공백/조커가 낀 라운드는 승패 자체가 없다 (DESIGN.md).
            return "MATCH_OUTCOME_NONE";
        }

        if (view.LastRoundOutcome == ERoundOutcome.MyWin)
        {
            return "MATCH_OUTCOME_WIN";
        }

        if (view.LastRoundOutcome == ERoundOutcome.OpponentWin)
        {
            return "MATCH_OUTCOME_LOSS";
        }

        return "MATCH_OUTCOME_TIE";
    }

    private static string DisplayNameOf(ECardName card)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(card);
        if (cardData == null)
        {
            return card.ToString();
        }

        return cardData.DisplayName;
    }
}
