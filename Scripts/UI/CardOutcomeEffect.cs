using Godot;

namespace RockAndScissPaper.UI;

/// <summary>How a round's two cards are told apart once it resolves: the winner turns green
/// and grows behind a halo of its own shape, the loser darkens and shrinks. One class for
/// both, so the two cards always move on the same curve over the same time — a winner easing
/// in while a loser snapped would read as two unrelated things happening.
///
/// The halo is a Panel carrying nothing but a green StyleBoxFlat shadow, sized to the card
/// and drawn behind it. A radial gradient was the obvious first answer and the wrong one: a
/// circle behind a rectangle reads as a lamp pointed at the card, not as the card itself
/// giving off light.
///
/// Unlike every other field effect here, this one does not end on its own. It eases into its
/// outcome state and holds there, because the state *is* the message and it has to still be
/// readable while the cards sit on the field. Stop is what puts the card back, and the field
/// clearing is what calls it — 진 카드는 덱에 들어가기 전까지만 어둡다.</summary>
public partial class CardOutcomeEffect : Node2D
{
    /// <summary>How long the card takes to reach its outcome state.</summary>
    private const float SETTLE_SECONDS = 0.3f;

    private const float WIN_SCALE = 1.12f;
    private const float LOSS_SCALE = 0.88f;

    /// <summary>How far the halo sits proud of the card on every side, before its own falloff
    /// begins. A rim this size is what makes the glow read as belonging to the card rather
    /// than sitting behind it.</summary>
    private const float HALO_MARGIN_PIXELS = 8f;

    // Green over 1 while red and blue stay under it: Modulate multiplies, so pushing one
    // channel up and the other two down is the only way to come out both brighter and
    // greener — lifting all three washes the card out to pale mint instead. Green is kept
    // this close to 1 on purpose: every part of the art brighter than 1/green clips to flat
    // colour, and at 1.7 that swallowed most of the illustration.
    private static readonly Color WIN_TINT = new Color(0.78f, 1.40f, 0.90f);
    private static readonly Color LOSS_TINT = new Color(0.40f, 0.40f, 0.45f);

    private Panel _halo = null!;

    private CardView? _card;
    private float _elapsedSeconds;
    private float _targetScale;
    private Color _targetTint;

    public override void _Ready()
    {
        _halo = GetNode<Panel>("Halo");
        _halo.Visible = false;
        SetProcess(false);
    }

    /// <summary>Whether a card is currently held in an outcome state. True from the moment
    /// Play* is called until Stop, settling included — not just while it is moving.</summary>
    public bool IsPlaying
    {
        get { return _card != null; }
    }

    public void PlayWin(CardView cardView)
    {
        Begin(cardView, WIN_SCALE, WIN_TINT, true);
    }

    public void PlayLoss(CardView cardView)
    {
        Begin(cardView, LOSS_SCALE, LOSS_TINT, false);
    }

    /// <summary>Give the card back as it was. Called when the field clears, and again at the
    /// next reveal — the same CardView is reused every round, and one left dark and shrunk
    /// would open the next round already having lost it.</summary>
    public void Stop()
    {
        if (_card != null && IsInstanceValid(_card))
        {
            _card.Scale = Vector2.One;
            _card.Modulate = Colors.White;
        }

        _card = null;
        _halo.Visible = false;
        SetProcess(false);
    }

    public override void _Process(double delta)
    {
        // The card belongs to the screen, which may have freed it out from under this.
        if (_card == null || !IsInstanceValid(_card))
        {
            Stop();
            return;
        }

        _elapsedSeconds += (float)delta;

        float progress = _elapsedSeconds / SETTLE_SECONDS;
        if (progress > 1f)
        {
            progress = 1f;
        }

        // Ease out: the cards move most in the first frames after the outcome lands, so it
        // reads as a reaction to the result rather than a slow drift away from it.
        ApplyAt(1f - Mathf.Pow(1f - progress, 3f));

        if (progress >= 1f)
        {
            // Settled, and it stays settled — only Stop takes it back off.
            SetProcess(false);
        }
    }

    private void Begin(CardView cardView, float targetScale, Color targetTint, bool glows)
    {
        Stop();

        _card = cardView;
        _targetScale = targetScale;
        _targetTint = targetTint;
        _elapsedSeconds = 0f;

        // Scaling a Control grows it out of its top-left corner unless the pivot is moved
        // onto its middle — the same reason CardFlipEffect sets this before squashing a card.
        _card.PivotOffset = _card.Size / 2f;

        _halo.Visible = glows;
        if (glows)
        {
            // Sized off the card every time rather than in the scene: the halo is the card's
            // own silhouette grown by a margin, and nothing here should have to be kept in
            // sync by hand with CardView's 200x280.
            _halo.Size = _card.Size + (Vector2.One * (HALO_MARGIN_PIXELS * 2f));
            _halo.PivotOffset = _halo.Size / 2f;
        }

        SetProcess(true);
        ApplyAt(0f);
    }

    private void ApplyAt(float eased)
    {
        float scale = Mathf.Lerp(1f, _targetScale, eased);
        _card!.Scale = new Vector2(scale, scale);
        _card.Modulate = Colors.White.Lerp(_targetTint, eased);

        // Only the winner carries one, so its visibility doubles as "did this card win" —
        // there is nothing a second flag would say that this does not.
        if (!_halo.Visible)
        {
            return;
        }

        // Read off the card rather than this node's own position, and take the card's own
        // scale: both are pivoted on their middles and share a centre, so growing them by
        // the same factor keeps the halo a card-shaped ring however far the card has grown.
        _halo.GlobalPosition = _card.GlobalPosition - (Vector2.One * HALO_MARGIN_PIXELS);
        _halo.Scale = _card.Scale;
        _halo.Modulate = new Color(1f, 1f, 1f, eased);
    }
}
