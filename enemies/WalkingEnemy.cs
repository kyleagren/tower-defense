using Godot;
using System.Threading.Tasks;

// An Enemy that follows a shared Lane's waypoints, can break off to chase the player or attack an
// Ice Wall, and ragdolls on death. Everything ground-pathing-specific lives here rather than on
// the base Enemy, so a future FlyingEnemy (or the dev-only TrainingDummy) doesn't have to carry
// navmesh/leash/wall-engagement logic it has no use for.
public partial class WalkingEnemy : Enemy
{
    [Export] public float BaseSpeed = 2.625f;
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
    // Per second, exponential - how fast a shove bleeds off.
    [Export] public float KnockbackDrag = 6f;
    // Below this a shove is spent and the enemy takes back control.
    [Export] public float KnockbackRecoverySpeed = 0.5f;
    [Export] public NavigationAgent3D NavAgent { get; private set; }
    [Export] public Skeleton3D Skeleton { get; private set; }
    [Export] public PhysicalBoneSimulator3D BoneSimulator { get; private set; }
    [Export] public PhysicalBone3D RootPhysicalBone { get; private set; }

    // Debuffs fold in here rather than at the Move() call site, so anything reading Speed sees the
    // slowed value.
    public float Speed => BaseSpeed * DebuffSpeedMultiplier;

    public bool IsKnockedBack => _knockbackVelocity != Vector3.Zero || _launched;

    // True while a subclass is mid-attack and must neither turn nor walk - a committed attack has to
    // stay dodgeable, so it can't track its target once it's begun.
    protected virtual bool IsCommittedToAttack => false;

    [Export] public float WalkCycleFrequency = 6.0f;
    [Export] public float LegSwingAmplitude = 0.5f;
    [Export] public float ArmSwingAmplitude = 0.2f;

    // While launched, gravity is scaled by this - a floatier arc that stays up longer for the same
    // peak, provided the launch is lowered to match.
    [Export] public float LaunchGravityScale = 0.25f;

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
    private Vector3 _knockbackVelocity;
    // Set by an upward push until the enemy lands - it isn't steering while airborne.
    private bool _launched;
    private bool _rejoinPending;
    private float _gravity;
    private float _fallSpeed;

    // Copies only: where the synced position was last frame, and how much longer to keep walking
    // after it last moved - sync updates don't land every physics frame, so a copy that animated only
    // on the frames it moved would stutter between walking and standing.
    private Vector3 _copyLastPosition;
    private float _copyWalkRemaining;
    private const float CopyWalkHold = 0.2f;

    // How close a ranged enemy without a clear shot walks toward its target before stopping.
    private const float BlockedShotApproachDistance = 1f;

    private static readonly StandardMaterial3D _iceMaterial = new()
    {
        AlbedoColor = new Color(0.7f, 0.9f, 1f, 0.3f),
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled
    };

    private static readonly ShaderMaterial _shellMaterial = new()
    {
        Shader = GD.Load<Shader>("uid://bg0t4d2pik7ow")
    };

    // How far an electrocuted enemy's model is nudged each frame.
    private const float JitterDistance = 0.02f;

    // How far a knocked-down enemy's model tips back - nearly flat.
    private const float KnockdownTilt = -1.4f;

    private MeshInstance3D _iceBlock;
    private MeshInstance3D _electricShell;
    private Vector3 _skeletonRestPosition;
    private Vector3 _skeletonRestRotation;

    public override void _Ready()
    {
        base._Ready();
        _gravity = (float)ProjectSettings.GetSetting("physics/3d/default_gravity");

        _walkAnimator = new ProceduralWalkAnimator(Skeleton, WalkCycleFrequency, LegSwingAmplitude, ArmSwingAmplitude);

        // A copy on another machine doesn't steer or path; left on, avoidance would move it on its
        // own and fight the synced position.
        NavAgent.AvoidanceEnabled = IsSimulatedHere;
        if (BodyCollision.Shape is CapsuleShape3D capsuleShape)
        {
            NavAgent.Radius = capsuleShape.Radius;
        }
        NavAgent.VelocityComputed += OnVelocityComputed;

        SetPhysicalBonesCollision(0, 0);
        BuildCrowdControlVisuals();
    }

