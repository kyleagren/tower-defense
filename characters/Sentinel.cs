using Godot;
using System.Collections.Generic;

public abstract partial class Sentinel : PlayerCharacter, IDamageable
{
    // Every spawned Sentinel, keyed by which player controls it - SentinelSpawner sets OwnerId
    // before adding it to the tree, so this is always correct by the time _Ready registers it.
    // Local is a convenience for "the local player's own Sentinel," built on top of this rather
    // than tracked separately, so there's one source of truth instead of two that could disagree.
    public static Dictionary<PlayerId, Sentinel> ByOwner { get; } = new();
    public static Sentinel Local => ByOwner.GetValueOrDefault(PlayerSentinels.Local);

    [Export] public PlayerId OwnerId { get; private set; } = PlayerId.P1;
    [Export] public Node3D Weapon { get; private set; }
    [Export] public float BaseMaxHealth = 100f;

    public abstract CharacterId Id { get; }

    public bool IsDead => _currentHealth <= 0f;
    public bool IsDestroyed => IsDead;
    public float CurrentHealth => _currentHealth;

    public float MaxHealth => BaseMaxHealth * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMaxHealth));
    public override float Speed => BaseSpeed * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMoveSpeed));

    private float _currentHealth;

    public void SetOwnerId(PlayerId owner) => OwnerId = owner;

    public override void _Ready()
    {
        base._Ready();
        _currentHealth = MaxHealth;
        ByOwner[OwnerId] = this;
        SetActive(false);
        Weapon.Visible = false;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsActive)
        {
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (@event.IsActionPressed("ui_cancel"))
        {
            ToggleMouseCapture();
        }
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        bool shouldBeActive = phase == GamePhase.Combat;

        if (shouldBeActive)
        {
            HandoffFrom(Architect.ByOwner[OwnerId]);
        }
        else
        {
            HealToFull();
        }

        SetActive(shouldBeActive);
        Weapon.Visible = shouldBeActive;
    }

    public virtual void TakeDamage(DamagePacket damage, Vector3 impulse = default)
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
