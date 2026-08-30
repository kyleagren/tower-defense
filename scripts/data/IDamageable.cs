using Godot;

// Anything a spell, projectile, or player attack can damage. Lets one damage path serve enemies,
// wall segments, the objective, and rituals once they have HP (README section 7), instead of each
// growing its own TakeDamage shape.
public interface IDamageable
{
    bool IsDestroyed { get; }

    void TakeDamage(DamagePacket damage, Vector3 impulse = default);
}
