using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.UI;

/// <summary>Turns a face-down CardView into its face-up self, edge-on at the midpoint the way
/// a physical card actually turns over — no shader needed, since squashing a Control's own
/// Scale.X to 0 and back is already a per-node transform, not a per-pixel one.
///
/// Only ever plays on the opponent's played card. Mine is already face up the moment I drop
/// it on the submit zone -- I know what I played. The opponent's is the one card whose face
/// the player has not seen yet, and revealing it is the whole point of this effect.</summary>
public partial class CardFlipEffect : Node
{
    /// <summary>How long the turn takes, start to finish. Deliberately unhurried -- "느리게"
    /// was the ask, not a snap into place.</summary>
    private const float FLIP_SECONDS = 0.45f;

    [Signal]
    public delegate void FinishedEventHandler();

    private CardView? _card;
    private CardName? _revealCard;
    private float _elapsedSeconds;
    private bool _hasRevealed;

    public bool IsPlaying
    {
        get { return _card != null; }
    }

    /// <summary>Start the turn. cardView is assumed to already be showing its face-down side;
    /// revealCard is what it turns into at the midpoint, when the edge-on card can't show
    /// either face and swapping it is invisible.</summary>
    public void Play(CardView cardView, CardName revealCard)
    {
        Stop();

        _card = cardView;
        _revealCard = revealCard;
        _elapsedSeconds = 0f;
        _hasRevealed = false;

        // Scaling a Control shrinks it toward its top-left corner unless told otherwise --
        // the pivot has to sit on the card's own center for this to read as a turn in place
        // rather than a card sliding sideways as it thins out.
        _card.PivotOffset = _card.Size / 2f;
        SetProcess(true);
    }

    /// <summary>Give the card back as it was, mid-turn if need be -- the same reason every
    /// other field effect has a Stop: a new round reuses this CardView, and one left
    /// edge-on would open that round already broken.</summary>
    public void Stop()
    {
        if (_card != null && IsInstanceValid(_card))
        {
            _card.Scale = Vector2.One;
        }

        _card = null;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        if (_card == null || !IsInstanceValid(_card))
        {
            Stop();
            return;
        }

        _elapsedSeconds += (float)delta;

        float progress = _elapsedSeconds / FLIP_SECONDS;
        if (progress >= 1f)
        {
            _card.Scale = Vector2.One;
            Stop();
            EmitSignalFinished();
            return;
        }

        // The face swaps at the midpoint, where Scale.X passes through 0 -- the one moment
        // the card is edge-on and showing neither face, so the swap itself is never seen.
        if (!_hasRevealed && progress >= 0.5f)
        {
            _card.ShowFaceUp(_revealCard!.Value);
            _hasRevealed = true;
        }

        float scaleX = Mathf.Abs(1f - (2f * progress));
        _card.Scale = new Vector2(scaleX, 1f);
    }
}
