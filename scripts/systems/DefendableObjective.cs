using Godot;

public partial class DefendableObjective : Area3D
{
    [Export] public float KnockbackForce = 200.0f;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Enemy enemy)
        {
            Vector3 knockbackDirection = (enemy.GlobalPosition - GlobalPosition).Normalized();
            enemy.Die(knockbackDirection * KnockbackForce);
        }
    }
}
