using Godot;
using System.Collections.Generic;

// A straight beam that hits everything along it on a few quick ticks, then fades. Hits are a shape
// query at each tick rather than an Area3D, so the first lands the moment the beam appears instead
// of a physics step later, once overlaps have registered.
public partial class Beam : Node3D
{
    // A unit cylinder running from the origin down -Z, scaled to the beam's half-width and length.
    [Export] public Node3D Footprint { get; private set; }
    // How long the beam stays visible after its last tick.
    [Export] public float Linger { get; private set; } = 0.15f;
    // Optional: a mesh whose shader takes the beam's age as "progress", 0 as it fires to 1 as it goes.
    [Export] public GeometryInstance3D FadeMesh { get; private set; }

    public float Duration { get; private set; }
    public float Length { get; private set; }

    // Generous: a wide beam down a packed lane can hold many enemies, and each can report more than
    // one shape.
    private const int MaxResults = 64;

    private PhysicsShapeQueryParameters3D _query;
    private DamagePacket _damage;
    private SpellHit _spellHit;
    private float _ragdollForce;
    private float _pushImpulse;
    private int _ticksLeft;
    private float _tickInterval;
    private float _nextTick;
    private float _elapsed;
    private bool _isCopy;

    // Called from the Ritual's physics step, so the first tick's space query is safe here.
    public void Initialize(Vector3 origin, Vector3 direction, float length, float halfWidth, BeamCastData data, DamagePacket damage, float ragdollForce, SpellHit spellHit)
    {
        Place(origin, direction, length, halfWidth, (data.TickCount - 1) * data.TickInterval + Linger);

        _damage = damage;
        _spellHit = spellHit;
        _ragdollForce = ragdollForce;
        _pushImpulse = data.PushImpulse;
        _tickInterval = data.TickInterval;
        _ticksLeft = data.TickCount;

        // The cylinder's axis is its local Y, turned onto the beam's -Z and centered halfway out.
        _query = new PhysicsShapeQueryParameters3D
        {
            Shape = new CylinderShape3D { Radius = halfWidth, Height = length },
            Transform = new Transform3D(GlobalBasis * new Basis(Vector3.Right, -Mathf.Pi / 2f), origin + direction * length / 2f),
            CollisionMask = PhysicsLayers.Enemies
        };

        Tick();
    }

    // Another machine's view of a beam the host fired: shown for its duration, never hitting.
    public void InitializeCopy(Vector3 origin, Vector3 direction, float length, float halfWidth, float duration)
    {
        _isCopy = true;
        Place(origin, direction, length, halfWidth, duration);
    }

    public override void _PhysicsProcess(double delta)
    {
        _elapsed += (float)delta;

        if (!_isCopy && _ticksLeft > 0 && _elapsed >= _nextTick)
        {
            Tick();
        }

        FadeMesh?.SetInstanceShaderParameter("progress", Mathf.Min(_elapsed / Duration, 1f));

        if (_elapsed >= Duration)
        {
            QueueFree();
        }
    }

    // The Ritual never aims straight up or down, so LookingAt always has a valid up.
    private void Place(Vector3 origin, Vector3 direction, float length, float halfWidth, float duration)
    {
        GlobalPosition = origin;
        GlobalBasis = Basis.LookingAt(direction, Vector3.Up);
        Footprint.Scale = new Vector3(halfWidth, halfWidth, length);
        Length = length;
        Duration = duration;
    }

    private void Tick()
    {
        _ticksLeft--;
        _nextTick += _tickInterval;

        Vector3 forward = -GlobalBasis.Z;
        Vector3 push = new Vector3(forward.X, 0f, forward.Z).Normalized() * _pushImpulse;
        HashSet<Enemy> struck = new HashSet<Enemy>();

        foreach (Godot.Collections.Dictionary result in GetWorld3D().DirectSpaceState.IntersectShape(_query, MaxResults))
        {
            // An enemy with a head hitbox reports once per shape.
            if (result["collider"].As<Node3D>() is not Enemy enemy || enemy.IsDead || !struck.Add(enemy))
            {
                continue;
            }

            enemy.TakeDamage(_spellHit.CreateHit(_damage, forward * _ragdollForce));

            if (_pushImpulse > 0f && !enemy.IsDead)
            {
                enemy.ApplyKnockback(push);
            }
        }
    }
}
