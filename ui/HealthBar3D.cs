using Godot;

public partial class HealthBar3D : Node3D
{
    [Export] public MeshInstance3D Fill { get; private set; }
    [Export] public float BarWidth = 1f;

    public void SetFraction(float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);

        QuadMesh fillMesh = (QuadMesh)Fill.Mesh;
        fillMesh.Size = new Vector2(BarWidth * fraction, fillMesh.Size.Y);
        Fill.Position = new Vector3(-BarWidth / 2f + fillMesh.Size.X / 2f, Fill.Position.Y, Fill.Position.Z);
    }
}
