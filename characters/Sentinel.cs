using Godot;

public abstract partial class Sentinel : PlayerCharacter, IDamageable
{
    // The Sentinel currently in combat, if any - null during Build phase since nothing attacks
    // the Architect. Enemies deal damage through this, not PlayerCharacter.ActiveCombatant, since
    // only a Sentinel has a health value to damage.
    public static Sentinel Active { get; private set; }

    // The local player's Sentinel, set once at spawn regardless of whether it's the active
    // combatant - unlike Active, this stays valid during Build phase too, so anything that needs
    // to read a Sentinel's health before its first Combat phase (PlayerHealthOrb's initial value)
    // has somewhere to look it up instead of only ever reacting to PlayerHealthChanged.
    public static Sentinel Local { get; private set; }

    public abstract CharacterId Id { get; }

    [Export] public float BaseMaxHealth = 100f;

    public bool IsDead => _currentHealth <= 0f;
    public bool IsDestroyed => IsDead;
    public float CurrentHealth => _currentHealth;

    public float MaxHealth => BaseMaxHealth * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMaxHealth));
    public override float Speed => BaseSpeed * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMoveSpeed));

    private float _currentHealth;

    public override void _Ready()
    {
        base._Ready();
        _currentHealth = MaxHealth;
        Local = this;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
    }

    protected override void SetActive(bool value)
    {
        base.SetActive(value);
        Active = value ? this : null;
    }

    public void TakeDamage(DamagePacket damage, Vector3 impulse = default)
    {
        if (IsDead)
        {
            return;
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - damage.Total);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);

        if (IsDead)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameEnded, false);
        }
    }

    // Full reset is a placeholder — a real healing mechanic (README §15) will replace this.
    protected void HealToFull()
    {
        _currentHealth = MaxHealth;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
    }
}
