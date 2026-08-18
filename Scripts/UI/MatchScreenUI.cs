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
    private const string REJECTION_MESSAGE = "낼 수 없는 카드입니다.";

    private const string TITLE_SCENE_PATH = "res://Scenes/Screens/TitleScreen.tscn";

    private const int SCORE_PIP_SIZE = 18;

    private Label _opponentDeckLabel = null!;
    private HandView _opponentHandView = null!;
    private Label _myScoreLabel = null!;
    private Label _opponentScoreLabel = null!;
    private HBoxContainer _myScorePipRow = null!;
    private HBoxContainer _opponentScorePipRow = null!;
    private Label _roundLabel = null!;
    private CardView _myPlayedCardView = null!;
    private Label _myActionLabel = null!;
    private CardView _opponentPlayedCardView = null!;
    private Label _opponentActionLabel = null!;
    private Label _outcomeLabel = null!;
    private Label _promptLabel = null!;
    private Button _confirmButton = null!;
    private HBoxContainer _targetPaletteRow = null!;
    private Label _myDeckLabel = null!;
    private HandView _myHandView = null!;
    private PanelContainer _matchEndOverlay = null!;
    private Label _matchEndResultLabel = null!;
    private Button _rematchButton = null!;
    private Label _rematchStatusLabel = null!;
    private Button _returnToTitleButton = null!;

    private readonly List<ColorRect> _myScorePips = new List<ColorRect>();
    private readonly List<ColorRect> _opponentScorePips = new List<ColorRect>();

    // 변화's "into" palette. Built once — the loaded card roster does not change mid-match —
    // and shown or hidden rather than rebuilt on every prompt.
    private readonly List<Button> _targetPaletteButtons = new List<Button>();

    public override void _Ready()
    {
        _opponentDeckLabel = GetNode<Label>("Rows/OpponentArea/OpponentDeckLabel");
        _opponentHandView = GetNode<HandView>("Rows/OpponentArea/OpponentHandView");
        _myScoreLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/MyScoreLabel");
        _myScorePipRow = GetNode<HBoxContainer>("Rows/MiddleRow/ScoreBoard/MyScorePipRow");
        _opponentScoreLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/OpponentScoreLabel");
        _opponentScorePipRow = GetNode<HBoxContainer>("Rows/MiddleRow/ScoreBoard/OpponentScorePipRow");
        _roundLabel = GetNode<Label>("Rows/MiddleRow/Field/RoundLabel");
        _myPlayedCardView = GetNode<CardView>("Rows/MiddleRow/Field/MyPlayedArea/MyPlayedCardView");
        _myActionLabel = GetNode<Label>("Rows/MiddleRow/Field/MyPlayedArea/MyActionLabel");
        _opponentPlayedCardView = GetNode<CardView>("Rows/MiddleRow/Field/OpponentPlayedArea/OpponentPlayedCardView");
        _opponentActionLabel = GetNode<Label>("Rows/MiddleRow/Field/OpponentPlayedArea/OpponentActionLabel");
        _outcomeLabel = GetNode<Label>("Rows/MiddleRow/Field/OutcomeLabel");
        _promptLabel = GetNode<Label>("Rows/PromptStrip/PromptRow/PromptLabel");
        _confirmButton = GetNode<Button>("Rows/PromptStrip/PromptRow/ConfirmButton");
        _targetPaletteRow = GetNode<HBoxContainer>("Rows/PromptStrip/PromptRow/TargetPaletteRow");
        _myDeckLabel = GetNode<Label>("Rows/MyArea/MyDeckLabel");
        _myHandView = GetNode<HandView>("Rows/MyArea/MyHandView");
        _matchEndOverlay = GetNode<PanelContainer>("MatchEndOverlay");
        _matchEndResultLabel = GetNode<Label>("MatchEndOverlay/Center/Box/ResultLabel");
        _rematchButton = GetNode<Button>("MatchEndOverlay/Center/Box/RematchButton");
        _rematchStatusLabel = GetNode<Label>("MatchEndOverlay/Center/Box/RematchStatusLabel");
        _returnToTitleButton = GetNode<Button>("MatchEndOverlay/Center/Box/ReturnToTitleButton");

        BuildScorePips(_myScorePipRow, _myScorePips);
        BuildScorePips(_opponentScorePipRow, _opponentScorePips);
        BuildTransformTargetPalette();

        _confirmButton.Pressed += OnConfirmSwapPressed;
        _myHandView.SelectionChanged += OnHandSelectionChanged;
        _rematchButton.Pressed += OnRematchPressed;
        _returnToTitleButton.Pressed += OnReturnToTitlePressed;

        ConnectToAutoloadSignals();
        RefreshEverything();
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
        RefreshEverything();
    }

    /// <summary>Both cards are public. On the host the choice prompt is set immediately
    /// after this, so the prompt strip is refreshed again by ChoiceRequired.</summary>
    private void OnRoundRevealed()
    {
        RefreshField();
        RefreshPromptStrip();
    }

    private void OnChoiceRequired()
    {
        RefreshPromptStrip();
    }

    private void OnRoundResolved()
    {
        RefreshEverything();
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

        _promptLabel.Text = REJECTION_MESSAGE;
    }

    private void OnHandSelectionChanged()
    {
        RefreshPromptStrip();
    }

    private void OnConfirmSwapPressed()
    {
        IReadOnlyList<CardName> chosen = _myHandView.SwapSelection;
        GameState.Instance!.RequestChoice(CardChoice.Swapping(chosen));

        // Optimistic: assume the host accepts it. If it rejects instead, OnRequestRejected
        // rebuilds this same picker from View.CardIMustChooseFor, which a rejection does not
        // clear.
        _myHandView.SetSelectionModeNone();
        _confirmButton.Visible = false;
        _promptLabel.Text = "선택을 보냈습니다...";
    }

    private void OnConfirmTransformTarget(CardName target)
    {
        CardName? source = _myHandView.TransformSourceSelection;
        if (!source.HasValue)
        {
            // The palette is disabled until a source is picked, so this should not fire —
            // guarded rather than trusted, per the project's own rule about client input.
            return;
        }

        GameState.Instance!.RequestChoice(CardChoice.Transforming(source.Value, target));

        _myHandView.SetSelectionModeNone();
        SetTargetPaletteVisible(false);
        _promptLabel.Text = "선택을 보냈습니다...";
    }

    private void OnMatchEnded(bool didIWin)
    {
        RefreshScoreBoard();

        _matchEndResultLabel.Text = didIWin ? "매치 승리" : "매치 패배";

        // Rematch is host-initiated only, the same asymmetry ConnectionScreenUI already uses
        // for 매치 시작 — a client has nothing to press here but a status line.
        bool isHost = Multiplayer.IsServer();
        _rematchButton.Visible = isHost;
        _rematchStatusLabel.Visible = !isHost;
        _rematchStatusLabel.Text = "호스트가 재대결을 시작하길 기다리는 중...";

        _matchEndOverlay.Visible = true;
    }

    private void OnOpponentLeft()
    {
        // Reconnection is out of scope (Scripts/Autoload/GameState.cs), so a rematch here has
        // nothing to reconnect to — only the door back to the title screen makes sense.
        _matchEndResultLabel.Text = "상대가 나갔습니다.";
        _rematchButton.Visible = false;
        _rematchStatusLabel.Visible = false;
        _matchEndOverlay.Visible = true;
    }

    private void OnRematchPressed()
    {
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

        _opponentDeckLabel.Text = $"상대 덱 {view.OpponentDeckCount}";
        _opponentHandView.ShowFaceDownCards(view.OpponentHandCount);
    }

    private void RefreshScoreBoard()
    {
        MatchView view = GameState.Instance!.View;

        _myScoreLabel.Text = $"나 {view.MyScore} / {MatchSession.WINS_NEEDED_FOR_MATCH}";
        _opponentScoreLabel.Text = $"상대 {view.OpponentScore} / {MatchSession.WINS_NEEDED_FOR_MATCH}";

        PaintScorePips(_myScorePips, view.MyScore, new Color(0.36f, 0.72f, 0.46f));
        PaintScorePips(_opponentScorePips, view.OpponentScore, new Color(0.85f, 0.42f, 0.40f));
    }

    private void RefreshField()
    {
        MatchView view = GameState.Instance!.View;

        _roundLabel.Text = $"{view.RoundNumber} 라운드";

        // Null before both sides have submitted: nothing is revealed yet, so the field shows
        // backs rather than an empty gap that would shift the row when a card lands.
        if (view.MyCard.HasValue)
        {
            _myPlayedCardView.ShowFaceUp(view.MyCard.Value);
        }
        else
        {
            _myPlayedCardView.ShowFaceDown();
        }

        if (view.OpponentCard.HasValue)
        {
            _opponentPlayedCardView.ShowFaceUp(view.OpponentCard.Value);
        }
        else
        {
            _opponentPlayedCardView.ShowFaceDown();
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
            return "변화 적용";
        }

        if (swappedCardCount > 0)
        {
            return $"교체 {swappedCardCount}장";
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

        if (view.CardIMustChooseFor == CardName.Swap)
        {
            ShowSwapPrompt();
            return;
        }

        if (view.CardIMustChooseFor == CardName.Transform)
        {
            ShowTransformPrompt();
            return;
        }

        HidePrompt();

        if (view.OpponentIsChoosing)
        {
            _promptLabel.Text = "상대가 선택하는 중입니다...";
            return;
        }

        _promptLabel.Text = string.Empty;
    }

    private void ShowSwapPrompt()
    {
        _myHandView.SetSelectionModeForSwap();
        _confirmButton.Visible = true;
        SetTargetPaletteVisible(false);

        int selectedCount = _myHandView.SwapSelection.Count;
        _promptLabel.Text = $"교체 — 덱에 넣을 카드를 클릭하세요 ({selectedCount}장 선택됨)";
    }

    private void ShowTransformPrompt()
    {
        _myHandView.SetSelectionModeForTransformSource();
        _confirmButton.Visible = false;

        CardName? source = _myHandView.TransformSourceSelection;
        SetTargetPaletteVisible(true);
        SetTargetPaletteEnabled(source.HasValue);

        if (!source.HasValue)
        {
            _promptLabel.Text = "변화 — 바꿀 카드를 패에서 클릭하세요";
        }
        else
        {
            _promptLabel.Text = $"변화 — {DisplayNameOf(source.Value)}을(를) 무엇으로 바꿀까요?";
        }
    }

    private void HidePrompt()
    {
        _myHandView.SetSelectionModeNone();
        _confirmButton.Visible = false;
        SetTargetPaletteVisible(false);
    }

    /// <summary>Built once from CardDatabase rather than a hardcoded roster — the same
    /// reasoning DeckAssembler follows for the special-card roster applies here.</summary>
    private void BuildTransformTargetPalette()
    {
        if (CardDatabase.Instance == null)
        {
            return;
        }

        foreach (CardName card in CardDatabase.Instance.LoadedCardNames)
        {
            CardName target = card;

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

        _myDeckLabel.Text = $"내 덱 {view.MyDeckCount}";
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
            // 특수/더미/조커가 낀 라운드는 승패 자체가 없다 (DESIGN.md).
            return "승패 없음";
        }

        if (view.LastRoundOutcome == RoundOutcome.MyWin)
        {
            return "승";
        }

        if (view.LastRoundOutcome == RoundOutcome.OpponentWin)
        {
            return "패";
        }

        return "무승부";
    }

    private static string DisplayNameOf(CardName card)
    {
        CardData? cardData = CardDatabase.Instance?.GetCardData(card);
        if (cardData == null)
        {
            return card.ToString();
        }

        return cardData.DisplayName;
    }
}
