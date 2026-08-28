using Godot;

namespace RockAndScissPaper.UI;

/// <summary>소멸: a card coming apart from the top down into dust, rather than switching off.
///
/// It owns both halves of that, because they are one effect and have to agree frame by frame:
/// a dissolve shader eating the card from its top edge, and a band of dust emitted along the
/// exact line the card is being eaten at. Splitting them would mean two nodes each keeping
/// their own copy of how far along the effect is.
///
/// The card it plays on is not its own node. Like DeckView.AbsorbCard, it is handed what to
/// act on. Play gives it back untouched — the material comes off and the card is hidden — so a
/// CardView that is reused for the next round never carries a half-dissolved state into it;
/// PlayAndFreeAfterwards is for a card that exists only to be destroyed, which 변화 uses to
/// crumble the old card off the new one underneath it.</summary>
public partial class CardVanishEffect : Node2D
{
    private const string DISSOLVE_MATERIAL_PATH = "res://Assets/Materials/CardDissolve.tres";

    /// <summary>How long a card takes to come apart. It plays after the round is already
    /// decided, so it is the tail of a round rather than part of it — long enough to be seen
    /// crumbling, short enough not to be waited on.</summary>
    private const float DISSOLVE_SECONDS = 0.35f;

    private CpuParticles2D _dustParticles = null!;
    private ShaderMaterial _dissolveMaterial = null!;

    private CardView? _dissolvingCard;
    private float _cardHeight;
    private float _elapsedSeconds;

    // Whether the card, and this effect with it, are gone once the dissolve finishes. Off for
    // a card the caller keeps and reuses; on for one raised solely to be taken apart.
    private bool _freeWhenFinished;

    public override void _Ready()
    {
        _dustParticles = GetNode<CpuParticles2D>("DustParticles");

        // Duplicated rather than used straight off disk: both cards in a round can be 소멸
        // cards, and the two effects would otherwise be writing progress into one material.
        _dissolveMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(DISSOLVE_MATERIAL_PATH).Duplicate();

        SetProcess(false);
    }

    /// <summary>Whether a card is still coming apart. Read by the screen, which must not hide
    /// a card that is in the middle of being destroyed on it.</summary>
    public bool IsPlaying
    {
        get { return _dissolvingCard != null; }
    }

    /// <summary>Start taking this card apart. The card stays where it is and keeps being drawn
    /// by whoever owns it; only its material changes. It is hidden, not freed, at the end.</summary>
    public void Play(CardView cardView)
    {
        BeginDissolve(cardView, false);
    }

    /// <summary>The same effect on a card that has no life after it: both the card and this
    /// effect node are freed once it finishes. For a caller that raised the card only so that
    /// something could be seen coming apart, and has nothing to do with either afterwards.</summary>
    public void PlayAndFreeAfterwards(CardView cardView)
    {
        BeginDissolve(cardView, true);
    }

    private void BeginDissolve(CardView cardView, bool freeWhenFinished)
    {
        Stop();

        _dissolvingCard = cardView;
        _freeWhenFinished = freeWhenFinished;
        _cardHeight = cardView.Size.Y;
        _elapsedSeconds = 0f;

        _dissolveMaterial.SetShaderParameter("progress", 0f);
        _dissolveMaterial.SetShaderParameter("card_height", _cardHeight);

        // Set on the card's root, which draws nothing itself. Every visual part of a card is a
        // separate child node, and they carry use_parent_material so that this one material
        // reaches all of them — otherwise the background dissolves and the picture stays.
        cardView.Material = _dissolveMaterial;
        cardView.Visible = true;

        _dustParticles.GlobalPosition = DustPositionAt(0f);
        _dustParticles.Emitting = true;
        SetProcess(true);
    }

    /// <summary>Give the card back as it was, mid-effect if need be. Called when a new round
    /// arrives, since the same CardView is reused for it and a card left holding a
    /// half-finished dissolve would open the round with a bite taken out of it.</summary>
    public void Stop()
    {
        if (_dissolvingCard == null)
        {
            return;
        }

        // The node belongs to the screen, which may have freed it while this was running.
        if (IsInstanceValid(_dissolvingCard))
        {
            _dissolvingCard.Material = null;
        }

        _dissolvingCard = null;

        // Only the emitter stops; dust already in the air keeps falling and fading out on its
        // own, which is what stops the effect ending on a hard cut.
        _dustParticles.Emitting = false;
        SetProcess(false);
    }

    /// <summary>Only runs while a card is coming apart; Play switches it on and the end of the
    /// dissolve switches it off.</summary>
    public override void _Process(double delta)
    {
        // The card belongs to whoever handed it over, and they may have freed it out from
        // under this — a hand rebuilt mid-dissolve, a screen torn down.
        if (_dissolvingCard == null || !IsInstanceValid(_dissolvingCard))
        {
            FinishDissolve();
            return;
        }

        _elapsedSeconds += (float)delta;

        float progress = _elapsedSeconds / DISSOLVE_SECONDS;
        if (progress >= 1f)
        {
            FinishDissolve();
            return;
        }

        _dissolveMaterial.SetShaderParameter("progress", progress);

        // The dust is emitted from the line the card is being eaten at, not from the card as a
        // whole — that is the difference between a card crumbling and a card exploding.
        _dustParticles.GlobalPosition = DustPositionAt(progress);
    }

    /// <summary>Where the dissolve front is, on screen. Read off the card rather than off this
    /// node's own position, so the effect works wherever it is put — beside a fixed field slot,
    /// or inside a 패 whose cards are laid out somewhere different every frame.</summary>
    private Vector2 DustPositionAt(float progress)
    {
        Vector2 cardTopLeft = _dissolvingCard!.GlobalPosition;
        Vector2 cardSize = _dissolvingCard.Size;

        return cardTopLeft + new Vector2(cardSize.X / 2f, cardSize.Y * progress);
    }

    private void FinishDissolve()
    {
        bool freeWhenFinished = _freeWhenFinished;
        CardView? card = _dissolvingCard;

        // Hidden as well as fully dissolved: the shader has discarded every pixel by now, but
        // the material comes off in Stop, and a card left visible without it would snap back.
        if (card != null && IsInstanceValid(card))
        {
            card.Visible = false;
        }

        Stop();

        if (!freeWhenFinished)
        {
            return;
        }

        if (card != null && IsInstanceValid(card))
        {
            card.GetParent()?.RemoveChild(card);
            card.QueueFree();
        }

        // Nothing else refers to this node, and there is no second card for it to take apart.
        QueueFree();
    }
}
