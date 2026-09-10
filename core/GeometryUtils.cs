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
}
