using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class TagSigilCandidatePanel : Control
{
    [Export] public float Radius = 80f;
    [Export] public Color LineColor = new Color(1f, 0.85f, 0.3f);
    [Export] public float LineWidth = 17.5f;
    [Export] public float FlairLength = 14f;
    [Export] public float CrossbarWidth = 8f;
    [Export] public float RampWidth = 10f;
    [Export] public float JointDiamondSize = 14f;

    private readonly List<(Vector2 A, Vector2 B, float Width)> _strokes = new List<(Vector2, Vector2, float)>();
    private readonly List<(Vector2 From, Vector2 Control, Vector2 To, float Width)> _curves = new List<(Vector2, Vector2, Vector2, float)>();
    private readonly List<Vector2[]> _polygons = new List<Vector2[]>();

    public void SetPairing(IReadOnlyList<Tag> active, int[] targets)
    {
        _strokes.Clear();
        _curves.Clear();
        _polygons.Clear();

        if (targets == null || active.Count < 2)
        {
            QueueRedraw();
            return;
        }

        Vector2 center = Size / 2f;
        Vector2[] points = active.Select(tag => TagSigilGenerator.GetAnchorPosition(tag, center, Radius)).ToArray();

        HashSet<(int, int)> uniqueEdges = new HashSet<(int, int)>();
        for (int i = 0; i < targets.Length; i++)
        {
            uniqueEdges.Add((Mathf.Min(i, targets[i]), Mathf.Max(i, targets[i])));
        }

        int[] degree = new int[active.Count];
        foreach ((int a, int b) in uniqueEdges)
        {
            degree[a]++;
            degree[b]++;
        }

        // A dot with no line of its own can still sit exactly on top of some other edge's line
        // (the grid makes rows/columns/diagonals of active dots common) — exclude those from
        // flairing too, since a flair there reads as attached to a line it isn't actually part of.
        bool[] flairEligible = new bool[active.Count];
        for (int i = 0; i < active.Count; i++)
        {
            flairEligible[i] = degree[i] == 1 && !uniqueEdges.Any(edge =>
                edge.Item1 != i && edge.Item2 != i && PointLiesOnSegment(points[i], points[edge.Item1], points[edge.Item2]));
        }

        foreach ((int a, int b) in uniqueEdges)
        {
            Vector2 start = points[a];
            Vector2 end = points[b];
            float edgeLength = start.DistanceTo(end);

            _strokes.Add((start, end, LineWidth));

            if (flairEligible[a])
            {
                BuildFlair(start, (start - end).Normalized(), edgeLength);
            }

            if (flairEligible[b])
            {
                BuildFlair(end, (end - start).Normalized(), edgeLength);
            }
        }

        // Where two or more lines actually meet at an angle, hide the seam under a small
        // diamond instead of trying to compute a directional flair for a joint with no single
        // "outward" direction.
        for (int i = 0; i < points.Length; i++)
        {
            if (degree[i] >= 2)
            {
                _polygons.Add(BuildDiamond(points[i]));
            }
        }

        QueueRedraw();
    }

    public override void _Draw()
    {
        foreach ((Vector2 a, Vector2 b, float width) in _strokes)
        {
            DrawLine(a, b, LineColor, width);
        }

        foreach ((Vector2 from, Vector2 control, Vector2 to, float width) in _curves)
        {
            const int Segments = 10;
            Vector2[] curvePoints = new Vector2[Segments + 1];

            for (int i = 0; i <= Segments; i++)
            {
                float t = (float)i / Segments;
                curvePoints[i] = from.Lerp(control, t).Lerp(control.Lerp(to, t), t);
            }

            DrawPolyline(curvePoints, LineColor, width);
        }

        foreach (Vector2[] polygon in _polygons)
        {
            DrawColoredPolygon(polygon, LineColor);
        }
    }

    private static bool PointLiesOnSegment(Vector2 point, Vector2 a, Vector2 b)
    {
        const float DistanceTolerance = 2f;
        const float EndpointMargin = 0.02f;

        Vector2 ab = b - a;
        float lengthSquared = ab.LengthSquared();
        if (lengthSquared < 0.0001f)
        {
            return false;
        }

        float t = (point - a).Dot(ab) / lengthSquared;
        if (t < EndpointMargin || t > 1f - EndpointMargin)
        {
            return false;
        }

        Vector2 closestPoint = a + ab * t;
        return point.DistanceTo(closestPoint) <= DistanceTolerance;
    }

    // A perpendicular crossbar at a loose end, with a curved "ramp" sweeping from a point back
    // along the main line up to each crossbar tip. The ramp is a quadratic Bezier using the
    // endpoint itself as the control point, so it's tangent to the main line at the back point
    // and tangent to the crossbar at the tip — no sharp corner at either join.
    private void BuildFlair(Vector2 point, Vector2 outwardDirection, float edgeLength)
    {
        const float RampReach = 0.6f;
        const float BackReachMultiplier = 5f;

        Vector2 back = -outwardDirection;
        Vector2 perp = new Vector2(-outwardDirection.Y, outwardDirection.X);

        Vector2 tipLeft = point + perp * FlairLength;
        Vector2 tipRight = point - perp * FlairLength;

        // Never let the ramp's back point reach past the far end of its own line — on a tight
        // grid a short edge would otherwise send the curve shooting out past its other endpoint.
        float backDistance = Mathf.Min(FlairLength * BackReachMultiplier, edgeLength * 0.9f);
        Vector2 backPoint = point + back * backDistance;

        // The ramp stops short of the crossbar's outer tips so it dives into the flat bar's
        // body instead of defining its edge — the crossbar's own straight line is what actually
        // terminates the shape, giving a flat serif foot instead of a rounded curve tip.
        Vector2 rampEndLeft = point + perp * FlairLength * RampReach;
        Vector2 rampEndRight = point - perp * FlairLength * RampReach;

        _strokes.Add((tipLeft, tipRight, CrossbarWidth));
        _curves.Add((backPoint, point, rampEndLeft, RampWidth));
        _curves.Add((backPoint, point, rampEndRight, RampWidth));
    }

    private Vector2[] BuildDiamond(Vector2 center)
    {
        return new[]
        {
            center + new Vector2(0, -JointDiamondSize),
            center + new Vector2(JointDiamondSize, 0),
            center + new Vector2(0, JointDiamondSize),
            center + new Vector2(-JointDiamondSize, 0)
        };
    }
}
