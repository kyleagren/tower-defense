using Godot;

// Vent's look: heat haze and steam rising off the Tank, over a faint disc on the ground with a crisp
// ring at its edge, so it still reads apart from Retaliation's circle when the two overlap.
public partial class VentAura : MeshInstance3D
{
    [Export] public Color FillColor { get; private set; } = new Color(1f, 0.65f, 0.2f, 0.12f);
    [Export] public Color RingColor { get; private set; } = new Color(1f, 0.75f, 0.3f, 0.85f);
    [Export] public float RingWidth { get; private set; } = 0.12f;

    // A touch above Retaliation's circle, so the two don't z-fight when both are down.
    private const float GroundOffset = 0.07f;

    // Must be parented to the Tank - it positions itself locally and rides along with him.
    public void Begin(float radius)
    {
        // Its own process mode rather than the Tank's: on another player's screen the Tank is a puppet
        // that doesn't process, and the steam still has to run.
        ProcessMode = ProcessModeEnum.Pausable;

        Position = Vector3.Up * GroundOffset;
        CastShadow = ShadowCastingSetting.Off;

        float inner = Mathf.Max(radius - RingWidth, 0f);
        ArrayMesh mesh = GroundMesh.Disc(inner);
        GroundMesh.Ring(inner, radius, mesh);
        Mesh = mesh;

        SetSurfaceOverrideMaterial(0, GroundMesh.Material(FillColor));
        SetSurfaceOverrideMaterial(1, GroundMesh.Material(RingColor));
    }
}
