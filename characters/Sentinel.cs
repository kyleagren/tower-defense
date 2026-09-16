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
    // Not exported: these point at nodes in the inherited SentinelBody.tscn base, and a typed
    // export of a custom C# class against a base-scene node fails its cast during scene-build,
    // then gets written out as null on the next save. Same reason RitualPlacer/TokenDeployer
    // resolve their own Character via GetParent() instead of an export.
    public RitualPlacer RitualTokenPlacer { get; private set; }
    public TokenDeployer TokenDeployer { get; private set; }
    [Export] public Godot.Collections.Array<Ability> AbilitySlots { get; private set; } = new();

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

    public float AttackDamage
    {
        get
        {
            Weapon weapon = SaveManager.Instance.Data.GetOrCreateLoadout(Id).Weapon;
            float baseDamage = weapon?.BaseAttackDamage ?? GameRules.UnarmedAttackDamage;
            return baseDamage * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.WeaponAttackDamage));
        }
    }

    public float AttackSpeed
    {
        get
        {
            Weapon weapon = SaveManager.Instance.Data.GetOrCreateLoadout(Id).Weapon;
            return weapon?.ResolvedBaseAttackSpeed ?? GameRules.UnarmedAttackSpeed;
        }
    }

    protected override bool ExtraCombatInputGate => !RitualTokenPlacer.IsPlacing;

    // 1/2/3 double as token pick mode's slot keys, so abilities yield to it entirely - slot 4
    // included, since firing an ability while choosing a token to place shouldn't happen either.
    // Separate from CanUseCombatInput so attack/alt_action behavior is unchanged. Public so an
    // ability capturing the attack binding can gate its own clicks on the same rule.
    public bool CanUseAbilityInput => CanUseCombatInput && !TokenDeployer.IsPickModeActive;

    // Deliberately not folded into ExtraCombatInputGate: CanUseAbilityInput is built on top of that,
    // so putting the capture term there would make a capturing ability gate away its own clicks.
    protected bool CanUseWeaponInput => CanUseCombatInput && !IsAbilityCapturingAttack;

    private bool IsAbilityCapturingAttack
    {
        get
        {
            foreach (Ability ability in AbilitySlots)
            {
                if (ability != null && ability.CapturesAttackInput)
                {
                    return true;
                }
            }

            return false;
        }
    }

    private static readonly string[] _abilityActions = { "ability_1", "ability_2", "ability_3", "ability_4" };

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
        RitualTokenPlacer = GetNode<RitualPlacer>("RitualTokenPlacer");
        TokenDeployer = GetNode<TokenDeployer>("TokenDeployer");
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
            else if (TryCancelCapturingAbility())
            {
                return;
            }
            else if (!RitualTokenPlacer.IsPlacing)
            {
                ToggleMouseCapture();
            }
        }

        if (!CanUseAbilityInput)
        {
            return;
        }

        for (int slot = 0; slot < _abilityActions.Length && slot < AbilitySlots.Count; slot++)
        {
            if (@event.IsActionPressed(_abilityActions[slot]))
            {
                AbilitySlots[slot]?.TryActivate();
                return;
            }
        }
    }

    private bool TryCancelCapturingAbility()
    {
        foreach (Ability ability in AbilitySlots)
        {
            if (ability != null && ability.CapturesAttackInput)
            {
                ability.CancelCapture();
                return true;
            }
        }

        return false;
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

    // Public so the Character Sheet UI can show the same numbers UpdateHealthRegen actually uses,
    // rather than a second copy of this formula.
    public float BaseHealthRegenPerSecond => CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelBaseHealthRegen);

    public float TotalHealthRegenPerSecond =>
        BaseHealthRegenPerSecond * (1f + CharacterSheetCache.GetSheet(Id).GetBonus(StatId.SentinelHealthRegen));

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
    public DamagePacket RollAttackDamage(float baseDamage, bool guaranteedCrit = false)
    {
        CharacterSheet sheet = CharacterSheetCache.GetSheet(Id);
        Weapon weapon = SaveManager.Instance.Data.GetOrCreateLoadout(Id).Weapon;

        float baseCritChance = weapon?.ResolvedBaseCritChance ?? GameRules.UnarmedCritChance;
        float baseCritDamageBonus = weapon?.BaseCritDamageBonus ?? GameRules.UnarmedCritDamageBonus;
        float moreMultiplier = weapon?.CritDamageBonusMoreMultiplier ?? 1f;

        float critChance = baseCritChance * (1f + sheet.GetBonus(StatId.SentinelCritChance));
        bool isCrit = guaranteedCrit || GD.Randf() < critChance;
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
        if (IsDead || _currentHealth >= MaxHealth || BaseHealthRegenPerSecond <= 0f)
        {
            return;
        }

        _currentHealth = Mathf.Min(MaxHealth, _currentHealth + TotalHealthRegenPerSecond * delta);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
    }

    // Full reset is a placeholder — a real healing mechanic will replace this.
    protected void HealToFull()
    {
        _currentHealth = MaxHealth;
        _currentShield = MaxShield;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, _currentShield, MaxShield);
    }
}
