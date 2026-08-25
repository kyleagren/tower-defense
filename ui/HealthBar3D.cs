using Godot;

public partial class HealthBar3D : Node3D
{
    [Export] public MeshInstance3D Fill { get; private set; }
    [Export] public MeshInstance3D Empty { get; private set; }
    [Export] public float BarWidth = 1f;

    public void SetFraction(float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);

        float fillWidth = BarWidth * fraction;
        float emptyWidth = BarWidth - fillWidth;

        QuadMesh fillMesh = (QuadMesh)Fill.Mesh;
        fillMesh.Size = new Vector2(fillWidth, fillMesh.Size.Y);
        Fill.Position = new Vector3(-BarWidth / 2f + fillWidth / 2f, Fill.Position.Y, Fill.Position.Z);
        Fill.Visible = fillWidth > 0f;

        QuadMesh emptyMesh = (QuadMesh)Empty.Mesh;
        emptyMesh.Size = new Vector2(emptyWidth, emptyMesh.Size.Y);
        Empty.Position = new Vector3(BarWidth / 2f - emptyWidth / 2f, Empty.Position.Y, Empty.Position.Z);
        Empty.Visible = emptyWidth > 0f;
    }
}
