using Godot;
using System.Collections.Generic;

public abstract partial class Sentinel : PlayerCharacter, IDamageable
{
    // The local player's own Sentinel in the current scene, whether or not it's the active body.
    public static Sentinel Local => LevelContext.Current?.Sentinels.GetValueOrDefault(PlayerProfiles.LocalId);

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

    public override bool CanAct => base.CanAct && !IsDead;

    // Seconds until this Sentinel stands back up - zero while alive.
    public float RespawnRemaining { get; private set; }

    // Mana to stand back up right now - grows with each death this match, like the wait itself.
    public int RespawnSkipCost =>
        GameRules.RespawnSkipBaseManaCost + GameRules.RespawnSkipManaCostPerExtraDeath * (_deathsThisMatch - 1);

    private int _deathsThisMatch;
    private uint _aliveCollisionLayer;

    // This Sentinel's own owner's gear - never the local save's - so two players on the same
    // Sentinel resolve independently.
    public CharacterSheet Sheet => PlayerProfiles.Get(OwnerId).GetSheet(Id);
    private Weapon EquippedWeapon => PlayerProfiles.Get(OwnerId).GetLoadout(Id).Weapon;

    // Carried on every hit this Sentinel's weapon deals.
    protected Attribution HitAttribution => new Attribution { Player = OwnerId, Source = this };

    public float MaxHealth => BaseMaxHealth * (1f + Sheet.GetBonus(StatId.SentinelMaxHealth));
    public override float Speed => BaseSpeed * (1f + Sheet.GetBonus(StatId.SentinelMoveSpeed));

    public float MaxShield
    {
        get
        {
            CharacterSheet sheet = Sheet;
            return sheet.GetLocalTotal(StatId.SentinelShield) * (1f + sheet.GetBonus(StatId.SentinelTotalShield));
        }
    }

    public float TotalArmor
    {
        get
        {
            CharacterSheet sheet = Sheet;
            return sheet.GetLocalTotal(StatId.SentinelArmor) * (1f + sheet.GetBonus(StatId.SentinelTotalArmor));
        }
    }

    public float AttackDamage
    {
        get
        {
            Weapon weapon = EquippedWeapon;
            float baseDamage = weapon?.BaseAttackDamage ?? GameRules.UnarmedAttackDamage;
            return baseDamage * (1f + Sheet.GetBonus(StatId.WeaponAttackDamage));
        }
    }

    public float AttackSpeed
    {
        get
        {
            Weapon weapon = EquippedWeapon;
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
        Level.Sentinels[OwnerId] = this;
        SetActive(false);
        Weapon.Visible = false;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EventBus.Instance.InventoryChanged += OnGearChanged;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);

        // Spawned into a match already underway, the phase events so far were missed - catch up
        // on the current phase. The hub has no match; its spawner activates the body directly.
        if (Level.Match != null)
        {
            OnGamePhaseChanged(Level.Match.Phase);
        }
    }

    public override void _ExitTree()
    {
        base._ExitTree();
        EventBus.Instance.GamePhaseChanged -= OnGamePhaseChanged;
        EventBus.Instance.InventoryChanged -= OnGearChanged;

        if (Level.Sentinels.TryGetValue(OwnerId, out Sentinel current) && current == this)
        {
            Level.Sentinels.Remove(OwnerId);
        }
    }

    // Gear can change max health or shield at any time. Current values are only ever clamped down,
    // never topped up - equipping shouldn't double as a heal, and regen closes a raised gap. The HUD
    // is told either way, so it shows the new maximum straight away.
    private void OnGearChanged()
    {
        // Invalidated here too rather than trusting the global handler to have run first - the
        // maximums below must come from the new gear.
        PlayerProfiles.Get(OwnerId).InvalidateSheets();

        _currentHealth = Mathf.Min(_currentHealth, MaxHealth);
        _currentShield = Mathf.Min(_currentShield, MaxShield);

        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);
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

        if (IsDead && @event.IsActionPressed("interact"))
        {
            TrySkipRespawn();
            return;
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

        // A wave ending while dead revives outright, whether it leads into Build or PostGame.
        if (phase == GamePhase.Build || phase == GamePhase.PostGame)
        {
            Respawn();
        }

        if (phase == GamePhase.Combat)
        {
            HandoffFrom(Level.Architects[OwnerId]);
        }
        else if (phase == GamePhase.Build)
        {
            HealToFull();
        }

        SetActive(shouldBeActive);
        Weapon.Visible = shouldBeActive;
    }

