using Godot;

public partial class DefendableObjective : Area3D, IDamageable
{
    [Export] public float MaxHealth = 100f;
    [Export] public float ApproachRadius = 1.1f; // matches the solid collision shape's own radius — enemies physically can't get closer than this
    [Export] public HealthBar3D HealthBar { get; private set; }

    private float _currentHealth;
    private bool _destroyed;

    public bool IsDestroyed => _destroyed;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
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
}
