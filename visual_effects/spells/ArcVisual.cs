using Godot;

public partial class ArcVisual : Node3D
{
    [Export] public float Lifetime = 0.15f;
    [Export] public int SegmentCount = 5;
    [Export] public float JitterAmount = 0.3f;
    [Export] public Mesh SegmentMesh;
    [Export] public Material SegmentMaterial;

    private static readonly Transform3D _meshCorrection = new Transform3D(
        new Vector3(1, 0, 0),
        new Vector3(0, 0, -1),
        new Vector3(0, 1, 0),
        Vector3.Zero
    );

    public void Show(Vector3 from, Vector3 to)
    {
        Vector3 direction = to - from;
        float totalLength = direction.Length();

        if (totalLength < 0.001f)
        {
            GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
            return;
        }

        Vector3 perpendicular = direction.Cross(Vector3.Up).Normalized();
        if (perpendicular.LengthSquared() < 0.001f)
        {
            perpendicular = direction.Cross(Vector3.Right).Normalized();
        }

        Vector3 previousPoint = from;

        for (int i = 1; i <= SegmentCount; i++)
        {
            Vector3 point = i == SegmentCount
                ? to
                : from.Lerp(to, (float)i / SegmentCount) + perpendicular * (float)GD.RandRange(-JitterAmount, JitterAmount);

            SpawnSegment(previousPoint, point);
            previousPoint = point;
        }

        GetTree().CreateTimer(Lifetime).Timeout += QueueFree;
    }

    private void SpawnSegment(Vector3 from, Vector3 to)
    {
        Node3D wrapper = new Node3D();
        AddChild(wrapper);

        MeshInstance3D meshInstance = new MeshInstance3D
        {
            Mesh = SegmentMesh,
            MaterialOverride = SegmentMaterial,
            Transform = _meshCorrection
        };
        wrapper.AddChild(meshInstance);

        wrapper.GlobalPosition = (from + to) / 2f;
        wrapper.LookAt(to, Vector3.Up);
        wrapper.Scale = new Vector3(wrapper.Scale.X, wrapper.Scale.Y, from.DistanceTo(to));
    }
}