    public virtual void TakeDamage(Hit hit)
    {
        if (IsDead)
        {
            return;
        }

        float remainingDamage = hit.Damage.Total;

        if (hit.Damage.Origin == DamageOrigin.Projectile)
        {
            _timeSinceProjectileHit = 0f;

            float absorbed = Mathf.Min(_currentShield, remainingDamage);
            _currentShield -= absorbed;
            remainingDamage -= absorbed;

            EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);
        }
        else
        {
            remainingDamage *= (1f - MeleeDamageReductionPercent);
        }

        _currentHealth = Mathf.Max(0f, _currentHealth - remainingDamage);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);

        if (IsDead)
        {
            Die();
        }
    }

    // Death is a timed respawn on the spot, not a loss. Each death this match waits longer than the
    // last, so dying repeatedly costs more than a single slip; mana can skip the wait.
    private void Die()
    {
        _deathsThisMatch++;
        RespawnRemaining = GameRules.RespawnBaseSeconds + GameRules.RespawnSecondsPerExtraDeath * (_deathsThisMatch - 1);

        BodyMesh.Visible = false;
        Weapon.Visible = false;

        // Off every layer rather than just hidden, so enemies walk through the body and nothing
        // collides with it until it stands back up.
        _aliveCollisionLayer = CollisionLayer;
        CollisionLayer = 0;

        TokenDeployer.ExitPickMode();

        if (RitualTokenPlacer.IsPlacing)
        {
            RitualTokenPlacer.CancelPlacement();
        }

        foreach (Ability ability in AbilitySlots)
        {
            ability?.CancelCapture();
        }
    }

    // Stands back up on the spot at full health. Also the hook an ally's revive will call once
    // co-op exists.
    public void Respawn()
    {
        if (!IsDead)
        {
            return;
        }

        RespawnRemaining = 0f;
        CollisionLayer = _aliveCollisionLayer;
        BodyMesh.Visible = IsActive;
        Weapon.Visible = IsActive;
        HealToFull();
    }

    public void TrySkipRespawn()
    {
        if (IsDead && EconomyManager.Instance.TrySpendMana(RespawnSkipCost))
        {
            Respawn();
        }
    }

    // Called first from a subclass's _PhysicsProcess. True while dead, in which case the subclass
    // skips movement and attacks for this tick.
    protected bool UpdateDeath(float delta)
    {
        if (!IsDead)
        {
            return false;
        }

        RespawnRemaining -= delta;

        if (RespawnRemaining <= 0f)
        {
            Respawn();
        }

        return true;
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
    public float BaseHealthRegenPerSecond => Sheet.GetBonus(StatId.SentinelBaseHealthRegen);

    public float TotalHealthRegenPerSecond =>
        BaseHealthRegenPerSecond * (1f + Sheet.GetBonus(StatId.SentinelHealthRegen));

    // Called by whichever Sentinel is active, from its own _PhysicsProcess - passively recharges
    // Shield once it's been GameRules.ShieldRegenDelay seconds since the last projectile hit.
    public void UpdateShieldRegen(float delta)
    {
        _timeSinceProjectileHit += delta;

        if (_currentShield >= MaxShield || _timeSinceProjectileHit < GameRules.ShieldRegenDelay)
        {
            return;
        }

        _currentShield = Mathf.Min(MaxShield, _currentShield + MaxShield * GameRules.ShieldRegenRatePercent * delta);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);
    }

    // Rolls whether this hit crits and returns the final damage packet - shared by Soldier and Tank
    // so the crit math lives in one place instead of being copy-pasted into both attack sites.
    // Layers base (weapon-inherent, boosted by the weapon's own local rolls) -> increased (the
    // global CharacterSheet pool from non-weapon gear) -> more (the weapon's own multiplier, on
    // crit damage only).
    public DamagePacket RollAttackDamage(float baseDamage, bool guaranteedCrit = false)
    {
        CharacterSheet sheet = Sheet;
        Weapon weapon = EquippedWeapon;

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
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);
    }

    // Full reset is a placeholder — a real healing mechanic will replace this.
    protected void HealToFull()
    {
        _currentHealth = MaxHealth;
        _currentShield = MaxShield;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);
    }
}
