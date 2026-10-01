using Godot;
using System.Collections.Generic;

// What a Resonant Ritual listens to, drawn over the ground: its resonance radius as an outline ring
// when it's smaller than the range circle (Far Resonance), and a light line to every placed Ritual
// whose casts would pulse it. Placement redraws it each frame as the ghost moves; a placed Ritual
// draws it once, while Alt shows its range.
public partial class ResonanceOverlay : MeshInstance3D
{
    private const int RingSegments = 64;
    // Just above the range fill, so the ring reads on top of it.
    private const float RingHeight = 0.06f;
    // Lines run between Rituals at about core height, clear of the ground fill.
    private const float LineHeight = 1f;

    private static readonly StandardMaterial3D _material = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        VertexColorUseAsAlbedo = true
    };

    private static readonly Color _ringColor = new Color(0.8f, 0.7f, 1f, 0.8f);
    private static readonly Color _lineColor = new Color(0.8f, 0.7f, 1f, 0.35f);

    private readonly ImmediateMesh _mesh = new();

    public override void _Ready()
    {
        Mesh = _mesh;
        MaterialOverride = _material;
        CastShadow = ShadowCastingSetting.Off;
    }

    // self is the Ritual this belongs to, so it isn't linked to itself - null for a placement ghost,
    // which never registers as a Ritual.
    public void Draw(float resonanceRadius, float range, Ritual self)
    {
        _mesh.ClearSurfaces();

        bool showRing = resonanceRadius < range - 0.01f;
        List<Vector3> linked = new();

        foreach (Ritual ritual in LevelContext.Current.Rituals.Active)
        {
            if (ritual != self && ritual.GlobalPosition.DistanceTo(GlobalPosition) <= resonanceRadius)
            {
                linked.Add(ToLocal(ritual.GlobalPosition));
            }
        }

        // An ImmediateMesh surface can't be empty.
        if (!showRing && linked.Count == 0)
        {
            return;
        }

        _mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);

        if (showRing)
        {
            _mesh.SurfaceSetColor(_ringColor);

            for (int i = 0; i < RingSegments; i++)
            {
                _mesh.SurfaceAddVertex(RingPoint(i, resonanceRadius));
                _mesh.SurfaceAddVertex(RingPoint(i + 1, resonanceRadius));
            }
        }

        _mesh.SurfaceSetColor(_lineColor);

        foreach (Vector3 point in linked)
        {
            _mesh.SurfaceAddVertex(Vector3.Up * LineHeight);
            _mesh.SurfaceAddVertex(point + Vector3.Up * LineHeight);
        }

        _mesh.SurfaceEnd();
    }

    private static Vector3 RingPoint(int i, float radius)
    {
        float angle = Mathf.Tau * i / RingSegments;
        return new Vector3(Mathf.Cos(angle) * radius, RingHeight, Mathf.Sin(angle) * radius);
    }
}
