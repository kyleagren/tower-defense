using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class Projectile : Area3D
{
    public float Damage { get; private set; }
    public float Radius { get; private set; }
    public float KnockbackForce { get; private set; }
    public int ChainCount { get; private set; }
    public float ChainRange { get; private set; }
    public Vector3 SpawnPosition { get; set; }
    public ProjectileHitEffect HitEffect { get; set; }
    public HashSet<Enemy> ReturnHitTracker { get; set; }
    public HashSet<Enemy> ChainHitTracker { get; set; }

    private int pierceRemaining;
    private float speed = 20f;
    private bool hasExpired;
    private bool isReturn;
    private bool hasReturned;
    private const float ReturnArrivalDistance = 0.75f;
    private const float MaxReturnDistance = 10f;
    private HashSet<Node3D> alreadyHit = new HashSet<Node3D>();

    public void Initialize(ProjectileCastContext context)
    {
        Damage = context.Damage;
        Radius = context.Radius;
        KnockbackForce = context.KnockbackForce;
        ChainCount = context.ChainCount;
        ChainRange = context.ChainRange;
        pierceRemaining = context.PierceCount;
        isReturn = context.IsReturn;
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
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

        if (body is not Enemy hitEnemy)
        {
            if (isReturn)
            {
                hasReturned = true;
                LookAt(SpawnPosition, Vector3.Up);
                return;
            }

            hasExpired = true;
            QueueFree();
            return;
        }

        ChainHitTracker?.Add(hitEnemy);

        if (pierceRemaining > 0)
        {
            pierceRemaining--;
            return;
        }

        if (ChainCount > 0 && TryRedirectToChainTarget(hitEnemy))
        {
            ChainCount--;
            return;
        }

        if (isReturn)
        {
            hasReturned = true;
            LookAt(SpawnPosition, Vector3.Up);
            return;
        }

        hasExpired = true;
        QueueFree();
    }

    private void HandleReturnHit(Node3D body)
    {
        if (body is not Enemy enemy)
        {
            hasExpired = true;
            QueueFree();
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
            Vector3 knockback = -GlobalTransform.Basis.Z * KnockbackForce;
            enemy.TakeDamage(Damage, knockback);
        }

        HitEffect?.OnHit(this, body);
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
