using Godot;

public static class GeometryUtils
{
    public static float DistanceSquaredToSegment(Vector3 point, Vector3 segmentStart, Vector3 segmentEnd)
    {
        Vector3 segment = segmentEnd - segmentStart;
        float lengthSquared = segment.LengthSquared();

        if (lengthSquared < 0.0001f)
        {
            return point.DistanceSquaredTo(segmentStart);
        }

        float t = Mathf.Clamp((point - segmentStart).Dot(segment) / lengthSquared, 0f, 1f);
        Vector3 closestPoint = segmentStart + segment * t;
        return point.DistanceSquaredTo(closestPoint);
    }

    // LookAt throws when its up vector is parallel to the direction it's aiming - a shot taken
    // straight down at an enemy underfoot, or a beam between two points stacked vertically.
    public static Vector3 SafeUpFor(Vector3 direction)
    {
        return Mathf.Abs(direction.Normalized().Dot(Vector3.Up)) > 0.999f ? Vector3.Forward : Vector3.Up;
    }
}
