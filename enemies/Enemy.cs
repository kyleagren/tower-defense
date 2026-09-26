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
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 100f;
    [Export] public float CenterMassHeight = 1f;
    // Kilograms. Knockback divides by it, so a heavy enemy moves less but is never made immune.
    [Export] public float Mass = 80f;
    [Export] public int ManaReward = 1;
    [Export] public int GoldReward = 1;
    [Export] public float DropChance = 0.15f;
    [Export] public int LootRolls = 1;
    [Export] public HealthBar3D HealthBar { get; private set; }
    [Export] public PackedScene FloatingTextScene { get; private set; }

    // The elements that can actually be a Status - the same four DamagePacket.DominantElement
    // considers, since Wind is consumer-only and never becomes one.
    private static readonly Tag[] _statusElements = { Tag.Fire, Tag.Water, Tag.Cold, Tag.Lightning };

    // TODO placeholder: remove once there's a real way to display an enemy's element/ailments.
    // Colors come from DamageTypeColors rather than a second hardcoded palette here, so the tint an
    // enemy shows and the color its damage numbers use can't drift apart.
    private static readonly Dictionary<Tag, StandardMaterial3D> _statusMaterials = BuildStatusMaterials();

    private static readonly StandardMaterial3D _burnFlashMaterial = CreateOverlayMaterial(new Color(0.5f, 0f, 0f));
    private const float BurnFlashDuration = 0.1f;

    private static Dictionary<Tag, StandardMaterial3D> BuildStatusMaterials()
    {
        Dictionary<Tag, StandardMaterial3D> materials = new Dictionary<Tag, StandardMaterial3D>();

        foreach (Tag element in _statusElements)
        {
            materials[element] = CreateOverlayMaterial(DamageTypeColors.GetStatusOverlay(element));
        }

        return materials;
    }

    private static StandardMaterial3D CreateOverlayMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }

    private bool _isDead;
    private readonly List<Debuff> _debuffs = new List<Debuff>();
    // TODO placeholder: remove alongside StatusMaterials/BurnFlashMaterial above.
    private readonly List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
    private Material _appliedOverlay;
    private float _burnFlashTimer;

    public bool IsDead => _isDead;
    public bool IsDestroyed => _isDead;
    public Tag? Status { get; private set; }
    public IReadOnlyList<Debuff> Debuffs => _debuffs;

    // Remaining travel distance to the objective. Only meaningful for enemies that follow a lane
    // (WalkingEnemy updates it); anything else just leaves it at the default, which keeps it from
    // ever winning a ClosestToGoal targeting comparison.
    public float DistanceToGoal { get; protected set; } = float.MaxValue;

    protected float CurrentHealth { get; set; }

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

    private bool IsWeakPointMarker(MeshInstance3D meshInstance) =>
        meshInstance == HeadWeakPoint || meshInstance == BodyWeakPoint || meshInstance == ArmLWeakPoint ||
        meshInstance == ArmRWeakPoint || meshInstance == LegLWeakPoint || meshInstance == LegRWeakPoint;

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
        Level = LevelContext.Current;

        // Group membership is for editor / remote-debugger inspection only — code paths that need
        // living enemies go through LevelContext.Enemies, which excludes ragdolls.
        AddToGroup(Groups.Enemies);
        Level.Enemies.Register(this);

        HealthBar.SetFraction(1f);
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;

        CollectMeshInstances();

        if (Level.IsWeakPointVisorActive)
        {
            ExposeWeakPoints(Level.WeakPointVisorCount);
        }
    }

    // Safety net for enemies freed without dying. Unregistering twice is a no-op, so the Die() path
    // unregistering first is unaffected.
    public override void _ExitTree()
    {
        Level.Enemies.Unregister(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        TickDebuffs((float)delta);
        UpdateElementalVisual((float)delta); // TODO placeholder: remove once there's a real way to display this.
    }

    public void TakeDamage(Hit hit)
    {
        if (_isDead)
        {
            return;
        }

        DamagePacket amount = hit.Damage;
        bool statusConsumed = false;

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

        CurrentHealth -= amount.Total;
        HealthBar.SetFraction(CurrentHealth / MaxHealth);
        OnDamageTaken(amount);

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
    }

    // Takes this enemy's Status outright, for something that spends it without landing a hit.
    public Tag? RemoveStatus()
    {
        Tag? removed = Status;
        Status = null;
        return removed;
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
                multiplier *= debuff.SpeedMultiplier;
            }

            return multiplier;
        }
    }

    // Universal death handling: unregister, hide the health bar, stop simulating, disable
    // collision, pay out rewards/loot, free after a delay. A subclass with more to do on death
    // (ragdoll physics) overrides this and calls base.Die() first — see WalkingEnemy. A subclass
    // that shouldn't die at all (a training dummy) overrides this and skips base entirely.
    public virtual void Die(Vector3 ragdollImpulse)
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        Level.Enemies.Unregister(this);
        HealthBar.Visible = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, ManaReward, GoldReward, GlobalPosition, DropChance, LootRolls);

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        GetTree().CreateTimer(RagdollCleanupDelay).Timeout += QueueFree;
    }

    // A non-lethal shove from outside - a pylon, a shield slam - in kg·m/s. No-op for an enemy that
    // can't be moved, like the training dummy.
    public virtual void ApplyKnockback(Vector3 impulse)
    {
    }

    // No-op by default — only an enemy with a ragdoll-capable rig (WalkingEnemy) does anything here.
    public virtual void ApplyExtraRagdollImpulse(Vector3 impulse)
    {
    }

    // Spawns a floating damage number for this hit, colored by its dominant element. Virtual so a
    // subclass can also hook into "I just took damage" for its own purposes (TrainingDummy uses
    // this for DPS tracking) without overriding all of TakeDamage.
    protected virtual void OnDamageTaken(DamagePacket amount)
    {
        bool enabled = amount.Source == DamageSource.Weapon ? DebugSettings.ShowWeaponDamageNumbers : DebugSettings.ShowTowerDamageNumbers;
        if (!enabled || FloatingTextScene == null)
        {
            return;
        }

        (Color fill, Color outline) = DamageTypeColors.GetForDamage(amount);

        FloatingCombatText text = FloatingTextScene.Instantiate<FloatingCombatText>();
        GetTree().CurrentScene.AddChild(text);
        text.Show(GetJitteredCenterMass(0.2f), amount.Total, fill, outline, amount.IsCrit);
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

    // TODO placeholder: remove this and CollectMeshInstances once there is a real way to display
    // an enemy's element and ailments.
    private void UpdateElementalVisual(float delta)
    {
        bool isBurning = _debuffs.Exists(debuff => debuff is BurnAilment);

        if (isBurning)
        {
            _burnFlashTimer += delta;
            if (_burnFlashTimer >= 1f)
            {
                _burnFlashTimer -= 1f;
            }
        }
        else
        {
            _burnFlashTimer = 0f;
        }

        bool isFlashing = isBurning && _burnFlashTimer < BurnFlashDuration;

        Material overlay = null;
        if (isFlashing)
        {
            overlay = _burnFlashMaterial;
        }
        else if (Status.HasValue && _statusMaterials.TryGetValue(Status.Value, out StandardMaterial3D statusMaterial))
        {
            overlay = statusMaterial;
        }

        // Only write on an actual transition. This runs every physics frame for every living enemy
        // and the overlay is usually unchanged, so reassigning unconditionally would cost a property
        // write per mesh per enemy per frame for nothing.
        if (overlay == _appliedOverlay)
        {
            return;
        }

        _appliedOverlay = overlay;

        foreach (MeshInstance3D meshInstance in _meshInstances)
        {
            meshInstance.MaterialOverlay = overlay;
        }
    }

    private void CollectMeshInstances()
    {
        // owned: false is required to walk into instanced sub-scenes rather than stopping at their
        // roots. That also reaches HealthBar's own Fill/Empty quads, which must be excluded: they
        // would otherwise take the body's elemental status overlay, whose material doesn't share
        // their no_depth_test/cull_mode setup, and render as a stray second bar.
        foreach (Node node in FindChildren("*", nameof(MeshInstance3D), recursive: true, owned: false))
        {
            if (node is MeshInstance3D meshInstance && !HealthBar.IsAncestorOf(meshInstance) && !IsWeakPointMarker(meshInstance))
            {
                _meshInstances.Add(meshInstance);
            }
        }
    }
}
