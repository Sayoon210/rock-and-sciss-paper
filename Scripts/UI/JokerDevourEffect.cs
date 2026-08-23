using Godot;

namespace RockAndScissPaper.UI;

/// <summary>조커: the card it hit is pulled into it and then the 조커 bursts. Two beats of one
/// effect, played in order — the 삼킴 has to finish before the burst starts, or the 조커 comes
/// apart around a card still on its way in.
///
/// Neither card is this node's own. Like CardVanishEffect it is handed what to act on, because
/// the two cards live in fixed field slots that belong to the screen. It gives them both back
/// on Stop, so a round cut short mid-effect never leaves a card shrunk, turned, or parked on
/// top of the other one.
///
/// It plays only when exactly one side played a 조커. Two 조커 vanish without targeting each
/// other (DESIGN.md), so there is nothing to pull in and the ordinary 소멸 is what belongs
/// there — MatchScreenUI.TryPlayJokerDevour is where that is decided.</summary>
public partial class JokerDevourEffect : Node2D
{
    private const string SHATTER_MATERIAL_PATH = "res://Assets/Materials/CardShatter.tres";

    /// <summary>How long the doomed card takes to spiral in, and how long the 조커 then takes
    /// to come apart. Together a little over twice a plain 소멸, which is the point — this is
    /// the strongest card in the game and it should not be over as quickly.</summary>
    private const float DEVOUR_SECONDS = 0.5f;
    private const float SHATTER_SECONDS = 0.45f;

    /// <summary>How many full turns the devoured card makes on its way in. Coming straight in
    /// reads as a card sliding aside; it is the winding that makes it look swallowed.</summary>
    private const float DEVOUR_SPIRAL_TURNS = 1.25f;

    /// <summary>How sharply the pull accelerates. At 1 the card travels at a constant speed and
    /// looks like it is moving itself; above 1 it holds out at first and is snatched at the end.
    /// </summary>
    private const float DEVOUR_PULL_EXPONENT = 2.5f;

    /// <summary>How far the 조커 swells while it eats, and again as it bursts. The first makes
    /// the burst look like a consequence of the swallowing rather than a separate event; the
    /// second is what throws the shards outward, since the shader can only erase them in place.
    /// </summary>
    private const float SWELL_WHILE_DEVOURING = 1.05f;
    private const float SWELL_WHILE_SHATTERING = 1.14f;

    [Signal]
    public delegate void FinishedEventHandler();

    private CpuParticles2D _shardParticles = null!;
    private ShaderMaterial _shatterMaterial = null!;

    private CardView? _jokerCard;
    private CardView? _victimCard;

    private Vector2 _victimStartCenter;
    private Vector2 _jokerCenter;
    private Vector2 _victimHomePosition;

    private float _elapsedSeconds;
    private bool _shattering;

    public override void _Ready()
    {
        _shardParticles = GetNode<CpuParticles2D>("ShardParticles");

        // Duplicated for the same reason CardVanishEffect duplicates its own: the material is
        // where this effect's progress lives, and one taken straight off disk would be shared
        // with anything else that ever loads it.
        _shatterMaterial = (ShaderMaterial)GD.Load<ShaderMaterial>(SHATTER_MATERIAL_PATH).Duplicate();

        SetProcess(false);
    }

    /// <summary>Whether either beat is still running. Read by the screen, which must not put
    /// these two cards back in their slots while one is being pulled out of its own.</summary>
    public bool IsPlaying
    {
        get { return _jokerCard != null; }
    }

    /// <summary>Start the 삼킴. victimCard is the card the 조커 destroyed — it is pulled into
    /// jokerCard and hidden, and then jokerCard bursts and is hidden too.</summary>
    public void Play(CardView jokerCard, CardView victimCard)
    {
        Stop();

        _jokerCard = jokerCard;
        _victimCard = victimCard;
        _elapsedSeconds = 0f;
        _shattering = false;

        // Both cards turn and shrink about their own middles rather than their top-left
        // corners — the same thing CardFlipEffect sets before it squashes a card.
        jokerCard.PivotOffset = jokerCard.Size / 2f;
        victimCard.PivotOffset = victimCard.Size / 2f;

        // Where the devoured card has to go back to if this is cut short. It sits in a fixed
        // slot, so its position at the start is the whole answer.
        _victimHomePosition = victimCard.Position;

        // Taken in global space because the two cards live under different slots, and read
        // once here rather than each frame: the 조커 swells as it eats, and a centre measured
        // off a growing card would drag the target around while the other card flew at it.
        _victimStartCenter = victimCard.GlobalPosition + (victimCard.Size / 2f);
        _jokerCenter = jokerCard.GlobalPosition + (jokerCard.Size / 2f);

        SetProcess(true);
    }

