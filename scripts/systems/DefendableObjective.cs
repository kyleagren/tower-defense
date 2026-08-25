using Godot;

public partial class DefendableObjective : Area3D
{
    [Export] public float RagdollForce = 200.0f;
    [Export] public float RagdollAngleJitterDegrees = 20.0f;
    [Export] public float MaxHealth = 100f;
    [Export] public float DamagePerHit = 10f;
    [Export] public HealthBar3D HealthBar { get; private set; }

    private float currentHealth;
    private bool destroyed;

    public override void _Ready()
    {
        currentHealth = MaxHealth;
        BodyEntered += OnBodyEntered;
        HealthBar.SetFraction(1f);
    }

    private void OnBodyEntered(Node3D body)
    {
        if (destroyed)
        {
            return;
        }

        if (body is Enemy enemy)
        {
            Vector3 ragdollDirection = (enemy.GlobalPosition - GlobalPosition).Normalized();

            float jitterRadians = Mathf.DegToRad((float)GD.RandRange(-RagdollAngleJitterDegrees, RagdollAngleJitterDegrees));
            ragdollDirection = ragdollDirection.Rotated(Vector3.Up, jitterRadians);

            enemy.Die(ragdollDirection * RagdollForce);

            currentHealth -= DamagePerHit;
            HealthBar.SetFraction(currentHealth / MaxHealth);

            if (currentHealth <= 0f)
            {
                destroyed = true;
                EventBus.Instance.EmitSignal(EventBus.SignalName.GameEnded, false);
            }
        }
    }
}
