using Godot;
using System.Collections.Generic;

// Something that travels along the ground for a duration, following the terrain up and down, and
// hits each enemy it passes through once - Gust's twister. A wall, or a drop deeper than one step,
// ends it early. Every machine runs the same movement against the same terrain, so a copy follows
// the host's path without being told where it went.
public partial class GroundSweep : Node3D
{
    // Authored at unit radius, standing on the origin; scaled sideways to the sweep's radius.
    [Export] public Node3D Footprint { get; private set; }
    [Export] public float Height { get; private set; } = 1.8f;
    // The most it can climb or drop between frames and still count as following the ground.
    [Export] public float MaxStep { get; private set; } = 0.5f;

    private const int MaxResults = 64;

    private readonly HashSet<Enemy> _struck = new();
    private readonly PhysicsRayQueryParameters3D _ray = new() { CollisionMask = PhysicsLayers.World };
    private PhysicsShapeQueryParameters3D _query;
    private Vector3 _direction;
    private float _speed;
    private float _remaining;
    private DamagePacket _damage;
    private SpellHit _spellHit;
    private float _ragdollForce;
    private bool _isCopy;

    // Called from the Ritual's physics step, so the first hit's space query is safe here.
    public void Initialize(Vector3 start, Vector3 direction, float speed, float radius, float duration, DamagePacket damage, float ragdollForce, SpellHit spellHit)
    {
        Place(start, direction, speed, radius, duration);

        _damage = damage;
        _ragdollForce = ragdollForce;
        _spellHit = spellHit;
        _query = new PhysicsShapeQueryParameters3D
        {
            Shape = new CylinderShape3D { Radius = radius, Height = Height },
            CollisionMask = PhysicsLayers.Enemies
        };

        HitWhatsInside();
    }

    // Another machine's view of a sweep the host sent: it travels the same path, never hitting.
    public void InitializeCopy(Vector3 start, Vector3 direction, float speed, float radius, float duration)
    {
        _isCopy = true;
        Place(start, direction, speed, radius, duration);
    }

    public override void _PhysicsProcess(double delta)
    {
        _remaining -= (float)delta;

        if (_remaining <= 0f || !TryStep(_speed * (float)delta))
        {
            QueueFree();
            return;
        }

        if (!_isCopy)
        {
            HitWhatsInside();
        }
    }

    private void Place(Vector3 start, Vector3 direction, float speed, float radius, float duration)
    {
        GlobalPosition = start;
        _direction = direction;
        _speed = speed;
        _remaining = duration;
        Footprint.Scale = new Vector3(radius, 1f, radius);
    }

    // Moves along the ground, or reports that it can't: terrain in the way above step height, or no
    // ground within a step of where it would land.
    private bool TryStep(float distance)
    {
        PhysicsDirectSpaceState3D space = GetWorld3D().DirectSpaceState;
        Vector3 from = GlobalPosition;
        Vector3 to = from + _direction * distance;
        Vector3 wallCheck = Vector3.Up * (MaxStep + 0.1f);

        _ray.From = from + wallCheck;
        _ray.To = to + wallCheck;

        if (space.IntersectRay(_ray).Count > 0)
        {
            return false;
        }

        _ray.From = to + Vector3.Up * MaxStep;
        _ray.To = to + Vector3.Down * MaxStep;
        Godot.Collections.Dictionary ground = space.IntersectRay(_ray);

        if (ground.Count == 0)
        {
            return false;
        }

        GlobalPosition = (Vector3)ground["position"];
        return true;
    }

    private void HitWhatsInside()
    {
        _query.Transform = new Transform3D(Basis.Identity, GlobalPosition + Vector3.Up * Height / 2f);

        foreach (Godot.Collections.Dictionary result in GetWorld3D().DirectSpaceState.IntersectShape(_query, MaxResults))
        {
            // Once per enemy per sweep, however many frames it stays inside or shapes it reports.
            if (result["collider"].As<Node3D>() is not Enemy enemy || enemy.IsDead || !_struck.Add(enemy))
            {
                continue;
            }

            enemy.TakeDamage(_spellHit.CreateHit(_damage, _direction * _ragdollForce));
        }
    }
}
