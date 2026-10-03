using Godot;

// Repel's field: every pulse, throws whatever is inside outward along one of an octagon's side
// normals rather than straight away from the center, so enemies scatter along a few readable lines.
// The octagon lines up with the caster's facing, and the four sides on those axes are wider than the
// diagonals, so most enemies fly forward, back, left or right.
public partial class RepelField : EffectVolume
{
    [Export] public float AxisWedgeDegrees { get; private set; } = 60f;
    // Bursts of motes thrown out from the core to the rim, one per pulse.
    [Export] public GpuParticles3D Motes { get; private set; }

    private Vector3 _forward = Vector3.Forward;
    private float _impulse;

    public void Configure(Vector3 forward, float impulse)
    {
        _forward = new Vector3(forward.X, 0f, forward.Z).Normalized();
        _impulse = impulse;
    }

    public override void Initialize(Vector3 position, float radius, float duration)
    {
        base.Initialize(position, radius, duration);
        SizeMotes(radius);
    }

    // Each burst reaches the rim as its lifetime ends. A fresh copy of the scene's material, since that
    // one is shared by every field.
    private void SizeMotes(float radius)
    {
        float speed = radius / (float)Motes.Lifetime;

        ParticleProcessMaterial material = (ParticleProcessMaterial)Motes.ProcessMaterial.Duplicate();
        material.InitialVelocityMin = speed * 0.8f;
        material.InitialVelocityMax = speed;

        Motes.ProcessMaterial = material;
        Motes.VisibilityAabb = new Aabb(new Vector3(-radius, -1f, -radius), new Vector3(radius * 2f, 4f, radius * 2f));
    }

    protected override void OnTick(Enemy enemy)
    {
        Vector3 outward = enemy.GlobalPosition - GlobalPosition;
        outward.Y = 0f;
        enemy.ApplyKnockback(QuantizedPushDirection(outward) * _impulse);
    }

    // Snaps to the nearest axis inside its wedge, otherwise to the diagonal between the two nearest
    // axes. An enemy dead on the center has no outward direction and goes forward.
    private Vector3 QuantizedPushDirection(Vector3 outward)
    {
        if (outward == Vector3.Zero)
        {
            return _forward;
        }

        float angle = Mathf.RadToDeg(_forward.SignedAngleTo(outward, Vector3.Up));
        float nearestAxis = Mathf.Round(angle / 90f) * 90f;
        float fromAxis = angle - nearestAxis;

        float snapped = Mathf.Abs(fromAxis) <= AxisWedgeDegrees / 2f
            ? nearestAxis
            : nearestAxis + Mathf.Sign(fromAxis) * 45f;

        return _forward.Rotated(Vector3.Up, Mathf.DegToRad(snapped));
    }
}
