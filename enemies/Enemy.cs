using Godot;
using System.Collections.Generic;

public partial class Enemy : CharacterBody3D, IDamageable
{
    [Export] public CollisionShape3D BodyCollision { get; private set; }
    [Export] public CollisionShape3D HeadCollision { get; private set; }
    [Export] public MeshInstance3D HeadWeakPoint { get; private set; }
    [Export] public MeshInstance3D BodyWeakPoint { get; private set; }
    [Export] public MeshInstance3D ArmLWeakPoint { get; private set; }
    [Export] public MeshInstance3D ArmRWeakPoint { get; private set; }
    [Export] public MeshInstance3D LegLWeakPoint { get; private set; }
    [Export] public MeshInstance3D LegRWeakPoint { get; private set; }
    [Export] public Node3D ShoulderAttachment { get; private set; }
    // TODO placeholder: the one mesh the status tint and burn flash swap onto - the torso, so the rest
    // of the body keeps its own look. Optional; an enemy with none wired just shows no tint.
    [Export] public MeshInstance3D StatusMesh { get; private set; }
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 200f;
    [Export] public float CenterMassHeight = 1f;
    // Kilograms. Knockback divides by it, so a heavy enemy moves less but is never made immune.
    [Export] public float Mass = 80f;
    [Export] public int ManaReward = 1;
    [Export] public int GoldReward = 1;
    [Export] public float DropChance = 0.15f;
    [Export] public int LootRolls = 1;
    [Export] public HealthBar3D HealthBar { get; private set; }
    [Export] public PackedScene FloatingTextScene { get; private set; }

    // TODO placeholder: remove once there's a real way to display an enemy's element/ailments.
    // Colors come from DamageTypeColors rather than a second hardcoded palette here, so the tint an
    // enemy shows and the color its damage numbers use can't drift apart.
    private static readonly Dictionary<Tag, StandardMaterial3D> _statusMaterials = BuildStatusMaterials();

    private static readonly StandardMaterial3D _burnFlashMaterial = CreateTintMaterial(new Color(0.5f, 0f, 0f));
    private static readonly StandardMaterial3D _frostburnFlashMaterial = CreateTintMaterial(new Color(0.04f, 0.07f, 0.28f));
    private const float AilmentFlashDuration = 0.1f;

    private static Dictionary<Tag, StandardMaterial3D> BuildStatusMaterials()
    {
        Dictionary<Tag, StandardMaterial3D> materials = new Dictionary<Tag, StandardMaterial3D>();

        foreach (Tag element in ElementTags.StatusCapable)
        {
            materials[element] = CreateTintMaterial(DamageTypeColors.GetStatusOverlay(element));
        }

        return materials;
    }

    private static StandardMaterial3D CreateTintMaterial(Color color)
    {
        return new StandardMaterial3D { AlbedoColor = color };
    }

    private bool _isDead;
    private readonly List<Debuff> _debuffs = new List<Debuff>();
    private Material _appliedTint;
    private Material _statusMeshRestMaterial;
    private float _ailmentFlashTimer;

    public bool IsDead => _isDead;
    public bool IsDestroyed => _isDead;

    // The host's own health as a share of the maximum - read by things that run on the host, like an
    // execute's threshold.
    public float HealthFraction => CurrentHealth / MaxHealth;
    public Tag? Status { get; private set; }
    public IReadOnlyList<Debuff> Debuffs => _debuffs;

    // Remaining travel distance to the objective. Only meaningful for enemies that follow a lane
    // (WalkingEnemy updates it); anything else just leaves it at the default, which keeps it from
    // ever winning a ClosestToGoal targeting comparison.
    public float DistanceToGoal { get; protected set; } = float.MaxValue;

    public float CurrentHealth { get; protected set; }

    // Only the host simulates an enemy - AI, debuffs, death and what it pays out. Everywhere else it's
    // a copy its MultiplayerSynchronizer keeps in step.
    public bool IsSimulatedHere => IsMultiplayerAuthority();

