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

    private int pierceRemaining;
    private float speed;
    private bool hasExpired;
    private bool isReturn;
    private bool hasReturned;
    private const float ReturnArrivalDistance = 0.75f;
    private const float MaxReturnDistance = 10f;
    private HashSet<Node3D> alreadyHit = new HashSet<Node3D>();

    public void Initialize(SpellCastContext context)
    {
        Damage = context.Damage;
        AreaMultiplier = context.Area?.AreaMultiplier ?? 1f;
        Radius = context.Projectile.Radius * AreaMultiplier;
        RagdollForce = context.RagdollForce;
        ChainCount = context.Chain?.ChainCount ?? 0;
        ChainRange = context.Chain?.ChainRange ?? 0f;
        pierceRemaining = context.Projectile.PierceCount;
        isReturn = context.Projectile.IsReturn;
        speed = context.Projectile.Speed;
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        BodyMesh.Scale = Vector3.One * Radius;
        BodyCollision.Scale = Vector3.One * Radius;
    }

    public override void _PhysicsProcess(double delta)
    {
        Position -= Transform.Basis.Z * speed * (float)delta;

        if (isReturn && !hasReturned && alreadyHit.Count == 0
            && GlobalPosition.DistanceSquaredTo(SpawnPosition) > MaxReturnDistance * MaxReturnDistance)
        {
            hasReturned = true;
            LookAt(SpawnPosition, Vector3.Up);
        }

        if (hasReturned && GlobalPosition.DistanceSquaredTo(SpawnPosition) < ReturnArrivalDistance * ReturnArrivalDistance)
        {
            hasExpired = true;
            QueueFree();
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (hasExpired)
        {
            return;
        }

        if (hasReturned)
        {
            HandleReturnHit(body);
            return;
        }

        if (alreadyHit.Contains(body))
        {
            return;
        }

        alreadyHit.Add(body);
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
        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        if (ChainCount > 0 && TryRedirectToChainTarget(hitEnemy))
        {
            ChainCount--;
            Damage *= ChainResolver.DamageFalloffPerChain;
            return;
        }

        if (isReturn)
        {
            hasReturned = true;
            LookAt(SpawnPosition, Vector3.Up);
            return;
        }

        Expire(hitEnemy);
    }

    private void ResolveOutcomeAfterNonEnemyHit(Node3D body)
    {
        if (isReturn)
        {
            hasReturned = true;
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
            enemy.TakeDamage(Damage, ragdollImpulse);
        }
    }

    private void Expire(Node3D lastHitBody)
    {
        hasExpired = true;

        if (!isReturn)
        {
            HitEffect?.OnHit(this, lastHitBody);
        }

        QueueFree();
    }

    private bool TryRedirectToChainTarget(Enemy hitEnemy)
    {
        IEnumerable<Enemy> ownHits = alreadyHit.OfType<Enemy>();

        Enemy next = null;

        if (ChainHitTracker != null)
        {
            next = EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, ownHits.Concat(ChainHitTracker));
        }

        next ??= EnemyTargeting.FindNearest(hitEnemy.GlobalPosition, ChainRange, ownHits);

        if (next == null)
        {
            return false;
        }

        ChainHitTracker?.Add(next);
        LookAt(next.GlobalPosition, Vector3.Up);
        return true;
    }
}
