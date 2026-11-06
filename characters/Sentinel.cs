using Godot;
using System.Collections.Generic;

public abstract partial class Sentinel : PlayerCharacter, IDamageable
{
    // The local player's own Sentinel in the current scene.
    public static Sentinel Local => LevelContext.Current?.Sentinels.GetValueOrDefault(PlayerProfiles.LocalId);

    [Export] public Node3D Weapon { get; private set; }
    [Export] public float AimPointHeight = 1.2f;
    [Export] public float DodgeDistance = 5f;
    [Export] public float DodgeDuration = 0.35f;
    // Measured from the start of the roll, so its tail end can still be hit.
    [Export] public float DodgeImmunityDuration = 0.25f;
    // Not exported: these point at nodes in the inherited sentinel_body.tscn base, and a typed
    // export of a custom C# class against a base-scene node fails its cast during scene-build,
    // then gets written out as null on the next save. Same reason RitualPlacer/TokenDeployer
    // resolve their own Character via GetParent() instead of an export.
    public RitualPlacer RitualPlacer { get; private set; }
    public TokenDeployer TokenDeployer { get; private set; }
    [Export] public Godot.Collections.Array<Ability> AbilitySlots { get; private set; } = new();
    // The ability each slot fires on a hold instead of a tap, or null. A slot with one waits on every
    // press: releasing before GameRules.AbilityHoldThreshold is a tap, reaching it is a hold. A slot
    // without one fires on press.
    [Export] public Godot.Collections.Array<Ability> HoldAbilitySlots { get; private set; } = new();

    // Every ability this Sentinel carries, tap and hold alike.
    public IEnumerable<Ability> AllAbilities
    {
        get
        {
            foreach (Ability ability in AbilitySlots)
            {
                if (ability != null)
                {
                    yield return ability;
                }
            }

            foreach (Ability ability in HoldAbilitySlots)
            {
                if (ability != null)
                {
                    yield return ability;
                }
            }
        }
    }

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
        GameRules.RespawnSkipBaseManaCost + GameRules.RespawnSkipManaCostPerExtraDeath * (LevelState.Deaths - 1);

    private uint _aliveCollisionLayer;

    // This player's state for the level - what outlives this body.
    public PlayerLevelState LevelState => Level.StateOf(OwnerId);

    // This Sentinel's own owner's gear - never the local save's - so two players on the same
    // Sentinel resolve independently.
    public CharacterSheet Sheet => PlayerProfiles.Get(OwnerId).GetSheet(Id);
    private Weapon EquippedWeapon => PlayerProfiles.Get(OwnerId).GetLoadout(Id).Weapon;

    // Carried on every hit this Sentinel's weapon deals.
    public Attribution HitAttribution => new Attribution { Player = OwnerId, Source = this };

    public SentinelDefinition Definition => SentinelCatalog.Definition(Id);
    public SentinelStats Stats => new SentinelStats(Definition, Sheet, LevelState);

    public float MaxHealth => Stats.MaxHealth;
    public override float Speed => BaseSpeed * (1f + Sheet.GetBonus(StatId.SentinelMoveSpeed));

    public float MaxShield => Stats.MaxShield;

    // Shield past MaxShield is overshield - only Harvest grants it. It lives in the same pool, so a
    // hit drains it first; regen never reaches it, since regen stops at MaxShield, and any gear
    // change clamps back down to MaxShield, wiping it.
    public float ShieldCeiling => MaxShield * GameRules.OvershieldCeilingMultiplier;

    public float TotalArmor => Stats.TotalArmor;

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

    protected override bool ExtraCombatInputGate => !RitualPlacer.IsPlacing && !IsDodging;

    public bool IsDodging => _dodgeRemaining > 0f;
    protected bool IsDodgeImmune => _dodgeRemaining > DodgeDuration - DodgeImmunityDuration;

    // True while the character is doing something sprinting shouldn't overlap - firing, aiming,
    // swinging, blocking.
    protected virtual bool IsInCombatAction => false;

    protected override bool CanSprint => !IsDodging && !IsInCombatAction;

    private float _dodgeRemaining;
    private Vector3 _dodgeVelocity;
    private Vector3 _dodgeSpinAxis;
    private Basis _bodyMeshRestBasis;

