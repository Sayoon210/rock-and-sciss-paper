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

    private const int SCORE_PIP_SIZE = 18;

    private Label _opponentDeckLabel = null!;
    private HandView _opponentHandView = null!;
    private Label _myScoreLabel = null!;
    private Label _opponentScoreLabel = null!;
    private HBoxContainer _myScorePipRow = null!;
    private HBoxContainer _opponentScorePipRow = null!;
    private Label _roundLabel = null!;
    private CardView _myPlayedCardView = null!;
    private CardView _opponentPlayedCardView = null!;
    private Label _outcomeLabel = null!;
    private Label _promptLabel = null!;
    private Label _myDeckLabel = null!;
    private HandView _myHandView = null!;

    private readonly List<ColorRect> _myScorePips = new List<ColorRect>();
    private readonly List<ColorRect> _opponentScorePips = new List<ColorRect>();

    public override void _Ready()
    {
        _opponentDeckLabel = GetNode<Label>("Rows/OpponentArea/OpponentDeckLabel");
        _opponentHandView = GetNode<HandView>("Rows/OpponentArea/OpponentHandView");
        _myScoreLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/MyScoreLabel");
        _myScorePipRow = GetNode<HBoxContainer>("Rows/MiddleRow/ScoreBoard/MyScorePipRow");
        _opponentScoreLabel = GetNode<Label>("Rows/MiddleRow/ScoreBoard/OpponentScoreLabel");
        _opponentScorePipRow = GetNode<HBoxContainer>("Rows/MiddleRow/ScoreBoard/OpponentScorePipRow");
        _roundLabel = GetNode<Label>("Rows/MiddleRow/Field/RoundLabel");
        _myPlayedCardView = GetNode<CardView>("Rows/MiddleRow/Field/MyPlayedCardView");
        _opponentPlayedCardView = GetNode<CardView>("Rows/MiddleRow/Field/OpponentPlayedCardView");
        _outcomeLabel = GetNode<Label>("Rows/MiddleRow/Field/OutcomeLabel");
        _promptLabel = GetNode<Label>("Rows/PromptStrip/PromptLabel");
        _myDeckLabel = GetNode<Label>("Rows/MyArea/MyDeckLabel");
        _myHandView = GetNode<HandView>("Rows/MyArea/MyHandView");

        BuildScorePips(_myScorePipRow, _myScorePips);
        BuildScorePips(_opponentScorePipRow, _opponentScorePips);

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
        _promptLabel.Text = REJECTION_MESSAGE;
    }

    private void OnMatchEnded(bool didIWin)
    {
        RefreshScoreBoard();

        if (didIWin)
        {
            _promptLabel.Text = "매치 승리";
        }
        else
        {
            _promptLabel.Text = "매치 패배";
        }
    }

    private void OnOpponentLeft()
    {
        _promptLabel.Text = "상대가 나갔습니다.";
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

        _outcomeLabel.Text = DescribeOutcome(view);
    }

    /// <summary>The prompt strip is a permanent region that is empty most of the time. If it
    /// only existed during the choice phase the 패 below it would jump up and down mid-round.
    /// The real 교체/변화 picker replaces this plain label in a later pass.</summary>
    private void RefreshPromptStrip()
    {
        MatchView view = GameState.Instance!.View;

        if (view.CardIMustChooseFor.HasValue)
        {
            _promptLabel.Text = $"{DisplayNameOf(view.CardIMustChooseFor.Value)} — 선택이 필요합니다.";
            return;
        }

        if (view.OpponentIsChoosing)
        {
            _promptLabel.Text = "상대가 선택하는 중입니다...";
            return;
        }

        _promptLabel.Text = string.Empty;
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
