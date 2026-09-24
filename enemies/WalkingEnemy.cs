using Godot;
using System.Threading.Tasks;

// An Enemy that follows a shared Lane's waypoints, can break off to chase the player or attack an
// Ice Wall, and ragdolls on death. Everything ground-pathing-specific lives here rather than on
// the base Enemy, so a future FlyingEnemy (or the dev-only TrainingDummy) doesn't have to carry
// navmesh/leash/wall-engagement logic it has no use for.
public partial class WalkingEnemy : Enemy
{
    [Export] public float BaseSpeed = 3.5f;
    [Export] public float RotationSpeed = 10f;
    [Export] public float WaypointArrivalDistance = 0.5f;
    [Export] public float EngagementCheckInterval = 0.25f;
    [Export] public float WallDetectionRange = 4f;
    [Export] public bool CanAttackRituals = false;
    [Export] public float RitualDetectionRange = 4f;
    [Export] public bool RitualsOutrankPlayer = false;
    [Export] public PackedScene ProjectileScene;
    [Export] public float LeashRange = 6f;
    [Export] public float AttackRange = 1.5f;
    [Export] public float AttackDamage = 10f;
    [Export] public float AttackInterval = 1f;
    [Export] public NavigationAgent3D NavAgent { get; private set; }
    [Export] public Skeleton3D Skeleton { get; private set; }
    [Export] public PhysicalBoneSimulator3D BoneSimulator { get; private set; }
    [Export] public PhysicalBone3D RootPhysicalBone { get; private set; }

    // Ailments fold in here rather than at the Move() call site, so anything reading Speed sees the
    // slowed value.
    public float Speed => BaseSpeed * AilmentSpeedMultiplier;

    [Export] public float WalkCycleFrequency = 6.0f;
    [Export] public float LegSwingAmplitude = 0.5f;
    [Export] public float ArmSwingAmplitude = 0.2f;

    private enum MovementMode { FollowingLane, ChasingPlayer, AttackingWall, AttackingRitual, RejoiningLane, ApproachingObjective }


    private ProceduralWalkAnimator _walkAnimator;
    private Lane _lane;
    private DefendableObjective _objective;
    private int _waypointIndex;
    private MovementMode _movementMode = MovementMode.FollowingLane;
    private float _engagementCheckTimer;
    private IceWall _targetWall;
    private Ritual _targetRitual;
    private Sentinel _targetSentinel;
    private float _attackCooldown;
    private bool _hasFacedInitialDirection;

    public override void _Ready()
    {
        base._Ready();

        _walkAnimator = new ProceduralWalkAnimator(Skeleton, WalkCycleFrequency, LegSwingAmplitude, ArmSwingAmplitude);

        NavAgent.AvoidanceEnabled = true;
        if (BodyCollision.Shape is CapsuleShape3D capsuleShape)
        {
            NavAgent.Radius = capsuleShape.Radius;
        }
        NavAgent.VelocityComputed += OnVelocityComputed;

        SetPhysicalBonesCollision(0, 0);
    }

    public void SetLane(Lane lane, DefendableObjective objective)
    {
        _lane = lane;
        _objective = objective;
        _waypointIndex = 0;
        NavAgent.TargetPosition = lane.Waypoints[0];
    }

    private void UpdateDistanceToGoal()
    {
        DistanceToGoal = _lane.DistanceToGoal(_waypointIndex, GlobalPosition);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);

        UpdateDistanceToGoal();

        // Engagement is decided on an interval (mirrors Ritual.TargetSearchInterval) rather than every
        // frame: towers, then players, then walls, in that priority order.
        _engagementCheckTimer -= (float)delta;

        if (_engagementCheckTimer <= 0f)
        {
            _engagementCheckTimer = EngagementCheckInterval;
            UpdateRitualEngagement();
            UpdatePlayerLeash();
            UpdateWallEngagement();
        }