    // Built per enemy, since each body is its own size: an ice block a little inside the body
    // capsule, and an open shell just outside it for the electrocuted lines.
    private void BuildCrowdControlVisuals()
    {
        _skeletonRestPosition = Skeleton.Position;
        _skeletonRestRotation = Skeleton.Rotation;

        float radius = 0.4f;
        float height = 1.8f;

        if (BodyCollision.Shape is CapsuleShape3D capsule)
        {
            radius = capsule.Radius;
            height = capsule.Height;
        }

        Vector3 center = BodyCollision.Position;

        _iceBlock = new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(radius * 1.8f, height * 0.9f, radius * 1.8f) },
            MaterialOverride = _iceMaterial,
            Position = center,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };

        _electricShell = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = radius * 1.1f, BottomRadius = radius * 1.1f, Height = height, CapTop = false, CapBottom = false },
            MaterialOverride = _shellMaterial,
            Position = center,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Visible = false
        };

        AddChild(_iceBlock);
        AddChild(_electricShell);
    }

    // Every machine, every physics frame, off the synced debuff types - so a recovering CC never shows.
    private void UpdateCrowdControlVisuals()
    {
        bool electrocuted = HasDebuff(DebuffType.Electrocuted);
        _iceBlock.Visible = HasDebuff(DebuffType.Frozen);
        _electricShell.Visible = electrocuted;

        Skeleton.Position = electrocuted
            ? _skeletonRestPosition + new Vector3((float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0), (float)GD.RandRange(-1.0, 1.0)) * JitterDistance
            : _skeletonRestPosition;

        Skeleton.Rotation = HasDebuff(DebuffType.KnockedDown)
            ? _skeletonRestRotation + new Vector3(KnockdownTilt, 0f, 0f)
            : _skeletonRestRotation;
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
        UpdateCrowdControlVisuals();

        if (!IsSimulatedHere)
        {
            AnimateCopy(delta);
            return;
        }

        UpdateDistanceToGoal();

        if (IsKnockedBack)
        {
            ProcessKnockback(delta);
            return;
        }

        // Frozen or electrocuted: it holds still, and its attack timer waits with it.
        if (IsActionPrevented)
        {
            Stop(delta);
            return;
        }

        _attackCooldown -= (float)delta;

        // Deferred until the enemy can walk again, so one rooted in place doesn't re-path every time
        // a pull nudges it.
        if (_rejoinPending && Speed > 0f)
        {
            _rejoinPending = false;
            BeginRejoin();
        }

        // Engagement is decided on an interval (mirrors Ritual.TargetSearchInterval) rather than every
        // frame: rituals, then players, then walls, in that priority order.
        _engagementCheckTimer -= (float)delta;

        if (_engagementCheckTimer <= 0f)
        {
            _engagementCheckTimer = EngagementCheckInterval;

            if (!TryFollowForcedTarget())
            {
                UpdateRitualEngagement();
                UpdatePlayerLeash();
                UpdateWallEngagement();
            }
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

    // A sideways push slides it; an upward part launches it, a stiff hop that lands upright, since a
    // real tumble would need a stand-up animation per enemy type. Gravity brings it back down.
    protected override void ResolveKnockback(Vector3 impulse)
    {
        if (IsDead)
        {
            return;
        }

        _knockbackVelocity += new Vector3(impulse.X, 0f, impulse.Z) / Mass;

        if (impulse.Y > 0f)
        {
            _fallSpeed = Mathf.Min(_fallSpeed, -impulse.Y / Mass);
            _launched = true;
        }
    }

    // A shoved enemy isn't steering: it slides with the shove and can't attack. Once the shove is
    // spent, an enemy that was on a lane re-paths to one rather than walking a straight line back to
    // a waypoint that may now be behind a wall - every other mode already steers by the nav agent
    // and carries on from wherever it landed.
    private void ProcessKnockback(double delta)
    {
        Velocity = _knockbackVelocity + Vector3.Down * UpdateFallSpeed((float)delta);
        MoveAndSlide();
        _walkAnimator.Animate(0.0, delta);

        _knockbackVelocity *= Mathf.Exp(-KnockbackDrag * (float)delta);

        // Landed once it's back on the floor and no longer rising.
        if (_launched && IsOnFloor() && _fallSpeed >= 0f)
        {
            _launched = false;
        }

        if (_launched || _knockbackVelocity.Length() > KnockbackRecoverySpeed)
        {
            return;
        }

        _knockbackVelocity = Vector3.Zero;

        if (_movementMode == MovementMode.FollowingLane || _movementMode == MovementMode.RejoiningLane)
        {
            _rejoinPending = true;
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
            Vector3 offset = GlobalPosition - current;
            offset.Y = 0f;

            bool arrived = offset.Length() <= WaypointArrivalDistance;
            bool alreadyPastIt = offset.Dot(forward) > 0f;

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

        bool inReach = GlobalPosition.DistanceTo(_objective.GlobalPosition) - _objective.ApproachRadius <= AttackRange;
        EngageTarget(inReach ? _objective : null, delta);
    }

    // A taunt outranks everything - lane, leash, walls, Rituals and the objective, Sappers included.
    // Once it lapses, the ordinary checks pick back up: out of leash range, the chase gives up and
    // rejoins the lane like any other.
    private bool TryFollowForcedTarget()
    {
        Sentinel forced = ForcedTarget;
        if (forced == null)
        {
            return false;
        }

        _targetSentinel = forced;
        _targetWall = null;
        _targetRitual = null;
        _movementMode = MovementMode.ChasingPlayer;
        NavAgent.TargetPosition = forced.GlobalPosition;
        return true;
    }

    // A cheap distance check, not a live per-enemy nav query.
    private void UpdatePlayerLeash()
    {
        // A ritual attack only blocks the leash for enemies where rituals outrank the player
        // (Sapper). For everything else, the player can interrupt an opportunistic ritual attack
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

        foreach (EnemySpawner spawner in Level.EnemySpawners.Values)
        {
            if (spawner.Lane == null)
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

        EngageTarget(FindNearestSegment(AttackRange, _targetWall), delta);
    }

    // Opt-in per enemy (CanAttackRituals) - most enemies never touch rituals at all. Sapper sets an
    // effectively unlimited RitualDetectionRange (and RitualsOutrankPlayer) so this fires the
    // instant it spawns and outranks the player entirely; a short-range, RitualsOutrankPlayer=false
    // enemy instead only attacks a ritual opportunistically, and the player can interrupt it at any
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

        bool inReach = GlobalPosition.DistanceTo(_targetRitual.GlobalPosition) <= AttackRange;
        EngageTarget(inReach ? _targetRitual : null, delta);
    }

    private void ProcessChasingPlayer(double delta)
    {
        if (_targetSentinel == null || !IsInstanceValid(_targetSentinel) || !_targetSentinel.IsActive || _targetSentinel.IsDead)
        {
            return;
        }

        bool inReach = GlobalPosition.DistanceTo(_targetSentinel.GlobalPosition) <= AttackRange;
        EngageTarget(inReach ? _targetSentinel : null, delta);
    }

    // The shared end of every engagement. Given a target in reach, stand, face it and strike whenever
    // the attack is off cooldown; given null, keep pathing wherever the nav agent was pointed when the
    // engagement began.
    private void EngageTarget(IDamageable inReach, double delta)
    {
        // A ranged enemy in reach but blocked by terrain closes in along its path until it can see.
        if (inReach != null && ProjectileScene != null && !LineOfSight.IsClear(this, CenterMass, inReach.AimPoint))
        {
            inReach = null;
            NavAgent.TargetDesiredDistance = Mathf.Min(NavAgent.TargetDesiredDistance, BlockedShotApproachDistance);
        }

        if (inReach != null)
        {
            Stop(delta);
            RotateTowardsMovement((inReach.AimPoint - GlobalPosition).Normalized(), delta);

            if (_attackCooldown <= 0f)
            {
                _attackCooldown = AttackInterval;
                DealDamage(inReach);
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
    protected virtual void DealDamage(IDamageable target)
    {
        if (ProjectileScene != null)
        {
            LaunchProjectile().Initialize(target.AimPoint, AttackDamage, this);
            Rpc(MethodName.ReceiveShot, target.AimPoint);
            return;
        }

        target.TakeDamage(new Hit
        {
            Damage = new DamagePacket { PhysicalDamage = AttackDamage },
            Attribution = new Attribution { Source = this }
        });
    }

    [Rpc(MultiplayerApi.RpcMode.Authority, TransferMode = MultiplayerPeer.TransferModeEnum.Unreliable)]
    private void ReceiveShot(Vector3 targetPosition)
    {
        LaunchProjectile().InitializeVisual(targetPosition);
    }

    private EnemyProjectile LaunchProjectile()
    {
        EnemyProjectile projectile = ProjectileScene.Instantiate<EnemyProjectile>();
        Level.LevelRoot.AddChild(projectile);
        projectile.GlobalPosition = CenterMass;
        return projectile;
    }

    // A copy has no steering to drive its walk cycle, so it runs off how far the synced position
    // moves instead.
    private void AnimateCopy(double delta)
    {
        Vector3 moved = GlobalPosition - _copyLastPosition;
        moved.Y = 0f;
        _copyLastPosition = GlobalPosition;

        _copyWalkRemaining = moved.LengthSquared() > 0.0001f ? CopyWalkHold : _copyWalkRemaining - (float)delta;
        _walkAnimator.Animate(_copyWalkRemaining > 0f ? 1.0 : 0.0, delta);
    }

    private void Stop(double delta)
    {
        NavAgent.Velocity = Vector3.Zero;
        _walkAnimator.Animate(0.0, delta);
    }

    // Direction arrives pointing at a path point, which sits at navmesh height rather than on the
    // floor - only its horizontal part is steering.
    private void Move(Vector3 direction, double delta)
    {
        if (IsCommittedToAttack)
        {
            Stop(delta);
            return;
        }

        direction = new Vector3(direction.X, 0f, direction.Z).Normalized();
        NavAgent.Velocity = direction * Speed;

        RotateTowardsMovement(direction, delta);
        _walkAnimator.Animate(Speed > 0f ? 1.0 : 0.0, delta);
    }

    private void OnVelocityComputed(Vector3 safeVelocity)
    {
        float fallSpeed = UpdateFallSpeed((float)GetPhysicsProcessDeltaTime());
        Velocity = new Vector3(safeVelocity.X, -fallSpeed, safeVelocity.Z);
        MoveAndSlide();
    }

    // Steering is horizontal only, so this is what keeps a walking enemy on the real floor - the
    // navmesh its lanes and paths come from sits a little above it. A rising enemy (negative fall
    // speed) keeps rising even while still touching the floor, so a launch isn't cancelled on the
    // frame it starts.
    private float UpdateFallSpeed(float delta)
    {
        float gravity = _launched ? _gravity * LaunchGravityScale : _gravity;
        _fallSpeed = IsOnFloor() && _fallSpeed >= 0f ? 0f : _fallSpeed + gravity * delta;
        return _fallSpeed;
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
        if (IsCommittedToAttack)
        {
            return;
        }

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

        // Physics processing stops with death, so nothing else would clear these off the ragdoll.
        _iceBlock.Visible = false;
        _electricShell.Visible = false;
        Skeleton.Position = _skeletonRestPosition;
        Skeleton.Rotation = _skeletonRestRotation;

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
