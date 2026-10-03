using Godot;
using System.Collections.Generic;

// One Status-colored wisp drawn out of a harvested enemy, arcing into the Soldier who took it. Purely
// visual - Harvest has already taken the Status by the time it flies.
public partial class HarvestWisp : Node3D
{
    [Export] public MeshInstance3D Head { get; private set; }
    // Top-level, so the ribbon's vertices are written straight in world space.
    [Export] public MeshInstance3D Trail { get; private set; }
    [Export] public float MinFlightTime { get; private set; } = 0.5f;
    [Export] public float MaxFlightTime { get; private set; } = 1f;
    // Added per meter before the clamp, so a far wisp takes a little longer.
    [Export] public float FlightTimePerMeter { get; private set; } = 0.04f;
    // How far the arc bows off a straight line, as a share of the distance.
    [Export] public float ArcBow { get; private set; } = 0.35f;
    // How long a point the head passed stays on the trail.
    [Export] public float TrailLifetime { get; private set; } = 0.3f;
    // Half the ribbon's width at the head; it tapers to nothing at the tail.
    [Export] public float TrailHalfWidth { get; private set; } = 0.11f;

    private readonly List<(Vector3 Position, float Time)> _trailPoints = new();
    private readonly ImmediateMesh _trailMesh = new();

    private Sentinel _target;
    private Vector3 _start;
    private Vector3 _bow;
    private Vector3 _targetPoint;
    private Color _color;
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

        _color = DamageTypeColors.Get(element);
        Head.SetInstanceShaderParameter("wisp_color", _color);

        Trail.TopLevel = true;
        Trail.GlobalTransform = Transform3D.Identity;
        Trail.Mesh = _trailMesh;
        _trailPoints.Add((from, 0f));
    }

    public override void _Process(double delta)
    {
        _elapsed += (float)delta;

        if (!_arrived)
        {
            Fly();
        }

        // Points age out of the tail; once the head is home and the last one has gone, so has the wisp.
        while (_trailPoints.Count > 0 && _elapsed - _trailPoints[0].Time > TrailLifetime)
        {
            _trailPoints.RemoveAt(0);
        }

        if (_arrived && _trailPoints.Count == 0)
        {
            QueueFree();
            return;
        }

        BuildTrail();
    }

    // Follows the Soldier as he moves, easing in so it leaves the enemy slowly and snaps home.
    private void Fly()
    {
        if (_target != null && IsInstanceValid(_target))
        {
            _targetPoint = _target.AimPoint;
        }

        float t = Mathf.Min(_elapsed / _flightTime, 1f);
        Vector3 control = (_start + _targetPoint) / 2f + _bow;
        GlobalPosition = QuadraticBezier(_start, control, _targetPoint, t * t);
        _trailPoints.Add((GlobalPosition, _elapsed));

        if (t >= 1f)
        {
            _arrived = true;
            Head.Visible = false;
        }
    }

    // A strip through every point the head passed, turned to face the camera and joined end to end, so
    // it stays one unbroken ribbon however far the head moved in a frame. Narrows and fades with age.
    private void BuildTrail()
    {
        _trailMesh.ClearSurfaces();
        Camera3D camera = GetViewport().GetCamera3D();

        if (_trailPoints.Count < 2 || camera == null)
        {
            return;
        }

        Vector3 cameraPosition = camera.GlobalPosition;
        _trailMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);

        for (int i = 0; i < _trailPoints.Count; i++)
        {
            Vector3 point = _trailPoints[i].Position;
            Vector3 along = _trailPoints[Mathf.Min(i + 1, _trailPoints.Count - 1)].Position - _trailPoints[Mathf.Max(i - 1, 0)].Position;
            Vector3 side = along.Cross(cameraPosition - point);
            side = side.LengthSquared() > 0.000001f ? side.Normalized() : Vector3.Up;

            float life = 1f - Mathf.Clamp((_elapsed - _trailPoints[i].Time) / TrailLifetime, 0f, 1f);
            Color color = _color with { A = life };

            _trailMesh.SurfaceSetColor(color);
            _trailMesh.SurfaceAddVertex(point + side * TrailHalfWidth * life);
            _trailMesh.SurfaceSetColor(color);
            _trailMesh.SurfaceAddVertex(point - side * TrailHalfWidth * life);
        }

        _trailMesh.SurfaceEnd();
    }

    private static Vector3 QuadraticBezier(Vector3 from, Vector3 control, Vector3 to, float t)
    {
        float u = 1f - t;
        return u * u * from + 2f * u * t * control + t * t * to;
    }
}
