using Godot;
using System.Collections.Generic;

public partial class Projectile : Area3D
{
    [Export] public MeshInstance3D BodyMesh { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }

    public DamagePacket Damage { get; private set; }
    public float Radius { get; private set; }
    public float AreaMultiplier { get; private set; }
    public float RagdollForce { get; private set; }
    public int ChainCount { get; private set; }
    public float ChainRange { get; private set; }
    public Vector3 SpawnPosition { get; set; }
    public ProjectileHitEffect HitEffect { get; private set; }
    public HashSet<Enemy> ReturnHitTracker { get; private set; }
    public HashSet<Enemy> ChainHitTracker { get; private set; }
    public Vector3 LastRagdollImpulse { get; private set; }
    public SpellHit SpellHit { get; private set; }

    private int _pierceRemaining;
    private float _speed;
    private bool _hasExpired;
    private bool _isReturn;
    private bool _hasReturned;
    private const float ReturnArrivalDistance = 0.75f;
    private const float MaxReturnDistance = 10f;
    private readonly HashSet<Node3D> _alreadyHit = new HashSet<Node3D>();

    // The enemy-only half of _alreadyHit, maintained alongside it. _alreadyHit also holds walls and
    // terrain, so it can't be handed to EnemyTargeting directly, and building a filtered copy per
    // chain jump is exactly the allocation this avoids.
    private readonly HashSet<Enemy> _ownEnemyHits = new HashSet<Enemy>();

    // The trackers are shared across every projectile in one cast, so they're passed in rather
    // than read off the context. Everything else the projectile needs comes from the context —
    // SpawnPosition is the one exception, since it can only be read after the caller positions it.
    public void Initialize(SpellCastContext context, HashSet<Enemy> returnHitTracker, HashSet<Enemy> chainHitTracker)
    {
        Damage = context.Damage;
        AreaMultiplier = context.Area?.Multiplier.Value ?? 1f;
        Radius = context.Projectile.Radius * AreaMultiplier;
        RagdollForce = context.RagdollForce.Value;
        ChainCount = context.Chain?.ChainCount ?? 0;
        ChainRange = context.Chain?.ChainRange ?? 0f;
        SpellHit = context.SpellHit;
        HitEffect = context.Projectile.HitEffect;

        ReturnHitTracker = returnHitTracker;
        ChainHitTracker = chainHitTracker;

        _pierceRemaining = context.Projectile.PierceCount;
        _isReturn = context.Projectile.IsReturn;
        _speed = context.Projectile.Speed.Value;
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyMesh.Scale = Vector3.One * Radius;

        // A fresh shape per projectile: the one in the projectile scene is shared across every
        // instance. Sizing the shape is preferred over scaling the CollisionShape3D node, which
        // Godot advises against. Scaling is uniform on all axes, so a future non-round projectile (an
        // elongated Ice Spear) needs its own formula here.
        BodyCollision.Shape = new SphereShape3D { Radius = Radius };
    }

    public override void _PhysicsProcess(double delta)
    {
        Position -= Transform.Basis.Z * _speed * (float)delta;

        if (_isReturn && !_hasReturned && _alreadyHit.Count == 0
            && GlobalPosition.DistanceSquaredTo(SpawnPosition) > MaxReturnDistance * MaxReturnDistance)
        {
            _hasReturned = true;
            LookAt(SpawnPosition, Vector3.Up);
        }

        if (_hasReturned && GlobalPosition.DistanceSquaredTo(SpawnPosition) < ReturnArrivalDistance * ReturnArrivalDistance)
        {
            _hasExpired = true;
            QueueFree();
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_hasExpired)
        {
            return;
        }

        if (_hasReturned)
        {
            HandleReturnHit(body);
            return;
        }

        if (_alreadyHit.Contains(body))
        {
            return;
        }

        _alreadyHit.Add(body);
        ApplyHit(body);

        if (body is Enemy hitEnemy)
        {
            _ownEnemyHits.Add(hitEnemy);
            ChainHitTracker?.Add(hitEnemy);
            ResolveOutcomeAfterEnemyHit(hitEnemy);
            return;
        }

        ResolveOutcomeAfterNonEnemyHit(body);
    }

    private void ResolveOutcomeAfterEnemyHit(Enemy hitEnemy)
    {
        if (_pierceRemaining > 0)
        {
            _pierceRemaining--;
            return;
        }

        if (ChainCount > 0 && TryRedirectToChainTarget(hitEnemy))
        {
            ChainCount--;
            Damage *= ChainResolver.DamageFalloffPerChain;
            return;
        }

        if (_isReturn)
        {
            _hasReturned = true;
            LookAt(SpawnPosition, Vector3.Up);
            return;
        }

        Expire(hitEnemy);
    }

    private void ResolveOutcomeAfterNonEnemyHit(Node3D body)
    {
        if (_isReturn)
        {
            _hasReturned = true;
            LookAt(SpawnPosition, Vector3.Up);
            return;
        }

        Expire(body);
    }

    private void HandleReturnHit(Node3D body)
    {
        if (body is not Enemy enemy)
        {
            Expire(body);
            return;
        }

        if (ReturnHitTracker != null && ReturnHitTracker.Contains(enemy))
        {
            return;
        }

        ReturnHitTracker?.Add(enemy);
        ApplyHit(body);
    }

    private void ApplyHit(Node3D body)
    {
        if (body is Enemy enemy)
        {
            Vector3 ragdollImpulse = -GlobalTransform.Basis.Z * RagdollForce;
            LastRagdollImpulse = ragdollImpulse;
            enemy.TakeDamage(Damage, ragdollImpulse, spellHit: SpellHit);
        }
    }

    private void Expire(Node3D lastHitBody)
    {
        _hasExpired = true;

        if (!_isReturn)
        {
            HitEffect?.OnHit(this, lastHitBody);
        }

        QueueFree();
    }

    private bool TryRedirectToChainTarget(Enemy hitEnemy)
    {
        // Both exclusion sets are passed through as-is rather than merged into a new one: this runs
        // on every chain jump, and merging would cost two HashSet allocations per hit to answer a
        // membership question the targeting loop can ask of each set directly.
        Enemy next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, _ownEnemyHits, ChainHitTracker);

        // Nothing untouched in range — fall back to something a sibling chain already caught rather
        // than dropping the remaining jumps entirely.
        if (next == null && ChainHitTracker != null)
        {
            next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, _ownEnemyHits);
        }

        if (next == null)
        {
            return false;
        }

        ChainHitTracker?.Add(next);
        LookAt(next.GlobalPosition, Vector3.Up);
        return true;
    }
}
