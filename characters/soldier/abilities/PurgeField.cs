using Godot;

// Purge's field: every pulse, throws whatever is inside outward along one of an octagon's side
// normals rather than straight away from the center, so enemies scatter along a few readable lines.
// The octagon lines up with the caster's facing, and the four sides on those axes are wider than the
// diagonals, so most enemies fly forward, back, left or right.
public partial class PurgeField : EffectVolume
{
    [Export] public float AxisWedgeDegrees { get; private set; } = 60f;

    private Vector3 _forward = Vector3.Forward;
    private float _impulse;

    public void Configure(Vector3 forward, float impulse)
    {
        _forward = new Vector3(forward.X, 0f, forward.Z).Normalized();
        _impulse = impulse;
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