    // 1/2/3 double as token pick mode's slot keys, so abilities yield to it entirely - slot 4
    // included, since firing an ability while choosing a token to place shouldn't happen either.
    // Separate from CanUseCombatInput so attack/alt_action behavior is unchanged. Public so an
    // ability capturing the attack binding can gate its own clicks on the same rule.
    public bool CanUseAbilityInput => CanUseCombatInput && !TokenDeployer.IsPickModeActive;

    // Whether the attack press currently held down reached this Sentinel rather than being taken by
    // an ability's own targeting. The weapon polls Input instead of reading events, so without this
    // a click that closed a capture - Health Link's second spike - would carry straight on into
    // weapon fire the moment the capture ended, while the button was still down.
    private bool _weaponOwnsAttackPress;

    // Deliberately not folded into ExtraCombatInputGate: CanUseAbilityInput is built on top of that,
    // so putting the capture term there would make a capturing ability gate away its own clicks.
    protected bool CanUseWeaponInput => CanUseCombatInput && !IsAbilityCapturingAttack && _weaponOwnsAttackPress;

    private bool IsAbilityCapturingAttack
    {
        get
        {
            foreach (Ability ability in AllAbilities)
            {
                if (ability.CapturesAttackInput)
                {
                    return true;
                }
            }

            return false;
        }
    }

    // Lets a long-running capture stand aside for a short one layered on top of it.
    public bool IsAnotherAbilityCapturing(Ability self)
    {
        foreach (Ability ability in AllAbilities)
        {
            if (ability != self && ability.CapturesAttackInput)
            {
                return true;
            }
        }

        return false;
    }

    private static readonly string[] _abilityActions = { "ability_1", "ability_2", "ability_3", "ability_4" };

    public static string AbilityAction(int slot) => _abilityActions[slot];

