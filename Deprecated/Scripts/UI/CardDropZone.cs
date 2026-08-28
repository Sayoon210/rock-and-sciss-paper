using System.Collections.Generic;
using Godot;

namespace RockAndScissPaper.UI;

/// <summary>A zone a dragged CardView can be released onto, wherever its own rect happens to
/// sit in the current layout — hit-testing is a plain check against GetGlobalRect(), so
/// nothing here has to know or care where it was placed or how big it is. That is the
/// "general-purpose" part: the same scene works as a submit zone today and as whatever other
/// drop target shows up later, without this class changing.
///
/// It only reports what was dropped; it does not decide what dropping it means. The owning
/// screen listens for CardDropped and turns that into an actual request.
///
/// Driven directly by CardView (FindZoneContaining, SetHighlighted, NotifyDropped) rather
/// than through Godot's built-in Control drag-and-drop API — that API cannot preserve the
/// exact point a card was grabbed at or animate a smooth return on an invalid drop, and
/// CardView needed both.</summary>
public partial class CardDropZone : Control
{
    [Signal] public delegate void CardDroppedEventHandler(CardView cardView);

    private static readonly List<CardDropZone> _activeZones = new List<CardDropZone>();

    private Panel _highlightOverlay = null!;

    public override void _Ready()
    {
        _highlightOverlay = GetNode<Panel>("HighlightOverlay");
        _activeZones.Add(this);
    }

    public override void _ExitTree()
    {
        _activeZones.Remove(this);
    }

    /// <summary>The zone whose current screen rect contains globalPosition, or null. A plain
    /// linear scan — there is exactly one zone today, and the root project's own rule against
    /// building for hypothetical scale applies here too.</summary>
    public static CardDropZone? FindZoneContaining(Vector2 globalPosition)
    {
        foreach (CardDropZone zone in _activeZones)
        {
            if (zone.GetGlobalRect().HasPoint(globalPosition))
            {
                return zone;
            }
        }

        return null;
    }

    public void SetHighlighted(bool highlighted)
    {
        _highlightOverlay.Visible = highlighted;
    }

    public void NotifyDropped(CardView cardView)
    {
        _highlightOverlay.Visible = false;
        EmitSignal(SignalName.CardDropped, cardView);
    }
}
