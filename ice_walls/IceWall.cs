using Godot;
using System.Collections.Generic;

public partial class IceWall : Node3D
{
    private readonly List<IceWallSegment> _segments = new List<IceWallSegment>();
    private float _durationRemaining;
    private bool _hasDuration;

    public void Initialize(Vector3 start, Vector3 end, WallCastData data)
    {
        int segmentCount = ComputeSegmentCount(start.DistanceTo(end), data.SegmentWidth);

        for (int i = 0; i < segmentCount; i++)
        {
            float t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            Vector3 position = start.Lerp(end, t);

            IceWallSegment segment = data.SegmentScene.Instantiate<IceWallSegment>();
            AddChild(segment);
            segment.GlobalPosition = position;
            segment.Initialize(data.SegmentHealth, data.SegmentWidth);
            _segments.Add(segment);
        }

        _hasDuration = data.WallDuration > 0f;
        _durationRemaining = data.WallDuration;
    }

    public override void _Process(double delta)
    {
        if (!_hasDuration)
        {
            return;
        }

        _durationRemaining -= (float)delta;
        if (_durationRemaining <= 0f)
        {
            _hasDuration = false;

            foreach (IceWallSegment segment in _segments)
            {
                if (IsInstanceValid(segment))
                {
                    segment.Break();
                }
            }
        }
    }

    // Adds a new segment as soon as the total empty space along the wall would reach one
    // segment's width, rather than capping the gap between any single pair of segments.
    public static int ComputeSegmentCount(float distance, float segmentWidth)
    {
        int count = Mathf.CeilToInt(distance / segmentWidth);
        return Mathf.Max(2, count);
    }
}
