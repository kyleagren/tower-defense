using Godot;

// The Health Link beam: thin from the gun to the first spike while the ability is arming, thick and
// green between both spikes once the link forms, thin and red between two spikes that failed to
// link. Endpoints are live nodes re-read every physics frame rather than fixed points, so it tracks
// spiked enemies as they walk, and hides itself once either end is gone. Scales its own node rather
// than the mesh, and colors its own material rather than the scene's - both resources are shared by
// every instance of this scene.
public partial class HealthLinkTether : Node3D
{
    [Export] public MeshInstance3D Beam { get; private set; }
    [Export] public float ArmedRadius = 0.015f;
    [Export] public float LinkedRadius = 0.05f;
    [Export] public float FailedRadius = 0.02f;
    [Export] public Color ArmedColor = new Color(0.85f, 0.2f, 0.35f);
    [Export] public Color LinkedColor = new Color(0.2f, 0.9f, 0.35f);
    [Export] public Color FailedColor = new Color(0.9f, 0.1f, 0.1f);

    private Node3D _from;
    private Node3D _to;
    private float _radius;
    private StandardMaterial3D _material;

    public override void _Ready()
    {
        _material = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded };
        Beam.MaterialOverride = _material;
    }

    public void Arm(Node3D from, Node3D to) => SetEndpoints(from, to, ArmedRadius, ArmedColor);

    public void Link(Node3D from, Node3D to) => SetEndpoints(from, to, LinkedRadius, LinkedColor);

    public void Fail(Node3D from, Node3D to) => SetEndpoints(from, to, FailedRadius, FailedColor);

    private void SetEndpoints(Node3D from, Node3D to, float radius, Color color)
    {
        _from = from;
        _to = to;
        _radius = radius;
        _material.AlbedoColor = color;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsInstanceValid(_from) || !IsInstanceValid(_to))
        {
            Beam.Visible = false;
            return;
        }

        Vector3 from = _from.GlobalPosition;
        Vector3 to = _to.GlobalPosition;
        float length = from.DistanceTo(to);

        if (length < 0.001f)
        {
            Beam.Visible = false;
            return;
        }

        Beam.Visible = true;
        GlobalPosition = (from + to) / 2f;
        LookAt(to, GeometryUtils.SafeUpFor(to - from));
        Scale = new Vector3(_radius, _radius, length);
    }
}