    protected override bool CanLook
    {
        get
        {
            foreach (Ability ability in AllAbilities)
            {
                if (ability.SuspendsLook)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private float _currentHealth;
    private float _currentShield;
    private float _timeSinceLastHit;
    private int _heldSlot = -1;
    private float _heldTime;

    public override void Activate()
    {
        base.Activate();
        Weapon.Visible = true;
    }

    public override void _Ready()
    {
        base._Ready();
        RitualPlacer = GetNode<RitualPlacer>("RitualPlacer");
        TokenDeployer = GetNode<TokenDeployer>("TokenDeployer");
        _bodyMeshRestBasis = BodyMesh.Basis;

        for (int slot = 0; slot < AbilitySlots.Count; slot++)
        {
            AbilitySlots[slot]?.AssignSlot(slot);
            GetHoldAbility(slot)?.AssignSlot(slot);
        }

        string[] slotNames = new string[AbilitySlots.Count];

        for (int slot = 0; slot < slotNames.Length; slot++)
        {
            slotNames[slot] = SlotDisplayName(slot);
        }

        SentinelCatalog.RecordSlotNames(Id, slotNames);
        _currentHealth = MaxHealth;
        _currentShield = MaxShield;
        Level.Sentinels[OwnerId] = this;
        SetActive(false);
        Weapon.Visible = false;
        EventBus.Instance.GamePhaseChanged += OnGamePhaseChanged;
        EventBus.Instance.InventoryChanged += OnGearChanged;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);
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
        base._UnhandledInput(@event);

        if (InputPress.Began(@event, "attack"))
        {
            _weaponOwnsAttackPress = true;
        }
        else if (InputPress.Ended(@event, "attack"))
        {
            _weaponOwnsAttackPress = false;
        }

        if (!IsActive)
        {
            return;
        }

        if (@event is InputEventMouseMotion mouseMotion)
        {
            HandleLook(mouseMotion);
        }

        if (IsDead && InputChords.IsPressedWithoutAbilityModifier(@event, "interact"))
        {
            TrySkipRespawn();
            return;
        }

        if (InputChords.IsPressedWithoutAbilityModifier(@event, "ui_cancel"))
        {
            if (TokenDeployer.IsPickModeActive)
            {
                TokenDeployer.ExitPickMode();
            }
            else if (TryCancelCapturingAbility())
            {
                return;
            }
            else if (!RitualPlacer.IsPlacing &&!UiPanelStack.AnyOpen && @event is not InputEventJoypadButton)
            {
                PauseMenu.Instance?.Open();
                GetViewport().SetInputAsHandled();
            }
        }

        if (InputChords.IsPressedWithoutAbilityModifier(@event, "dodge") && TryStartDodge())
        {
            return;
        }

        if (!CanUseAbilityInput)
        {
            return;
        }

        for (int slot = 0; slot < _abilityActions.Length && slot < AbilitySlots.Count; slot++)
        {
            if (InputChords.IsAbilityPressed(@event, _abilityActions[slot]))
            {
                PressSlot(slot);
                return;
            }

            if (@event.IsActionReleased(_abilityActions[slot]))
            {
                ReleaseSlot(slot);
                return;
            }
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        if (_heldSlot < 0)
        {
            return;
        }

        // Losing ability input mid-hold (death, token pick mode) drops the press rather than letting
        // it fire later - and a release arriving while input is gated would never be seen anyway.
        if (!CanUseAbilityInput)
        {
            _heldSlot = -1;
            return;
        }

        _heldTime += (float)delta;

        if (_heldTime >= GameRules.AbilityHoldThreshold)
        {
            int slot = _heldSlot;
            _heldSlot = -1;
            TryActivateInSlot(slot, GetHoldAbility(slot));
        }
    }

    private void PressSlot(int slot)
    {
        if (GetHoldAbility(slot) == null)
        {
            TryActivateInSlot(slot, AbilitySlots[slot]);
            return;
        }

        _heldSlot = slot;
        _heldTime = 0f;
    }

    private void ReleaseSlot(int slot)
    {
        if (_heldSlot != slot)
        {
            return;
        }

        _heldSlot = -1;
        TryActivateInSlot(slot, AbilitySlots[slot]);
    }

    // A slot's two abilities never run at once - one field at a time, no Harvest mid-Overdrive.
    private void TryActivateInSlot(int slot, Ability ability)
    {
        if (ability == null)
        {
            return;
        }

        Ability partner = ability == AbilitySlots[slot] ? GetHoldAbility(slot) : AbilitySlots[slot];
        if (partner != null && partner.IsActive)
        {
            return;
        }

        if (IsLockedOutBy(ability))
        {
            return;
        }

        ability.TryActivate();
    }

    // The locking ability never locks itself out, so a second press can still end it.
    private bool IsLockedOutBy(Ability ability)
    {
        foreach (Ability other in AllAbilities)
        {
            if (other != ability && other.IsActive && other.LocksOutOtherAbilities)
            {
                return true;
            }
        }

        return false;
    }

    private Ability GetHoldAbility(int slot) => slot < HoldAbilitySlots.Count ? HoldAbilitySlots[slot] : null;

    // "Converge / Repel" for a paired slot, the lone name otherwise, null for an empty slot.
    public string SlotDisplayName(int slot)
    {
        Ability tap = slot < AbilitySlots.Count ? AbilitySlots[slot] : null;
        if (tap == null || !IsInstanceValid(tap))
        {
            return null;
        }

        Ability hold = GetHoldAbility(slot);
        return hold != null && IsInstanceValid(hold) ? $"{tap.DisplayName} / {hold.DisplayName}" : tap.DisplayName;
    }

    // Rolls along the movement input, or straight ahead with none. Grounded only.
    private bool TryStartDodge()
    {
        if (!CanUseCombatInput || !CanMove || !IsOnFloor())
        {
            return false;
        }

        Vector2 inputDir = Input.GetVector("move_left", "move_right", "move_forward", "move_back");
        Vector3 direction = inputDir == Vector2.Zero
            ? -Transform.Basis.Z
            : (Transform.Basis * new Vector3(inputDir.X, 0f, inputDir.Y)).Normalized();

        _dodgeVelocity = direction * (DodgeDistance / DodgeDuration);
        _dodgeSpinAxis = Vector3.Up.Cross(direction).Normalized();
        _dodgeRemaining = DodgeDuration;
        return true;
    }

    protected override Vector3? UpdateForcedMovement(float delta)
    {
        if (_dodgeRemaining <= 0f)
        {
            return null;
        }

        _dodgeRemaining -= delta;
        UpdateDodgeSpin();
        return _dodgeVelocity;
    }

    // One full tumble over the roll. The axis is held in world space and re-expressed each tick,
    // since mouse look keeps turning the body mid-roll.
    private void UpdateDodgeSpin()
    {
        if (_dodgeRemaining <= 0f)
        {
            BodyMesh.Basis = _bodyMeshRestBasis;
            return;
        }

        float progress = 1f - _dodgeRemaining / DodgeDuration;
        Vector3 localAxis = (Transform.Basis.Inverse() * _dodgeSpinAxis).Normalized();
        BodyMesh.Basis = new Basis(localAxis, Mathf.Tau * progress) * _bodyMeshRestBasis;
    }

    private bool TryCancelCapturingAbility()
    {
        foreach (Ability ability in AllAbilities)
        {
            if (ability.CapturesAttackInput && ability.CanCancelCapture)
            {
                ability.CancelCapture();
                return true;
            }
        }

        return false;
    }

    // A wave ending while dead revives outright, whether it leads into Build or PostGame. Build
    // heals to full and ends whatever abilities are still running; Combat starts with every ability
    // ready.
    private void OnGamePhaseChanged(GamePhase phase)
    {
        if (phase == GamePhase.Combat)
        {
            foreach (Ability ability in AllAbilities)
            {
                ability.ResetCooldown();
            }

            return;
        }

        if (phase != GamePhase.Build && phase != GamePhase.PostGame)
        {
            return;
        }

        Respawn();

        if (phase == GamePhase.PostGame)
        {
            return;
        }

        HealToFull();

        foreach (Ability ability in AllAbilities)
        {
            ability.CancelCapture();
            ability.OnBuildStarted();
        }
    }

    public void TakeDamage(Hit hit)
    {
        if (IsDead)
        {
            return;
        }

        // Resolved by this Sentinel's own player, whose machine knows their gear, block and dodge.
        if (!IsLocallyControlled)
        {
            RpcId(GetMultiplayerAuthority(), MethodName.ReceiveHit, hit.Damage.ToNetwork(), (int)hit.Type);
            return;
        }

        ResolveHit(hit);
    }

    // Only enemies hurt a Sentinel, and only the host runs enemies.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHit(float[] damage, int type)
    {
        if (NetworkSession.Instance.IsFromHost && IsLocallyControlled && !IsDead)
        {
            ResolveHit(new Hit { Damage = DamagePacket.FromNetwork(damage), Type = (HitType)type });
        }
    }

    protected virtual void ResolveHit(Hit hit)
    {
        if (IsDead || IsDodgeImmune)
        {
            return;
        }

        _timeSinceLastHit = 0f;

        bool isProjectile = hit.Damage.Origin == DamageOrigin.Projectile;
        float remainingDamage = hit.Damage.Total * IncomingDamageMultiplier * (1f - DamageReductionPercent(isProjectile));
        remainingDamage = AbsorbWithShield(remainingDamage, isProjectile ? GameRules.ShieldProjectileEffectiveness : 1f);

        _currentHealth = Mathf.Max(0f, _currentHealth - remainingDamage);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerHealthChanged, (int)OwnerId, _currentHealth, MaxHealth);

        if (IsDead)
        {
            Die();
        }
    }

    // Scales every incoming hit before armor or Shield sees it - Retaliation's damage reduction.
    protected virtual float IncomingDamageMultiplier => 1f;

    // Armor goes first, so it also slows how fast a hit drains Shield.
    private float DamageReductionPercent(bool isProjectile) =>
        isProjectile ? MeleeDamageReductionPercent * GameRules.ArmorProjectileEffectiveness : MeleeDamageReductionPercent;

    // Shield takes every kind of damage, but at `effectiveness` damage stopped per point spent - so
    // against projectiles a point of Shield stops two. Returns what's left over for Health.
    private float AbsorbWithShield(float damage, float effectiveness)
    {
        if (_currentShield <= 0f || damage <= 0f)
        {
            return damage;
        }

        float absorbed = Mathf.Min(_currentShield * effectiveness, damage);
        _currentShield -= absorbed / effectiveness;
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);

        return damage - absorbed;
    }

    // Death is a timed respawn on the spot, not a loss. Each death this match waits longer than the
    // last, so dying repeatedly costs more than a single slip; mana can skip the wait.
    private void Die()
    {
        LevelState.Deaths++;
        OnDied();
        RespawnRemaining = GameRules.RespawnBaseSeconds + GameRules.RespawnSecondsPerExtraDeath * (LevelState.Deaths - 1);
        GoDown();
        Rpc(MethodName.ReceiveDowned);

        TokenDeployer.ExitPickMode();

        if (RitualPlacer.IsPlacing)
        {
            RitualPlacer.CancelPlacement();
        }

        foreach (Ability ability in AllAbilities)
        {
            ability.CancelCapture();
            ability.OnCharacterDied();
        }
    }

    // What every machine shows of a death. Off every layer rather than just hidden, so enemies walk
    // through the body and nothing collides with it until it stands back up.
    private void GoDown()
    {
        _dodgeRemaining = 0f;
        BodyMesh.Basis = _bodyMeshRestBasis;
        BodyMesh.Visible = false;
        Weapon.Visible = false;
        _aliveCollisionLayer = CollisionLayer;
        CollisionLayer = 0;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDowned()
    {
        _currentHealth = 0f;
        GoDown();
    }

    // For a subclass with its own state that death should clear.
    protected virtual void OnDied()
    {
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

        if (IsLocallyControlled)
        {
            Rpc(MethodName.ReceiveStoodUp);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveStoodUp()
    {
        Respawn();
    }

    public void TrySkipRespawn()
    {
        if (!IsDead)
        {
            return;
        }

        if (NetworkSession.Instance.IsHost)
        {
            if (Level.Economy.TrySpendMana(RespawnSkipCost))
            {
                Respawn();
            }
        }
        else if (Level.Economy.CanAffordMana(RespawnSkipCost))
        {
            RpcId(1, MethodName.RequestRespawnSkip, RespawnSkipCost);
        }
    }

    // The cost comes from the owner, the only machine that counts this Sentinel's deaths.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void RequestRespawnSkip(int cost)
    {
        long sender = Multiplayer.GetRemoteSenderId();

        if (NetworkSession.Instance.SeatOf(sender) == OwnerId && Level.Economy.TrySpendMana(cost))
        {
            RpcId(sender, MethodName.ReceiveRespawnSkip);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveRespawnSkip()
    {
        if (NetworkSession.Instance.IsFromHost && IsDead)
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

    public float MeleeDamageReductionPercent => Stats.MeleeDamageReductionPercent;
    public float BaseHealthRegenPerSecond => Stats.BaseHealthRegenPerSecond;
    public float TotalHealthRegenPerSecond => Stats.TotalHealthRegenPerSecond;

    // Called by whichever Sentinel is active, from its own _PhysicsProcess - passively recharges
    // Shield once it's been GameRules.ShieldRegenDelay seconds since the last hit of any kind.
    public void UpdateShieldRegen(float delta)
    {
        _timeSinceLastHit += delta;

        if (_currentShield >= MaxShield || _timeSinceLastHit < GameRules.ShieldRegenDelay)
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

        DamagePacket damage = new DamagePacket { PhysicalDamage = baseDamage };
        AddExtraElementalDamage(ref damage, baseDamage);

        damage *= multiplier;
        damage.Source = DamageSource.Weapon;
        damage.IsCrit = isCrit;
        return damage;
    }

    // Shares of a hit's base damage added on top as another element - the Tank's heat is the first
    // source. Added before the crit multiplier, so a crit multiplies the added portion too.
    protected virtual void AddExtraElementalDamage(ref DamagePacket damage, float baseDamage)
    {
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

    public void GrantShield(float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        _currentShield = Mathf.Min(ShieldCeiling, _currentShield + amount);
        EventBus.Instance.EmitSignal(EventBus.SignalName.PlayerShieldChanged, (int)OwnerId, _currentShield, MaxShield);
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
