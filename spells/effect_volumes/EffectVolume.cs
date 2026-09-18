using Godot;
using System.Collections.Generic;

// A volume that affects whatever is inside it, on an interval, for a duration, then frees itself.
// Subclasses decide what "affects" means; this owns only occupancy, timing and cleanup.
//
// An Area3D rather than a per-tick spatial scan: the broadphase reports entry and exit instead of
// sampling, so an enemy clipping the edge between ticks can't be missed, and a large enemy brushing
// the boundary counts by its actual shape rather than by its origin.
public abstract partial class EffectVolume : Area3D
{
    [Export] public CollisionShape3D BodyCollision { get; private set; }
    // A Node3D rather than a MeshInstance3D: it's only ever scaled, so a volume whose footprint is
    // several meshes can hand over the parent that groups them.
    [Export] public Node3D Footprint { get; private set; }
    [Export] public float TickInterval { get; private set; } = 0.25f;
    [Export] public float Height { get; private set; } = 4f;

    // Whether stepping inside takes effect at once or waits for the next tick. Terrain applies
    // immediately; a bombardment doesn't, since walking into a storm shouldn't hurt more than
    // standing in one already does.
    protected virtual bool AffectsOnEntry => false;

    // Absorbs float error in duration / TickInterval, so a duration that is an exact multiple of the
    // interval (0.6 / 0.2) doesn't floor to one tick short.
    private const float TickCountEpsilon = 0.0001f;

    private readonly List<Enemy> _occupants = new List<Enemy>();
    private float _duration;
    private float _elapsed;
    private int _tickCount;
    private int _ticksFired;

    public override void _Ready()
    {
        CollisionLayer = 0;
        CollisionMask = PhysicsLayers.Enemies;
        BodyEntered += OnBodyEntered;
        BodyExited += OnBodyExited;
    }

    public virtual void Initialize(Vector3 position, float radius, float duration)
    {
        GlobalPosition = position;
        _duration = duration;

        // One tick per TickInterval mark that falls inside the duration; a partial interval at the
        // end doesn't earn a tick.
        _tickCount = Mathf.FloorToInt(duration / TickInterval + TickCountEpsilon);

        // A fresh shape rather than the scene's own: a sub-resource authored in a .tscn is shared by
        // every instance of that scene, so sizing that copy would resize every other volume too.
        BodyCollision.Shape = new CylinderShape3D { Radius = radius, Height = Height };

        // Only the footprint scales, never the root - scaling the root would stretch every child
        // visual along with it.
        Footprint.Scale = new Vector3(radius, 1f, radius);
    }

    public override void _PhysicsProcess(double delta)
    {
        _elapsed += (float)delta;

        if (_ticksFired < _tickCount && _elapsed >= (_ticksFired + 1) * TickInterval)
        {
            _ticksFired++;
            TickOccupants();
        }

        // Waits on every tick having fired as well as on the clock, so a final tick due on the same
        // frame the duration runs out still lands.
        if (_ticksFired >= _tickCount && _elapsed >= _duration)
        {
            QueueFree();
        }
    }

    private void TickOccupants()
    {
        // Backwards, since dead occupants are dropped mid-loop. A dying enemy disables its collision
        // rather than leaving the tree, so an exit signal isn't guaranteed to arrive for it.
        for (int i = _occupants.Count - 1; i >= 0; i--)
        {
            Enemy occupant = _occupants[i];

            if (!IsInstanceValid(occupant) || occupant.IsDead)
            {
                _occupants.RemoveAt(i);
                continue;
            }

            OnTick(occupant);
        }
    }

    protected abstract void OnTick(Enemy enemy);

    private void OnBodyEntered(Node3D body)
    {
        if (body is not Enemy enemy || _occupants.Contains(enemy))
        {
            return;
        }

        _occupants.Add(enemy);

        if (AffectsOnEntry)
        {
            OnTick(enemy);
        }
    }

    private void OnBodyExited(Node3D body)
    {
        if (body is Enemy enemy)
        {
            _occupants.Remove(enemy);
        }
    }
}
