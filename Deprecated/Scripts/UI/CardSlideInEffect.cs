using Godot;

namespace RockAndScissPaper.UI;

/// <summary>Eases a CardView in from above its rest position, so a card appearing in a field
/// slot reads as having just left the 패 sitting above it rather than popping into existence.
/// Purely a Position converge -- no face change, no Scale. CardFlipEffect owns those, later
/// and separately, once both cards are actually revealed.</summary>
public partial class CardSlideInEffect : Node
{
    /// <summary>How far above its rest position the card starts, in the parent slot's own
    /// local pixels.</summary>
    private const float START_OFFSET_Y = -90f;

    /// <summary>How sharply the card closes the distance, as a rate rather than a duration —
    /// the same 1 - e^(-rate*delta) shape HandView's own SETTLE_RATE uses, so this reads as
    /// part of the same family of motion as everything else a card does on this screen.</summary>
    private const float SETTLE_RATE = 10f;

    /// <summary>Close enough to rest that the remaining distance is not worth another frame.</summary>
    private const float SETTLE_DISTANCE_PIXELS = 0.5f;

    private CardView? _card;

    /// <summary>Start the slide. cardView is expected to already be showing whatever face it
    /// should (a face-down back, for the one caller this has today) — this only ever moves
    /// it, never changes what it shows.</summary>
    public void Play(CardView cardView)
    {
        _card = cardView;
        _card.Position = new Vector2(0f, START_OFFSET_Y);
        SetProcess(true);
    }

    /// <summary>Give the card back at rest, mid-slide if need be — the same reason every
    /// other field effect has a Stop: a new round reuses this CardView, and one left
    /// partway through the slide would open that round already out of place.</summary>
    public void Stop()
    {
        if (_card != null && IsInstanceValid(_card))
        {
            _card.Position = Vector2.Zero;
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

        float t = 1f - Mathf.Exp(-SETTLE_RATE * (float)delta);
        _card.Position = _card.Position.Lerp(Vector2.Zero, t);

        if (_card.Position.Length() < SETTLE_DISTANCE_PIXELS)
        {
            Stop();
        }
    }
}
