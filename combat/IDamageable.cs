using Godot;

// Anything a spell, projectile, or player attack can damage. Lets one damage path serve enemies,
// wall segments, the objective and rituals, instead of each growing its own TakeDamage shape.
public interface IDamageable
{
    bool IsDestroyed { get; }
    Vector3 AimPoint { get; }

    void TakeDamage(DamagePacket damage, Vector3 impulse = default);
}
