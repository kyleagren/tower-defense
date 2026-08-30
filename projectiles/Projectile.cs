using Godot;
using System.Collections.Generic;
using System.Linq;

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
    public ProjectileHitEffect HitEffect { get; set; }
    public HashSet<Enemy> ReturnHitTracker { get; set; }
    public HashSet<Enemy> ChainHitTracker { get; set; }
    public Vector3 LastRagdollImpulse { get; private set; }
    public ReactionRune Reaction { get; private set; }

    private int _pierceRemaining;
    private float _speed;
    private bool _hasExpired;
    private bool _isReturn;
    private bool _hasReturned;
    private const float ReturnArrivalDistance = 0.75f;
    private const float MaxReturnDistance = 10f;
    private HashSet<Node3D> _alreadyHit = new HashSet<Node3D>();

    public void Initialize(SpellCastContext context)
    {
        Damage = context.Damage;
        AreaMultiplier = context.Area?.Multiplier.Value ?? 1f;
        Radius = context.Projectile.Radius * AreaMultiplier;
        RagdollForce = context.RagdollForce.Value;
        ChainCount = context.Chain?.ChainCount ?? 0;
        ChainRange = context.Chain?.ChainRange ?? 0f;
        _pierceRemaining = context.Projectile.PierceCount;
        _isReturn = context.Projectile.IsReturn;
        _speed = context.Projectile.Speed.Value;
        Reaction = context.Reaction;
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyMesh.Scale = Vector3.One * Radius;

        // A fresh shape per projectile: the one in the projectile scene is shared across every
        // instance. Sizing the shape is preferred over scaling the CollisionShape3D node, which
        // Godot advises against. A future non-round projectile changes this line (README 206).
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
            enemy.TakeDamage(Damage, ragdollImpulse, reaction: Reaction);
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
        // Materialized as sets so the exclusion test is a hash lookup — the old lazy OfType/Concat
        // made EnemyTargeting re-walk the whole sequence for every candidate enemy.
        HashSet<Enemy> ownHits = new HashSet<Enemy>(_alreadyHit.OfType<Enemy>());

        HashSet<Enemy> allHits = ownHits;
        if (ChainHitTracker != null)
        {
            allHits = new HashSet<Enemy>(ownHits);
            allHits.UnionWith(ChainHitTracker);
        }

        Enemy next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, allHits);

        if (next == null && ChainHitTracker != null)
        {
            next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, ownHits);
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
