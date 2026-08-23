using Godot;

public partial class DefendableObjective : Area3D
{
    [Export] public float KnockbackForce = 200.0f;
    [Export] public float KnockbackAngleJitterDegrees = 5.0f;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Enemy enemy)
        {
            Vector3 knockbackDirection = (enemy.GlobalPosition - GlobalPosition).Normalized();

            float jitterRadians = Mathf.DegToRad((float)GD.RandRange(-KnockbackAngleJitterDegrees, KnockbackAngleJitterDegrees));
            knockbackDirection = knockbackDirection.Rotated(Vector3.Up, jitterRadians);

            enemy.Die(knockbackDirection * KnockbackForce);
        }
    }
}
