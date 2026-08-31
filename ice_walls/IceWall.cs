using Godot;
using System.Collections.Generic;

public partial class IceWall : Node3D
{
    [Signal] public delegate void ExpiredEventHandler();
    [Signal] public delegate void DestroyedEventHandler();

    private readonly List<IceWallSegment> _segments = new List<IceWallSegment>();
    private Vector3 _start;
    private Vector3 _end;
    private float _currentHealth;
    private float _durationRemaining;
    private bool _hasDuration;

    public bool IsDestroyed => _currentHealth <= 0f;

    public void Initialize(Vector3 start, Vector3 end, WallCastData data)
    {
        _start = start;
        _end = end;

        int segmentCount = ComputeSegmentCount(start.DistanceTo(end), data.SegmentWidth);

        for (int i = 0; i < segmentCount; i++)
        {
            float t = segmentCount > 1 ? (float)i / (segmentCount - 1) : 0f;
            Vector3 position = start.Lerp(end, t);

            IceWallSegment segment = data.SegmentScene.Instantiate<IceWallSegment>();
            AddChild(segment);
            segment.GlobalPosition = position;
            segment.Initialize(this, data.SegmentWidth);
            _segments.Add(segment);
        }

        // One entity healthwise: total HP is the sum of what each segment would have had on its
        // own, but damage anywhere drains one shared pool, and every segment breaks together.
        _currentHealth = data.SegmentHealth * segmentCount;

        _hasDuration = data.WallDuration > 0f;
        _durationRemaining = data.WallDuration;
    }

    // Also one entity for targeting: the point along the wall's own line nearest an arbitrary
    // position, so enemies converging on the wall each aim at their own nearest point instead of
    // all being sent to the same segment's center.
    public Vector3 ClosestPointOnWall(Vector3 fromPosition)
    {
        Vector3 wallVector = _end - _start;
        float lengthSquared = wallVector.LengthSquared();

        if (lengthSquared < 0.0001f)
        {
            return _start;
        }

        float t = Mathf.Clamp((fromPosition - _start).Dot(wallVector) / lengthSquared, 0f, 1f);
        return _start + wallVector * t;
    }

    public void TakeDamage(DamagePacket damage)
    {
        if (_currentHealth <= 0f)
        {
            return;
        }

        _currentHealth -= damage.Total;
        if (_currentHealth <= 0f)
        {
            EmitSignal(SignalName.Destroyed);
            BreakAllSegments();
        }
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
            _currentHealth = 0f;
            EmitSignal(SignalName.Expired);
            BreakAllSegments();
        }
    }

    private void BreakAllSegments()
    {
        foreach (IceWallSegment segment in _segments)
        {
            if (IsInstanceValid(segment))
            {
                segment.Break();
            }
        }

        QueueFree();
    }

    // Adds a new segment as soon as the total empty space along the wall would reach one
    // segment's width, rather than capping the gap between any single pair of segments.
    public static int ComputeSegmentCount(float distance, float segmentWidth)
    {
        int count = Mathf.CeilToInt(distance / segmentWidth);
        return Mathf.Max(2, count);
    }
}