        switch (_movementMode)
        {
            case MovementMode.ChasingPlayer:
                ProcessChasingPlayer(delta);
                break;
            case MovementMode.AttackingWall:
                ProcessAttackingWall(delta);
                break;
            case MovementMode.AttackingRitual:
                ProcessAttackingRitual(delta);
                break;
            case MovementMode.RejoiningLane:
                ProcessRejoiningLane(delta);
                break;
            case MovementMode.ApproachingObjective:
                ProcessApproachingObjective(delta);
                break;
            default:
                ProcessLaneFollowing(delta);
                break;
        }
    }

    private void ProcessLaneFollowing(double delta)
    {
        AdvanceWaypointIfPassed();

        if (_waypointIndex >= _lane.Waypoints.Count - 1)
        {
            BeginApproachingObjective();
            ProcessApproachingObjective(delta);
            return;
        }

        Move((_lane.Waypoints[_waypointIndex] - GlobalPosition).Normalized(), delta);
    }

    // Catches up _waypointIndex to wherever the enemy actually is, not just where it was heading
    // before being interrupted (e.g. a wall fight, or a player leash-chase). Uses a projection
    // onto the direction the path continues from the current waypoint, not raw distance, so it
    // still works when waypoints are sparse (a distance comparison would favor whichever waypoint
    // happens to be geometrically nearer, which is unreliable when the next one is far away).
    private void AdvanceWaypointIfPassed()
    {
        while (_waypointIndex < _lane.Waypoints.Count - 1)
        {
            Vector3 current = _lane.Waypoints[_waypointIndex];
            Vector3 next = _lane.Waypoints[_waypointIndex + 1];
            Vector3 forward = (next - current).Normalized();

            bool arrived = GlobalPosition.DistanceTo(current) <= WaypointArrivalDistance;
            bool alreadyPastIt = (GlobalPosition - current).Dot(forward) > 0f;

            if (!arrived && !alreadyPastIt)
            {
                break;
            }

            _waypointIndex++;
            NavAgent.TargetPosition = _lane.Waypoints[_waypointIndex];
        }
    }

    private void BeginApproachingObjective()
    {
        _movementMode = MovementMode.ApproachingObjective;

        if (_objective != null)
        {
            NavAgent.TargetDesiredDistance = _objective.ApproachRadius + AttackRange;
            NavAgent.TargetPosition = _objective.GlobalPosition;
        }
    }

    private void ProcessApproachingObjective(double delta)
    {
        if (_objective == null || _objective.IsDestroyed)
        {
            Stop(delta);
            return;
        }

        if (GlobalPosition.DistanceTo(_objective.GlobalPosition) - _objective.ApproachRadius <= AttackRange)
        {
            AttackObjective(delta);
            return;
        }

        if (NavAgent.IsNavigationFinished())
        {
            Stop(delta);
            return;
        }

        Move((NavAgent.GetNextPathPosition() - GlobalPosition).Normalized(), delta);
    }

    private void AttackObjective(double delta)
    {
        Stop(delta);

        _attackCooldown -= (float)delta;

        if (_attackCooldown <= 0f)
        {
            _attackCooldown = AttackInterval;
            DealDamage(_objective);
        }
    }

    // A cheap distance check, not a live per-enemy nav query.
    private void UpdatePlayerLeash()
    {
        // A ritual attack only blocks the leash for enemies where rituals outrank the player
        // (Sapper). For everything else, the player can interrupt an opportunistic tower attack
        // at any time.
        if (RitualsOutrankPlayer && _movementMode == MovementMode.AttackingRitual)
        {
            return;
        }

        Sentinel nearest = FindNearestTargetableSentinel(LeashRange);

        if (nearest != null)
        {
            _targetSentinel = nearest;
            _movementMode = MovementMode.ChasingPlayer;
            NavAgent.TargetPosition = nearest.GlobalPosition;
        }
        else if (_movementMode == MovementMode.ChasingPlayer)
        {
            _targetSentinel = null;
            BeginRejoin();
        }
    }

    // Every player's Sentinel counts, not just this machine's - the nearest live, active one in
    // range wins, so in co-op an enemy engages whoever actually walks up to it.
    private Sentinel FindNearestTargetableSentinel(float maxDistance)
    {
        Sentinel nearest = null;
        float bestDistance = maxDistance;

        foreach (Sentinel sentinel in Level.Sentinels.Values)
        {
            if (!sentinel.IsActive || sentinel.IsDead)
            {
                continue;
            }

            float distance = GlobalPosition.DistanceTo(sentinel.GlobalPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                nearest = sentinel;
            }
        }

        return nearest;
    }

    // Give-up-and-rejoin across lanes (README: Enemies): one real pathfind to whichever waypoint is
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
        NavAgent.TargetDesiredDistance = WaypointArrivalDistance;
        NavAgent.TargetPosition = lane.Waypoints[waypointIndex];
        _movementMode = MovementMode.RejoiningLane;
    }

    private (Lane lane, int waypointIndex) FindNearestWaypointAcrossLanes()
    {
        Lane bestLane = null;
        int bestIndex = -1;
        float bestDistance = float.MaxValue;

        foreach (Node node in GetTree().GetNodesInGroup(Groups.EnemySpawners))
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

    // Runs every tick regardless of current mode so a wall can interrupt lane-following or
    // objective-attacking, but never interrupts chasing the player, who always outranks it.
    private void UpdateWallEngagement()
    {
        if (_movementMode == MovementMode.ChasingPlayer || _movementMode == MovementMode.AttackingWall
            || _movementMode == MovementMode.AttackingRitual)
        {
            return;
        }

        IceWallSegment nearestSegment = FindNearestSegment(WallDetectionRange);
        if (nearestSegment == null)
        {
            return;
        }

        _targetWall = nearestSegment.Wall;
        _movementMode = MovementMode.AttackingWall;

        NavAgent.TargetDesiredDistance = AttackRange;
        NavAgent.TargetPosition = _targetWall.ClosestPointOnWall(GlobalPosition);
    }

    // One scan over the live segments, serving both the "is there any wall worth engaging?"
    // acquire case (wall left null, wide detection range) and the "which segment of the wall I've
    // already committed to can I actually hit?" case (wall set, attack range).
    private IceWallSegment FindNearestSegment(float maxDistance, IceWall wall = null)
    {
        IceWallSegment nearest = null;
        float bestDistance = maxDistance;

        foreach (IceWallSegment segment in Level.IceWallSegments.Active)
        {
            if (wall != null && segment.Wall != wall)
            {
                continue;
            }

            float distance = GlobalPosition.DistanceTo(segment.GlobalPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                nearest = segment;
            }
        }

        return nearest;
    }

    // Walks toward whichever point on the wall's own line is closest to this enemy's position at
    // the moment it engages, rather than a shared segment center every nearby enemy would
    // otherwise converge on. Which actual segment takes the damage is resolved separately, once
    // close enough to hit something.
    private void ProcessAttackingWall(double delta)
    {
        if (_targetWall == null || !IsInstanceValid(_targetWall) || _targetWall.IsDestroyed)
        {
            _targetWall = null;
            _movementMode = MovementMode.FollowingLane;
            ProcessLaneFollowing(delta);
            return;
        }

        IceWallSegment target = FindNearestSegment(AttackRange, _targetWall);

        if (target != null)
        {
            Stop(delta);
            RotateTowardsMovement((target.GlobalPosition - GlobalPosition).Normalized(), delta);

            _attackCooldown -= (float)delta;
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;
                DealDamage(target);
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

    // Opt-in per enemy (CanAttackRituals) - most enemies never touch towers at all. Sapper sets an
    // effectively unlimited RitualDetectionRange (and RitualsOutrankPlayer) so this fires the
    // instant it spawns and outranks the player entirely; a short-range, RitualsOutrankPlayer=false
    // enemy instead only attacks a tower opportunistically, and the player can interrupt it at any
    // time via UpdatePlayerLeash's own matching check.
    private void UpdateRitualEngagement()
    {
        if (!CanAttackRituals)
        {
            return;
        }

        bool blockedByPlayer = !RitualsOutrankPlayer && _movementMode == MovementMode.ChasingPlayer;
        if (blockedByPlayer || _movementMode == MovementMode.AttackingWall || _movementMode == MovementMode.AttackingRitual)
        {
            return;
        }

        Ritual nearestRitual = FindNearestRitual(RitualDetectionRange);
        if (nearestRitual == null)
        {
            return;
        }

        _targetRitual = nearestRitual;
        _movementMode = MovementMode.AttackingRitual;

        NavAgent.TargetDesiredDistance = AttackRange;
        NavAgent.TargetPosition = nearestRitual.GlobalPosition;
    }

    private Ritual FindNearestRitual(float maxDistance)
    {
        Ritual nearest = null;
        float bestDistance = maxDistance;

        foreach (Ritual ritual in Level.Rituals.Active)
        {
            if (ritual.IsDisabled || ritual.IsShrouded)
            {
                continue;
            }

            float distance = GlobalPosition.DistanceTo(ritual.GlobalPosition);
            if (distance <= bestDistance)
            {
                bestDistance = distance;
                nearest = ritual;
            }
        }

        return nearest;
    }

    private void ProcessAttackingRitual(double delta)
    {
        if (_targetRitual == null || !IsInstanceValid(_targetRitual) || _targetRitual.IsDisabled)
        {
            _targetRitual = null;
            _movementMode = MovementMode.FollowingLane;
            ProcessLaneFollowing(delta);
            return;
        }

        float distance = GlobalPosition.DistanceTo(_targetRitual.GlobalPosition);

        if (distance <= AttackRange)
        {
            Stop(delta);
            RotateTowardsMovement((_targetRitual.GlobalPosition - GlobalPosition).Normalized(), delta);

            _attackCooldown -= (float)delta;
            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;
                DealDamage(_targetRitual);
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

    private void ProcessChasingPlayer(double delta)
    {
        if (_targetSentinel == null || !IsInstanceValid(_targetSentinel) || !_targetSentinel.IsActive || _targetSentinel.IsDead)
        {
            return;
        }

        float distanceToPlayer = GlobalPosition.DistanceTo(_targetSentinel.GlobalPosition);
        _attackCooldown -= (float)delta;

        if (distanceToPlayer <= AttackRange)
        {
            Stop(delta);
            RotateTowardsMovement((_targetSentinel.GlobalPosition - GlobalPosition).Normalized(), delta);

            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;
                DealDamage(_targetSentinel);
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

    // Always uses a real traveling projectile when ProjectileScene is set - no instant-hit
    // fallback for a ranged enemy, regardless of target type. Only enemies with no ProjectileScene
    // (Grunt, Sapper) keep the instant-hit path.
    private void DealDamage(IDamageable target)
    {
        if (ProjectileScene != null)
        {
            EnemyProjectile projectile = ProjectileScene.Instantiate<EnemyProjectile>();
            GetTree().CurrentScene.AddChild(projectile);
            projectile.GlobalPosition = CenterMass;
            projectile.Initialize(target.AimPoint, AttackDamage, this);
            return;
        }

        target.TakeDamage(new Hit
        {
            Damage = new DamagePacket { PhysicalDamage = AttackDamage },
            Attribution = new Attribution { Source = this }
        });
    }

    private void Stop(double delta)
    {
        NavAgent.Velocity = Vector3.Zero;
        _walkAnimator.Animate(0.0, delta);
    }

    private void Move(Vector3 direction, double delta)
    {
        NavAgent.Velocity = direction * Speed;

        RotateTowardsMovement(direction, delta);
        _walkAnimator.Animate(1.0, delta);
    }

    private void OnVelocityComputed(Vector3 safeVelocity)
    {
        Velocity = safeVelocity;
        MoveAndSlide();
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

    public override void Die(Vector3 ragdollImpulse)
    {
        if (IsDead)
        {
            return;
        }

        base.Die(ragdollImpulse);

        NavAgent.AvoidanceEnabled = false;
        SetPhysicalBonesCollision(PhysicsLayers.Ragdoll, PhysicsLayers.World);
        BoneSimulator.PhysicalBonesStartSimulation();
        _ = ApplyRagdollImpulseNextPhysicsFrame(ragdollImpulse);
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

    public override void ApplyExtraRagdollImpulse(Vector3 impulse)
    {
        if (!IsDead)
        {
            return;
        }

        _ = ApplyRagdollImpulseNextPhysicsFrame(impulse);
    }
}