    // What copies need from the host, synced by the enemy's MultiplayerSynchronizer.
    public float SyncedHealth
    {
        get => CurrentHealth;
        set
        {
            // Health only rises through something the local guess can't know about (a heal, a
            // dummy's reset), so the guess is stale.
            if (value > CurrentHealth)
            {
                _predictionExpiresAt = 0.0;
            }

            CurrentHealth = value;
        }
    }

    // Long enough to cover a round trip through Steam's relay, so the host's result arrives before
    // the guess lapses; short enough that a hit the host rejected doesn't linger on the bar.
    private const double HitPredictionSeconds = 0.5;

    private float _predictedHealth;
    private double _predictionExpiresAt;

    private static double Now => Time.GetTicksMsec() / 1000.0;

    // A copy's bar shows its own player's hits before the host confirms them. The lower value wins,
    // since the host's number drops below the guess once it includes the hit plus anyone else's.
    private float DisplayedHealth => Now < _predictionExpiresAt ? Mathf.Min(CurrentHealth, _predictedHealth) : CurrentHealth;

    // -1 for no Status.
    public int SyncedStatus
    {
        get => Status.HasValue ? (int)Status.Value : -1;
        set => Status = value < 0 ? null : (Tag)value;
    }

    private DebuffType _copyDebuffTypes;

    // Which types of debuff are on this enemy. Read live where it's simulated; a copy holds no
    // debuffs, so it keeps the summary its synchronizer last brought.
    public DebuffType DebuffTypes
    {
        get
        {
            if (!IsSimulatedHere)
            {
                return _copyDebuffTypes;
            }

            DebuffType types = DebuffType.None;

            foreach (Debuff debuff in _debuffs)
            {
                if (!debuff.IsRecovering)
                {
                    types |= debuff.Type;
                }
            }

            return types;
        }
    }

    // DebuffTypes as one number, for the synchronizer.
    public int SyncedDebuffTypes
    {
        get => (int)DebuffTypes;
        set => _copyDebuffTypes = (DebuffType)value;
    }

    public bool HasDebuff(DebuffType types) => (DebuffTypes & types) != 0;

    // Frozen or electrocuted: no steering, turning or attacking. Checked against the debuff types
    // rather than the list, so a copy - which only has the synced types - agrees with the host.
    private const DebuffType ActionPreventing = DebuffType.Frozen | DebuffType.Electrocuted | DebuffType.KnockedDown;

    public bool IsActionPrevented => HasDebuff(ActionPreventing);

    public Vector3 CenterMass => GlobalPosition + Vector3.Up * CenterMassHeight;
    public Vector3 AimPoint => CenterMass;

    // Optional - an enemy with no HeadCollision wired (e.g. TrainingDummy) just never registers a
    // headshot. Checked as its own ray-vs-box test against the head hitbox's actual volume, rather
    // than relying on which shape a raycast's closest-hit result reports - the head box sits mostly
    // inside/behind the body capsule's own silhouette, so it's rarely ever the *closest* shape a
    // raycast finds even when a shot's line genuinely passes through it.
    public bool IsHeadshot(Vector3 rayOrigin, Vector3 rayEnd)
    {
        if (HeadCollision == null || HeadCollision.Shape is not BoxShape3D boxShape)
        {
            return false;
        }

        Transform3D toLocal = HeadCollision.GlobalTransform.AffineInverse();
        Vector3 localOrigin = toLocal * rayOrigin;
        Vector3 localEnd = toLocal * rayEnd;

        Aabb headBounds = new Aabb(-boxShape.Size / 2f, boxShape.Size);
        return headBounds.IntersectsSegment(localOrigin, localEnd);
    }

    private const float WeakPointHitRadius = 0.18f;

    protected LevelContext Level { get; private set; }

    private readonly List<MeshInstance3D> _activeWeakPoints = new List<MeshInstance3D>();

