using Godot;

// Retaliation's charge-up, on the ground for the whole window: a circle that starts faint and red and
// grows more solid and orange as the blast builds. A placeholder for the molten fissures it stands in
// for.
public partial class RetaliationChargeCircle : MeshInstance3D
{
    [Export] public Color StartColor { get; private set; } = new Color(0.9f, 0.2f, 0.15f, 0.15f);
    [Export] public Color EndColor { get; private set; } = new Color(1f, 0.55f, 0.1f, 0.65f);

    // Just above the ground, so the circle doesn't z-fight the floor it lies on.
    private const float GroundOffset = 0.05f;

    private StandardMaterial3D _material;

    // Must already be in the tree - it positions itself globally.
    public void Begin(Vector3 origin, float radius)
    {
        GlobalPosition = origin + Vector3.Up * GroundOffset;
        Mesh = ShieldSlamWave.BuildFan(Vector3.Forward, radius, Mathf.Tau);
        CastShadow = ShadowCastingSetting.Off;

        _material = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled
        };
        MaterialOverride = _material;
        SetProgress(0f);
    }

    // 0 at the start of the window, 1 as the blast releases.
    public void SetProgress(float progress)
    {
        _material.AlbedoColor = StartColor.Lerp(EndColor, progress);
    }
}
