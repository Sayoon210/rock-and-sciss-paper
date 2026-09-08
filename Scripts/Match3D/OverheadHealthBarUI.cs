using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>Pins a 2D strip over a point in the 3D world every frame — the opponent's health,
/// floating above their head instead of sitting in a corner of the screen.
///
/// Over the head rather than in the HUD because that is where the player is already looking:
/// the whole match is spent watching the person across the table, and a bar in the corner asks
/// them to look away from the thing the bar is about.
///
/// Unprojected onto the HUD rather than built as geometry in the world. A 3D bar would take the
/// scene's lighting and fog, which are tuned to make the far side of the table dim — exactly
/// the wrong thing to do to a readout. This stays screen-crisp at a constant size, and the only
/// thing it borrows from 3D is where to sit.
///
/// Which point it follows is wired in the scene rather than found by a path constant here,
/// because the anchor lives in a different branch of the tree from this node: a constant would
/// have to spell out a walk up through the CanvasLayer and back down, and would silently break
/// the moment either node moved.</summary>
public partial class OverheadHealthBarUI : Control
{
    /// <summary>The 3D point this sits directly above — a marker over the character's head.
    /// The strip's BOTTOM edge lands on it, so the marker is the top of the head and the bar
    /// clears it by however tall the strip is.</summary>
    [Export]
    public NodePath HeadAnchorPath { get; set; } = new NodePath();

    private Node3D? _headAnchor;

    public override void _Ready()
    {
        _headAnchor = GetNodeOrNull<Node3D>(HeadAnchorPath);

        // Hidden until the first _Process places it. Left visible it would draw one frame at
        // whatever position the scene authored, which is wherever the editor happened to leave
        // the rect.
        Visible = false;
    }

    public override void _Process(double delta)
    {
        Camera3D? camera = GetViewport().GetCamera3D();
        if (_headAnchor == null || camera == null)
        {
            Visible = false;
            return;
        }

        Vector3 headPosition = _headAnchor.GlobalPosition;

        // UnprojectPosition has no answer for a point behind the lens — it returns a mirrored
        // position in front of it, which would park the opponent's health on screen while the
        // player is looking the other way. Asked first, not cleaned up after.
        if (camera.IsPositionBehind(headPosition))
        {
            Visible = false;
            return;
        }

        Vector2 headOnScreen = camera.UnprojectPosition(headPosition);
        Position = headOnScreen - new Vector2(Size.X / 2f, Size.Y);

        // Nothing holds it inside the frame. The head camera's rest pitch looks about 36 degrees
        // DOWN at the table, which puts the space above the opponent's head off the top of the
        // screen whenever the player is looking at the cards — so the strip is genuinely absent
        // for a good part of the match, by choice. Pinning it to the edge instead would keep a
        // readout on screen that is no longer pointing at anything the player can see.
        Visible = GetViewportRect().Intersects(new Rect2(Position, Size));
    }
}
