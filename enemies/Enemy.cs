using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class Enemy : CharacterBody3D, IDamageable
{
    [Export] public float Speed = 3.5f;
    [Export] public float RotationSpeed = 10f;
    [Export] public float WaypointArrivalDistance = 0.5f;
    [Export] public float PlayerCheckInterval = 0.25f;
    [Export] public float LeashRange = 6f;
    [Export] public float AttackRange = 1.5f;
    [Export] public float AttackDamage = 10f;
    [Export] public float AttackInterval = 1f;
    [Export] public NavigationAgent3D NavAgent { get; private set; }
    [Export] public Skeleton3D Skeleton { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }
    [Export] public PhysicalBoneSimulator3D BoneSimulator { get; private set; }
    [Export] public PhysicalBone3D RootPhysicalBone { get; private set; }

    [Export] public float WalkCycleFrequency = 6.0f;
    [Export] public float LegSwingAmplitude = 0.5f;
    [Export] public float ArmSwingAmplitude = 0.2f;
    [Export] public float RagdollCleanupDelay = 2f;
    [Export] public float MaxHealth = 100f;
    [Export] public float CenterMassHeight = 1f;
    [Export] public int ManaReward = 1;
    [Export] public int GoldReward = 1;
    [Export] public HealthBar3D HealthBar { get; private set; }

    // TODO placeholder: remove once there's a real way to display an enemy's element/ailments (Section 15 iconography).
    private static readonly Dictionary<Tag, StandardMaterial3D> _statusMaterials = new Dictionary<Tag, StandardMaterial3D>
    {
        { Tag.Fire, CreateOverlayMaterial(new Color(1f, 0.5f, 0f)) },
        { Tag.Water, CreateOverlayMaterial(new Color(0.05f, 0.15f, 0.55f)) },
        { Tag.Cold, CreateOverlayMaterial(new Color(0.75f, 0.95f, 1f)) },
        { Tag.Lightning, CreateOverlayMaterial(new Color(1f, 0.9f, 0.1f)) }
    };

    private static readonly StandardMaterial3D _burnFlashMaterial = CreateOverlayMaterial(new Color(0.5f, 0f, 0f));
    private const float BurnFlashDuration = 0.1f;

    private static StandardMaterial3D CreateOverlayMaterial(Color color)
    {
        return new StandardMaterial3D
        {
            AlbedoColor = color,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded
        };
    }

    private enum MovementMode { FollowingLane, ChasingPlayer, RejoiningLane }

    private const string SpawnerGroup = "enemy_spawners";

    private ProceduralWalkAnimator _walkAnimator;
    private Lane _lane;
    private int _waypointIndex;
    private MovementMode _movementMode = MovementMode.FollowingLane;
    private float _playerCheckTimer;
    private float _attackCooldown;
    private bool _isDead;
    private bool _hasFacedInitialDirection;
    private float _currentHealth;
    private readonly List<Ailment> _ailments = new List<Ailment>();
    // TODO placeholder: remove alongside StatusMaterials/BurnFlashMaterial above.
    private readonly List<MeshInstance3D> _meshInstances = new List<MeshInstance3D>();
    private float _burnFlashTimer;

    public bool IsDead => _isDead;
    public bool IsDestroyed => _isDead;
    public Tag? Status { get; private set; }

    // Remaining travel distance to the objective. Comparable across lanes, since every lane
    // converges on the same objective — that's what makes it usable as a targeting key.
    public float DistanceToGoal { get; private set; } = float.MaxValue;
    public Vector3 CenterMass => GlobalPosition + Vector3.Up * CenterMassHeight;

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
        _walkAnimator = new ProceduralWalkAnimator(Skeleton, WalkCycleFrequency, LegSwingAmplitude, ArmSwingAmplitude);

        _currentHealth = MaxHealth;

        // Group membership is for editor / remote-debugger inspection only — code paths that need
        // living enemies go through EnemyRegistry, which excludes ragdolls.
        AddToGroup("enemies");
        EnemyRegistry.Register(this);

        HealthBar.SetFraction(1f);
        HealthBar.Visible = DebugSettings.ShowEnemyHealthBars;

        SetPhysicalBonesCollision(0, 0);
        CollectMeshInstances(Skeleton);
    }

    // Safety net for enemies freed without dying — scene reload, in particular. Unregistering
    // twice is a no-op, so the Die() path unregistering first is unaffected.
    public override void _ExitTree()
    {
        EnemyRegistry.Unregister(this);
    }

    public void SetLane(Lane lane)
    {
        _lane = lane;
        _waypointIndex = 0;
    }

    private void UpdateDistanceToGoal()
    {
        DistanceToGoal = _lane.DistanceToGoal(_waypointIndex, GlobalPosition);
    }

    // Explicit implementation: the overload below carries extra optional parameters, so it can't
    // satisfy the interface signature on its own.
    void IDamageable.TakeDamage(DamagePacket damage, Vector3 impulse) => TakeDamage(damage, impulse);

    public void TakeDamage(DamagePacket amount, Vector3 ragdollImpulse = default, bool updateStatus = true, ReactionRune reaction = null)
    {
        if (_isDead)
        {
            return;
        }

        bool reactionTriggered = reaction?.OnHit(this, amount) ?? false;

        _currentHealth -= amount.Total;
        HealthBar.SetFraction(_currentHealth / MaxHealth);

        if (updateStatus)
        {
            if (reactionTriggered)
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

        if (_currentHealth <= 0f)
        {
            Die(ragdollImpulse);
        }
    }

    public void ApplyAilment(Ailment ailment)
    {
        if (ailment is BurnAilment newBurn)
        {
            BurnAilment existingBurn = _ailments.Find(a => a is BurnAilment) as BurnAilment;
            if (existingBurn != null)
            {
                newBurn.MergeFrom(existingBurn);
                _ailments.Remove(existingBurn);
            }
        }

        _ailments.Add(ailment);
    }

    public void Die(Vector3 ragdollImpulse)
    {
        if (_isDead)
        {
            return;
        }

        _isDead = true;
        EnemyRegistry.Unregister(this);
        HealthBar.Visible = false;
        HealthBar.SetProcess(false);
        EventBus.Instance.EmitSignal(EventBus.SignalName.EnemyKilled, ManaReward, GoldReward);

        SetPhysicsProcess(false);
        BodyCollision.Disabled = true;

        SetPhysicalBonesCollision(PhysicsLayers.Ragdoll, PhysicsLayers.World);

        BoneSimulator.PhysicalBonesStartSimulation();
        _ = ApplyRagdollImpulseNextPhysicsFrame(ragdollImpulse);

        GetTree().CreateTimer(RagdollCleanupDelay).Timeout += QueueFree;
    }

    private void SetPhysicalBonesCollision(uint layer, uint mask)
    {
        foreach (Node child in BoneSimulator.GetChildren())
        {
            if (child is PhysicalBone3D physicalBone)
            {
                physicalBone.CollisionLayer = layer;
                physicalBone.CollisionMask = mask;
            }
        }
    }

    private async Task ApplyRagdollImpulseNextPhysicsFrame(Vector3 impulse)
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        RootPhysicalBone.ApplyCentralImpulse(impulse);
    }

    public void ApplyExtraRagdollImpulse(Vector3 impulse)
    {
        if (!_isDead)
        {
            return;
        }

        _ = ApplyRagdollImpulseNextPhysicsFrame(impulse);
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateDistanceToGoal();
        TickAilments((float)delta);
        UpdateElementalVisual((float)delta); // TODO placeholder: remove once there's a real way to display this.
        UpdatePlayerLeash((float)delta);

        switch (_movementMode)
        {
            case MovementMode.ChasingPlayer:
                ProcessChasingPlayer(delta);
                break;
            case MovementMode.RejoiningLane:
                ProcessRejoiningLane(delta);
                break;
            default:
                ProcessLaneFollowing(delta);
                break;
        }
    }

    private void ProcessLaneFollowing(double delta)
    {
        if (_waypointIndex < _lane.Waypoints.Count && GlobalPosition.DistanceTo(_lane.Waypoints[_waypointIndex]) <= WaypointArrivalDistance)
        {
            _waypointIndex++;
        }

        if (_waypointIndex >= _lane.Waypoints.Count)
        {
            Stop(delta);
            return;
        }

        Move((_lane.Waypoints[_waypointIndex] - GlobalPosition).Normalized(), delta);
    }

    // Leash/chase state is decided on an interval (mirrors Ritual.TargetSearchInterval) rather
    // than every frame — a cheap distance check, not a live per-enemy nav query.
    private void UpdatePlayerLeash(float delta)
    {
        _playerCheckTimer -= delta;
        if (_playerCheckTimer > 0f || PlayerCharacter.ActiveCombatant == null)
        {
            return;
        }

        _playerCheckTimer = PlayerCheckInterval;

        float distanceToPlayer = GlobalPosition.DistanceTo(PlayerCharacter.ActiveCombatant.GlobalPosition);
        bool withinLeash = distanceToPlayer <= LeashRange;

        if (withinLeash && _movementMode != MovementMode.ChasingPlayer)
        {
            _movementMode = MovementMode.ChasingPlayer;
        }
        else if (!withinLeash && _movementMode == MovementMode.ChasingPlayer)
        {
            BeginRejoin();
            return;
        }

        if (_movementMode == MovementMode.ChasingPlayer)
        {
            NavAgent.TargetPosition = PlayerCharacter.ActiveCombatant.GlobalPosition;
        }
    }

    // Give-up-and-rejoin across lanes (README §15): one real pathfind to whichever waypoint is
    // nearest across every lane's spawner, not just the one this enemy started on.
    private void BeginRejoin()
    {
        (Lane lane, int waypointIndex) = FindNearestWaypointAcrossLanes();

        if (lane == null)
        {
            _movementMode = MovementMode.FollowingLane;
            return;
        }

        _lane = lane;
        _waypointIndex = waypointIndex;
        NavAgent.TargetPosition = lane.Waypoints[waypointIndex];
        _movementMode = MovementMode.RejoiningLane;
    }

    private (Lane lane, int waypointIndex) FindNearestWaypointAcrossLanes()
    {
        Lane bestLane = null;
        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        foreach (Node node in GetTree().GetNodesInGroup(SpawnerGroup))
        {
            if (node is not EnemySpawner spawner || spawner.Lane == null)
            {
                continue;
            }

            // Never rejoin behind where this enemy already progressed on its own lane — only
            // other lanes, which it never walked, are fair game at any point along their length.
            int startIndex = spawner.Lane == _lane ? _waypointIndex : 0;

            for (int i = startIndex; i < spawner.Lane.Waypoints.Count; i++)
            {
                float distance = GlobalPosition.DistanceTo(spawner.Lane.Waypoints[i]);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestLane = spawner.Lane;
                    bestIndex = i;
                }
            }
        }

        return (bestLane, bestIndex);
    }

    private void ProcessRejoiningLane(double delta)
    {
        if (NavAgent.IsNavigationFinished())
        {
            _movementMode = MovementMode.FollowingLane;
            ProcessLaneFollowing(delta);
            return;
        }

        Move((NavAgent.GetNextPathPosition() - GlobalPosition).Normalized(), delta);
    }

    private void ProcessChasingPlayer(double delta)
    {
        float distanceToPlayer = GlobalPosition.DistanceTo(PlayerCharacter.ActiveCombatant.GlobalPosition);
        _attackCooldown -= (float)delta;

        if (distanceToPlayer <= AttackRange)
        {
            Stop(delta);
            RotateTowardsMovement((PlayerCharacter.ActiveCombatant.GlobalPosition - GlobalPosition).Normalized(), delta);

            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;
                PlayerCharacter.ActiveCombatant.TakeDamage(new DamagePacket { PhysicalDamage = AttackDamage });
            }

            return;
        }

        if (NavAgent.IsNavigationFinished())
        {
            Stop(delta);
            return;
        }

        Move((NavAgent.GetNextPathPosition() - GlobalPosition).Normalized(), delta);
    }

    private void Stop(double delta)
    {
        Velocity = Vector3.Zero;
        MoveAndSlide();
        _walkAnimator.Animate(0.0, delta);
    }

    private void Move(Vector3 direction, double delta)
    {
        Velocity = direction * Speed;
        MoveAndSlide();

        RotateTowardsMovement(direction, delta);
        _walkAnimator.Animate(1.0, delta);
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

    // TODO placeholder block: remove this method and CollectMeshInstances once there's a real way to display an enemy's element/ailments.
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

        foreach (MeshInstance3D meshInstance in _meshInstances)
        {
            meshInstance.MaterialOverlay = overlay;
        }
    }

    private void CollectMeshInstances(Node node)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is MeshInstance3D meshInstance)
            {
                _meshInstances.Add(meshInstance);
            }

            CollectMeshInstances(child);
        }
    }

    private static Basis? ComputeFacingBasis(Vector3 direction)
    {
        Vector3 flatDirection = new Vector3(direction.X, 0f, direction.Z);
        if (flatDirection.LengthSquared() < 0.0001f)
        {
            return null;
        }

        return Basis.LookingAt(flatDirection, Vector3.Up);
    }

    public void SetInitialFacing(Vector3 direction)
    {
        Basis? facingBasis = ComputeFacingBasis(direction);
        if (facingBasis.HasValue)
        {
            Basis = facingBasis.Value;
            _hasFacedInitialDirection = true;
        }
    }

    private void RotateTowardsMovement(Vector3 direction, double delta)
    {
        Basis? targetBasis = ComputeFacingBasis(direction);
        if (!targetBasis.HasValue)
        {
            return;
        }

        if (!_hasFacedInitialDirection)
        {
            _hasFacedInitialDirection = true;
            Basis = targetBasis.Value;
            return;
        }

        Basis = Basis.Slerp(targetBasis.Value, Mathf.Min(1f, (float)delta * RotationSpeed));
    }
}
