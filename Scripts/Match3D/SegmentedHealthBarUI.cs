using System.Collections.Generic;
using Godot;
using RockAndScissPaper.GameLogic;

namespace RockAndScissPaper.Match3D;

/// <summary>One side's health as MatchSession.STARTING_HEALTH discrete cells rather than one
/// continuous fill, and a burst of sparks out of every cell a hit takes.
///
/// Discrete because health is discrete: DESIGN.md's damage table is whole points, 바위 taking
/// two and 가위/보 one each, and a continuous bar shows the difference between those as a
/// slightly longer slide. Cells show it as one cell going out or two, which is the same fact
/// the rules deal in and is countable at a glance from across the table.
///
/// The cell count comes from MatchSession.STARTING_HEALTH rather than from a number in the
/// scene, so retuning starting health cannot leave the bar drawing the wrong number of cells.
///
/// Shows health; never works out what health should be. It is not even the thing that decides
/// WHEN to show it — MatchWorldView calls in at the frame the blow lands, so the cells go out
/// on the impact rather than a second earlier when the signal arrived.</summary>
public partial class SegmentedHealthBarUI : HBoxContainer
{
    private const string SEGMENT_SCENE_PATH = "res://Scenes/Match3D/HealthSegment.tscn";
    private const string BURST_PATH = "Burst";

    private static readonly Color FILLED_COLOR = new Color(0.29f, 0.78f, 0.36f);

    // Not transparent-black: an empty cell has to stay visible, because "3 of 10 left" is only
    // readable if the 7 that are gone are still drawn as sockets to count against.
    private static readonly Color EMPTY_COLOR = new Color(0.09f, 0.11f, 0.1f, 0.72f);

    private readonly List<ColorRect> _segments = new List<ColorRect>();

    /// <summary>What the cells are currently drawing, which is not the same as what health is —
    /// it lags View.MyHealth by the length of the blow animation, and the gap between the two is
    /// exactly the set of cells that should burst on the next call.</summary>
    private int _shownHealth = MatchSession.STARTING_HEALTH;

    public override void _Ready()
    {
        PackedScene segmentScene = GD.Load<PackedScene>(SEGMENT_SCENE_PATH);
        for (int cell = 0; cell < MatchSession.STARTING_HEALTH; cell++)
        {
            ColorRect segment = segmentScene.Instantiate<ColorRect>();

            // The cells share the strip's width evenly, so the same scene fills a wide bar in
            // the corner and a narrow one over a character's head without either being authored
            // at a size. The scene's own custom_minimum_size is only a floor.
            segment.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            segment.SizeFlagsVertical = SizeFlags.Fill;
            segment.Color = FILLED_COLOR;

            AddChild(segment);
            _segments.Add(segment);
        }
    }

    /// <summary>Draws health, bursting every cell that just went out. Returns how many cells
    /// that was, so the caller can scale something to the size of the hit without counting the
    /// difference a second time — the screen-edge blood does exactly that.
    ///
    /// A rise (a rematch resetting to full) refills silently. Sparks are what a hit looks like
    /// and there is no hit to show.</summary>
    public int ShowHealth(int health)
    {
        int newHealth = Mathf.Clamp(health, 0, _segments.Count);

        for (int cell = newHealth; cell < _shownHealth; cell++)
        {
            Burst(_segments[cell]);
        }

        for (int cell = 0; cell < _segments.Count; cell++)
        {
            if (cell < newHealth)
            {
                _segments[cell].Color = FILLED_COLOR;
            }
            else
            {
                _segments[cell].Color = EMPTY_COLOR;
            }
        }

        int cellsLost = _shownHealth - newHealth;
        _shownHealth = newHealth;
        return Mathf.Max(cellsLost, 0);
    }

    /// <summary>Positioned at fire time rather than in the scene: the cell is stretched to
    /// whatever width the container gave it, which is not known when the scene is instantiated
    /// and differs between the two strips on screen.</summary>
    private static void Burst(ColorRect segment)
    {
        GpuParticles2D burst = segment.GetNode<GpuParticles2D>(BURST_PATH);
        burst.Position = segment.Size / 2f;

        // Restart rather than setting Emitting: a one-shot that already ran is finished, and
        // re-arming it is the only way the same cell can burst again in a later match.
        burst.Restart();
    }
}
