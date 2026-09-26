using Godot;

// A two-quad world-space health bar. Billboarding is done by the material (BillboardMode), not by
// re-aiming the node every frame: at 50-70 enemies alive, a per-bar _Process calling
// GetViewport().GetCamera3D() plus LookAt is one of the more expensive things in the frame, and the
// GPU does the same job for free.
//
// The fill/empty split is expressed through each QuadMesh's own Size/CenterOffset rather than by
// offsetting the MeshInstance3D nodes: billboarding replaces a node's basis with the camera's, so
// a node-space X offset would slide along world X instead of staying screen-horizontal. Mesh
// writes only happen on an actual health change, never per frame.
public partial class HealthBar3D : Node3D
{
    [Export] public MeshInstance3D Fill { get; private set; }
    [Export] public MeshInstance3D Empty { get; private set; }
    [Export] public float BarWidth = 1f;
    [Export] public Color FillColor = new Color(0.2f, 0.9f, 0.2f, 1f);
    [Export] public Color EmptyColor = new Color(0.15f, 0.05f, 0.05f, 1f);

    private QuadMesh _fillMesh;
    private QuadMesh _emptyMesh;

    public override void _Ready()
    {
        _fillMesh = PrepareMesh(Fill, FillColor);
        _emptyMesh = PrepareMesh(Empty, EmptyColor);

        SetFraction(1f);
    }

    // A fresh mesh (and, via the deep duplicate, a fresh material) per bar: the ones authored in
    // health_bar_3d.tscn are shared by every instance of the scene, so sizing or recoloring them in
    // place would hit every other bar currently on screen.
    private static QuadMesh PrepareMesh(MeshInstance3D meshInstance, Color color)
    {
        QuadMesh mesh = (QuadMesh)meshInstance.Mesh.Duplicate(true);
        meshInstance.Mesh = mesh;

        StandardMaterial3D material = (StandardMaterial3D)mesh.Material;
        material.AlbedoColor = color;
        material.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;

        return mesh;
    }

    public void SetFraction(float fraction)
    {
        fraction = Mathf.Clamp(fraction, 0f, 1f);

        float fillWidth = BarWidth * fraction;
        float emptyWidth = BarWidth - fillWidth;

        // Both quads sit on the node's own origin and are pushed apart inside mesh space, so the
        // pair reads as one continuous bar draining right-to-left from the viewer's perspective.
        _fillMesh.Size = new Vector2(fillWidth, _fillMesh.Size.Y);
        _fillMesh.CenterOffset = new Vector3(-(BarWidth - fillWidth) / 2f, 0f, 0f);
        Fill.Visible = fillWidth > 0f;

        _emptyMesh.Size = new Vector2(emptyWidth, _emptyMesh.Size.Y);
        _emptyMesh.CenterOffset = new Vector3(fillWidth / 2f, 0f, 0f);
        Empty.Visible = emptyWidth > 0f;
    }
}