    // Head is always included when wired, so there's always a guaranteed high-value target; the
    // rest are drawn at random, at most one per remaining body part, from whichever markers this
    // enemy actually has - optional per enemy, same as HeadCollision.
    public void ExposeWeakPoints(int count)
    {
        ClearWeakPoints();

        if (HeadWeakPoint != null)
        {
            ActivateWeakPoint(HeadWeakPoint);
            count--;
        }

        List<MeshInstance3D> candidates = new List<MeshInstance3D> { BodyWeakPoint, ArmLWeakPoint, ArmRWeakPoint, LegLWeakPoint, LegRWeakPoint };
        candidates.RemoveAll(spot => spot == null);

        for (int i = 0; i < count && candidates.Count > 0; i++)
        {
            int index = GD.RandRange(0, candidates.Count - 1);
            ActivateWeakPoint(candidates[index]);
            candidates.RemoveAt(index);
        }
    }

    public void ClearWeakPoints()
    {
        foreach (MeshInstance3D spot in _activeWeakPoints)
        {
            spot.Visible = false;
        }

        _activeWeakPoints.Clear();
    }

    // Same geometric-proximity approach as IsHeadshot - checks how close the shot's line passes to
    // the marker rather than requiring a real physics hit.
    public bool IsWeakPointHit(Vector3 rayOrigin, Vector3 rayEnd)
    {
        foreach (MeshInstance3D spot in _activeWeakPoints)
        {
            if (GeometryUtils.DistanceSquaredToSegment(spot.GlobalPosition, rayOrigin, rayEnd) <= WeakPointHitRadius * WeakPointHitRadius)
            {
                return true;
            }
        }

        return false;
    }

    private void ActivateWeakPoint(MeshInstance3D spot)
    {
        spot.Visible = true;
        _activeWeakPoints.Add(spot);
    }

    public Vector3 GetJitteredCenterMass(float jitterAmount)
    {
        Vector3 jitter = new Vector3(
            (float)GD.RandRange(-jitterAmount, jitterAmount),
            (float)GD.RandRange(-jitterAmount, jitterAmount),
            (float)GD.RandRange(-jitterAmount, jitterAmount)
        );

        return CenterMass + jitter;
    }

    public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        _statusMeshRestMaterial = StatusMesh?.MaterialOverride;
        Level = LevelContext.Current;

        // Group membership is for editor / remote-debugger inspection only — code paths that need
        // living enemies go through LevelContext.Enemies, which excludes ragdolls.
        AddToGroup(Groups.Enemies);
        Level.Enemies.Register(this);

        HealthBar.SetFraction(1f);
        RefreshHealthBarVisibility();

