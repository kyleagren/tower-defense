using Godot;
using System.Collections.Generic;

public partial class Projectile : Area3D
{
    [Export] public MeshInstance3D BodyMesh { get; private set; }
    [Export] public CollisionShape3D BodyCollision { get; private set; }

    public DamagePacket Damage { get; private set; }
    public float Radius { get; private set; }
    // 0 for a round projectile; see ProjectileCastData.
    public float Length { get; private set; }
    public bool Crosswise { get; private set; }
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

    // Set by SpellEffects on the host's projectile and on each copy of it, so the two can be matched.
    public int ReplicaId { get; set; }
    public float Speed => _speed;

    private SpellEffects _effects;
    private bool _isCopy;

    private int _pierceRemaining;
    private float _speed;
    private bool _hasExpired;
    private bool _isReturn;
    private bool _hasReturned;
    private const float ReturnArrivalDistance = 0.75f;
    private const float MaxReturnDistance = 10f;

    // A safety net, not a range: a level's boundary walls should end any miss long before this.
    private const float MaxLifetime = 3f;

    private float _lifetime;
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
        Length = context.Projectile.Length * AreaMultiplier;
        Crosswise = context.Projectile.Crosswise;
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

    // Another machine's view of a projectile the host fired: it only flies, turned and ended by the
    // host, and never hits anything itself.
    public void InitializeCopy(float speed, float radius, float length, bool crosswise)
    {
        _isCopy = true;
        _speed = speed;
        Radius = radius;
        Length = length;
        Crosswise = crosswise;
        Monitoring = false;
    }

    public override void _Ready()
    {
        _effects = LevelContext.Current.SpellEffects;
        BodyEntered += OnBodyEntered;

        // A fresh shape per projectile: the one in the projectile scene is shared across every
        // instance. Sizing the shape is preferred over scaling the CollisionShape3D node, which
        // Godot advises against.
        if (Length <= 0f)
        {
            BodyMesh.Scale = Vector3.One * Radius;
            BodyCollision.Shape = new SphereShape3D { Radius = Radius };
        }
        else
        {
            // The mesh and capsule are both authored along local Y; this turns that axis onto the
            // flight direction (-Z), or across it onto X.
            Basis axis = Crosswise ? new Basis(Vector3.Back, Mathf.Pi / 2f) : new Basis(Vector3.Right, -Mathf.Pi / 2f);
            BodyMesh.Basis = axis * Basis.FromScale(new Vector3(Radius, Length, Radius));
            BodyCollision.Basis = axis;
            // A capsule's height counts its rounded ends, so it can't be shorter than its width.
            BodyCollision.Shape = new CapsuleShape3D { Radius = Radius, Height = Mathf.Max(Length, Radius * 2f) };
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        _lifetime += (float)delta;

        // Freed quietly rather than through Expire, so a timed-out Fireball doesn't explode in mid-air.
        if (_lifetime >= MaxLifetime)
        {
            End();
            return;
        }

        Position -= Transform.Basis.Z * _speed * (float)delta;

        if (_isReturn && !_hasReturned && _alreadyHit.Count == 0
            && GlobalPosition.DistanceSquaredTo(SpawnPosition) > MaxReturnDistance * MaxReturnDistance)
        {
            _hasReturned = true;
            TurnToward(SpawnPosition);
        }

        if (_hasReturned && GlobalPosition.DistanceSquaredTo(SpawnPosition) < ReturnArrivalDistance * ReturnArrivalDistance)
        {
            End();
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
            TurnToward(SpawnPosition);
            return;
        }

        Expire(hitEnemy);
    }

    private void ResolveOutcomeAfterNonEnemyHit(Node3D body)
    {
        if (_isReturn)
        {
            _hasReturned = true;
            TurnToward(SpawnPosition);
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
            enemy.TakeDamage(SpellHit.CreateHit(Damage, ragdollImpulse));
        }
    }

    private void Expire(Node3D lastHitBody)
    {
        _hasExpired = true;

        if (!_isReturn)
        {
            HitEffect?.OnHit(this, lastHitBody);
        }

        End();
    }

    // Ends this projectile, and with it every copy.
    private void End()
    {
        _hasExpired = true;

        if (!_isCopy)
        {
            _effects.EndProjectile(this);
        }

        QueueFree();
    }

    private void TurnToward(Vector3 target)
    {
        LookAt(target, Vector3.Up);

        if (!_isCopy)
        {
            _effects.TurnProjectile(this);
        }
    }

    private bool TryRedirectToChainTarget(Enemy hitEnemy)
    {
        // Both exclusion sets are passed through as-is rather than merged into a new one: this runs
        // on every chain jump, and merging would cost two HashSet allocations per hit to answer a
        // membership question the targeting loop can ask of each set directly.
        Enemy next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, _ownEnemyHits, ChainHitTracker, GlobalPosition);

        // Nothing untouched in range — fall back to something a sibling chain already caught rather
        // than dropping the remaining jumps entirely.
        if (next == null && ChainHitTracker != null)
        {
            next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, _ownEnemyHits, sightFrom: GlobalPosition);
        }

        if (next == null)
        {
            return false;
        }

        ChainHitTracker?.Add(next);
        TurnToward(next.AimPoint);
        return true;
    }
}
