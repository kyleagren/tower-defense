using Godot;

// A permanent, unkillable target for testing Ritual behavior against - no movement, no death, no
// rewards, no loot. Dev-only; not part of the real enemy roster. Place directly in a level.
public partial class TrainingDummy : Enemy
{
    public override void Die(Vector3 ragdollImpulse)
    {
        CurrentHealth = MaxHealth;
        HealthBar.SetFraction(1f);
    }
}
