using Godot;

// A simple, self-contained projectile fired by a ranged enemy - deliberately not the Ritual-side
// Projectile class (tightly coupled to SpellCastContext/chain/pierce, only ever hits Enemy). Real
// Area3D collision against whatever it actually hits first (Player | Obstacles covers every
// target type plus plain obstacles like fences), not a pre-assigned target - so it can be blocked
// by something else in the way, and correctly damages whatever was actually struck.
public partial class EnemyProjectile : Area3D
{
    [Export] public float Speed = 12f;
    [Export] public float MaxLifetime = 5f;

    private float _damage;
    private DamageOrigin _origin;
    private float _lifetime;
    private bool _hasHit;

    public void Initialize(Vector3 targetPosition, float damage, DamageOrigin origin = DamageOrigin.Projectile)
    {
        _damage = damage;
        _origin = origin;
        LookAt(targetPosition, Vector3.Up);
    }

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    public override void _PhysicsProcess(double delta)
    {
        Position -= Transform.Basis.Z * Speed * (float)delta;

        _lifetime += (float)delta;
        if (_lifetime >= MaxLifetime)
        {
            QueueFree();
        }
    }

    private void OnBodyEntered(Node3D body)
    {
        if (_hasHit)
        {
            return;
        }

        _hasHit = true;

        IDamageable hitTarget = ResolveTarget(body);
        if (hitTarget != null && !hitTarget.IsDestroyed)
        {
            hitTarget.TakeDamage(new DamagePacket { Origin = _origin, PhysicalDamage = _damage });
        }

        QueueFree();
    }

    // Sentinel/IceWallSegment implement IDamageable directly on the collided body; Ritual and
    // DefendableObjective's solid collision bodies are children of the actual IDamageable node -
    // same "the raycast reports the child, not the parent" case RitualInteractor already handles.
    private static IDamageable ResolveTarget(Node3D body)
    {
        if (body is IDamageable direct)
        {
            return direct;
        }

        return body.GetParent() as IDamageable;
    }
}
