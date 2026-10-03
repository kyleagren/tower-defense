using Godot;
using System.Collections.Generic;

// One Status-colored wisp drawn out of a harvested enemy, arcing into the Soldier who took it. Purely
// visual - Harvest has already taken the Status by the time it flies.
public partial class HarvestWisp : Node3D
{
    [Export] public MeshInstance3D Head { get; private set; }
    [Export] public GpuParticles3D Trail { get; private set; }
    [Export] public float MinFlightTime { get; private set; } = 0.5f;
    [Export] public float MaxFlightTime { get; private set; } = 1f;
    // Added per meter before the clamp, so a far wisp takes a little longer.
    [Export] public float FlightTimePerMeter { get; private set; } = 0.04f;
    // How far the arc bows off a straight line, as a share of the distance.
    [Export] public float ArcBow { get; private set; } = 0.35f;

    // One per element, shared by every wisp of that color.
    private static readonly Dictionary<Tag, ParticleProcessMaterial> _trailMaterials = new();

    private Sentinel _target;
    private Vector3 _start;
    private Vector3 _bow;
    private Vector3 _targetPoint;
    private float _flightTime;
    private float _elapsed;
    private bool _arrived;

    public void Launch(Vector3 from, Sentinel target, Tag element)
    {
        GlobalPosition = from;
        _start = from;
        _target = target;
        _targetPoint = target.AimPoint;

        float distance = from.DistanceTo(_targetPoint);
        _flightTime = Mathf.Clamp(MinFlightTime + distance * FlightTimePerMeter, MinFlightTime, MaxFlightTime);

        // A random sideways and upward bow, so a crowd's wisps fan out instead of flying one line.
        Vector3 side = (_targetPoint - from).Cross(Vector3.Up);
        side = side.LengthSquared() > 0.0001f ? side.Normalized() : Vector3.Right;
        Vector3 bowDirection = side * (float)GD.RandRange(-1.0, 1.0) + Vector3.Up * (float)GD.RandRange(0.3, 1.0);
        _bow = bowDirection.Normalized() * distance * ArcBow;

        Color color = DamageTypeColors.Get(element);
        Head.SetInstanceShaderParameter("wisp_color", color);
        Trail.ProcessMaterial = TrailMaterial(element, color);
        Trail.Emitting = true;
    }

    // Follows the Soldier as he moves, easing in so it leaves the enemy slowly and snaps home.
    public override void _Process(double delta)
    {
        if (_arrived)
        {
            return;
        }

        if (_target != null && IsInstanceValid(_target))
        {
            _targetPoint = _target.AimPoint;
        }

        _elapsed += (float)delta;
        float t = Mathf.Min(_elapsed / _flightTime, 1f);
        Vector3 control = (_start + _targetPoint) / 2f + _bow;
        GlobalPosition = QuadraticBezier(_start, control, _targetPoint, t * t);

        if (t >= 1f)
        {
            Arrive();
        }
    }

    // The head goes at once; the trail it left fades out before the node goes.
    private void Arrive()
    {
        _arrived = true;
        Head.Visible = false;
        Trail.Emitting = false;

        Tween tween = CreateTween();
        tween.TweenInterval(Trail.Lifetime);
        tween.TweenCallback(Callable.From(QueueFree));
    }

    private ParticleProcessMaterial TrailMaterial(Tag element, Color color)
    {
        if (!_trailMaterials.TryGetValue(element, out ParticleProcessMaterial material))
        {
            material = (ParticleProcessMaterial)Trail.ProcessMaterial.Duplicate();
            material.Color = color;
            _trailMaterials[element] = material;
        }

        return material;
    }

    private static Vector3 QuadraticBezier(Vector3 from, Vector3 control, Vector3 to, float t)
    {
        float u = 1f - t;
        return u * u * from + 2f * u * t * control + t * t * to;
    }
}
