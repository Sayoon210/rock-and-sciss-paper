using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>The blood closing in from the edges of the screen when the player takes a hit.
/// Owns only the fade; the red itself is DamageVignette.gdshader.
///
/// Fires on the player being hit and not on the player throwing the blow, which is the one
/// thing that separates it from the camera shake. The shake is deliberately felt on BOTH
/// screens — a punch only the thrower feels reads as the victim not having been hit — so it
/// says "a blow landed" and cannot say whose face it landed on. This says that.</summary>
public partial class DamageVignetteUI : ColorRect
{
    private const string INTENSITY_PARAMETER = "intensity";

    // What one point of health costs, and what each point after it adds. 바위 takes two points
    // (WinLossRules) and lands on full red; 가위 and 보 take one and land well short of it, so
    // the difference the damage table draws is visible on the screen edge as well as on the
    // health cells. Scaled off the health actually lost rather than off the card that won —
    // this file has no business knowing the damage table.
    private const float FIRST_POINT_INTENSITY = 0.6f;
    private const float EXTRA_INTENSITY_PER_POINT = 0.4f;

    // Slower than HeadFollowCamera's shake decay on purpose. A knock is over the instant it
    // happens; being hurt is not, and a vignette that vanishes as fast as the shake reads as a
    // flicker rather than as damage.
    private const float DECAY_PER_SECOND = 2.4f;

    // Below this the red is under a percent of full and invisible, and still costs a full-screen
    // transparent pass every frame forever. Snapped to zero, which hides the rect entirely.
    private const float NEGLIGIBLE_INTENSITY = 0.01f;

    private ShaderMaterial _vignetteMaterial = null!;
    private float _intensity;

    public override void _Ready()
    {
        _vignetteMaterial = (ShaderMaterial)Material;
        ApplyIntensity();
    }

    /// <summary>Kicks the red to full for this hit and lets it fade. Takes health lost rather
    /// than a strength so the caller does not have to convert; a call with nothing lost is a
    /// no-op, which is what makes it safe to call on every resolved round.</summary>
    public void Flash(int healthLost)
    {
        if (healthLost <= 0)
        {
            return;
        }

        float hitIntensity = FIRST_POINT_INTENSITY + (healthLost - 1) * EXTRA_INTENSITY_PER_POINT;

        // Takes the larger of the two rather than adding, for the same reason the camera shake
        // does: two hits landing close together must not stack past what either was meant to be.
        _intensity = Mathf.Min(Mathf.Max(_intensity, hitIntensity), 1f);
        ApplyIntensity();
    }

    /// <summary>Exponential rather than linear, so the red drops away hard and then lingers —
    /// and because that shape is frame-rate independent, the same reasoning RemoteHeadLook's
    /// smoothing and the camera shake's decay are both written to.</summary>
    public override void _Process(double delta)
    {
        if (_intensity <= 0f)
        {
            return;
        }

        _intensity *= Mathf.Exp(-DECAY_PER_SECOND * (float)delta);
        if (_intensity < NEGLIGIBLE_INTENSITY)
        {
            _intensity = 0f;
        }

        ApplyIntensity();
    }

    private void ApplyIntensity()
    {
        // Hidden outright at zero rather than left drawing a fully transparent full-screen rect,
        // which is a real fill cost for a picture that is guaranteed to be nothing.
        Visible = _intensity > 0f;
        _vignetteMaterial.SetShaderParameter(INTENSITY_PARAMETER, _intensity);
    }
}
