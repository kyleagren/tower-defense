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

    [Export] public Node3D Weapon { get; private set; }
    [Export] public float BaseMaxHealth = 100f;
    [Export] public float AimPointHeight = 1.2f;
    [Export] public RitualPlacer RitualTokenPlacer { get; private set; }
    [Export] public TokenDeployer TokenDeployer { get; private set; }

    public abstract CharacterId Id { get; }

    public bool IsDead => _currentHealth <= 0f;
    public bool IsDestroyed => IsDead;
    public Vector3 AimPoint => GlobalPosition + Vector3.Up * AimPointHeight;
    public float CurrentHealth => _currentHealth;
    public float CurrentShield => _currentShield;

    public float MaxHealth => BaseMaxHealth * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMaxHealth));
    public override float Speed => BaseSpeed * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelMoveSpeed));

    public float MaxShield
    {
        get
        {
            CharacterSheet sheet = CharacterSheetCache.GetSheet(Id);
            return sheet.GetLocalTotal(StatId.SentinelShield) * (1f + sheet.GetBonus(StatId.SentinelTotalShield));
        }
    }

    public float TotalArmor
    {
        get
        {
            CharacterSheet sheet = CharacterSheetCache.GetSheet(Id);
            return sheet.GetLocalTotal(StatId.SentinelArmor) * (1f + sheet.GetBonus(StatId.SentinelTotalArmor));
        }
    }

    protected override bool ExtraCombatInputGate => !RitualTokenPlacer.IsPlacing;

    private float _currentHealth;
    private float _currentShield;
    private float _timeSinceProjectileHit;

    public override void ActivateStandalone()
    {
        base.ActivateStandalone();
        Weapon.Visible = true;
    }

    public override void _Ready()
    {
        base._Ready();
        _currentHealth = MaxHealth;
        _currentShield = MaxShield;
        ByOwner[OwnerId] = this;
        SetActive(false);
        Weapon.Visible = false;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, _currentShield, MaxShield);
    }

    public override void _ExitTree()
    {
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;

        if (ByOwner.TryGetValue(OwnerId, out Sentinel current) && current == this)
        {
            ByOwner.Remove(OwnerId);
        }
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
            if (TokenDeployer.IsPickModeActive)
            {
                TokenDeployer.ExitPickMode();
            }
            else if (!RitualTokenPlacer.IsPlacing)
            {
                ToggleMouseCapture();
            }
        }
    }

    private void OnGamePhaseChanged(GamePhase phase)
    {
        bool shouldBeActive = phase != GamePhase.Build;

        if (phase == GamePhase.Combat)
        {
            HandoffFrom(Architect.ByOwner[OwnerId]);
        }
        else if (phase == GamePhase.Build)
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

        float remainingDamage = damage.Total;

        if (damage.Origin == DamageOrigin.Projectile)
        {
            _timeSinceProjectileHit = 0f;

            float absorbed = Mathf.Min(_currentShield, remainingDamage);
            _currentShield -= absorbed;
            remainingDamage -= absorbed;

            EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, _currentShield, MaxShield);
        }
        else
        {
            remainingDamage *= (1f - MeleeDamageReductionPercent);
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - remainingDamage);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);

        if (IsDead)
        {
            EventBus.Instance.EmitSignal(EventBus.SignalName.GameEnded, false);
        }
    }

    // Armor mitigates melee only - Shield (above) is a separate pool, not a % reduction. S-curve
    // (exponent > 1): trivial gear barely registers, endgame gear feels strong, climbs faster in
    // the middle than at either end. Public so the Character Sheet UI can show the same number
    // TakeDamage actually uses, rather than a second copy of this formula.
    public float MeleeDamageReductionPercent
    {
        get
        {
            float armorPow = Mathf.Pow(TotalArmor, GameRules.SentinelArmorCurveExponent);
            float kPow = Mathf.Pow(GameRules.SentinelArmorCurveConstant, GameRules.SentinelArmorCurveExponent);
            return armorPow / (armorPow + kPow);
        }
    }

    // Called by whichever Sentinel is active, from its own _PhysicsProcess - passively recharges
    // Shield once it's been GameRules.ShieldRegenDelay seconds since the last projectile hit.
    public void UpdateShieldRegen(float delta)
    {
        if (_currentShield >= MaxShield)
        {
            return;
        }

        _timeSinceProjectileHit += delta;

        if (_timeSinceProjectileHit < GameRules.ShieldRegenDelay)
        {
            return;
        }

        _currentShield = Mathf.Min(MaxShield, _currentShield + MaxShield * GameRules.ShieldRegenRatePercent * delta);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, _currentShield, MaxShield);
    }

    // Rolls whether this hit crits and returns the final damage packet - shared by Soldier and Tank
    // so the crit math lives in one place instead of being copy-pasted into both attack sites.
    // Layers base (weapon-inherent, boosted by the weapon's own local rolls) -> increased (the
    // global CharacterSheet pool from non-weapon gear) -> more (the weapon's own multiplier, on
    // crit damage only).
    public DamagePacket RollAttackDamage(float baseDamage)
    {
        CharacterSheet sheet = CharacterSheetCache.GetSheet(Id);
        Weapon weapon = SaveManager.Instance.Data.GetOrCreateLoadout(Id).Weapon;

        float baseCritChance = weapon?.ResolvedBaseCritChance ?? GameRules.UnarmedCritChance;
        float baseCritDamageBonus = weapon?.BaseCritDamageBonus ?? GameRules.UnarmedCritDamageBonus;
        float moreMultiplier = weapon?.CritDamageBonusMoreMultiplier ?? 1f;

        float critChance = baseCritChance * (1f + sheet.GetBonus(StatId.SentinelCritChance));
        bool isCrit = GD.Randf() < critChance;
        float critDamageBonus = baseCritDamageBonus * (1f + sheet.GetBonus(StatId.SentinelCritDamage)) * moreMultiplier;
        float multiplier = isCrit ? 1f + critDamageBonus : 1f;

        return new DamagePacket { PhysicalDamage = baseDamage * multiplier, Source = DamageSource.Weapon, IsCrit = isCrit };
    }

    // Called by whichever Sentinel is active, from its own _PhysicsProcess, alongside
    // UpdateShieldRegen. Continuous, not gated behind a no-damage-recently window like Shield -
    // Health Regen only exists at all once gear grants it, so there's no baseline mechanic here to
    // preserve the shape of.
    public void UpdateHealthRegen(float delta)
    {
        if (IsDead || _currentHealth >= MaxHealth)
        {
            return;
        }

        CharacterSheet sheet = CharacterSheetCache.GetSheet(Id);
        float baseRegen = sheet.GetBonus(StatId.SentinelBaseHealthRegen);
        if (baseRegen <= 0f)
        {
            return;
        }

        float regenPerSecond = baseRegen * (1f + sheet.GetBonus(StatId.SentinelHealthRegen));
        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + regenPerSecond * delta);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
    }

    // Full reset is a placeholder — a real healing mechanic (README §15) will replace this.
    protected void HealToFull()
    {
        _currentHealth = MaxHealth;
        _currentShield = MaxShield;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, _currentShield, MaxShield);
    }
}
