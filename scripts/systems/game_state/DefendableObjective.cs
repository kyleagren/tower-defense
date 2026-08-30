using Godot;

public partial class DefendableObjective : Area3D, IDamageable
{
    [Export] public float RagdollForce = 200.0f;
    [Export] public float RagdollAngleJitterDegrees = 20.0f;
    [Export] public float MaxHealth = 100f;
    [Export] public float DamagePerHit = 10f;
    [Export] public HealthBar3D HealthBar { get; private set; }

    private float _currentHealth;
    private bool _destroyed;

    public bool IsDestroyed => _destroyed;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        BodyEntered += OnBodyEntered;
        HealthBar.SetFraction(1f);
    }

    public void TakeDamage(DamagePacket damage, Vector3 impulse = default)
    {
        if (_destroyed)
        {
            return;
        }

        _currentHealth -= damage.Total;
        HealthBar.SetFraction(_currentHealth / MaxHealth);

        if (_currentHealth <= 0f)
        {
            _destroyed = true;
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameEnded, false);
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_destroyed || body is not Enemy enemy)
        {
            return;
        }

        Vector3 ragdollDirection = (enemy.GlobalPosition - GlobalPosition).Normalized();

        float jitterRadians = Mathf.DegToRad((float)GD.RandRange(-RagdollAngleJitterDegrees, RagdollAngleJitterDegrees));
        ragdollDirection = ragdollDirection.Rotated(Vector3.Up, jitterRadians);

        enemy.Die(ragdollDirection * RagdollForce);

        TakeDamage(new DamagePacket { PhysicalDamage = DamagePerHit });
    }
}
