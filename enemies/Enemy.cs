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
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 100f;
    [Export] public float CenterMassHeight = 1f;
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
    private readonly List<Ailment> _ailments = new List<Ailment>();
    // TODO placeholder: remove alongside StatusMaterials/BurnFlashMaterial above.
    private readonly List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
    private Material _appliedOverlay;
    private float _burnFlashTimer;

    public bool IsDead => _isDead;
    public bool IsDestroyed => _isDead;
    public Tag? Status { get; private set; }
    public IReadOnlyList<Ailment> Ailments => _ailments;

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

    // Level-wide rather than per-instance: Weak-Point Visor marks every enemy regardless of who
    // cast it, so an enemy spawning mid-duration (WaveController, EnemySpawner) needs a static flag
    // to check in _Ready rather than relying on the ability re-scanning EnemyRegistry, which only
    // ever sees enemies that already existed at activation time.
    public static bool IsWeakPointVisorActive { get; private set; }
    private static int _activeWeakPointVisorCount;

    public static void ActivateWeakPointVisor(int count)
    {
        IsWeakPointVisorActive = true;
        _activeWeakPointVisorCount = count;

        foreach (Enemy enemy in EnemyRegistry.Active)
        {
            enemy.ExposeWeakPoints(count);
        }
    }

    public static void DeactivateWeakPointVisor()
    {
        IsWeakPointVisorActive = false;

        foreach (Enemy enemy in EnemyRegistry.Active)
        {
            enemy.ClearWeakPoints();
        }
    }

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

        // Group membership is for editor / remote-debugger inspection only — code paths that need
        // living enemies go through EnemyRegistry, which excludes ragdolls.
        AddToGroup(Groups.Enemies);
        EnemyRegistry.Register(this);

        HealthBar.SetFraction(1f);
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;

        CollectMeshInstances();

        if (IsWeakPointVisorActive)
        {
            ExposeWeakPoints(_activeWeakPointVisorCount);
        }
    }

    // Safety net for enemies freed without dying — scene reload, in particular. Unregistering
    // twice is a no-op, so the Die() path unregistering first is unaffected.
    public override void _ExitTree()
    {
        EnemyRegistry.Unregister(this);
    }

    public override void _PhysicsProcess(double delta)
    {
        TickAilments((float)delta);
        UpdateElementalVisual((float)delta); // TODO placeholder: remove once there's a real way to display this.
    }

    // Explicit implementation: the overload below carries extra optional parameters, so it can't
    // satisfy the interface signature on its own.
    void IDamageable.TakeDamage(DamagePacket damage, Vector3 impulse) => TakeDamage(damage, impulse);

    public void TakeDamage(DamagePacket amount, Vector3 ragdollImpulse = default, bool updateStatus = true,
        SpellHit spellHit = default, bool notifyAilments = true)
    {
        if (_isDead)
        {
            return;
        }

        bool statusConsumed = false;

        if (spellHit.OnHitRunes != null)
        {
            OnHitContext hit = new OnHitContext { Target = this, Damage = amount, SpellTags = spellHit.SpellTags };

            foreach (OnHitRune rune in spellHit.OnHitRunes)
            {
                // |= rather than ||= - every rune runs, since one that adds an effect of its own
                // still has work to do after a reaction has already claimed the Status.
                statusConsumed |= rune.OnHit(hit);
            }
        }

        CurrentHealth -= amount.Total;
        HealthBar.SetFraction(CurrentHealth / MaxHealth);
        OnDamageTaken(amount);

        if (updateStatus)
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

        if (notifyAilments)
        {
            NotifyAilmentsOfDamage(amount);
        }

        if (CurrentHealth <= 0f)
        {
            Die(ragdollImpulse);
        }
    }

    // Backwards, like TickAilments: a hook can reach back into this list, and this can itself run
    // from inside TickAilments when a damage-over-time tick is the hit that landed.
    private void NotifyAilmentsOfDamage(DamagePacket amount)
    {
        for (int i = _ailments.Count - 1; i >= 0; i--)
        {
            _ailments[i].OnHostDamaged(this, amount);
        }
    }

    // False when the incoming ailment lost to one already in its slot, so a caller can tell whether
    // its effect actually landed. At most one occupant per slot, since every add resolves.
    public bool ApplyAilment(Ailment ailment)
    {
        for (int i = 0; i < _ailments.Count; i++)
        {
            if (!ailment.OccupiesSameSlotAs(_ailments[i]))
            {
                continue;
            }

            if (ailment.ResolveAgainst(_ailments[i]) == AilmentApplication.Discard)
            {
                return false;
            }

            _ailments.RemoveAt(i);
            _ailments.Add(ailment);
            return true;
        }

        _ailments.Add(ailment);
        return true;
    }

    // Every ailment's SpeedMultiplier folded together. Multiplicative, so two independent slows
    // compound toward zero rather than summing past a full stop.
    public float AilmentSpeedMultiplier
    {
        get
        {
            float multiplier = 1f;

            foreach (Ailment ailment in _ailments)
            {
                multiplier *= ailment.SpeedMultiplier;
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
        EnemyRegistry.Unregister(this);
        HealthBar.Visible = false;
        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, ManaReward, GoldReward, GlobalPosition, DropChance, LootRolls);

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        GetTree().CreateTimer(RagdollCleanupDelay).Timeout += QueueFree;
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

    private void TickAilments(float delta)
    {
        for (int i = _ailments.Count - 1; i >= 0; i--)
        {
            if (!_ailments[i].Tick(this, delta))
            {
                _ailments.RemoveAt(i);
            }
        }
    }

    // TODO placeholder: remove this and CollectMeshInstances once there is a real way to display
    // an enemy's element and ailments.
    private void UpdateElementalVisual(float delta)
    {
        bool isBurning = _ailments.Exists(ailment => ailment is BurnAilment);

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
