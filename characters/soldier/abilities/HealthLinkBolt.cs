using Godot;
using System;

// Health Link's spike in flight. Sweeps a ray along each frame's travel rather than relying on a
// collision shape, so a fast bolt can't slip through a thin enemy between frames. Reports where it
// ended up - an enemy, a surface, or nothing within range - then frees itself.
public partial class HealthLinkBolt : Node3D
{
    public struct Impact
    {
        // Null for a surface or a miss.
        public Enemy Enemy;
        // Terrain or an Ice Wall. False with no Enemy means it ran out of range.
        public bool HitSurface;
        public Vector3 Position;
        public Vector3 Direction;
        // The final stretch of flight - the shot line for headshot and weak-point checks.
        public Vector3 SegmentFrom;
        public Vector3 SegmentTo;
    }

    [Export] public float Speed { get; private set; } = 60f;

    private const uint HitMask = PhysicsLayers.Enemies | PhysicsLayers.World | PhysicsLayers.Obstacles;

    private Vector3 _direction;
    private float _rangeRemaining;
    private Action<Impact> _onResolved;

    // Must already be in the tree - it positions and orients itself globally.
    public void Launch(Vector3 from, Vector3 toward, float range, Action<Impact> onResolved)
    {
        _direction = (toward - from).Normalized();
        _rangeRemaining = range;
        _onResolved = onResolved;

        GlobalPosition = from;
        LookAt(from + _direction, GeometryUtils.SafeUpFor(_direction));
    }

    public override void _PhysicsProcess(double delta)
    {
        float step = Mathf.Min(Speed * (float)delta, _rangeRemaining);
        Vector3 from = GlobalPosition;
        Vector3 next = from + _direction * step;

        PhysicsRayQueryParameters3D query = PhysicsRayQueryParameters3D.Create(from, next);
        query.CollisionMask = HitMask;
        Godot.Collections.Dictionary result = GetWorld3D().DirectSpaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            Enemy enemy = result["collider"].As<Node3D>() as Enemy;
            bool isLiveEnemy = enemy != null && !enemy.IsDead;

            Resolve(new Impact
            {
                Enemy = isLiveEnemy ? enemy : null,
                HitSurface = !isLiveEnemy,
                Position = (Vector3)result["position"],
                Direction = _direction,
                SegmentFrom = from,
                SegmentTo = next
            });
            return;
        }

        GlobalPosition = next;
        _rangeRemaining -= step;

        if (_rangeRemaining <= 0f)
        {
            Resolve(new Impact { Position = next, Direction = _direction, SegmentFrom = from, SegmentTo = next });
        }
    }

    private void Resolve(Impact impact)
    {
        _onResolved?.Invoke(impact);
        _onResolved = null;
        QueueFree();
    }
}