    /// <summary>Give both cards back as they were, mid-effect if need be — the same reason
    /// every other field effect has a Stop. The next round reuses these two CardViews, and one
    /// left shrunk or sitting on top of the other would open that round already broken.
    ///
    /// Deliberately silent: a round torn down partway through is not a finished effect, and
    /// waking the screen's field-clear from here would clear a field the next round is
    /// already filling.</summary>
    public void Stop()
    {
        if (_victimCard != null && IsInstanceValid(_victimCard))
        {
            _victimCard.Position = _victimHomePosition;
            _victimCard.Scale = Vector2.One;
            _victimCard.Rotation = 0f;
        }

        if (_jokerCard != null && IsInstanceValid(_jokerCard))
        {
            _jokerCard.Scale = Vector2.One;
            _jokerCard.Material = null;
        }

        _victimCard = null;
        _jokerCard = null;

        // Only the emitter stops; shards already in the air keep flying and fading on their
        // own, which is what keeps the burst from ending on a hard cut.
        _shardParticles.Emitting = false;
        SetProcess(false);
    }

    /// <summary>Only runs between Play and the end of the burst; Play switches it on and
    /// Finish switches it off.</summary>
    public override void _Process(double delta)
    {
        // Either card may have been freed out from under this — a screen torn down, a round
        // rebuilt. Finish rather than Stop, because the screen is waiting on Finished to clear
        // the field and would otherwise sit on this round forever.
        if (_jokerCard == null || !IsInstanceValid(_jokerCard)
            || _victimCard == null || !IsInstanceValid(_victimCard))
        {
            Finish();
            return;
        }

        _elapsedSeconds += (float)delta;

        if (_shattering)
        {
            ProcessShatter();
            return;
        }

        ProcessDevour();
    }

    private void ProcessDevour()
    {
        float progress = _elapsedSeconds / DEVOUR_SECONDS;
        if (progress >= 1f)
        {
            BeginShatter();
            return;
        }

        float pull = Mathf.Pow(progress, DEVOUR_PULL_EXPONENT);

        // Wound in around the 조커 rather than aimed at it: the radius closes while the angle
        // keeps turning, which is the difference between being swallowed and being shoved.
        Vector2 fromJokerToVictim = _victimStartCenter - _jokerCenter;
        float radius = fromJokerToVictim.Length() * (1f - pull);
        float angle = fromJokerToVictim.Angle() + (pull * Mathf.Tau * DEVOUR_SPIRAL_TURNS);

        Vector2 center = _jokerCenter + (Vector2.FromAngle(angle) * radius);

        _victimCard!.GlobalPosition = center - (_victimCard.Size / 2f);
        _victimCard.Scale = Vector2.One * (1f - pull);
        _victimCard.Rotation = pull * Mathf.Tau * DEVOUR_SPIRAL_TURNS;

        _jokerCard!.Scale = Vector2.One * Mathf.Lerp(1f, SWELL_WHILE_DEVOURING, pull);
    }

    private void BeginShatter()
    {
        _shattering = true;
        _elapsedSeconds = 0f;

        // The devoured card is gone from here on. It is also put back in its slot at full size
        // first: the screen hands these same CardViews to the next round, and one left
        // shrunk to nothing under the 조커 would come back that way.
        _victimCard!.Visible = false;
        _victimCard.Position = _victimHomePosition;
        _victimCard.Scale = Vector2.One;
        _victimCard.Rotation = 0f;

        _shatterMaterial.SetShaderParameter("progress", 0f);

        // Set on the card's root, which draws nothing itself. Every visual part of a card is a
        // separate child carrying use_parent_material, so this one material reaches all of
        // them — the same arrangement CardVanishEffect relies on.
        _jokerCard!.Material = _shatterMaterial;

        _shardParticles.GlobalPosition = _jokerCenter;
        _shardParticles.Restart();
    }

    private void ProcessShatter()
    {
        float progress = _elapsedSeconds / SHATTER_SECONDS;
        if (progress >= 1f)
        {
            Finish();
            return;
        }

        _shatterMaterial.SetShaderParameter("progress", progress);

        _jokerCard!.Scale = Vector2.One * Mathf.Lerp(SWELL_WHILE_DEVOURING, SWELL_WHILE_SHATTERING, progress);
    }

    private void Finish()
    {
        CardView? jokerCard = _jokerCard;
        CardView? victimCard = _victimCard;

        // Both come off the field here. The shader has discarded every pixel of the 조커 by
        // now, but Stop takes the material off, and a card left visible without it would snap
        // back whole — the same last step CardVanishEffect takes.
        if (jokerCard != null && IsInstanceValid(jokerCard))
        {
            jokerCard.Visible = false;
        }

        if (victimCard != null && IsInstanceValid(victimCard))
        {
            victimCard.Visible = false;
        }

        Stop();

        EmitSignalFinished();
    }
}
