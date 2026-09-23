using Godot;

public partial class DefendableObjective : Area3D, IDamageable
{
    [Export] public float MaxHealth = 100f;
    [Export] public float ApproachRadius = 1.1f; // matches the solid collision shape's own radius — enemies physically can't get closer than this
    [Export] public float AimPointHeight = 1f;
    [Export] public HealthBar3D HealthBar { get; private set; }

    // The mage's own shell - where PostGame loot sprays from. Energy Harvesters, which work
    // identically minus the mage inside, leave this off.
    [Export] public bool IsPrimary { get; private set; } = true;

    private float _currentHealth;
    private bool _destroyed;

    public bool IsDestroyed => _destroyed;
    public Vector3 AimPoint => GlobalPosition + Vector3.Up * AimPointHeight;

    public override void _Ready()
    {
        _currentHealth = MaxHealth;
        HealthBar.SetFraction(1f);

        if (IsPrimary)
        {
            LevelContext.Current.PrimaryObjective = this;
        }
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
            LevelContext.Current.Match.End(victory: false);
        }
    }
}
