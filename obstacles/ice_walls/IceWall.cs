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
            IceWallSegment segment = data.SegmentScene.Instantiate<IceWallSegment>();
            AddChild(segment);
            segment.GlobalPosition = SegmentPosition(start, end, i, segmentCount);
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

    // --- Shared segment layout -------------------------------------------------------------
    //
    // Everything that has to agree on where a wall's segments sit goes through these: the real
    // wall (above), the placement ghosts and overlap checks (RitualPlacer), the reserved-footprint
    // collision (WallFootprintMarker), and the placement preview meshes (WallPlacementPreview).
    // None of them re-derive the lerp, box size or vertical offset themselves, so changing segment
    // spacing is one edit rather than five that have to agree.

    // Adds a new segment as soon as the total empty space along the wall would reach one
    // segment's width, rather than capping the gap between any single pair of segments.
    public static int ComputeSegmentCount(float distance, float segmentWidth)
    {
        int count = Mathf.CeilToInt(distance / segmentWidth);
        return Mathf.Max(2, count);
    }

    // The base cost covers the shortest possible wall - two segments, since it can't be any
    // shorter - and each segment past that adds its own cost.
    public static int ChargedSegmentCount(int segmentCount) => Mathf.Max(0, segmentCount - 2);

    // The ground point for segment `index`, with the first and last landing exactly on the wall's
    // two endpoints.
    public static Vector3 SegmentPosition(Vector3 start, Vector3 end, int index, int segmentCount)
    {
        float t = segmentCount > 1 ? (float)index / (segmentCount - 1) : 0f;
        return start.Lerp(end, t);
    }

    // Matches the collision box IceWallSegment.Initialize builds for a real segment, so a
    // reserved footprint or a placement overlap test occupies exactly the space the wall will.
    public static Vector3 SegmentBoxSize(float segmentWidth)
    {
        return new Vector3(segmentWidth, segmentWidth * 2f, segmentWidth);
    }

    // A segment's box is centered a full width above its ground point (its height is 2x width).
    public static Vector3 SegmentBoxCenter(Vector3 groundPosition, float segmentWidth)
    {
        return groundPosition + Vector3.Up * segmentWidth;
    }
}