        if (Level.IsWeakPointVisorActive)
        {
            ExposeWeakPoints(Level.WeakPointVisorCount);
        }
    }

    public void RefreshHealthBarVisibility()
    {
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;
    }

    // Safety net for enemies freed without dying. Unregistering twice is a no-op, so the Die() path
    // unregistering first is unaffected.
    public override void _ExitTree()
    {
        Level.Enemies.Unregister(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsSimulatedHere)
        {
            TickDebuffs((float)delta);
        }
        else
        {
            HealthBar.SetFraction(DisplayedHealth / MaxHealth);
        }

        UpdateElementalVisual((float)delta); // TODO placeholder: remove once there's a real way to display this.
    }

    public void TakeDamage(Hit hit)
    {
        if (_isDead)
        {
            return;
        }

        // A copy can't resolve a hit. The dealer sees their number and their bar drop straight away;
        // the host applies the damage and the result syncs back. Death waits for the host.
        if (!IsSimulatedHere)
        {
            ShowDamageNumber(hit.Damage);
            _predictedHealth = Mathf.Max(DisplayedHealth - hit.Damage.Total, 0f);
            _predictionExpiresAt = Now + HitPredictionSeconds;
            RpcId(1, MethodName.ReceiveForwardedHit, hit.Damage.ToNetwork(), (int)hit.Type, hit.RagdollImpulse);
            return;
        }

        ResolveHit(hit, dealerShowedNumber: false);
    }

    // Credited to whoever sent it. Rune on-hits don't travel: only Rituals carry them, and the host
    // runs every Ritual.
    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveForwardedHit(float[] damage, int type, Vector3 ragdollImpulse)
    {
        PlayerId dealer = NetworkSession.Instance.SenderSeat;

        if (_isDead || !IsSimulatedHere || dealer == default)
        {
            return;
        }

        ResolveHit(new Hit
        {
            Damage = DamagePacket.FromNetwork(damage),
            Attribution = AttributionFor(dealer),
            Type = (HitType)type,
            RagdollImpulse = ragdollImpulse
        }, dealerShowedNumber: true);
    }

    private void ResolveHit(Hit hit, bool dealerShowedNumber)
    {
        if (hit.Spell.ChosenTarget == this && hit.Spell.TargetingRune?.BoostsHitOn(this) == true)
        {
            hit.Damage *= 1f + hit.Spell.TargetingRune.MoreDamageToTarget;
        }

        DamagePacket amount = hit.Damage;
        bool statusConsumed = false;
        Tag? statusBefore = Status;

        if (hit.Spell.OnHitRunes != null)
        {
            OnHitContext onHit = new OnHitContext
            {
                Target = this,
                Damage = amount,
                SpellTags = hit.Spell.SpellTags,
                Attribution = hit.Attribution
            };

            foreach (OnHitRune rune in hit.Spell.OnHitRunes)
            {
                // |= rather than ||= - every rune runs, since one that adds an effect of its own
                // still has work to do after a reaction has already claimed the Status.
                statusConsumed |= rune.OnHit(onHit);
            }
        }

        float healthBefore = Mathf.Max(CurrentHealth, 0f);
        CurrentHealth -= amount.Total;
        HealthBar.SetFraction(CurrentHealth / MaxHealth);
        OnDamageTaken(amount);
        RouteDamageNumber(hit, dealerShowedNumber);

        if (hit.SetsStatus)
        {
            if (statusConsumed)
            {
                Status = null;
            }
            else
            {
                Tag? dominantElement = amount.DominantElement;
                if (dominantElement.HasValue)
                {
                    Status = dominantElement;
                }
            }
        }

        if (hit.NotifiesDebuffs)
        {
            NotifyDebuffsOfDamage(hit);
        }

        if (CurrentHealth <= 0f)
        {
            Die(hit.RagdollImpulse);
        }

        CreditRitual(hit, Mathf.Min(amount.Total, healthBefore), _isDead);

        // Last, so anything these start - a swirl's burst, an after-hit rune's own hits, this enemy's
        // included - lands on a finished hit rather than one still resolving.
        if (hit.Spell.OnHitRunes != null)
        {
            AfterHitContext afterHit = new AfterHitContext
            {
                Target = this,
                Damage = amount,
                SpellTags = hit.Spell.SpellTags,
                Attribution = hit.Attribution,
                StatusBefore = statusBefore,
                Killed = _isDead
            };

            foreach (OnHitRune rune in hit.Spell.OnHitRunes)
            {
                rune.AfterHit(afterHit);
            }
        }

        if (hit.Spell.ResonancePulse != null)
        {
            Ritual.PulseResonantNeighbours(hit.Spell.ResonancePulse, this);
        }

        // A Status a reaction rune already spent isn't there to swirl.
        if (!statusConsumed)
        {
            Swirl.TryTrigger(this, hit, statusBefore);
        }
    }

    // A Health Link copy is the Soldier's doing, not the Ritual's.
    private static void CreditRitual(Hit hit, float effective, bool killed)
    {
        if (hit.Type != HitType.Mirrored && hit.Attribution.Source is Ritual ritual && IsInstanceValid(ritual))
        {
            ritual.Stats.RecordHit(effective, hit.Damage.Total - effective, killed);
        }
    }

    // Each player sees the numbers credited to them. A hit with no player behind it counts as the
    // host's.
    private void RouteDamageNumber(Hit hit, bool dealerShowedNumber)
    {
        PlayerId credited = hit.Attribution.Player ?? PlayerProfiles.HostId;

        if (credited == PlayerProfiles.LocalId)
        {
            ShowDamageNumber(hit.Damage);
        }
        else if (!dealerShowedNumber && NetworkSession.Instance.IsSeatFilled(credited))
        {
            RpcId(NetworkSession.Instance.PeerOf(credited), MethodName.ReceiveDamageNumber, hit.Damage.ToNetwork());
        }
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveDamageNumber(float[] damage)
    {
        ShowDamageNumber(DamagePacket.FromNetwork(damage));
    }

    // Kills outright with an ordinary hit for whatever health is left, so death, kill credit and the
    // damage number all go the usual way. Host only. Unmarked, so it sets no Status of its own.
    public void Execute(Attribution attribution, Tag element)
    {
        DamagePacket damage = new DamagePacket { IsTrueDamage = true };
        damage.AddElement(element, CurrentHealth);
        TakeDamage(new Hit { Damage = damage, Attribution = attribution, Type = HitType.Unmarked });
    }

    // Sets this enemy's Status outright, for something that marks it without landing a hit. Host only,
    // like the ground effects that call it.
    public void SetStatus(Tag element)
    {
        Status = element;
    }

    // Takes this enemy's Status outright, for something that spends it without landing a hit.
    public Tag? RemoveStatus()
    {
        Tag? removed = Status;
        Status = null;

        if (removed.HasValue && !IsSimulatedHere)
        {
            RpcId(1, MethodName.ReceiveStatusRemoved);
        }

        return removed;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveStatusRemoved()
    {
        if (IsSimulatedHere && NetworkSession.Instance.SenderSeat != default)
        {
            Status = null;
        }
    }

    // Backwards, like TickDebuffs: a hook can reach back into this list, and this can itself run
    // from inside TickDebuffs when a damage-over-time tick is the hit that landed.
    private void NotifyDebuffsOfDamage(Hit hit)
    {
        for (int i = _debuffs.Count - 1; i >= 0; i--)
        {
            _debuffs[i].OnHostDamaged(this, hit);
        }
    }

    // False when the incoming debuff lost to one already in its slot, so a caller can tell whether
    // its effect actually landed. At most one occupant per slot, since every add resolves.
    public bool ApplyDebuff(Debuff debuff)
    {
        // A copy holds no debuffs; the host applies it, and it's assumed to land.
        if (!IsSimulatedHere)
        {
            Godot.Collections.Array data = debuff.ToNetwork();

            if (data != null)
            {
                RpcId(1, MethodName.ReceiveDebuff, data);
            }

            return true;
        }

        debuff.OnApplying(this);

        for (int i = 0; i < _debuffs.Count; i++)
        {
            if (!debuff.OccupiesSameSlotAs(_debuffs[i]))
            {
                continue;
            }

            if (debuff.ResolveAgainst(_debuffs[i]) == DebuffApplication.Discard)
            {
                return false;
            }

            _debuffs.RemoveAt(i);
            _debuffs.Add(debuff);
            return true;
        }

        _debuffs.Add(debuff);
        return true;
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDebuff(Godot.Collections.Array data)
    {
        PlayerId sender = NetworkSession.Instance.SenderSeat;

        if (_isDead || !IsSimulatedHere || sender == default)
        {
            return;
        }

        Debuff debuff = Debuff.FromNetwork(data, AttributionFor(sender));

        if (debuff != null)
        {
            ApplyDebuff(debuff);
        }
    }

    public void RequestHealthLink(Enemy partner, float duration, float mirrorPercent)
    {
        RpcId(1, MethodName.ReceiveHealthLink, GetPathTo(partner), duration, mirrorPercent);
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveHealthLink(NodePath partnerPath, float duration, float mirrorPercent)
    {
        if (IsSimulatedHere && NetworkSession.Instance.SenderSeat != default)
        {
            HealthLinkDebuff.Link(this, GetNodeOrNull<Enemy>(partnerPath), duration, mirrorPercent);
        }
    }

    // What a sender's hit or debuff is credited to - their seat, and their Sentinel on this machine.
    private Attribution AttributionFor(PlayerId seat) =>
        new Attribution { Player = seat, Source = Level.Sentinels.GetValueOrDefault(seat) };

    public void RemoveDebuff(Debuff debuff)
    {
        _debuffs.Remove(debuff);
    }

    // The Sentinel a taunt is forcing this enemy to attack, if any.
    public Sentinel ForcedTarget
    {
        get
        {
            foreach (Debuff debuff in _debuffs)
            {
                if (debuff is TauntedDebuff taunt && taunt.ActiveTaunter != null)
                {
                    return taunt.ActiveTaunter;
                }
            }

            return null;
        }
    }

    // Every debuff's SpeedMultiplier folded together. Multiplicative, so two independent slows
    // compound toward zero rather than summing past a full stop.
    public float DebuffSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;

            foreach (Debuff debuff in _debuffs)
            {
                if (!debuff.IsRecovering)
                {
                    multiplier *= debuff.SpeedMultiplier;
                }
            }

            return multiplier;
        }
    }

    // Universal death handling: unregister, hide the health bar, stop simulating, disable
    // collision - and where the enemy is simulated, pay out rewards/loot and free after a delay. A
    // subclass with more to do on death (ragdoll physics) overrides this and calls base.Die() first -
    // see WalkingEnemy. A subclass that shouldn't die at all (a training dummy) overrides this and
    // skips base entirely.
    public virtual void Die(Vector3 ragdollImpulse)
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        Level.Enemies.Unregister(this);
        HealthBar.Visible = false;
        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        if (HeadCollision != null)
        {
            HeadCollision.Disabled = true;
        }

        // The ragdoll takes over from wherever a copy is being shown.
        GetNodeOrNull<MotionPlayback>("Motion")?.Stop();

        // A copy only shows the death. The host pays out, tells the copies, and later frees the body,
        // which removes it everywhere - its synchronizer going first, so no update arrives for a body
        // another machine has already removed.
        if (!IsSimulatedHere)
        {
            return;
        }

        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, ManaReward, GoldReward, GlobalPosition, DropChance, LootRolls);
        GetNodeOrNull("MultiplayerSynchronizer")?.QueueFree();
        Rpc(MethodName.ReceiveDeath, ragdollImpulse);
        GetTree().CreateTimer(RagdollCleanupDelay).Timeout += QueueFree;
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveDeath(Vector3 ragdollImpulse)
    {
        Die(ragdollImpulse);
    }

    // A non-lethal shove from outside - a pylon, a shield slam - in kg·m/s. A copy passes it to the
    // host.
    public void ApplyKnockback(Vector3 impulse)
    {
        if (IsSimulatedHere)
        {
            ResolveKnockback(impulse);
        }
        else
        {
            RpcId(1, MethodName.ReceiveKnockback, impulse);
        }
    }

    [Rpc(MultiplayerApi.RpcMode.AnyPeer, TransferMode = MultiplayerPeer.TransferModeEnum.Reliable)]
    private void ReceiveKnockback(Vector3 impulse)
    {
        if (IsSimulatedHere && NetworkSession.Instance.SenderSeat != default)
        {
            ResolveKnockback(impulse);
        }
    }

    // No-op for an enemy that can't be moved, like the training dummy.
    protected virtual void ResolveKnockback(Vector3 impulse)
    {
    }

    // No-op by default — only an enemy with a ragdoll-capable rig (WalkingEnemy) does anything here.
    public virtual void ApplyExtraRagdollImpulse(Vector3 impulse)
    {
    }

    // For a subclass that wants to know it just took damage (TrainingDummy's DPS readout). Runs only
    // where the enemy is simulated.
    protected virtual void OnDamageTaken(DamagePacket amount)
    {
    }

    // A floating number colored by the hit's dominant element.
    private void ShowDamageNumber(DamagePacket amount)
    {
        bool enabled = amount.Source == DamageSource.Weapon ? DebugSettings.ShowWeaponDamageNumbers : DebugSettings.ShowRitualDamageNumbers;
        if (!enabled || FloatingTextScene == null)
        {
            return;
        }

        // An infused Wind hit - Wind plus exactly one other element - shows each part in its own
        // color. Every other mix stays one number, so many-element hits don't bury the screen.
        if (TrySplitInfusedWind(amount, out DamagePacket wind, out DamagePacket infusion))
        {
            SpawnDamageNumber(wind);
            SpawnDamageNumber(infusion);
            return;
        }

        SpawnDamageNumber(amount);
    }

    private void SpawnDamageNumber(DamagePacket amount)
    {
        (Color fill, Color outline) = DamageTypeColors.GetForDamage(amount);

        FloatingCombatText text = FloatingTextScene.Instantiate<FloatingCombatText>();
        Level.LevelRoot.AddChild(text);
        text.Show(GetJitteredCenterMass(0.2f), amount.Total, fill, outline, amount.IsCrit);
    }

    private static bool TrySplitInfusedWind(DamagePacket amount, out DamagePacket wind, out DamagePacket infusion)
    {
        wind = default;
        infusion = default;
        Tag? element = amount.DominantElement;

        if (amount.WindDamage <= 0f || amount.PhysicalDamage > 0f || element == null)
        {
            return false;
        }

        // Exactly one other element: everything that isn't Wind is that one.
        float rest = amount.Total - amount.WindDamage;

        if (!Mathf.IsEqualApprox(amount.Get(element.Value), rest))
        {
            return false;
        }

        wind = new DamagePacket { Origin = amount.Origin, Source = amount.Source, IsCrit = amount.IsCrit, WindDamage = amount.WindDamage };
        infusion = new DamagePacket { Origin = amount.Origin, Source = amount.Source, IsCrit = amount.IsCrit };
        infusion.AddElement(element.Value, rest);
        return true;
    }

    private void TickDebuffs(float delta)
    {
        for (int i = _debuffs.Count - 1; i >= 0; i--)
        {
            if (!_debuffs[i].Tick(this, delta))
            {
                _debuffs.RemoveAt(i);
            }
        }
    }

    // TODO placeholder: remove this and StatusMesh once there is a real way to display
    // an enemy's element and ailments.
    private void UpdateElementalVisual(float delta)
    {
        bool isBurning = HasDebuff(DebuffType.Burn | DebuffType.Immolation);
        bool isFrostburning = HasDebuff(DebuffType.Frostburn);

        if (isBurning || isFrostburning)
        {
            _ailmentFlashTimer += delta;
            if (_ailmentFlashTimer >= 1f)
            {
                _ailmentFlashTimer -= 1f;
            }
        }
        else
        {
            _ailmentFlashTimer = 0f;
        }

        bool isFlashing = (isBurning || isFrostburning) && _ailmentFlashTimer < AilmentFlashDuration;

        Material tint = null;
        if (isFlashing)
        {
            // An enemy both burning and frostburning flashes red; the two rarely overlap, since one
            // consumes the Fire Status the other is set off by.
            tint = isBurning ? _burnFlashMaterial : _frostburnFlashMaterial;
        }
        else if (Status.HasValue && _statusMaterials.TryGetValue(Status.Value, out StandardMaterial3D statusMaterial))
        {
            tint = statusMaterial;
        }

        // Only write on an actual transition. This runs every physics frame for every living enemy
        // and the tint is usually unchanged, so reassigning unconditionally would cost a property
        // write per enemy per frame for nothing.
        if (tint == _appliedTint)
        {
            return;
        }

        _appliedTint = tint;

        // Swapped in rather than drawn as a material_overlay: an opaque overlay shares the surface's
        // depth, so whether it shows depends on draw order, and a lit one reliably loses.
        if (StatusMesh != null)
        {
            StatusMesh.MaterialOverride = tint ?? _statusMeshRestMaterial;
        }
    }
}
