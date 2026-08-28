using System.Collections.Generic;
using Godot;

namespace RockAndScissPaper.Match3D;

/// <summary>The card's geometry, built once and shared by every CardView: a rounded-rectangle
/// face (used for both the front and the back) and the thin side wall that closes the gap
/// between them.
///
/// Built in code rather than authored as a BoxMesh + QuadMesh in CardView.tscn because Godot has
/// no rounded-rectangle primitive. That swap also removes a real rendering fault the primitives
/// had: the face quads sat at exactly +/- half the box's thickness, i.e. exactly coplanar with
/// the box's own front and back faces, which z-fights. Here the face and the side wall meet at a
/// right angle along the outline and share no surface at all.
///
/// The card's dimensions live here, not in the .tscn, so there is one source of truth for them —
/// the .tscn's node transforms would otherwise have to encode the thickness a second time.
///
/// Not [Tool], so a card shows nothing in the editor viewport (the mesh only exists once the
/// game runs). DebugHandPreview already behaved that way, so nothing that was visible before
/// stopped being visible.</summary>
public static class RoundedCardMesh
{
    // A standard poker card, 63.5 x 88.9 mm. The 0.5 mm thickness is a slight exaggeration of
    // real 0.3 mm card stock, so the card still reads as an object rather than a decal.
    public static readonly Vector2 CARD_SIZE = new Vector2(0.0635f, 0.0889f);
    public const float CARD_THICKNESS = 0.0005f;

    // A real playing card's corners are about 3.2 mm. 2 mm is deliberately under that — the
    // corners were asked to be rounded only slightly. This is the one number to change to
    // taste; everything else follows from it.
    public const float CORNER_RADIUS = 0.002f;

    private const int SEGMENTS_PER_CORNER = 6;

    /// <summary>The rounded rectangle, facing +Z and sitting at the card's front. The Back node
    /// uses this same mesh turned 180 degrees about Y, which both faces it the other way and
    /// mirrors it horizontally — the way a real card reads when flipped.</summary>
    public static readonly ArrayMesh FACE_MESH = BuildFaceMesh();

    /// <summary>The side wall alone — no caps, since FACE_MESH already covers both openings.</summary>
    public static readonly ArrayMesh EDGE_MESH = BuildEdgeMesh();

    /// <summary>The card's silhouette, counter-clockwise as seen from +Z. Each corner
    /// contributes its own arc endpoints rather than sharing them, so the straight edges fall
    /// out as the segments between one corner's last point and the next corner's first.</summary>
    private static Vector2[] BuildOutline()
    {
        float insetHalfWidth = CARD_SIZE.X / 2f - CORNER_RADIUS;
        float insetHalfHeight = CARD_SIZE.Y / 2f - CORNER_RADIUS;
        Vector2[] cornerCenters =
        {
            new Vector2(insetHalfWidth, insetHalfHeight),
            new Vector2(-insetHalfWidth, insetHalfHeight),
            new Vector2(-insetHalfWidth, -insetHalfHeight),
            new Vector2(insetHalfWidth, -insetHalfHeight),
        };

        List<Vector2> outline = new List<Vector2>();
        for (int corner = 0; corner < cornerCenters.Length; corner++)
        {
            for (int step = 0; step <= SEGMENTS_PER_CORNER; step++)
            {
                float angle = Mathf.Pi / 2f * (corner + (float)step / SEGMENTS_PER_CORNER);
                Vector2 armFromCenter = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * CORNER_RADIUS;
                outline.Add(cornerCenters[corner] + armFromCenter);
            }
        }

        return outline.ToArray();
    }

    private static ArrayMesh BuildFaceMesh()
    {
        Vector2[] outline = BuildOutline();
        float halfThickness = CARD_THICKNESS / 2f;

        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);
        surface.SetNormal(Vector3.Back);

        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 current = outline[i];
            Vector2 next = outline[(i + 1) % outline.Length];

            // Godot winds front faces CLOCKWISE as seen from the visible side — measured
            // against QuadMesh's own arrays, not assumed — so the counter-clockwise outline is
            // walked backwards here. Getting this the other way round renders the card
            // invisible from the front and visible from behind.
            AddFaceVertex(surface, Vector2.Zero, halfThickness);
            AddFaceVertex(surface, next, halfThickness);
            AddFaceVertex(surface, current, halfThickness);
        }

        return surface.Commit();
    }

    /// <summary>UV laid out the way QuadMesh lays its own out — origin at the top-left of the
    /// card — so CardView's existing atlas UV1 scale/offset crop still lands correctly.</summary>
    private static void AddFaceVertex(SurfaceTool surface, Vector2 point, float z)
    {
        surface.SetUV(new Vector2(
            (point.X + CARD_SIZE.X / 2f) / CARD_SIZE.X,
            (CARD_SIZE.Y / 2f - point.Y) / CARD_SIZE.Y));
        surface.AddVertex(new Vector3(point.X, point.Y, z));
    }

    private static ArrayMesh BuildEdgeMesh()
    {
        Vector2[] outline = BuildOutline();
        float halfThickness = CARD_THICKNESS / 2f;

        SurfaceTool surface = new SurfaceTool();
        surface.Begin(Mesh.PrimitiveType.Triangles);

        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 current = outline[i];
            Vector2 next = outline[(i + 1) % outline.Length];

            // Walking a counter-clockwise outline keeps the card's interior on the left, so
            // outward is to the right of travel. Set per segment, which flat-shades the wall —
            // it is a 0.5 mm strip of flat colour, so there is nothing to gain from smoothing it.
            Vector2 along = next - current;
            surface.SetNormal(new Vector3(along.Y, -along.X, 0f).Normalized());

            Vector3 currentFront = new Vector3(current.X, current.Y, halfThickness);
            Vector3 nextFront = new Vector3(next.X, next.Y, halfThickness);
            Vector3 currentBack = new Vector3(current.X, current.Y, -halfThickness);
            Vector3 nextBack = new Vector3(next.X, next.Y, -halfThickness);

            surface.AddVertex(currentFront);
            surface.AddVertex(nextFront);
            surface.AddVertex(currentBack);

            surface.AddVertex(nextFront);
            surface.AddVertex(nextBack);
            surface.AddVertex(currentBack);
        }

        return surface.Commit();
    }
}
