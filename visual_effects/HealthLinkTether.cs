using Godot;

// The Health Link beam: thin from the gun to the first spike while the ability is arming, thicker
// between both spikes once the link forms. Endpoints are live nodes re-read every physics frame
// rather than fixed points, so it tracks spiked enemies as they walk. Scales its own node rather
// than the mesh - the mesh resource is shared by every instance of this scene.
public partial class HealthLinkTether : Node3D
{
    [Export] public MeshInstance3D Beam { get; private set; }
    [Export] public float ArmedRadius = 0.015f;
    [Export] public float LinkedRadius = 0.05f;

    private Node3D _from;
    private Node3D _to;
    private float _radius;

    public void Arm(Node3D from, Node3D to) => SetEndpoints(from, to, ArmedRadius);

    public void Link(Node3D from, Node3D to) => SetEndpoints(from, to, LinkedRadius);

    private void SetEndpoints(Node3D from, Node3D to, float radius)
    {
        _from = from;
        _to = to;
        _radius = radius;
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
