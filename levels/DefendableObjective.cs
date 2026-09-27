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

    // Only the host's objective takes damage; every other copy shows the health it sends.
    public void TakeDamage(Hit hit)
    {
        if (_destroyed || !IsMultiplayerAuthority())
        {
            return;
        }

        SetHealth(_currentHealth - hit.Damage.Total);

        if (_currentHealth <= 0f)
        {
            _destroyed = true;
            LevelContext.Current.Match.End(victory: false);
        }
    }

    private void SetHealth(float health)
    {
        ShowHealth(health);
        Rpc(MethodName.ReceiveHealth, health);
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHealth(float health)
    {
        ShowHealth(health);
    }

    private void ShowHealth(float health)
    {
        _currentHealth = health;
        HealthBar.SetFraction(Mathf.Max(health, 0f) / MaxHealth);
    }
}
